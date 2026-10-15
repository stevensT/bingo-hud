using System.Text.Json;
using System.Text.Json.Serialization;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Settings;

/// <summary>
/// Reads and writes <see cref="UserSettings"/> as one small JSON file a person can hand-edit.
///
/// <para>
/// Same posture as <see cref="Alerts.AlertStateStore"/> — enums by name, a file that cannot be
/// read means defaults — with one addition: a key missing from the file keeps its default
/// without resetting the rest. Every existing file is missing a key the day a version adds a
/// setting, and forgetting the user's position on every upgrade is not acceptable.
/// </para>
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Format = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        // Names only. By default the converter also takes a number, including one no member has,
        // and an undefined direction throws on every repaint; refused here, it is a file not
        // understood like any other.
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) },
    };

    private readonly string _path;

    public SettingsStore(string path) => _path = path;

    public static string DefaultPath => Path.Combine(AppData.Directory, "settings.json");

    /// <summary>
    /// The settings on disk, with defaults filled in for anything missing or unusable.
    /// </summary>
    public UserSettings Load()
    {
        FileShape? file;
        try
        {
            file = JsonSerializer.Deserialize<FileShape>(File.ReadAllText(_path), Format);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException
            or NotSupportedException or ArgumentException or JsonException)
        {
            // Missing on first run, or in a shape this version does not recognize. Either way
            // the answer is the defaults.
            return UserSettings.Default;
        }

        if (file is null)
        {
            return UserSettings.Default;
        }

        var thresholds = file.Thresholds is { WarningAtRemaining: { } warning, CriticalAtRemaining: { } critical }
            && IsSane(new Thresholds(warning, critical))
                ? new Thresholds(warning, critical)
                : Thresholds.Default;

        return new UserSettings(
            Position: file.Position,
            Collapse: file.Collapse ?? UserSettings.Default.Collapse,
            Direction: file.Direction ?? UserSettings.Default.Direction,
            Thresholds: thresholds,
            ResetFormat: file.ResetFormat ?? UserSettings.Default.ResetFormat,
            ShowBar: file.ShowBar ?? UserSettings.Default.ShowBar,
            Products: ProductsFrom(file.Products));
    }

    /// <summary>
    /// The saved answer to the first-start question, or null when there is none to honour.
    ///
    /// <para>
    /// Absent means the question was never asked, as in every file 0.4.0 wrote. A product the
    /// answer leaves out is off: the answer was given, and the product it names is what it said.
    /// An answer with nothing on cannot be shown, so it counts as no answer, and the question is
    /// asked again rather than the HUD going blank.
    /// </para>
    /// </summary>
    private static ProductSwitches? ProductsFrom(ProductsShape? shape)
    {
        if (shape is null)
        {
            return null;
        }

        var switches = new ProductSwitches(shape.Claude ?? false, shape.Codex ?? false);

        return switches.AnyOn ? switches : null;
    }

    /// <summary>
    /// Writes the settings, creating Bingo's folder on first run. Returns false when the file
    /// could not be written; the settings still apply for this session, they just will not be
    /// there next time.
    /// </summary>
    public bool Save(UserSettings settings)
    {
        // A WPF window reports NaN for its position until it has been shown and placed. That is
        // "not yet placed", not a position, and JSON has no way to write it anyway.
        if (settings.Position is { } p && !(double.IsFinite(p.Left) && double.IsFinite(p.Top)))
        {
            settings = settings with { Position = null };
        }

        try
        {
            if (Path.GetDirectoryName(_path) is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, JsonSerializer.Serialize(settings, Format));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException
            or NotSupportedException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Both lines between 0 and 100, critical no higher than warning. Equal is allowed: one
    /// line is a legitimate choice.
    /// </summary>
    private static bool IsSane(Thresholds t) =>
        t.CriticalAtRemaining >= 0
        && t.CriticalAtRemaining <= t.WarningAtRemaining
        && t.WarningAtRemaining <= 100;

    // What the file is allowed to be missing. Every field is optional on the way in so that a
    // partial file — an older version's, or a hand-edited one — fills in rather than resets.
    private sealed record FileShape(
        HudPosition? Position,
        bool? Collapse,
        DisplayDirection? Direction,
        ThresholdsShape? Thresholds,
        ResetFormat? ResetFormat,
        bool? ShowBar,
        ProductsShape? Products);

    private sealed record ThresholdsShape(double? WarningAtRemaining, double? CriticalAtRemaining);

    private sealed record ProductsShape(bool? Claude, bool? Codex);
}
