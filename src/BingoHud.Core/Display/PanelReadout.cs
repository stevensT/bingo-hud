using System.Globalization;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Display;

/// <summary>
/// Composes the detail panel (AC-23, AC-24). A pure function, for the same reason
/// <see cref="Readout"/> is one: the WPF layer places these strings and decides none of them.
///
/// <para>
/// Where the HUD's job is to say as little as possible, the panel's is to make the HUD
/// accountable. It shows readings the HUD refuses to show — stale ones, frozen ones — because
/// the HUD blanking itself is not an explanation, and the panel is where a user goes to get one.
/// Showing them here is not a breach of principle 6: a number carrying its age and its status is
/// exactly what that principle asks for, and it is the bare percentage beside a live countdown
/// that the HUD is protecting against.
/// </para>
/// </summary>
public static class PanelReadout
{
    /// <summary>
    /// The panel's contents at this instant.
    /// </summary>
    /// <param name="state">The monitor's current state.</param>
    /// <param name="settings">The user's display preferences; the panel reads percentages the same way the HUD does.</param>
    /// <param name="version">The running build, which only the shell can know.</param>
    /// <param name="now">The moment of rendering, carrying the offset times are shown in.</param>
    /// <param name="culture">Whose clock conventions the times use.</param>
    /// <param name="lastRefresh">
    /// What came of the refresh the user last asked for, or null if they have not asked (AC-28).
    /// </param>
    public static PanelContent Compose(
        ReadingState state,
        UserSettings settings,
        string version,
        DateTimeOffset now,
        CultureInfo? culture = null,
        RefreshResult? lastRefresh = null)
    {
        var snapshot = state.Last;

        var windows = Rows(
            snapshot,
            w => w.Kind is WindowKind.Session or WindowKind.WeeklyAll,
            w => WindowName.Short(w.Kind),
            settings,
            now,
            culture);

        var perModel = Rows(
            snapshot,
            w => w.Kind == WindowKind.WeeklyScoped,
            // The server's scope string, unaltered. Mapping it to a friendlier model name would
            // mean inventing a vocabulary for a field no capture has ever carried a value in.
            w => w.Scope ?? UnnamedScope,
            settings,
            now,
            culture);

        return new PanelContent(
            Windows: windows,
            PerModelCaps: perModel,
            PerModelCapsEmptyState: perModel.Count == 0 ? NoPerModelCaps : null,
            // Always shown, rather than only once a reading goes stale. A number on this
            // screen without its age is what principle 6 forbids, and a user who only ever sees
            // the age appear when something is wrong has no idea what it looks like when things
            // are right.
            Age: snapshot is null ? null : AgeText.Old(state.Age),
            LastPoll: snapshot is null ? "never" : ResetFormatter.Exact(snapshot.ObservedAt, now, culture),
            NextPoll: state.PollReason,
            Version: version,
            Status: StatusMessage.Describe(state),
            RefreshNotice: RefreshNotice.Describe(lastRefresh, now));
    }

    /// <summary>
    /// Said in place of the per-model rows when the account has none.
    ///
    /// <para>
    /// The common case, not the exceptional one: every capture so far reports these keys as
    /// null. An empty area under a heading reads as a section that failed to load, so the panel
    /// says outright that the server reported none.
    /// </para>
    /// </summary>
    private const string NoPerModelCaps = "None reported for this account.";

    /// <summary>
    /// A per-model cap whose scope came back empty. The normalizer will not produce one — a cap
    /// with no scope is not a per-model cap — so this exists to make a future regression legible
    /// on screen rather than to be seen.
    /// </summary>
    private const string UnnamedScope = "unnamed";

    /// <summary>Said in place of a reset time the server did not report.</summary>
    private const string NoResetTime = "not reported";

    private static IReadOnlyList<PanelRow> Rows(
        QuotaSnapshot? snapshot,
        Func<QuotaWindow, bool> include,
        Func<QuotaWindow, string> label,
        UserSettings settings,
        DateTimeOffset now,
        CultureInfo? culture)
    {
        if (snapshot is null)
        {
            return [];
        }

        return snapshot.Windows
            .Where(include)
            .Select(w => new PanelRow(
                label(w),
                Percentage.Describe(w.UsedPercent, settings.Direction),
                w.ResetsAt is { } resetsAt
                    ? ResetFormatter.Exact(resetsAt, now, culture)
                    : NoResetTime))
            .ToList();
    }
}
