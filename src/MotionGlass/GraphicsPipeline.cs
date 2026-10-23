using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

// Direct3D explicitly accepts null SRV slots; the binding's array annotation is stricter.
#pragma warning disable CS8620

namespace MotionGlass;

// All textures and passes stay on the display's own GPU. No screen frames leave this process.
internal sealed class GraphicsPipeline : IDisposable
{
    internal readonly ID3D11Device Device;
    internal readonly ID3D11DeviceContext Context;
    internal readonly int Width, Height;
    private readonly List<IDisposable> resources = [];
    private readonly ID3D11VertexShader vertex;
    private readonly ID3D11PixelShader normalize, flow, blur, copy;
    private readonly ID3D11Buffer constants;
    private readonly ID3D11SamplerState sampler;
    private readonly Surface raw;
    private Surface current, result;
    private readonly Surface[] history, fields;
    private Surface accumulated;
    private Surface? pointer;
    private bool initialized;
    private BlurMode lastMode;
    private readonly ID3D11ShaderResourceView?[] views = new ID3D11ShaderResourceView?[7];
    private readonly ID3D11ShaderResourceView?[] emptyViews = new ID3D11ShaderResourceView?[7];
    private Params parameters;
    internal Surface Result => result;

    [StructLayout(LayoutKind.Sequential)] private struct Params
    {
        public Vector2 Size, InvSize;
        public float Amount, Delta, Scaling, Mode;
        public Vector2 CursorPosition, CursorSize;
        public float Rotation, CursorVisible, FlowStep, HasCoarse;
    }

    internal sealed class Surface : IDisposable
    {
        internal readonly ID3D11Texture2D Texture;
        internal readonly ID3D11ShaderResourceView View;
        internal readonly ID3D11RenderTargetView? Target;
        internal readonly int Width, Height;
        internal Surface(ID3D11Device device, int w, int h, Format format, bool target = true)
        {
            Width=w; Height=h;
            Texture = device.CreateTexture2D(new Texture2DDescription
            {
                Width=(uint)w, Height=(uint)h, MipLevels=1, ArraySize=1, Format=format,
                SampleDescription=new SampleDescription(1,0), Usage=ResourceUsage.Default,
                BindFlags=BindFlags.ShaderResource | (target ? BindFlags.RenderTarget : BindFlags.None)
            });
            View = device.CreateShaderResourceView(Texture);
            if (target) Target = device.CreateRenderTargetView(Texture);
        }
        public void Dispose() { Target?.Dispose(); View.Dispose(); Texture.Dispose(); }
    }

