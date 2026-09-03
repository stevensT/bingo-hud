using System.Windows;
using System.Windows.Input;
using BingoHud.Core.Display;
using BingoHud.Core.Settings;

namespace BingoHud.App;

/// <summary>
/// The always-on-top readout. Frameless and draggable (AC-19); reports where it was dropped so
/// the position survives a restart (AC-22).
/// </summary>
public partial class HudWindow : Window
{
    // Outside the snap distance, so a fresh HUD does not jump flush on its first nudge.
    private const double Margin = EdgeSnap.Distance + 8;

    private readonly HudPosition? _remembered;
    private readonly Action<HudPosition> _moved;

    public HudWindow(HudPosition? remembered, Action<HudPosition> moved)
    {
        InitializeComponent();
        _remembered = remembered;
        _moved = moved;

        // Placed before the first frame when there is something to place it at. Whether it
        // still fits on the current screens needs the rendered size, so that check waits for
        // Loaded and moves the window only if it has to.
        if (remembered is { } p)
        {
            Left = p.Left;
            Top = p.Top;
        }

        Loaded += (_, _) => Restore();
        MouseLeftButtonDown += (_, _) => Drag();
    }

    private void Restore()
    {
        var screens = new ScreenArea(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        if (_remembered is { } p && HudPlacement.Fits(p, ActualWidth, ActualHeight, screens))
        {
            // Snapped as well as restored, so a parked HUD that drifted a pixel past its edge
            // since last run comes back flush rather than a pixel off.
            MoveTo(EdgeSnap.Snap(p, ActualWidth, ActualHeight, PrimaryWorkArea()));
            return;
        }

        // First run, or the remembered spot is on a monitor that is no longer there: the top
        // right of the primary work area, clear of the taskbar wherever it is docked.
        var work = PrimaryWorkArea();
        MoveTo(new HudPosition(work.Left + work.Width - ActualWidth - Margin, work.Top + Margin));
    }

    private void Drag()
    {
        // DragMove returns when the button is released, so the position after it is the drop.
        DragMove();

        var snapped = EdgeSnap.Snap(new HudPosition(Left, Top), ActualWidth, ActualHeight, PrimaryWorkArea());
        MoveTo(snapped);
        _moved(snapped);
    }

    private void MoveTo(HudPosition position)
    {
        Left = position.Left;
        Top = position.Top;
    }

    private static ScreenArea PrimaryWorkArea()
    {
        var work = SystemParameters.WorkArea;
        return new ScreenArea(work.Left, work.Top, work.Width, work.Height);
    }
}
