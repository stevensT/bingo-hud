using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using BingoHud.Core;
using BingoHud.Core.Display;
using BingoHud.Core.Settings;

namespace BingoHud.App;

/// <summary>
/// The first-start question (codex-quota AC-8a, AC-8b): which products Bingo shows.
///
/// <para>
/// Every word and every starting tick is <see cref="FirstStartContent"/>'s, and whether Continue
/// is allowed is its rule. This file places them and decides none of them.
/// </para>
/// </summary>
public partial class FirstStartWindow : Window
{
    private static readonly Brush Dim = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));

    private readonly Dictionary<Product, CheckBox> _boxes = [];

    public FirstStartWindow(FirstStartContent content)
    {
        InitializeComponent();

        Title = content.Title;
        Question.Text = content.Question;
        ChangeLater.Text = content.ChangeLater;
        Continue.Content = content.Continue;

        foreach (var choice in content.Choices)
        {
            var box = new CheckBox { Content = choice.Name, IsChecked = choice.Ticked };
            box.Checked += (_, _) => UpdateContinue();
            box.Unchecked += (_, _) => UpdateContinue();
            _boxes[choice.Product] = box;

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            row.Children.Add(box);

            if (choice.Note is { } note)
            {
                row.Children.Add(new TextBlock { Text = note, Foreground = Dim, Margin = new Thickness(8, 0, 0, 0) });
            }

            Choices.Children.Add(row);
        }

        Continue.Click += (_, _) =>
        {
            Answer = Ticked;
            DialogResult = true;
        };

        UpdateContinue();

        SourceInitialized += (_, _) =>
            NativeMethods.UseDarkTitleBar(new WindowInteropHelper(this).Handle);
    }

    /// <summary>The products ticked when Continue was pressed, or null if the window was closed.</summary>
    public ProductSwitches? Answer { get; private set; }

    private ProductSwitches Ticked => new(
        Claude: _boxes[Product.Claude].IsChecked == true,
        Codex: _boxes[Product.Codex].IsChecked == true);

    private void UpdateContinue() => Continue.IsEnabled = FirstStartContent.CanContinue(Ticked);
}
