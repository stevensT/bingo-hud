namespace BingoHud.Core.Display;

/// <summary>The panel's independent settings-save notice, cleared by the next successful save.</summary>
public static class SettingsNotice
{
    public static string? Describe(bool saved, string path) => saved
        ? null
        : $"Settings could not be saved to {path}. Your changes apply until Bingo closes.";
}
