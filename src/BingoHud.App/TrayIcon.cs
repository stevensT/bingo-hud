using System.Windows;
using Forms = System.Windows.Forms;

namespace BingoHud.App;

/// <summary>
/// Bingo's presence in the notification area, and the menu on it.
///
/// <para>
/// The menu carries the settings and actions that have no other route in the app. Collapse and
/// display direction are both user settings the spec requires (AC-7, AC-2a) and both persist
/// (AC-22), but until now neither could be changed without editing the settings file by hand.
/// Opening the panel is here as well as on the HUD, because a HUD parked under another window is
/// hard to click and the tray icon never is.
/// </para>
/// <para>
/// Windows offers no WPF control for the notification area, so this wraps the Windows Forms one.
/// That is a deliberate dependency: the alternatives were a NuGet package or several hundred
/// lines of <c>Shell_NotifyIcon</c> plumbing with a message-only window to receive its callbacks.
/// </para>
/// <para>
/// The menu is a Windows Forms menu and so follows the system theme rather than the dark palette
/// the HUD and panel use. That is correct rather than unfinished: a notification-area menu that
/// ignores the system theme is the one that looks wrong.
/// </para>
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _collapse;
    private readonly Forms.ToolStripMenuItem _remaining;

    /// <param name="settings">The settings as they stand, read whenever the menu opens.</param>
    /// <param name="change">Applies and persists a change the user made from the menu.</param>
    /// <param name="openPanel">Opens the detail panel.</param>
    /// <param name="quit">Ends the application.</param>
    public TrayIcon(
        Func<Core.Settings.UserSettings> settings,
        Action<Core.Settings.UserSettings> change,
        Action openPanel,
        Action quit)
    {
        _collapse = new Forms.ToolStripMenuItem("Collapse to one line") { CheckOnClick = true };
        _collapse.Click += (_, _) => change(settings() with { Collapse = _collapse.Checked });

        _remaining = new Forms.ToolStripMenuItem("Show percentage remaining") { CheckOnClick = true };
        _remaining.Click += (_, _) => change(settings() with
        {
            Direction = _remaining.Checked
                ? Core.Settings.DisplayDirection.Remaining
                : Core.Settings.DisplayDirection.Consumed,
        });

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(new Forms.ToolStripMenuItem("Show details", null, (_, _) => openPanel()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_collapse);
        menu.Items.Add(_remaining);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("Quit Bingo", null, (_, _) => quit()));

        // Read from the settings each time rather than tracked here. The menu is not the only
        // thing that can change them, and a checkmark that remembers its own state is a
        // checkmark that will eventually disagree with the setting it claims to show.
        menu.Opening += (_, _) =>
        {
            var current = settings();
            _collapse.Checked = current.Collapse;
            _remaining.Checked = current.Direction == Core.Settings.DisplayDirection.Remaining;
        };

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            // The name shown on hover. Deliberately not a reading: the tooltip appears only on
            // hover and would be a percentage with no age beside it, which is the one thing the
            // HUD's own rules forbid.
            Text = "Bingo",
            ContextMenuStrip = menu,
            Visible = true,
        };

        // Double-click is the conventional way to open a tray app's window, and costs a user
        // nothing to try.
        _icon.DoubleClick += (_, _) => openPanel();
    }

    /// <summary>
    /// The icon, from the assembly rather than from a file beside the exe, so it is present
    /// however the app was published.
    /// </summary>
    private static System.Drawing.Icon LoadIcon()
    {
        var uri = new Uri("pack://application:,,,/Assets/bingo.ico");
        using var stream = Application.GetResourceStream(uri)?.Stream
            ?? throw new InvalidOperationException("The tray icon is missing from the assembly.");

        // Asked for the size the notification area wants. Left to itself the constructor takes
        // the first frame, which is not necessarily the one that will look right.
        return new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }

    public void Dispose()
    {
        // Explicitly hidden first. A NotifyIcon that is only disposed can leave its icon in the
        // notification area until something makes Windows re-examine it.
        _icon.Visible = false;
        _icon.Dispose();
    }
}
