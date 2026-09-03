// Throwaway spike. See specs/quota-hud/spikes/click-through-probe.md for the question,
// the method, and the decision criteria. Deleted when the spike closes.
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

static class Program
{
    static readonly List<string> Presses = new();

    [STAThread]
    static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

        var under = MakeWindow("Under", Color.FromRgb(200, 220, 255));
        var hud = MakeWindow("HUD", Color.FromArgb(220, 30, 30, 30));
        hud.WindowStyle = WindowStyle.None;
        hud.AllowsTransparency = true;
        hud.Topmost = true;
        hud.ShowInTaskbar = false;
        hud.ResizeMode = ResizeMode.NoResize;

        under.Show();
        hud.Show();

        app.Dispatcher.InvokeAsync(async () =>
        {
            int code;
            try { code = await Run(hud) ? 0 : 1; }
            catch (Exception ex) { Console.WriteLine($"crashed: {ex}"); code = 2; }
            app.Shutdown(code);
        }, DispatcherPriority.ApplicationIdle);

        return app.Run();
    }

    static Window MakeWindow(string name, Color colour)
    {
        var w = new Window
        {
            Title = name,
            Left = 300, Top = 300, Width = 320, Height = 200,
            Background = new SolidColorBrush(colour),
        };
        w.MouseLeftButtonDown += (_, _) => Presses.Add(name);
        return w;
    }

    static async Task<bool> Run(Window hud)
    {
        var hwnd = new WindowInteropHelper(hud).Handle;
        Native.GetWindowRect(hwnd, out var r);
        int cx = (r.Left + r.Right) / 2, cy = (r.Top + r.Bottom) / 2;
        var ok = true;

        // a: transparent, cursor over HUD → Under gets the click.
        SetTransparent(hwnd, true);
        Native.SetCursorPos(cx, cy);
        await Task.Delay(200);
        ok &= Step("a", "Under", await ClickAndSee());

        // b: style removed at runtime, cursor unchanged → HUD gets it.
        SetTransparent(hwnd, false);
        await Task.Delay(200);
        ok &= Step("b", "HUD", await ClickAndSee());

        // c: style restored at runtime, cursor unchanged → Under gets it again.
        SetTransparent(hwnd, true);
        await Task.Delay(200);
        ok &= Step("c", "Under", await ClickAndSee());

        // d: a 50 ms cursor timer owns the style. Out, in (click), out.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) =>
        {
            Native.GetCursorPos(out var p);
            Native.GetWindowRect(hwnd, out var rect);
            var inside = p.X >= rect.Left && p.X < rect.Right && p.Y >= rect.Top && p.Y < rect.Bottom;
            SetTransparent(hwnd, !inside);
        };
        timer.Start();

        Native.SetCursorPos(r.Left - 60, cy);
        await Task.Delay(200);
        var outBefore = IsTransparent(hwnd);
        Native.SetCursorPos(cx, cy);
        await Task.Delay(200);
        var inClear = !IsTransparent(hwnd);
        var receiver = await ClickAndSee();
        Native.SetCursorPos(r.Left - 60, cy);
        await Task.Delay(200);
        var outAfter = IsTransparent(hwnd);
        timer.Stop();

        var d = outBefore && inClear && receiver == "HUD" && outAfter;
        Console.WriteLine($"d: transparent-when-out={outBefore} clear-when-in={inClear} click→{receiver} transparent-after-leave={outAfter} {(d ? "PASS" : "FAIL")}");
        ok &= d;

        Console.WriteLine(ok ? "ALL PASS" : "FAILED");
        return ok;
    }

    static bool Step(string name, string expected, string got)
    {
        var pass = got == expected;
        Console.WriteLine($"{name}: expected {expected}, got {got} {(pass ? "PASS" : "FAIL")}");
        return pass;
    }

    static async Task<string> ClickAndSee()
    {
        Presses.Clear();
        Native.Click();
        await Task.Delay(250);
        return Presses.Count == 0 ? "nobody" : string.Join("+", Presses);
    }

    static void SetTransparent(IntPtr hwnd, bool on)
    {
        var style = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
        style = on ? style | Native.WS_EX_TRANSPARENT : style & ~Native.WS_EX_TRANSPARENT;
        Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, new IntPtr(style));
    }

    static bool IsTransparent(IntPtr hwnd) =>
        (Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64() & Native.WS_EX_TRANSPARENT) != 0;
}

static class Native
{
    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_TRANSPARENT = 0x20;
    const uint INPUT_MOUSE = 0;
    const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    const uint MOUSEEVENTF_LEFTUP = 0x0004;

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    // MOUSEINPUT is the largest member of the INPUT union, so this layout matches the native size.
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public MOUSEINPUT mi; }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    public static void Click()
    {
        var inputs = new[]
        {
            new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN } },
            new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP } },
        };
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length) Console.WriteLine($"SendInput sent {sent}/2, error {Marshal.GetLastWin32Error()}");
    }
}
