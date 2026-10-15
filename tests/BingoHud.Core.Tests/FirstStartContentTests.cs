using BingoHud.Core.Display;
using BingoHud.Core.Settings;

namespace BingoHud.Core.Tests;

/// <summary>
/// The first-start question, as words and ticks (codex-quota AC-8a, AC-8b).
///
/// <para>
/// Bingo's first question to the user. It is decided here and only drawn by the shell, like every
/// other word in the app, so that what it offers and when it lets the user continue are tested.
/// </para>
/// </summary>
public class FirstStartContentTests
{
    [Fact]
    public void BothFoundAreBothOfferedTicked()
    {
        var content = FirstStartContent.For(new ProductDetection(ClaudeFound: true, CodexFound: true));

        Assert.Equal(
            [
                new FirstStartChoice(Product.Claude, "Claude Code", Ticked: true, Note: null),
                new FirstStartChoice(Product.Codex, "Codex", Ticked: true, Note: null),
            ],
            content.Choices);
    }

    [Fact]
    public void AProductNotFoundIsOfferedUntickedAndSaysSo()
    {
        // It can still be ticked: it may be installed later (AC-8b).
        var content = FirstStartContent.For(new ProductDetection(ClaudeFound: true, CodexFound: false));
        var codex = Assert.Single(content.Choices, c => c.Product == Product.Codex);

        Assert.False(codex.Ticked);
        Assert.Equal("not found on this computer", codex.Note);
    }

    [Fact]
    public void BothProductsAreAlwaysOffered()
    {
        var content = FirstStartContent.For(new ProductDetection(false, false));

        Assert.Equal([Product.Claude, Product.Codex], content.Choices.Select(c => c.Product));
        Assert.All(content.Choices, c => Assert.False(c.Ticked));
    }

    [Fact]
    public void TheQuestionSaysItCanBeChangedLaterAndWhere()
    {
        var content = FirstStartContent.For(new ProductDetection(true, true));

        Assert.False(string.IsNullOrWhiteSpace(content.Title));
        Assert.False(string.IsNullOrWhiteSpace(content.Question));
        Assert.False(string.IsNullOrWhiteSpace(content.Continue));
        Assert.Contains("tray", content.ChangeLater, StringComparison.Ordinal);
    }

    [Fact]
    public void ContinuingNeedsSomethingTicked()
    {
        Assert.True(FirstStartContent.CanContinue(new ProductSwitches(true, false)));
        Assert.True(FirstStartContent.CanContinue(new ProductSwitches(false, true)));
        Assert.False(FirstStartContent.CanContinue(new ProductSwitches(false, false)));
    }

    [Fact]
    public void ClosingTheQuestionShowsWhatWasFoundForNow()
    {
        // Closed without an answer: nothing is saved, so the question comes back next start, and
        // until then Bingo shows what it found.
        Assert.Equal(new ProductSwitches(true, true), FirstStartContent.WhenClosed(new ProductDetection(true, true)));
        Assert.Equal(new ProductSwitches(false, true), FirstStartContent.WhenClosed(new ProductDetection(false, true)));
    }

    [Fact]
    public void ClosingWithNothingFoundShowsClaude()
    {
        // Something has to be on. Claude alone is what Bingo has always shown.
        Assert.Equal(new ProductSwitches(true, false), FirstStartContent.WhenClosed(new ProductDetection(false, false)));
    }
}
