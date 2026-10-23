using System.Diagnostics;
using Vortice.DirectComposition;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace MotionGlass;

internal sealed class OverlayWindow : Form
{
    internal OverlayWindow(Rectangle bounds)
    {
        FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; StartPosition=FormStartPosition.Manual;
        Bounds=bounds; TopMost=true; AutoScaleMode=AutoScaleMode.None; Text="MotionGlass overlay";
        _=Handle;
        if(!Native.SetWindowDisplayAffinity(Handle,0x11) || !Native.GetWindowDisplayAffinity(Handle,out uint affinity) || affinity!=0x11)
            throw new InvalidOperationException("Windows could not exclude the blur overlay from screen capture.");
        Native.SetLayeredWindowAttributes(Handle,0,255,2);
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var p=base.CreateParams; p.ExStyle |= 0x00080000|0x00000020|0x00000080|0x08000000|0x00200000; return p; }
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x84) { m.Result=-1; return; }
        if(m.Msg==0x21) { m.Result=3; return; }
        base.WndProc(ref m);
    }
    protected override void OnPaintBackground(PaintEventArgs e) { }
}

internal sealed class DesktopEngine : IDisposable
{
    private readonly Control dispatcher;
    private readonly List<DisplayWorker> workers=[];
    internal volatile Settings Settings = new();
    internal volatile bool Enabled;
    private bool disposed;
    internal Action? OverlayShown;
    internal DesktopEngine(Control dispatcher) { this.dispatcher=dispatcher; }
    internal void SetEnabled(bool enabled)
    {
        Enabled=enabled;
        if(enabled && workers.Count==0)
        {
            try
            {
                foreach(var screen in Screen.AllScreens)
                {
                    var window=new OverlayWindow(screen.Bounds);
                    var worker=new DisplayWorker(this,window,screen.DeviceName,screen.Bounds);
                    workers.Add(worker); worker.Start();
                }
            }
            catch { Enabled=false; foreach(var w in workers) w.Window.Hide(); throw; }
        }
        if(!enabled) foreach(var worker in workers) worker.Window.Hide();
    }
    internal void SetVisible(DisplayWorker worker,bool visible)
    {
        if(disposed || dispatcher.IsDisposed) return;
        try
        {
            dispatcher.BeginInvoke(() =>
            {
                if(disposed || worker.Window.IsDisposed) return;
                if(visible && Enabled && Settings.HasEffect && Stopwatch.GetElapsedTime(worker.LastPresent).TotalSeconds<.5)
                {
                    if(!worker.Window.Visible) { worker.Window.Show(); OverlayShown?.Invoke(); }
                }
                else worker.Window.Hide();
            });
        }
        catch(InvalidOperationException) { }
    }
    internal string Status()
    {
        if(!Enabled) return "Off · desktop unchanged";
        if(!Settings.HasEffect) return "Off · blur amount is 0%";
        foreach(var w in workers)
        {
            if(w.Window.Visible && Stopwatch.GetElapsedTime(w.LastPresent).TotalSeconds>.5) w.Window.Hide();
        }
        string? error=workers.Select(w=>w.Error).FirstOrDefault(e=>e is not null);
        if(error is not null) return error;
        if(workers.Count==0 || workers.Any(w=>w.Fps==0)) return "Starting display capture…";
        return $"Live · {workers.Count} display{(workers.Count==1?"":"s")} · {workers.Min(w=>w.Fps):0} FPS";
    }
    internal void DisplayChanged()
    {
        bool restart=Enabled; StopWorkers(); Enabled=false;
        if(restart) SetEnabled(true);
    }
    private void StopWorkers()
    {
        foreach(var w in workers) { w.Window.Hide(); w.Stop.Set(); }
        foreach(var w in workers) { w.Thread?.Join(2000); w.Window.Dispose(); }
        workers.Clear();
    }
    public void Dispose() { if(disposed) return; disposed=true; Enabled=false; StopWorkers(); }

