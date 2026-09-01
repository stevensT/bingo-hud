namespace BingoHud.Core.Credentials;

/// <summary>
/// Establishes why the credential file could not be read.
///
/// <para>
/// Separate from <see cref="FileCredentialProvider"/> on purpose. The provider answers "is there
/// a usable token", and its answer for every failure is the same null, because the app's
/// behaviour is the same in every failure: no token, no numbers. This answers the different
/// question of what to tell the user, and it is asked only once something has already gone
/// wrong.
/// </para>
/// </summary>
public static class CredentialProbe
{
    /// <summary>
    /// Probes the file at <paramref name="path"/> without reading its contents.
    ///
    /// <para>
    /// Two probes, because neither alone is conclusive. Opening the file is what actually
    /// establishes readability, but the exception it throws when a directory cannot be listed
    /// looks exactly like the file being missing. The metadata probe needs no access to the
    /// file's contents, so it can tell those apart.
    /// </para>
    /// </summary>
    public static CredentialAvailability Probe(string path)
    {
        var metadataSaysItExists = MetadataSaysItExists(path);

        try
        {
            // Opened, not read. FileShare.ReadWrite so that a writer holding the file the
            // ordinary way is not mistaken for a problem — Claude Code rewriting the token is
            // the expected case, not a failure.
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            return CredentialAvailability.Readable;
        }
        catch (UnauthorizedAccessException)
        {
            // Refused outright, or something that is not a file sitting where the file belongs.
            return CredentialAvailability.AccessDenied;
        }
        catch (FileNotFoundException)
        {
            return Missing(metadataSaysItExists);
        }
        catch (DirectoryNotFoundException)
        {
            return Missing(metadataSaysItExists);
        }
        catch (IOException)
        {
            // Held exclusively by someone else. Transient, and worth its own answer because the
            // advice is to wait rather than to sign in or to change a permission.
            return CredentialAvailability.Busy;
        }
    }

    /// <summary>
    /// The metadata probe. Deliberately quiet: any failure here means "cannot tell", and the
    /// open attempt is what decides.
    /// </summary>
    private static bool MetadataSaysItExists(string path)
    {
        try
        {
            return new FileInfo(path).Exists;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException
            or NotSupportedException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// The open attempt said the file is not there. Trust it unless the metadata probe saw the
    /// file, which means it exists and something about the path is being withheld from us.
    /// </summary>
    private static CredentialAvailability Missing(bool metadataSaysItExists) =>
        metadataSaysItExists
            ? CredentialAvailability.AccessDenied
            : CredentialAvailability.Absent;
}
