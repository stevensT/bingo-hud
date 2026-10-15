namespace BingoHud.Core.Codex;

/// <summary>
/// Finds the newest Codex rate-limit record with figures in the session logs (codex-quota AC-1).
///
/// <para>
/// Codex writes one JSONL log per session under <c>sessions\</c>, and moves old ones to
/// <c>archived_sessions\</c>. The reader takes the most recently written log first and walks it
/// from the end, so the first record with figures it meets is the newest one. A log holding only
/// null records, as the Codex app writes in a burst when it starts, yields nothing and the next
/// log is read. In practice that is one or two files: the newest log with any turn in it holds a
/// recent record.
/// </para>
/// <para>
/// The logs are the user's conversations. Only lines containing the text <c>"rate_limits"</c>
/// are handed to <see cref="CodexRecord.Parse"/>, and only what it returns is kept. Nothing else
/// in the Codex folder is opened: it also holds Codex's settings and sign-in.
/// </para>
/// </summary>
/// <param name="root">The Codex folder: <c>CODEX_HOME</c>, or <c>.codex</c> in the user profile.</param>
public sealed class CodexLogReader(string root)
{
    private const string LogPattern = "*.jsonl";

    // The two folders Codex keeps session logs in. Nothing outside them is read.
    private static readonly string[] LogFolders = ["sessions", "archived_sessions"];

    private static readonly EnumerationOptions Enumeration = new()
    {
        RecurseSubdirectories = true,
        // A subfolder Bingo may not read is not a reason to refuse every other log; the logs it
        // holds are simply not candidates.
        IgnoreInaccessible = true,
    };

    public string Root { get; } = root;

    /// <summary>
    /// The environment variable Codex reads to find its folder, read here the same way so Bingo
    /// looks where Codex writes. The one environment variable a shipped build honours: it moves
    /// where local logs are read from, and no credential or request goes anywhere because of it.
    /// </summary>
    public const string HomeVariable = "CODEX_HOME";

    /// <summary>
    /// The Codex folder: <paramref name="codexHome"/> when it is set, as Codex itself does, else
    /// <c>.codex</c> in the user profile.
    /// </summary>
    public static string RootFor(string userProfile, string? codexHome) =>
        string.IsNullOrWhiteSpace(codexHome) ? Path.Combine(userProfile, ".codex") : codexHome;

    /// <summary>
    /// When any session log was last written, or null when there are none or they cannot be
    /// listed. Reads no file, so it is cheap enough to ask every minute.
    /// </summary>
    public DateTime? NewestWrite()
    {
        if (!Directory.Exists(Root))
        {
            return null;
        }

        try
        {
            return LogFolders
                .Select(folder => Path.Combine(Root, folder))
                .Where(Directory.Exists)
                .SelectMany(folder => new DirectoryInfo(folder).EnumerateFiles(LogPattern, Enumeration))
                .Max(file => (DateTime?)file.LastWriteTimeUtc);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The scan that follows reports the folder as unreadable; this only decides whether
            // a scan is worth making.
            return null;
        }
    }

    public CodexScan Scan()
    {
        if (!Directory.Exists(Root))
        {
            return new CodexScan.NotFound();
        }

        List<FileInfo> logs;

        try
        {
            logs = NewestFirst();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Unreadable("The Codex log folders could not be listed.");
        }

        foreach (var log in logs)
        {
            string content;

            try
            {
                content = ReadShared(log.FullName);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return Unreadable(
                    "A Codex session log could not be opened, possibly because another program is holding it.");
            }

            if (NewestRecordIn(content) is { } found)
            {
                return found;
            }
        }

        return new CodexScan.NoReading();
    }

    private List<FileInfo> NewestFirst() => LogFolders
        .Select(folder => Path.Combine(Root, folder))
        .Where(Directory.Exists)
        .SelectMany(folder => new DirectoryInfo(folder).EnumerateFiles(LogPattern, Enumeration))
        .OrderByDescending(file => file.LastWriteTimeUtc)
        .ToList();

    /// <summary>
    /// The whole log, opened so that Codex can go on writing it while it is read. Codex appends
    /// to the log of a running session, and a reader that locked it out would break Codex.
    /// </summary>
    private static string ReadShared(string path)
    {
        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    /// <summary>
    /// The newest record with figures in one log, a problem if the newest record cannot be read,
    /// or null when the log holds no record with figures.
    /// </summary>
    private static CodexScan? NewestRecordIn(string content)
    {
        var lines = content.Split('\n');

        // Every finished line ends in a newline, so the text after the last one is a line Codex
        // is still writing, or nothing at all. It is never a record yet.
        for (var i = lines.Length - 2; i >= 0; i--)
        {
            if (!lines[i].Contains("\"rate_limits\"", StringComparison.Ordinal))
            {
                continue;
            }

            switch (CodexRecord.Parse(lines[i]))
            {
                case CodexRecordResult.Figures figures:
                    return new CodexScan.Reading(figures);

                // The newest record says Codex has changed what it writes. An older figure must
                // not stand in for it as though it were current.
                case CodexRecordResult.Unreadable unreadable:
                    return new CodexScan.Problem(new CodexProblem.UnknownShape(unreadable.Reason));

                // A null record, or a conversation line that mentions rate limits: keep looking.
                default:
                    continue;
            }
        }

        return null;
    }

    private static CodexScan.Problem Unreadable(string reason) =>
        new(new CodexProblem.LogsUnreadable(reason));
}