    internal sealed class DisplayWorker
    {
        private readonly DesktopEngine owner;
        internal readonly OverlayWindow Window;
        private readonly string name;
        private readonly Rectangle bounds;
        internal readonly ManualResetEventSlim Stop=new(false);
        internal Thread? Thread;
        internal volatile string? Error;
        internal volatile int Fps;
        internal long LastPresent=Stopwatch.GetTimestamp();
        internal DisplayWorker(DesktopEngine owner,OverlayWindow window,string name,Rectangle bounds)
        { this.owner=owner; Window=window; this.name=name; this.bounds=bounds; }
        internal void Start()
        {
            // HWND access stays on the UI thread; D3D resources belong exclusively to this worker.
            nint hwnd=Window.Handle;
            Thread=new Thread(()=>Run(hwnd)) { IsBackground=true,Name=$"MotionGlass {name}" };
            Thread.Start();
        }
        private void Run(nint hwnd)
        {
            while(!Stop.IsSet)
            {
                if(!owner.Enabled || !owner.Settings.HasEffect) { Stop.Wait(40); continue; }
                try { Render(hwnd); Error=null; }
                catch(Exception ex)
                {
                    Error=$"Display paused · {FriendlyError(ex)}";
                    Fps=0; owner.SetVisible(this,false);
                    Stop.Wait(1500);
                }
            }
        }
        private static string FriendlyError(Exception ex) => ex.Message.Contains("887A0026",StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("887A0022",StringComparison.OrdinalIgnoreCase)
            ? "waiting for the desktop" : ex.Message.Split('\n')[0];
        private void Render(nint hwnd)
        {
            using var factory=DXGI.CreateDXGIFactory1<IDXGIFactory2>();
            IDXGIAdapter1? selectedAdapter=null; IDXGIOutput? selectedOutput=null;
            for(uint a=0; factory.EnumAdapters1(a,out var adapter).Success; ++a)
            {
                for(uint o=0; adapter.EnumOutputs(o,out var output).Success; ++o)
                {
                    if(output.Description.DeviceName.TrimEnd('\0')==name) { selectedAdapter=adapter; selectedOutput=output; break; }
                    output.Dispose();
                }
                if(selectedOutput is not null) break;
                adapter.Dispose();
            }
            using var adapterLifetime=selectedAdapter;
            using var outputLifetime=selectedOutput;
            if(selectedAdapter is null || selectedOutput is null) throw new InvalidOperationException("The display is no longer connected.");
            using var output1=selectedOutput.QueryInterface<IDXGIOutput1>();
            using(var output6=selectedOutput.QueryInterfaceOrNull<IDXGIOutput6>())
            {
                if(output6 is not null && output6.Description1.ColorSpace==ColorSpaceType.RgbFullG2084NoneP2020)
                    throw new InvalidOperationException("Turn Windows HDR off for this display to use blur.");
            }
            int rotation=(int)selectedOutput.Description.Rotation;
            bool rotated=rotation is 2 or 4;
            using var graphics=new GraphicsPipeline(selectedAdapter,bounds.Width,bounds.Height,
                rotated?bounds.Height:bounds.Width,rotated?bounds.Width:bounds.Height);
            using var pacing=graphics.Device.QueryInterface<IDXGIDevice1>();
            pacing.MaximumFrameLatency=1;
            using var duplication=output1.DuplicateOutput(graphics.Device);
            var swapDescription=new SwapChainDescription1
            {
                Width=(uint)bounds.Width,Height=(uint)bounds.Height,Format=Format.B8G8R8A8_UNorm,
                BufferCount=2,BufferUsage=Usage.RenderTargetOutput,SampleDescription=new SampleDescription(1,0),
                Scaling=Scaling.Stretch,SwapEffect=SwapEffect.FlipSequential,AlphaMode=AlphaMode.Premultiplied
            };
            using var swap=factory.CreateSwapChainForComposition(graphics.Device,swapDescription);
            using var dxgiDevice=graphics.Device.QueryInterface<IDXGIDevice>();
            using var composition=DComp.DCompositionCreateDevice<IDCompositionDevice>(dxgiDevice);
            composition.CreateTargetForHwnd(hwnd,true,out var target).CheckError();
            using var targetLifetime=target;
            using var visual=composition.CreateVisual();
            visual.SetContent(swap).CheckError(); target.SetRoot(visual).CheckError(); composition.Commit().CheckError();
            using var back=swap.GetBuffer<ID3D11Texture2D>(0);
            using var backView=graphics.Device.CreateRenderTargetView(back);
            var cursor=new CursorImage();
            bool hasDesktop=false,separatePointer=true,shown=false,frameHeld=false;
            long previous=Stopwatch.GetTimestamp(),second=previous;
            int count=0;
            Error=null;
            try
            {
                while(!Stop.IsSet && owner.Enabled && owner.Settings.HasEffect)
                {
                    long start=Stopwatch.GetTimestamp();
                    // Keep ownership while processing so DXGI can coalesce intervening updates.
                    // Release immediately before acquiring, as recommended by the duplication API.
                    if(frameHeld) { frameHeld=false; duplication.ReleaseFrame().CheckError(); }
                    var acquire=duplication.AcquireNextFrame(0,out var info,out var desktop);
                    if(acquire.Success)
                    {
                        frameHeld=true;
                        try
                        {
                            using var texture=desktop.QueryInterface<ID3D11Texture2D>();
                            graphics.CopyDesktop(texture); hasDesktop=true;
                            if(info.LastMouseUpdateTime!=0) separatePointer=info.PointerPosition.Visible;
                        }
                        finally { desktop.Dispose(); }
                    }
                    else if(acquire!=Vortice.DXGI.ResultCode.WaitTimeout) acquire.CheckError();
                    if(!hasDesktop) { Stop.Wait(4); continue; }
                    float delta=(float)Math.Clamp(Stopwatch.GetElapsedTime(previous,start).TotalSeconds,.001,.1);
                    previous=start;
                    cursor.Update(graphics,bounds);
                    graphics.Process(owner.Settings,delta,rotation,cursor.Position,cursor.Size,cursor.Visible&&separatePointer);
                    graphics.PresentTo(backView);
                    swap.Present(1,PresentFlags.None).CheckError();
                    Interlocked.Exchange(ref LastPresent,Stopwatch.GetTimestamp());
                    if(!shown) { owner.SetVisible(this,true); shown=true; }
                    ++count;
                    if(Stopwatch.GetElapsedTime(second).TotalSeconds>=1) { Fps=count; count=0; second=Stopwatch.GetTimestamp(); }
                }
            }
            finally
            {
                if(frameHeld) duplication.ReleaseFrame();
                Fps=0; owner.SetVisible(this,false);
            }
        }
    }
}
