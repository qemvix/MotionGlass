using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MotionGlass;

internal sealed class CursorImage
{
    private nint lastHandle;
    internal Size Size { get; private set; }
    internal Point Hotspot { get; private set; }
    internal Point Position { get; private set; }
    internal bool Visible { get; private set; }
    internal void Update(GraphicsPipeline pipeline,Rectangle display)
    {
        var info=new Native.CursorInfo { Size=(uint)Marshal.SizeOf<Native.CursorInfo>() };
        if(!Native.GetCursorInfo(ref info)) { Visible=false; return; }
        Visible=(info.Flags&1)!=0 && display.Contains(info.Position);
        if(info.Cursor!=0 && info.Cursor!=lastHandle)
        {
            if(!Native.GetIconInfo(info.Cursor,out var icon)) { Visible=false; return; }
            try
            {
                Hotspot=new((int)icon.HotX,(int)icon.HotY);
                using var handleIcon=Icon.FromHandle(info.Cursor);
                int w=Math.Clamp(handleIcon.Width,1,256),h=Math.Clamp(handleIcon.Height,1,256);
                using var black=Draw(info.Cursor,w,h,Color.Black);
                using var white=Draw(info.Cursor,w,h,Color.White);
                var pixels=new byte[w*h*4];
                // Two backgrounds recover alpha for both legacy monochrome and modern color cursors.
                for(int y=0;y<h;++y) for(int x=0;x<w;++x)
                {
                    Color b=black.GetPixel(x,y), a=white.GetPixel(x,y);
                    int alpha=255-Math.Max(0,Math.Max(a.R-b.R,Math.Max(a.G-b.G,a.B-b.B)));
                    int at=(y*w+x)*4;
                    pixels[at]=(byte)Math.Clamp(alpha>0?b.R*255/alpha:0,0,255);
                    pixels[at+1]=(byte)Math.Clamp(alpha>0?b.G*255/alpha:0,0,255);
                    pixels[at+2]=(byte)Math.Clamp(alpha>0?b.B*255/alpha:0,0,255);
                    pixels[at+3]=(byte)alpha;
                }
                pipeline.SetPointer(pixels,w,h); Size=new(w,h); lastHandle=info.Cursor;
            }
            finally { if(icon.Mask!=0) Native.DeleteObject(icon.Mask); if(icon.Color!=0) Native.DeleteObject(icon.Color); }
        }
        Position=new(info.Position.X-display.X-Hotspot.X,info.Position.Y-display.Y-Hotspot.Y);
    }
    private static Bitmap Draw(nint icon,int w,int h,Color background)
    {
        var bitmap=new Bitmap(w,h,PixelFormat.Format32bppArgb);
        using var g=Graphics.FromImage(bitmap); g.Clear(background);
        nint dc=g.GetHdc();
        try { Native.DrawIconEx(dc,0,0,icon,w,h,0,0,3); }
        finally { g.ReleaseHdc(dc); }
        return bitmap;
    }
}
