using System.Globalization;
using BingoHud.Core.Display;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// With only Claude shown, the HUD reads as 0.4.0 does apart from the product label
/// (codex-quota AC-9, AC-7a).
///
/// <para>
/// Written before any display code changed for Codex. Each expectation below is what 0.4.0's
/// <see cref="Readout"/> produced for the same state, recorded from it on 2026-10-14, with one
/// edit: every line's window name gains "Claude ". The phrases, marks, percentages, reset
/// phrases, severities and bars are 0.4.0's, word for word. When the display is rebuilt around
/// product sections, this file changes only in how it asks for the content; if an expectation
/// has to change, the HUD has changed for every Claude-only user, and AC-9 says it must not.
/// </para>
/// </summary>
public class ClaudeOnlyHudTests
{
    private static readonly CultureInfo TwelveHour = new("en-US");
    private static readonly TimeZoneInfo Pacific = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 9, 34, 0, TimeSpan.FromHours(-7));

    private static QuotaWindow Window(
        WindowKind kind, double used, double? resetInMinutes,
        ServerSeverity severity = ServerSeverity.Normal, string? scope = null) =>
        new(kind, used, resetInMinutes is { } minutes ? Now.AddMinutes(minutes) : null, severity, scope);

    private static QuotaSnapshot Snapshot(params QuotaWindow[] windows) => new(windows, Now, "{}");

    private static ReadingState State(
        QuotaSnapshot? snapshot, Freshness freshness = Freshness.Fresh,
        FetchOutcome? failure = null, double ageInMinutes = 0) =>
        new(snapshot, freshness, failure, TimeSpan.FromMinutes(ageInMinutes), "test");

    // Session 12% resetting in 53 minutes, weekly 41% resetting in 2.3 days.
    private static QuotaSnapshot Both => Snapshot(
        Window(WindowKind.Session, 12, 53),
        Window(WindowKind.WeeklyAll, 41, 60 * 24 * 2.3));

    private static QuotaSnapshot SessionAt(double used, ServerSeverity severity = ServerSeverity.Normal) => Snapshot(
        Window(WindowKind.Session, used, 53, severity),
        Window(WindowKind.WeeklyAll, 41, 3000));

    // Codex is found and has a reading, so a Codex section would have something to show; with
    // Codex switched off there must be none.
    private static readonly Codex.CodexState CodexWithAReading = new(
        Codex.CodexPresence.Found,
        new QuotaSnapshot([new QuotaWindow(WindowKind.Session, 70, Now.AddMinutes(90), ServerSeverity.Unknown)], Now, "{}"),
        Problem: null,
        TimeSpan.FromMinutes(5),
        ReachedType: null,
        []);

    private static HudContent Content(ReadingState state, UserSettings? settings = null)
    {
        var hud = Readout.Compose(
            state,
            CodexWithAReading,
            (settings ?? UserSettings.Default) with { Products = new ProductSwitches(Claude: true, Codex: false) },
            Now,
            TwelveHour,
            Pacific);

        var section = Assert.Single(hud.Sections);
        Assert.Equal(Product.Claude, section.Product);

        return section.Content;
    }

    private static void ExpectReading(HudContent content, string? mark, bool frozen, params ReadoutLine[] lines)
    {
        var reading = Assert.IsType<HudContent.Reading>(content);
        Assert.Equal(mark, reading.Mark);
        Assert.Equal(frozen, reading.Frozen);
        Assert.Equal(lines, reading.Lines);
    }

    private static ReadoutLine Line(string window, string percent, string? reset, Severity severity = Severity.Normal) =>
        new(window, percent, reset, severity, Bar: null);

    [Fact]
    public void NoReadingYet()
    {
        Assert.Equal(new HudContent.Empty("No reading yet"), Content(State(null)));
    }

    [Fact]
    public void SignedOutBeforeAnyReading()
    {
        var content = Content(State(null, failure: new FetchOutcome.AuthFailed(AuthFailureKind.SignedOut)));

        Assert.Equal(new HudContent.Empty("Signed out"), content);
    }

    [Fact]
    public void BothWindowsFresh()
    {
        ExpectReading(Content(State(Both)), mark: null, frozen: false,
            Line("Claude 5h", "12% used", "resets in 53 min"),
            Line("Claude Week", "41% used", "resets Wed 4:45 PM"));
    }

    [Fact]
    public void ReadAsRemaining()
    {
        var settings = UserSettings.Default with { Direction = DisplayDirection.Remaining };

        ExpectReading(Content(State(Both), settings), mark: null, frozen: false,
            Line("Claude 5h", "88% left", "resets in 53 min"),
            Line("Claude Week", "59% left", "resets Wed 4:45 PM"));
    }

    [Fact]
    public void Warning()
    {
        ExpectReading(Content(State(SessionAt(80))), mark: null, frozen: false,
            Line("Claude 5h", "80% used", "resets in 53 min", Severity.Warning),
            Line("Claude Week", "41% used", "resets Wed 11:34 AM"));
    }

    [Fact]
    public void Critical()
    {
        ExpectReading(Content(State(SessionAt(95))), mark: null, frozen: false,
            Line("Claude 5h", "95% used", "resets in 53 min", Severity.Critical),
            Line("Claude Week", "41% used", "resets Wed 11:34 AM"));
    }

    [Fact]
    public void RateLimited()
    {
        ExpectReading(Content(State(SessionAt(100, ServerSeverity.Rejected))), mark: null, frozen: false,
            Line("Claude 5h", "100% used", "limited, resets in 53 min", Severity.RateLimited),
            Line("Claude Week", "41% used", "resets Wed 11:34 AM"));
    }

    [Fact]
    public void Stale()
    {
        ExpectReading(Content(State(Both, Freshness.Stale, ageInMinutes: 50)), mark: "50 min old", frozen: false,
            Line("Claude 5h", "12% used", "resets in 53 min"),
            Line("Claude Week", "41% used", "resets Wed 4:45 PM"));
    }

    [Fact]
    public void Frozen()
    {
        var state = State(Both, Freshness.Frozen, new FetchOutcome.AuthFailed(AuthFailureKind.SignedOut), ageInMinutes: 180);

        ExpectReading(Content(state), mark: "3 hours old, signed out", frozen: true,
            Line("Claude 5h", "12% used", "resets in 53 min"),
            Line("Claude Week", "41% used", "resets Wed 4:45 PM"));
    }

    [Fact]
    public void LastPollUnreadable()
    {
        ExpectReading(Content(State(Both, failure: new FetchOutcome.Unreadable("x"))), mark: "last poll unreadable", frozen: false,
            Line("Claude 5h", "12% used", "resets in 53 min"),
            Line("Claude Week", "41% used", "resets Wed 4:45 PM"));
    }

    [Fact]
    public void CollapsedToTheOneWindowInTrouble()
    {
        var settings = UserSettings.Default with { Collapse = true };

        ExpectReading(Content(State(SessionAt(80)), settings), mark: null, frozen: false,
            Line("Claude 5h", "80% used", "resets in 53 min", Severity.Warning));
    }

    [Fact]
    public void CollapseKeepsBothWhenBothAreInTrouble()
    {
        var settings = UserSettings.Default with { Collapse = true };
        var snapshot = Snapshot(Window(WindowKind.Session, 80, 53), Window(WindowKind.WeeklyAll, 85, 3000));

        ExpectReading(Content(State(snapshot), settings), mark: null, frozen: false,
            Line("Claude 5h", "80% used", "resets in 53 min", Severity.Warning),
            Line("Claude Week", "85% used", "resets Wed 11:34 AM", Severity.Warning));
    }

    [Fact]
    public void BarAndCountdown()
    {
        var settings = UserSettings.Default with { ShowBar = true, ResetFormat = ResetFormat.Countdown };

        ExpectReading(Content(State(Both), settings), mark: null, frozen: false,
            new ReadoutLine("Claude 5h", "12% used", "53m", Severity.Normal, Bar.Segments(12)),
            new ReadoutLine("Claude Week", "41% used", "2.2d", Severity.Normal, Bar.Segments(41)));
    }

    [Fact]
    public void NoResetTime()
    {
        ExpectReading(Content(State(Snapshot(Window(WindowKind.Session, 12, null)))), mark: null, frozen: false,
            Line("Claude 5h", "12% used", reset: null));
    }

    [Fact]
    public void OnlyPerModelCaps()
    {
        var snapshot = Snapshot(Window(WindowKind.WeeklyScoped, 30, 3000, scope: "opus"));

        Assert.Equal(new HudContent.Empty("No 5h or weekly window reported"), Content(State(snapshot)));
    }
}
