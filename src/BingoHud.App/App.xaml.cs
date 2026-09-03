using System.Windows;
using BingoHud.Core.Settings;

namespace BingoHud.App;

/// <summary>
/// The composition root. Everything Core provides is constructed here and handed to the shell;
/// nothing else in <c>App</c> news up a Core component.
/// </summary>
public partial class App : Application
{
    private readonly SettingsStore _settingsStore = new(SettingsStore.DefaultPath);
    private UserSettings _settings = UserSettings.Default;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settings = _settingsStore.Load();

        MainWindow = new HudWindow(_settings.Position, position => Remember(_settings with { Position = position }));
        MainWindow.Show();
    }

    // deferred: a failed save is dropped on the floor here. The settings still apply for this
    // session; surface it in the detail panel once 6.8 gives it somewhere to go.
    private void Remember(UserSettings changed)
    {
        _settings = changed;
        _settingsStore.Save(changed);
    }
}
