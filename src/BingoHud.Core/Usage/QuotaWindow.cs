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
public sealed record QuotaWindow(
    WindowKind Kind,
    double UsedPercent,
    DateTimeOffset? ResetsAt);
