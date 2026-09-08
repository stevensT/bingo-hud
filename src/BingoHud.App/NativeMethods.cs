using System.Runtime.InteropServices;

namespace BingoHud.App;

/// <summary>
/// Every call into Win32 the shell makes, in one place. Nothing else in <c>App</c> declares a
/// <c>DllImport</c>.
/// </summary>
internal static class NativeMethods
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TRANSPARENT = 0x20;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int size);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    /// <summary>
    /// The work area of the monitor under the window's top-left corner, in physical pixels:
    /// that monitor's rectangle minus the taskbar and any app bars. The nearest monitor if the
    /// corner is on none. Null if Windows cannot say, which it can during a display change.
    ///
    /// <para>
    /// The corner rather than the window, because the window is what has just changed size.
    /// Asked by whole window, a HUD grown across the seam between two monitors would belong to
    /// whichever held more of it, and be pulled fully onto the neighbour.
    /// </para>
    /// </summary>
    public static (int Left, int Top, int Right, int Bottom)? WorkAreaAtCornerOf(IntPtr hwnd)
    {
        GetWindowRect(hwnd, out var r);
        var monitor = MonitorFromPoint(new POINT { X = r.Left, Y = r.Top }, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };

        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            return null;
        }

        return (info.rcWork.Left, info.rcWork.Top, info.rcWork.Right, info.rcWork.Bottom);
    }

    /// <summary>
    /// Windows draws a window's title bar and border itself, in the system theme, so a dark
    /// window gets a light frame unless it asks for otherwise. This asks.
    /// </summary>
    /// <remarks>
    /// The attribute was numbered 19 before Windows 10 build 18985 and 20 from then on. Both are
    /// set: the wrong one on a given build is an unrecognized attribute, which the call rejects
    /// and nothing else. The result is ignored for the same reason — a light title bar is a
    /// cosmetic mismatch, not a condition worth failing a window over.
    /// </remarks>
    public static void UseDarkTitleBar(IntPtr hwnd)
    {
        var on = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref on, sizeof(int));
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
    }

    /// <summary>
    /// Whether mouse input passes through the window to whatever is beneath it. Takes effect on
    /// the next click; the window needs no re-show. Proven by the click-through spike.
    /// </summary>
    public static void SetClickThrough(IntPtr hwnd, bool on)
    {
        var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        style = on ? style | WS_EX_TRANSPARENT : style & ~WS_EX_TRANSPARENT;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style));
    }

    /// <summary>
    /// Whether the cursor is inside the window's rectangle. Asked of the system rather than of
    /// the window, because a click-through window is told nothing about the mouse.
    /// </summary>
    public static bool CursorIsOver(IntPtr hwnd)
    {
        GetCursorPos(out var p);
        GetWindowRect(hwnd, out var r);
        return p.X >= r.Left && p.X < r.Right && p.Y >= r.Top && p.Y < r.Bottom;
    }
}
