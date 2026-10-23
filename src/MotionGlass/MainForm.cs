using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MotionGlass;

internal static class Palette
{
    internal static readonly Color Background=Color.FromArgb(15,16,23),Card=Color.FromArgb(25,27,38),Border=Color.FromArgb(42,44,59);
    internal static readonly Color Text=Color.FromArgb(240,240,247),Muted=Color.FromArgb(151,155,176),Accent=Color.FromArgb(166,139,250),Disabled=Color.FromArgb(86,89,108);
    internal static GraphicsPath Round(RectangleF r,float radius)
    {
        var p=new GraphicsPath(); float d=radius*2;
        p.AddArc(r.X,r.Y,d,d,180,90); p.AddArc(r.Right-d,r.Y,d,d,270,90);
        p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.X,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p;
    }
}

internal sealed class ToggleSwitch : CheckBox
{
    internal ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
        Size=new(58,32); Cursor=Cursors.Hand; AccessibleRole=AccessibleRole.CheckButton;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var p=Palette.Round(new RectangleF(1,1,Width-2,Height-2),(Height-2)/2f);
        using var brush=new SolidBrush(!Enabled?Color.FromArgb(42,44,55):Checked?Palette.Accent:Color.FromArgb(63,66,85));
        e.Graphics.FillPath(brush,p);
        float d=Height-10,x=Checked?Width-d-5:5;
        using var dot=new SolidBrush(Enabled?Color.White:Palette.Disabled); e.Graphics.FillEllipse(dot,x,5,d,d);
        if(Focused) { using var pen=new Pen(Palette.Text,1); e.Graphics.DrawPath(pen,p); }
    }
}

internal sealed class AmountSlider : Control
{
    private int value=50;
    internal event EventHandler? ValueChanged;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int Value { get=>value; set { int next=Math.Clamp(value,0,100); if(this.value==next) return; this.value=next; Invalidate(); ValueChanged?.Invoke(this,EventArgs.Empty); } }
    internal AmountSlider()
    {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);
        Size=new(536,40); TabStop=true; Cursor=Cursors.Hand; AccessibleName="Motion blur percentage"; AccessibleRole=AccessibleRole.Slider;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        float pad=12,track=Width-pad*2,y=Height/2f,x=pad+track*Value/100;
        using var trackPen=new Pen(Palette.Border,6) { StartCap=LineCap.Round,EndCap=LineCap.Round };
        e.Graphics.DrawLine(trackPen,pad,y,Width-pad,y);
        using var fillPen=new Pen(Enabled?Palette.Accent:Palette.Disabled,6) { StartCap=LineCap.Round,EndCap=LineCap.Round };
        if(Value>0) e.Graphics.DrawLine(fillPen,pad,y,x,y);
        using var fill=new SolidBrush(Enabled?Palette.Text:Palette.Disabled); e.Graphics.FillEllipse(fill,x-8,y-8,16,16);
        if(Focused) { using var pen=new Pen(Palette.Accent,1); e.Graphics.DrawEllipse(pen,x-12,y-12,24,24); }
    }
    private void Track(int x) => Value=(int)Math.Round((x-12)*100d/Math.Max(1,Width-24));
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if(e.Button==MouseButtons.Left) { Focus(); Capture=true; Track(e.X); } }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if(Capture) Track(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Capture=false; }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if(e.KeyCode is Keys.Left or Keys.Down) Value-=1;
        if(e.KeyCode is Keys.Right or Keys.Up) Value+=1;
        if(e.KeyCode==Keys.PageUp) Value+=10; if(e.KeyCode==Keys.PageDown) Value-=10;
        if(e.KeyCode==Keys.Home) Value=0; if(e.KeyCode==Keys.End) Value=100;
    }
    protected override AccessibleObject CreateAccessibilityInstance() => new SliderAccessibility(this);
    private sealed class SliderAccessibility(AmountSlider owner) : ControlAccessibleObject(owner)
    {
        public override string? Value { get=>$"{owner.Value}%"; set { if(int.TryParse(value?.TrimEnd('%'),out int n)) owner.Value=n; } }
    }
}

