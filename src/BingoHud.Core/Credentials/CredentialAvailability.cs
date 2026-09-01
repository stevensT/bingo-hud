namespace BingoHud.Core.Credentials;

/// <summary>
/// What is actually wrong when the credential file cannot be read.
///
/// <para>
/// This exists for one reason: "you are not signed in" and "Bingo is not allowed to read your
/// credentials" send the user to completely different places, and getting it backwards costs
/// more than saying nothing would. Someone told to sign in when the real problem is a file
/// permission will sign in, watch nothing change, and have no idea why.
/// </para>
/// </summary>
public enum CredentialAvailability
{
    /// <summary>The file is there and can be opened.</summary>
    Readable,

    /// <summary>No file. The user has not signed in, or the profile was cleared.</summary>
    Absent,

    /// <summary>The file exists and the operating system refused to open it.</summary>
    AccessDenied,

    /// <summary>
    /// The file exists but something else is holding it right now. Transient, and worth
    /// distinguishing because the advice is to wait rather than to act.
    /// </summary>
    Busy,
}
