namespace BingoHud.Core.Codex;

/// <summary>
/// What one look through the Codex session logs found (codex-quota AC-1, AC-7).
/// </summary>
public abstract record CodexScan
{
    private CodexScan() { }

    /// <summary>There is no Codex folder at all: Codex is not installed for this user.</summary>
    public sealed record NotFound : CodexScan;

    /// <summary>
    /// The folder is there and was read, and no record with figures is in it: Codex has not been
    /// used, or has written only null records.
    /// </summary>
    public sealed record NoReading : CodexScan;

    /// <summary>The newest record with figures.</summary>
    public sealed record Reading(CodexRecordResult.Figures Figures) : CodexScan;

    /// <summary>The logs could not be read, or the newest record could not be understood.</summary>
    public sealed record Problem(CodexProblem Cause) : CodexScan;
}

/// <summary>
/// Why a look through the logs gave nothing usable. Two cases, because they call for different
/// advice: one is the machine refusing access, the other is Codex having changed what it writes.
/// </summary>
public abstract record CodexProblem
{
    private CodexProblem() { }

    /// <summary>A folder or file could not be opened.</summary>
    /// <param name="Reason">What failed, as a whole sentence, for the detail panel.</param>
    public sealed record LogsUnreadable(string Reason) : CodexProblem;

    /// <summary>The newest rate-limit record is in a shape Bingo does not recognise.</summary>
    /// <param name="Reason">The parser's reason, as a whole sentence, for the detail panel.</param>
    public sealed record UnknownShape(string Reason) : CodexProblem;
}