internal sealed class MainForm : Form
{
    private Settings settings;
    private readonly DesktopEngine engine;
    private readonly ToggleSwitch power=new() { AccessibleName="Enable desktop motion blur" },scaling=new() { AccessibleName="Refresh rate scaling" };
    private readonly AmountSlider amount=new();
    private readonly ComboBox modes=new();
    private readonly LinkLabel reset=new();
    private readonly System.Windows.Forms.Timer timer=new() { Interval=250 };
    private readonly NotifyIcon tray;
    private readonly bool persistSettings;
    private string status="Off · desktop unchanged";
    private bool updating,closed,toggleRegistered,stopRegistered;
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd,int attribute,ref int value,int size);
    internal MainForm(bool fresh=false,bool persistSettings=true)
    {
        this.persistSettings=persistSettings;
        settings=fresh?new():Settings.Load();
        AutoScaleDimensions=new SizeF(96,96); AutoScaleMode=AutoScaleMode.Dpi;
        Text="MotionGlass"; ClientSize=new(640,746); MinimumSize=Size; MaximumSize=Size;
        FormBorderStyle=FormBorderStyle.FixedSingle; MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen;
        BackColor=Palette.Background; ForeColor=Palette.Text; Font=new Font("Segoe UI",10);
        DoubleBuffered=true; Icon=SystemIcons.Application;
        engine=new DesktopEngine(this) { Settings=settings };
        engine.OverlayShown=()=> { if(Visible) Native.SetWindowPos(Handle,-1,0,0,0,0,0x13); };
        power.Location=new(518,142); scaling.Location=new(518,542);
        amount.Bounds=new(40,430,560,40);
        modes.Bounds=new(44,271,552,38); modes.Font=new Font("Segoe UI",12);
        modes.DropDownStyle=ComboBoxStyle.DropDownList; modes.FlatStyle=FlatStyle.Flat;
        modes.BackColor=Color.FromArgb(36,38,52); modes.ForeColor=Palette.Text;
        modes.DrawMode=DrawMode.OwnerDrawFixed; modes.ItemHeight=30;
        modes.AccessibleName="Motion blur mode"; modes.Items.AddRange(Settings.Names); modes.SelectedIndex=(int)settings.Mode;
        modes.DrawItem+=(s,e)=>
        {
            if(e.Index<0) return;
            e.DrawBackground();
            using var background=new SolidBrush((e.State&DrawItemState.Selected)!=0?Color.FromArgb(67,52,102):modes.BackColor);
            e.Graphics.FillRectangle(background,e.Bounds);
            TextRenderer.DrawText(e.Graphics,Settings.Names[e.Index],e.Font,new Rectangle(e.Bounds.X+10,e.Bounds.Y,e.Bounds.Width-12,e.Bounds.Height),Palette.Text,TextFormatFlags.VerticalCenter);
        };
        reset.Text="Reset defaults"; reset.AutoSize=true; reset.Location=new(490,661);
        reset.LinkColor=Palette.Muted; reset.ActiveLinkColor=Palette.Accent; reset.VisitedLinkColor=Palette.Muted; reset.TabStop=true;
        reset.LinkClicked+=(s,e)=> { settings=new(); ApplySettings(); if(persistSettings) settings.Save(); };
        Controls.AddRange([power,scaling,amount,modes,reset]);
        power.CheckedChanged+=(s,e)=>
        {
            if(updating) return;
            try { engine.SetEnabled(power.Checked); TopMost=power.Checked; }
            catch(Exception ex) { power.Checked=false; status=ex.Message; MessageBox.Show(this,ex.Message,"Could not start motion blur",MessageBoxButtons.OK,MessageBoxIcon.Information); }
            Invalidate();
        };
        modes.SelectedIndexChanged+=(s,e)=> { if(!updating) Change(settings with { Mode=(BlurMode)modes.SelectedIndex }); };
        amount.ValueChanged+=(s,e)=> { if(!updating) Change(settings with { Amount=amount.Value }); };
        scaling.CheckedChanged+=(s,e)=> { if(!updating) Change(settings with { RefreshScaling=scaling.Checked }); };
        tray=new NotifyIcon { Text="MotionGlass · Ctrl+Alt+B to toggle",Icon=Icon,Visible=true };
        var menu=new ContextMenuStrip(); menu.Items.Add("Open MotionGlass",null,(s,e)=>Restore());
        menu.Items.Add("Toggle motion blur",null,(s,e)=>power.Checked=!power.Checked);
        menu.Items.Add("Turn off",null,(s,e)=>power.Checked=false); menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Exit",null,(s,e)=>Close());
        tray.ContextMenuStrip=menu; tray.DoubleClick+=(s,e)=>Restore();
        timer.Tick+=(s,e)=> { string next=engine.Status(); if(next!=status) { status=next; Invalidate(); } };
        timer.Start(); SystemEvents.DisplaySettingsChanged+=DisplaysChanged;
        ApplySettings();
    }
    internal void Change(Settings value) { settings=value; ApplySettings(); if(persistSettings) settings.Save(); }
    private void ApplySettings()
    {
        updating=true;
        modes.SelectedIndex=(int)settings.Mode; amount.Value=settings.Amount; amount.Enabled=!settings.FixedStrength;
        scaling.Checked=settings.RefreshScaling; scaling.Enabled=settings.Mode==BlurMode.Velocity;
        engine.Settings=settings;
        if(!settings.HasEffect) engine.SetEnabled(power.Checked);
        updating=false; Invalidate();
    }
    private void Restore() { Show(); WindowState=FormWindowState.Normal; Activate(); }
    protected override void OnResize(EventArgs e) { base.OnResize(e); if(WindowState==FormWindowState.Minimized) Hide(); }
    private void DisplaysChanged(object? sender,EventArgs e)
    {
        if(closed || !IsHandleCreated) return;
        BeginInvoke(()=> { try { engine.DisplayChanged(); } catch(Exception ex) { power.Checked=false; status=ex.Message; } });
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e); int dark=1; DwmSetWindowAttribute(Handle,20,ref dark,4);
        toggleRegistered=Native.RegisterHotKey(Handle,Native.ToggleHotkey,0x4003,(uint)Keys.B);
        stopRegistered=Native.RegisterHotKey(Handle,Native.StopHotkey,0x4003,(uint)Keys.Escape);
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x312)
        {
            if(m.WParam==Native.StopHotkey) power.Checked=false;
            if(m.WParam==Native.ToggleHotkey) power.Checked=!power.Checked;
        }
        base.WndProc(ref m);
    }
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        closed=true; timer.Stop(); timer.Dispose();
        SystemEvents.DisplaySettingsChanged-=DisplaysChanged;
        Native.UnregisterHotKey(Handle,Native.ToggleHotkey); Native.UnregisterHotKey(Handle,Native.StopHotkey);
        engine.Dispose(); tray.Visible=false; tray.Dispose(); base.OnFormClosed(e);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g=e.Graphics; g.ScaleTransform(DeviceDpi/96f,DeviceDpi/96f); g.SmoothingMode=SmoothingMode.AntiAlias;
        void TextAt(string text,float x,float y,float size,Color color,bool bold=false,int width=560,int height=40)
        { using var font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular); using var brush=new SolidBrush(color); g.DrawString(text,font,brush,new RectangleF(x,y,width,height)); }
        void Card(float x,float y,float w,float h)
        { using var path=Palette.Round(new(x,y,w,h),14); using var brush=new SolidBrush(Palette.Card); using var pen=new Pen(Palette.Border); g.FillPath(brush,path); g.DrawPath(pen,path); }
        using(var pen=new Pen(Palette.Accent,4) { StartCap=LineCap.Round,EndCap=LineCap.Round })
        { g.DrawLine(pen,33,55,53,35); g.DrawLine(pen,33,69,65,37); g.DrawLine(pen,45,71,66,50); }
        TextAt("MotionGlass",84,26,23,Palette.Text,true);
        TextAt("A little motion. Everywhere.",85,66,10,Palette.Muted);
        using(var path=Palette.Round(new(498,40,102,29),14))
        { using var brush=new SolidBrush(Color.FromArgb(43,36,62)); g.FillPath(brush,path); }
        TextAt(power.Checked&&settings.HasEffect?"●  ENABLED":"●  STANDBY",510,46,8,Palette.Accent,true,90);
        Card(28,116,584,88);
        TextAt("Desktop motion blur",46,133,13,Palette.Text,true);
        TextAt(power.Checked?"Running across your connected displays":"Switch on when you're ready",46,163,10,Palette.Muted, false,450);
        TextAt("BLUR METHOD",44,235,9,Palette.Muted,true);
        TextAt(settings.Explanation,44,323,10,Palette.Muted,false,548,56);
        TextAt("Blur amount",44,395,12,amount.Enabled?Palette.Text:Palette.Disabled,true);
        TextAt(settings.FixedStrength?"FIXED":$"{settings.Amount}%",531,392,17,amount.Enabled?Palette.Accent:Palette.Disabled,true,80);
        TextAt("0% · Off",44,477,9,Palette.Muted); TextAt("100%",552,477,9,Palette.Muted);
        Card(28,516,584,90);
        TextAt("Refresh rate scaling",46,533,12,scaling.Enabled?Palette.Text:Palette.Disabled,true);
        TextAt(scaling.Enabled?"Keep a similar blur exposure at different refresh rates.":"Available with Velocity based only.",46,564,9,scaling.Enabled?Palette.Muted:Palette.Disabled,false,460);
        TextAt("YOUR SHORTCUTS",44,631,8,Palette.Muted,true);
        TextAt(toggleRegistered?"Ctrl + Alt + B   Toggle": "Toggle shortcut unavailable · already in use",44,654,9,Palette.Text);
        TextAt(stopRegistered?"Ctrl + Alt + Esc   Turn off":"Off shortcut unavailable · use the tray menu",44,676,9,Palette.Muted);
        using(var pen=new Pen(Palette.Border)) g.DrawLine(pen,28,707,612,707);
        TextAt(status,43,719,8,status.StartsWith("Live")?Palette.Accent:Palette.Muted,false,550,24);
    }
    internal void CapturePreview(string file)
    {
        using var bitmap=new Bitmap(Width,Height);
        using(var graphics=Graphics.FromImage(bitmap)) graphics.CopyFromScreen(Location,Point.Empty,Size);
        bitmap.Save(file);
    }
    internal void TestPower(bool enabled) => power.Checked=enabled;
    internal string TestStatus => engine.Status();
    // Used by the integration check to exercise the same controls and grey-out rules as a user.
    internal void ValidateControlRules()
    {
        foreach(BlurMode mode in Enum.GetValues<BlurMode>())
        {
            Change(new Settings(mode,50,false));
            if(amount.Enabled!=(mode is not (BlurMode.FrameBlending or BlurMode.Hybrid))) throw new Exception("Amount control state is incorrect.");
            if(scaling.Enabled!=(mode==BlurMode.Velocity)) throw new Exception("Refresh scaling control state is incorrect.");
        }
        Change(new Settings());
    }
}
