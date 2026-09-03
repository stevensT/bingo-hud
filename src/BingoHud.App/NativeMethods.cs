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
