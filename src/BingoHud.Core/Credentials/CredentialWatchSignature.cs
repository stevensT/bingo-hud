namespace BingoHud.Core.Credentials;

/// <summary>
/// A cheap fingerprint of the credential file, used to decide whether it is worth re-reading.
///
/// <para>
/// Claude Code rewrites the file when it refreshes the token, and that write is the only event
/// Bingo needs to react to. Comparing two fingerprints costs a metadata lookup, where re-reading
/// on a timer would mean opening a file holding a secret over and over for nothing.
/// </para>
/// <para>
/// Metadata only: path, size, and last write time. The file is never opened and the token is
/// never touched, which also sets the limit of the technique — a change that keeps both the byte
/// count and the timestamp identical is invisible. Nothing writes a credential file that way,
/// and the cost of missing one would be a stale read rather than a wrong number.
/// </para>
/// </summary>
/// <param name="Path">Which file this describes. Part of the identity, so a fingerprint taken
/// for one file can never compare equal to another's.</param>
/// <param name="Exists">Whether the file was there at all. Signing in and signing out are both
/// changes worth noticing.</param>
/// <param name="Length">Size in bytes, or zero when the file is absent.</param>
/// <param name="LastWriteUtc">Last write time, or default when the file is absent.</param>
public readonly record struct CredentialWatchSignature(
    string Path,
    bool Exists,
    long Length,
    DateTime LastWriteUtc)
{
    /// <summary>
    /// Takes a fingerprint of the file at <paramref name="path"/>. A file that is missing, or
    /// whose metadata cannot be read, fingerprints as absent — the caller's next read will
    /// establish what is actually wrong.
    /// </summary>
    public static CredentialWatchSignature Capture(string path)
    {
        try
        {
            var file = new FileInfo(path);

            return file.Exists
                ? new CredentialWatchSignature(path, true, file.Length, file.LastWriteTimeUtc)
                : Absent(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException
            or NotSupportedException or ArgumentException)
        {
            return Absent(path);
        }
    }

    private static CredentialWatchSignature Absent(string path) => new(path, false, 0, default);
}
