namespace BingoHud.Core;

/// <summary>
/// The one folder Bingo writes to. Everything that has to survive a restart lives here, and
/// nothing secret does.
///
/// <para>
/// Local application data rather than roaming: the HUD position is per-machine, and it shares
/// a file with the other settings, so the whole file is local.
/// </para>
/// </summary>
public static class AppData
{
    public static string Directory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Bingo");
}
