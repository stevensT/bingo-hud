using System.Text.Json;

namespace BingoHud.Core.Credentials;

/// <summary>
/// Reads the OAuth token from Claude Code's credential file.
///
/// <para>
/// This is the whole of Bingo's credential handling. It never refreshes the token, never writes
/// the file, and never starts a process — see the decision recorded at task 3.1. Claude Code
/// maintains the token, and Bingo only has anything to report while Claude Code is being used.
/// </para>
/// <para>
/// Every failure returns null rather than throwing. A missing file, a file in a shape this
/// version does not know, a file being rewritten as it is read — all of them mean the same
/// thing to the app, which is that there is no usable token right now.
/// </para>
/// </summary>
public sealed class FileCredentialProvider
{
    private readonly string _path;

    public FileCredentialProvider(string path) => _path = path;

    /// <summary>
    /// Where Claude Code keeps the file.
    /// </summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude",
        ".credentials.json");

    /// <summary>
    /// Reads the current token, or null when the file is missing, unreadable, or carries no
    /// token.
    /// </summary>
    public async Task<Credential?> GetAsync(CancellationToken cancellationToken = default)
    {
        string content;

        try
        {
            content = await File.ReadAllTextAsync(_path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException
            or NotSupportedException or ArgumentException)
        {
            // Missing, locked, denied, or a path the platform will not accept. Whether the file
            // exists but is refused is a question worth answering separately, because it sends
            // the user somewhere different — that is what CredentialAvailability is for.
            return null;
        }

        return ReadCredential(content);
    }

    /// <summary>
    /// Pulls the token out of the file's contents. The nested <c>claudeAiOauth</c> object is the
    /// shape Claude Code writes today; a token at the root is the older shape, kept because
    /// falling back costs one lookup and failing to would present as an unexplained sign-in
    /// prompt.
    /// </summary>
    private static Credential? ReadCredential(string content)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            // The expiry is taken from whichever object supplied the token. An expiry sitting
            // beside a different token describes that other token, and borrowing it would
            // attach a confident-looking lifetime to a credential it says nothing about.
            var oauth = root.TryGetProperty("claudeAiOauth", out var nested) ? nested : default;

            if (ReadToken(oauth) is { } nestedToken)
            {
                return new Credential(nestedToken, ReadExpiresAt(oauth));
            }

            return ReadToken(root) is { } rootToken
                ? new Credential(rootToken, ReadExpiresAt(root))
                : null;
        }
    }

    /// <summary>
    /// The magnitude at which a Unix timestamp stops being plausible as seconds and starts being
    /// milliseconds.
    ///
    /// <para>
    /// Ten billion seconds is the year 2286; ten billion milliseconds is 1970. No real token
    /// expiry falls near either, so this is the one boundary where the two readings cannot both
    /// be credible. The live file stores milliseconds and prior art has seen seconds, and the
    /// file itself never says which.
    /// </para>
    /// </summary>
    private const long MillisecondsBoundary = 10_000_000_000;

    /// <summary>
    /// Reads <c>expiresAt</c>, inferring its unit from magnitude. Anything unreadable yields
    /// null: a token with no known expiry is a state the app already handles, and a wrongly
    /// scaled one is not — read as seconds, a millisecond value lands tens of thousands of
    /// years out, so the token would never appear to expire.
    /// </summary>
    private static DateTimeOffset? ReadExpiresAt(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("expiresAt", out var expiresAt)
            || expiresAt.ValueKind != JsonValueKind.Number
            || !expiresAt.TryGetInt64(out var value))
        {
            return null;
        }

        return value >= MillisecondsBoundary
            ? DateTimeOffset.FromUnixTimeMilliseconds(value)
            : DateTimeOffset.FromUnixTimeSeconds(value);
    }

    /// <summary>
    /// Reads an <c>accessToken</c> from an object, or null when there is not a usable one. A
    /// blank token counts as absent: sending it would produce a 401, which would report the
    /// user as rejected when they are simply not signed in.
    /// </summary>
    private static string? ReadToken(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("accessToken", out var accessToken)
            || accessToken.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var token = accessToken.GetString();

        return string.IsNullOrWhiteSpace(token) ? null : token;
    }
}
