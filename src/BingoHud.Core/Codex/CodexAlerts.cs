using BingoHud.Core.Alerts;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Codex;

/// <summary>
/// The Codex alerts a reading is due (codex-quota AC-10, AC-3).
///
/// <para>
/// The same engine, thresholds and once-per-occurrence rule as Claude's, with the alerts marked as
/// Codex's. One step comes first: a window whose reset has passed is left out. A Codex reading is
/// as old as the last Codex turn, so the morning after a heavy evening it may still hold 95% for a
/// window that reset overnight. That figure belongs to an ended occurrence, the HUD shows no
/// figure for it, and a notification about it would announce a limit that has already lifted.
/// </para>
/// </summary>
public static class CodexAlerts
{
    public static IReadOnlyList<Alert> TakeNew(
        AlertEngine engine, CodexState state, Thresholds thresholds, DateTimeOffset now)
    {
        if (state.Last is not { } snapshot)
        {
            return [];
        }

        var live = snapshot.Windows
            .Where(window => window.ResetsAt is not { } resetsAt || resetsAt > now)
            .ToList();

        return engine.TakeNewAlerts(snapshot with { Windows = live }, thresholds, Product.Codex);
    }
}
