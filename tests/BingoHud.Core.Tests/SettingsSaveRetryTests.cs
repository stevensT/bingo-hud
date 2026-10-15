using BingoHud.Core.Settings;

namespace BingoHud.Core.Tests;

public class SettingsSaveRetryTests
{
    [Fact]
    public void AFileHeldForTheWholeSaveIsRetriedOnceThenReturnsFalse()
    {
        using var directory = new TempDirectory();
        var path = directory.WriteFile("settings.json", "{}");
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var pauses = 0;
        var store = new SettingsStore(path, () => pauses++);

        Assert.False(store.Save(UserSettings.Default));
        Assert.Equal(1, pauses);
    }

    [Fact]
    public void AFileReleasedDuringThePauseIsSavedOnTheRetry()
    {
        using var directory = new TempDirectory();
        var path = directory.WriteFile("settings.json", "{}");
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var pauses = 0;
        var store = new SettingsStore(path, () => { pauses++; held.Dispose(); });
        var changed = UserSettings.Default with { ShowBar = true };

        Assert.True(store.Save(changed));
        Assert.Equal(1, pauses);
        Assert.Equal(changed, new SettingsStore(path).Load());
    }

    [Fact]
    public void ASaveThatWorksFirstTimeDoesNotPause()
    {
        using var directory = new TempDirectory();
        var pauses = 0;
        var store = new SettingsStore(directory.PathTo("settings.json"), () => pauses++);

        Assert.True(store.Save(UserSettings.Default));
        Assert.Equal(0, pauses);
    }
}
