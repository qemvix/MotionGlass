using System.Runtime.InteropServices;

namespace MotionGlass;

internal static class Native
{
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowDisplayAffinity(nint hwnd, uint affinity);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool GetWindowDisplayAffinity(nint hwnd, out uint affinity);
    [DllImport("user32.dll")] internal static extern bool SetLayeredWindowAttributes(nint hwnd, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] internal static extern bool GetCursorInfo(ref CursorInfo info);
    [DllImport("user32.dll")] internal static extern bool GetIconInfo(nint icon, out IconInfo info);
    [DllImport("user32.dll")] internal static extern bool DrawIconEx(nint hdc, int x, int y, nint icon, int width, int height, uint step, nint brush, uint flags);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(nint obj);
    [StructLayout(LayoutKind.Sequential)] internal struct CursorInfo { public uint Size, Flags; public nint Cursor; public Point Position; }
    [StructLayout(LayoutKind.Sequential)] internal struct IconInfo { [MarshalAs(UnmanagedType.Bool)] public bool IsIcon; public uint HotX, HotY; public nint Mask, Color; }
    internal const int ToggleHotkey = 4101, StopHotkey = 4102;
}
