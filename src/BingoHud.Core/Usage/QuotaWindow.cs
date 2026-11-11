namespace BingoHud.Core.Usage;

/// <summary>
/// One quota window as the server reported it.
///
/// <para>
/// <see cref="UsedPercent"/> is stored exactly as received — consumed, 0 to 100. The display
/// direction is a user setting, and it is applied once at render. Keeping the stored value in
/// the server's direction means there is never a question of which way round a percentage is
/// at a given point in the pipeline.
/// </para>
/// </summary>
/// <param name="Kind">Which window this is.</param>
/// <param name="UsedPercent">Utilization as the server reported it: consumed, 0 to 100.</param>
/// <param name="ResetsAt">
/// When the window resets. Null is a real, observed case: a window can report a utilization
/// with no reset time. Such a window shows its percentage and no countdown — never a guessed
/// one.
/// </param>
/// <param name="Severity">
/// The server's own assessment, carried rather than derived. Local thresholds are a separate
/// judgement made later; this is what the endpoint said.
/// </param>
/// <param name="Scope">
/// What this window is restricted to, for a per-model weekly cap: the server's own name for the
/// model, read from <c>scope.model.display_name</c>, or <c>scope.model.id</c> when there is no
/// usable display name, and shown in the detail panel exactly as sent. Null for the two windows
/// the HUD shows, which are restricted to nothing, and always null on the flat-key fallback,
/// which carries no scope. Bingo maps the name to no friendlier one, because a
/// label invented for a cap is the panel saying something the server did not.
/// </param>
public sealed record QuotaWindow(
    WindowKind Kind,
    double UsedPercent,
    DateTimeOffset? ResetsAt,
    ServerSeverity Severity,
    string? Scope = null);
