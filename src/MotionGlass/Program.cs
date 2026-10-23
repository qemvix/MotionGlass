namespace MotionGlass;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if(args.Contains("--self-test")) { SelfTest.Run(args); return; }
#if MOTIONGLASS_DEVELOPMENT_TESTS
        if(args.Contains("--desktop-test")) { SelfTest.Desktop(args); return; }
#endif
        if(!OperatingSystem.IsWindowsVersionAtLeast(10,0,19041))
        { MessageBox.Show("MotionGlass needs Windows 10 version 2004 or newer.","MotionGlass"); return; }
        using var instance=new Mutex(true,"Local\\MotionGlass.DesktopBlur",out bool created);
        if(!created) { MessageBox.Show("MotionGlass is already running. Open it from the system tray.","MotionGlass"); return; }
        Application.Run(new MainForm(args.Contains("--fresh")));
    }
}
