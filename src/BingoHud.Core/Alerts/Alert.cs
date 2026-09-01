using BingoHud.Core.Usage;

namespace BingoHud.Core.Alerts;

/// <summary>
/// One alert the user is due, as a decision rather than as a message.
///
/// <para>
/// It carries no title or body. The wording of every user-facing state is written in one place
/// so the vocabulary stays consistent, and this record holds the facts that wording needs: which
/// window, which line was crossed, how bad it is, and the number to quote.
/// </para>
/// </summary>
/// <param name="Key">Which threshold, on which window, in which occurrence.</param>
/// <param name="Severity">How the alert should be presented.</param>
/// <param name="UsedPercent">Utilization as the server reported it: consumed, 0 to 100.</param>
public sealed record Alert(AlertKey Key, Severity Severity, double UsedPercent);