    internal GraphicsPipeline(IDXGIAdapter? adapter, int width, int height, int rawWidth, int rawHeight, bool warp = false)
    {
        Width=width; Height=height;
        try
        {
            Vortice.Direct3D11.D3D11.D3D11CreateDevice(adapter, warp ? DriverType.Warp : DriverType.Unknown,
                DeviceCreationFlags.BgraSupport, [FeatureLevel.Level_11_0], out Device, out Context).CheckError();
            resources.Add(Device); resources.Add(Context);
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MotionGlass.Shaders.hlsl")!;
            using var reader = new StreamReader(stream);
            string shader = reader.ReadToEnd();
            vertex = Own(Device.CreateVertexShader(Compiler.Compile(shader,"VS","Shaders.hlsl","vs_5_0").ToArray()));
            normalize = Own(Device.CreatePixelShader(Compiler.Compile(shader,"NormalizePS","Shaders.hlsl","ps_5_0").ToArray()));
            flow = Own(Device.CreatePixelShader(Compiler.Compile(shader,"FlowPS","Shaders.hlsl","ps_5_0").ToArray()));
            blur = Own(Device.CreatePixelShader(Compiler.Compile(shader,"BlurPS","Shaders.hlsl","ps_5_0").ToArray()));
            copy = Own(Device.CreatePixelShader(Compiler.Compile(shader,"CopyPS","Shaders.hlsl","ps_5_0").ToArray()));
            constants = Own(Device.CreateBuffer(new BufferDescription(64,BindFlags.ConstantBuffer,ResourceUsage.Default)));
            sampler = Own(Device.CreateSamplerState(new SamplerDescription(Filter.MinMagMipLinear,TextureAddressMode.Clamp,TextureAddressMode.Clamp,TextureAddressMode.Clamp)));
            raw=Make(rawWidth,rawHeight,false); current=Make(width,height); result=Make(width,height);
            accumulated=Make(width,height);
            history=[Make(width,height),Make(width,height),Make(width,height)];
            fields=[Make(Math.Max(1,width/32),Math.Max(1,height/32),true,Format.R16G16B16A16_Float),
                Make(Math.Max(1,width/16),Math.Max(1,height/16),true,Format.R16G16B16A16_Float),
                Make(Math.Max(1,width/8),Math.Max(1,height/8),true,Format.R16G16B16A16_Float)];
            parameters.Size=new(width,height); parameters.InvSize=new(1f/width,1f/height);
            Context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            Context.VSSetShader(vertex); Context.PSSetSampler(0,sampler); Context.PSSetConstantBuffer(0,constants);
        }
        catch { Dispose(); throw; }
    }
    private T Own<T>(T resource) where T:IDisposable { resources.Add(resource); return resource; }
    private Surface Make(int w,int h,bool target=true,Format format=Format.B8G8R8A8_UNorm) => Own(new Surface(Device,w,h,format,target));
    internal void CopyDesktop(ID3D11Texture2D texture) => Context.CopyResource(raw.Texture,texture);
    internal unsafe void UploadDesktop(byte[] bgra)
    {
        fixed(byte* p=bgra) Context.UpdateSubresource(raw.Texture,0,null,(nint)p,(uint)(raw.Width*4),0);
    }
    internal unsafe void SetPointer(byte[] rgba,int w,int h)
    {
        if(pointer is null || pointer.Width != w || pointer.Height != h)
        { pointer?.Dispose(); pointer=new Surface(Device,w,h,Format.R8G8B8A8_UNorm,false); }
        fixed(byte* p=rgba) Context.UpdateSubresource(pointer.Texture,0,null,(nint)p,(uint)(w*4),0);
    }
    internal void Reset() => initialized=false;
    private void Draw(Surface target,ID3D11PixelShader shader)
    {
        Context.PSSetShaderResources(0,emptyViews);
        Context.OMSetRenderTargets(target.Target!);
        Context.RSSetViewport(0,0,target.Width,target.Height);
        Context.UpdateSubresource(parameters,constants);
        Context.PSSetShaderResources(0,views);
        Context.PSSetShader(shader); Context.Draw(3,0);
        Context.PSSetShaderResources(0,emptyViews);
        Context.UnsetRenderTargets();
    }
    internal void Process(Settings settings,float delta,int rotation,Point cursor,Size cursorSize,bool cursorVisible)
    {
        // Last frame's output becomes history by exchanging ownership, without copying pixels.
        (result,accumulated)=(accumulated,result);
        parameters.Amount=settings.Amount/100f; parameters.Delta=delta;
        parameters.Scaling=settings.RefreshScaling ? 1:0; parameters.Mode=(int)settings.Mode;
        parameters.Rotation=rotation; parameters.CursorPosition=new(cursor.X,cursor.Y);
        parameters.CursorSize=new(cursorSize.Width,cursorSize.Height); parameters.CursorVisible=cursorVisible?1:0;
        Array.Clear(views); views[0]=raw.View; views[4]=pointer?.View;
        Draw(current,normalize);
        if(!initialized || settings.Mode != lastMode)
        {
            foreach(var item in history) Context.CopyResource(item.Texture,current.Texture);
            Context.CopyResource(accumulated.Texture,current.Texture);
            initialized=true; lastMode=settings.Mode;
        }
        views[0]=current.View; views[1]=history[0].View; views[2]=history[1].View; views[3]=history[2].View;
        views[4]=null; views[5]=null; views[6]=accumulated.View;
        if(settings.Mode is BlurMode.Velocity or BlurMode.Hybrid)
        {
            for(int j=0;j<3;++j)
            {
                parameters.FlowStep=j==0?32:j==1?8:2;
                parameters.HasCoarse=j>0?1:0;
                views[5]=j>0?fields[j-1].View:null;
                Draw(fields[j],flow);
            }
            views[5]=fields[2].View;
        }
        Draw(result,blur);
        // Recycle the oldest input surface for the next frame's normalization pass.
        var recycled=history[2];
        history[2]=history[1]; history[1]=history[0]; history[0]=current; current=recycled;
    }
    internal void PresentTo(ID3D11RenderTargetView target)
    {
        Array.Clear(views); views[0]=result.View;
        Context.OMSetRenderTargets(target); Context.RSSetViewport(0,0,Width,Height);
        Context.PSSetShaderResources(0,views); Context.PSSetShader(copy); Context.Draw(3,0);
        Context.PSSetShaderResources(0,emptyViews); Context.UnsetRenderTargets();
    }
    internal unsafe byte[] ReadResult()
    {
        var desc=result.Texture.Description;
        desc.Usage=ResourceUsage.Staging; desc.BindFlags=BindFlags.None; desc.CPUAccessFlags=CpuAccessFlags.Read;
        using var staging=Device.CreateTexture2D(desc);
        Context.CopyResource(staging,result.Texture);
        var mapped=Context.Map(staging,0,MapMode.Read,Vortice.Direct3D11.MapFlags.None);
        var bytes=new byte[Width*Height*4];
        try { for(int y=0;y<Height;++y) Marshal.Copy(mapped.DataPointer+(nint)(y*mapped.RowPitch),bytes,y*Width*4,Width*4); }
        finally { Context.Unmap(staging,0); }
        return bytes;
    }
    public void Dispose()
    {
        Context?.ClearState(); Context?.Flush(); pointer?.Dispose();
        for(int j=resources.Count-1;j>=0;--j) resources[j].Dispose();
        resources.Clear();
    }
}
