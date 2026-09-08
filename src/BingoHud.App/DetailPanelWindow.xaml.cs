using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using BingoHud.Core.Display;

namespace BingoHud.App;

/// <summary>
/// The detail panel (AC-23, AC-24): the two windows with exact reset times, per-model weekly
/// caps, the age of the reading, when the last poll succeeded, why the next one is due when it
/// is, and the running version.
///
/// <para>
/// This is where the HUD is held to account. The HUD blanks a stale or frozen reading rather
/// than show a percentage that will be read as current; the panel shows it with its age, because
/// otherwise "why has the HUD gone empty" has no answer anywhere in the app.
/// </para>
/// <para>
/// Every word here is <see cref="PanelReadout"/>'s. This file places strings and composes none.
/// </para>
/// </summary>
public partial class DetailPanelWindow : Window
{
    private static readonly Brush Dim = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));

    private readonly Func<PanelContent> _content;
    private readonly Func<Task> _refresh;
    private readonly DispatcherTimer _watch = new() { Interval = TimeSpan.FromSeconds(1) };
    private PanelContent? _shown;

    /// <param name="content">Everything the panel shows, composed in Core.</param>
    /// <param name="refresh">
    /// Asks for a refresh now (AC-28). Whether one happened is not returned: the answer shows up
    /// in the next composed content, either as a newer reading or as a refusal that says why.
    /// </param>
    public DetailPanelWindow(Func<PanelContent> content, Func<Task> refresh)
    {
        InitializeComponent();
        _content = content;
        _refresh = refresh;

        Refresh.Click += async (_, _) =>
        {
            // Disabled only for the duration of the call. The monitor joins concurrent callers
            // rather than starting a second fetch, so a double click is harmless; this is for
            // the user, who otherwise gets no sign the button did anything.
            Refresh.IsEnabled = false;

            try
            {
                await _refresh();
            }
            finally
            {
                Refresh.IsEnabled = true;
            }

            Render();
        };

        // Once a second, matching the HUD. The age and the last-poll time are the whole point of
        // this window, and a panel left open showing an age that stopped counting would be
        // saying something false about how old the reading is.
        Render();
        _watch.Tick += (_, _) => Render();
        Loaded += (_, _) => _watch.Start();
        Closed += (_, _) => _watch.Stop();

        // The frame is Windows' to draw, and it draws it light unless asked. Asked here rather
        // than in XAML because there is no XAML for it.
        SourceInitialized += (_, _) =>
            NativeMethods.UseDarkTitleBar(new WindowInteropHelper(this).Handle);
    }

    private void Render()
    {
        var content = _content();

        if (_shown is not null && Same(_shown, content))
        {
            return;
        }

        _shown = content;

        Fill(Windows, content.Windows);
        Fill(PerModelCaps, content.PerModelCaps);

        PerModelCapsEmptyState.Text = content.PerModelCapsEmptyState ?? string.Empty;
        PerModelCapsEmptyState.Visibility =
            content.PerModelCapsEmptyState is null ? Visibility.Collapsed : Visibility.Visible;

        RefreshNotice.Text = content.RefreshNotice ?? string.Empty;
        RefreshNotice.Visibility =
            content.RefreshNotice is null ? Visibility.Collapsed : Visibility.Visible;

        Facts.Children.Clear();
        Facts.RowDefinitions.Clear();

        // The age comes first because it qualifies everything above it. Null only before the
        // first reading, when there is no age to give and the rows above are empty anyway.
        if (content.Age is { } age)
        {
            Fact("Reading", age);
        }

        Fact("Last poll", content.LastPoll);
        Fact("Next poll", content.NextPoll);
        Fact("Version", content.Version);
    }

    /// <summary>
    /// Whether two compositions would draw the same panel, and the redraw can be skipped.
    ///
    /// <para>
    /// Written out field by field rather than left to the record's own equality, which compares
    /// the two row lists by reference and would therefore call every composition different. The
    /// guard exists because most of what the panel shows changes by the minute while the panel
    /// re-reads by the second.
    /// </para>
    /// <para>
    /// A field added to <see cref="PanelContent"/> and not added here would stop the panel from
    /// noticing when it changes. That is the cost of comparing by hand, and it is why the list
    /// below is the whole record.
    /// </para>
    /// </summary>
    private static bool Same(PanelContent a, PanelContent b) =>
        a.Windows.SequenceEqual(b.Windows)
        && a.PerModelCaps.SequenceEqual(b.PerModelCaps)
        && a.PerModelCapsEmptyState == b.PerModelCapsEmptyState
        && a.Age == b.Age
        && a.LastPoll == b.LastPoll
        && a.NextPoll == b.NextPoll
        && a.Version == b.Version
        && a.RefreshNotice == b.RefreshNotice;

    private void Fill(Grid grid, IReadOnlyList<PanelRow> rows)
    {
        grid.Children.Clear();
        grid.RowDefinitions.Clear();

        for (var row = 0; row < rows.Count; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition());
            Place(grid, new TextBlock { Text = rows[row].Label, Foreground = Dim }, row, column: 0);
            Place(grid, new TextBlock { Text = rows[row].Percent, Margin = new Thickness(12, 0, 0, 0) }, row, column: 1);
            Place(grid, new TextBlock { Text = rows[row].Reset, Foreground = Dim, Margin = new Thickness(12, 0, 0, 0) }, row, column: 2);
        }
    }

    private void Fact(string label, string value)
    {
        var row = Facts.RowDefinitions.Count;
        Facts.RowDefinitions.Add(new RowDefinition());
        Place(Facts, new TextBlock { Text = label, Foreground = Dim }, row, column: 0);
        Place(Facts, new TextBlock { Text = value, Margin = new Thickness(12, 0, 0, 0) }, row, column: 1);
    }

    private static void Place(Grid grid, TextBlock text, int row, int column)
    {
        Grid.SetRow(text, row);
        Grid.SetColumn(text, column);
        grid.Children.Add(text);
    }
}
