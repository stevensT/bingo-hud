using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using BingoHud.Core.Display;
using BingoHud.Core.Settings;
using BingoHud.Core.Time;

namespace BingoHud.App;

/// <summary>
/// The always-on-top readout. Frameless and draggable (AC-19); reports where it was dropped so
/// the position survives a restart (AC-22); click-through until the cursor rests on it (AC-21).
/// Shows the lines Core decides, re-read once a second (AC-1 through AC-3). A click that does
/// not move it opens the detail panel (AC-23).
/// </summary>
public partial class HudWindow : Window
{
    // Outside the snap distance, so a fresh HUD does not jump flush on its first nudge.
    private const double Inset = EdgeSnap.Distance + 8;

    private static readonly Brush SolidEdge = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF));
    private static readonly Brush Dim = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));

    private readonly HudPosition? _remembered;
    private readonly Action<HudPosition> _moved;
    private readonly IClock _clock;
    private readonly Func<IReadOnlyList<ReadoutLine>> _readout;
    private readonly Action _openPanel;
    private readonly DwellPolicy _dwell = new();
    private readonly DispatcherTimer _cursorWatch = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly DispatcherTimer _readoutWatch = new() { Interval = TimeSpan.FromSeconds(1) };
    private IReadOnlyList<ReadoutLine>? _shown;
    private IntPtr _hwnd;
    private bool _solid;

    public HudWindow(
        HudPosition? remembered,
        Action<HudPosition> moved,
        IClock clock,
        Func<IReadOnlyList<ReadoutLine>> readout,
        Action openPanel)
    {
        InitializeComponent();
        _remembered = remembered;
        _moved = moved;
        _clock = clock;
        _readout = readout;
        _openPanel = openPanel;

        // Click-through from the first frame. A click-through window is told nothing about the
        // mouse, so a timer asks the system where the cursor is and DwellPolicy decides.
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.SetClickThrough(_hwnd, on: true);
            _cursorWatch.Start();
        };
        _cursorWatch.Tick += (_, _) => WatchCursor();
        Closed += (_, _) => _cursorWatch.Stop();

        // Rendered once before placement, so Restore measures a real first frame rather than an
        // empty grid; then once a second, which is what moves the countdown and shows a new
        // reading within a second of the loop taking it.
        Render();
        _readoutWatch.Tick += (_, _) => Render();
        Closed += (_, _) => _readoutWatch.Stop();

        // Placed before the first frame when there is something to place it at. Whether it
        // still fits on the current screens needs the rendered size, so that check waits for
        // Loaded and moves the window only if it has to.
        if (remembered is { } p)
        {
            Left = p.Left;
            Top = p.Top;
        }

        Loaded += (_, _) =>
        {
            Restore();
            _readoutWatch.Start();
            // Only after placement: a resize before Loaded is layout settling, not text changing.
            SizeChanged += (_, _) => StayOnScreen();
        };
        MouseLeftButtonDown += (_, _) => Press();
    }

    /// <summary>
    /// Puts Core's lines on screen, touching the tree only when they differ from what is shown.
    /// A rebuild every second would otherwise cost a layout pass for nothing.
    /// </summary>
    private void Render()
    {
        var lines = _readout();

        if (_shown is not null && _shown.SequenceEqual(lines))
        {
            return;
        }

        _shown = lines;
        Lines.Children.Clear();
        Lines.RowDefinitions.Clear();

        if (lines.Count == 0)
        {
            // deferred: one phrase for every reason there is nothing to show. 7.1 writes the
            // copy that tells signed-out from unreadable from not-yet-fetched.
            Lines.RowDefinitions.Add(new RowDefinition());
            Lines.Children.Add(new TextBlock { Text = "no reading yet", Foreground = Dim });
            return;
        }

        for (var row = 0; row < lines.Count; row++)
        {
            Lines.RowDefinitions.Add(new RowDefinition());
            Place(new TextBlock { Text = lines[row].Window, Foreground = Dim }, row, column: 0);
            Place(new TextBlock { Text = lines[row].Percent, Margin = new Thickness(10, 0, 0, 0) }, row, column: 1);

            if (lines[row].Reset is { } reset)
            {
                Place(new TextBlock { Text = reset, Foreground = Dim, Margin = new Thickness(10, 0, 0, 0) }, row, column: 2);
            }
        }
    }

    private void Place(TextBlock text, int row, int column)
    {
        Grid.SetRow(text, row);
        Grid.SetColumn(text, column);
        Lines.Children.Add(text);
    }

    private void Restore()
    {
        if (_remembered is { } p && HudPlacement.Fits(p, ActualWidth, ActualHeight, Screens()))
        {
            // Snapped as well as restored, so a parked HUD that drifted a pixel past its edge
            // since last run comes back flush rather than a pixel off. Then kept within the
            // nearest monitor: Fits allows the gap between staggered monitors, and a HUD
            // restored into one would be invisible until its text next changed size.
            var area = WorkAreaUnderHud();
            MoveTo(HudPlacement.KeepWithin(EdgeSnap.Snap(p, ActualWidth, ActualHeight, area), ActualWidth, ActualHeight, area));
            return;
        }

        // First run, or the remembered spot is on a monitor that is no longer there: the top
        // right of the primary work area, clear of the taskbar wherever it is docked.
        var work = PrimaryWorkArea();
        MoveTo(new HudPosition(work.Left + work.Width - ActualWidth - Inset, work.Top + Inset));
    }

    /// <summary>
    /// After the text changed size: a HUD parked at a right or bottom edge has grown past it,
    /// and is pulled back only as far as it must be. The saved position is not touched; that
    /// records where the user dropped it, and this is not a drop.
    /// </summary>
    /// <remarks>
    /// Kept within the monitor it is on, not the virtual screen. The virtual screen is the
    /// bounding box of every monitor, and on a staggered layout a HUD can grow off the primary
    /// into a gap that is inside that box and on no monitor at all. Seen on first run, on a
    /// desk with a portrait monitor to the right of the primary that starts lower than it.
    /// </remarks>
    private void StayOnScreen()
    {
        var current = new HudPosition(Left, Top);
        var kept = HudPlacement.KeepWithin(current, ActualWidth, ActualHeight, WorkAreaUnderHud());

        if (kept != current)
        {
            MoveTo(kept);
        }
    }

    private void WatchCursor()
    {
        var solid = _dwell.Update(NativeMethods.CursorIsOver(_hwnd), _clock.Now);
        if (solid == _solid)
        {
            return;
        }

        _solid = solid;
        NativeMethods.SetClickThrough(_hwnd, on: !solid);
        // Visibly solid, so a click that will land is never a surprise.
        Panel.BorderBrush = solid ? SolidEdge : Brushes.Transparent;
    }

    /// <summary>
    /// A press on the HUD is either a drag or a click, and which one is not known until the
    /// button comes back up.
    ///
    /// <para>
    /// The window is moved by <see cref="Window.DragMove"/>, which returns on release, so the
    /// question "did this press move the HUD" is answered by comparing the position across it.
    /// Unmoved means the user clicked, and a click opens the panel (AC-23). Dragging the HUD to
    /// where they want it and having a window open on release would make the HUD unplaceable.
    /// </para>
    /// </summary>
    private void Press()
    {
        // Only reachable while solid: a click-through window never receives the button press.
        var before = new HudPosition(Left, Top);

        DragMove();

        if (new HudPosition(Left, Top) == before)
        {
            _openPanel();
            return;
        }

        var snapped = EdgeSnap.Snap(new HudPosition(Left, Top), ActualWidth, ActualHeight, WorkAreaUnderHud());
        MoveTo(snapped);
        _moved(snapped);
    }

    /// <summary>
    /// The work area of the monitor the HUD's top-left corner is on, in WPF units; the primary
    /// work area if Windows cannot say, which is what every caller used before there was a
    /// per-monitor answer.
    /// </summary>
    private ScreenArea WorkAreaUnderHud()
    {
        if (NativeMethods.WorkAreaAtCornerOf(_hwnd) is not var (left, top, right, bottom))
        {
            return PrimaryWorkArea();
        }

        // deferred: physical pixels scaled through this window's DPI transform. The app declares
        // no per-monitor DPI awareness, so that transform is the system scale; a second monitor
        // at a different scale gets a work area that is off by the ratio. A PerMonitorV2
        // manifest, verified on a mixed-DPI desk, lifts it.
        var source = PresentationSource.FromVisual(this)
            ?? throw new InvalidOperationException("The HUD has no window handle yet; the work area was asked before SourceInitialized.");
        var toUnits = source.CompositionTarget.TransformFromDevice;
        var topLeft = toUnits.Transform(new Point(left, top));
        var bottomRight = toUnits.Transform(new Point(right, bottom));

        return new ScreenArea(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    private void MoveTo(HudPosition position)
    {
        Left = position.Left;
        Top = position.Top;
    }

    private static ScreenArea Screens() => new(
        SystemParameters.VirtualScreenLeft,
        SystemParameters.VirtualScreenTop,
        SystemParameters.VirtualScreenWidth,
        SystemParameters.VirtualScreenHeight);

    private static ScreenArea PrimaryWorkArea()
    {
        var work = SystemParameters.WorkArea;
        return new ScreenArea(work.Left, work.Top, work.Width, work.Height);
    }
}
