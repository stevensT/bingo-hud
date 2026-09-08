using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// Per-model weekly caps: the windows the detail panel exists to show (AC-23).
///
/// <para>
/// Every capture so far reports these as null, so none of this has ever run against a real
/// response. That makes the rule below deliberately the smallest one available: an entry whose
/// <c>scope</c> names something is a cap restricted to that something. <c>scope</c> is a field
/// the observed payload already carries — always null so far — so recognizing it guesses at no
/// value the server has never sent. The alternative, predicting what <c>kind</c> string a scoped
/// entry would use, would have been a guess, and a wrong guess there is silent: the entry is
/// skipped and the panel shows an empty state that looks exactly like the honest one.
/// </para>
/// <para>
/// The scope string is shown as the server sent it. Bingo does not map it to a friendlier model
/// name, because a mapping is a guess about a vocabulary that has never been observed, and a
/// label invented for a cap is the panel telling the user something the server did not say.
/// </para>
/// </summary>
public class UsageNormalizerScopedCapTests
{
    private static readonly DateTimeOffset ObservedAt =
        new(2026, 8, 30, 10, 9, 23, TimeSpan.FromHours(-7));

    private static QuotaSnapshot Parse(string body) =>
        Assert.IsType<FetchOutcome.Success>(UsageNormalizer.Normalize(body, ObservedAt)).Snapshot;

    /// <summary>
    /// A limits array holding one entry, written out so each test states the whole shape it is
    /// asserting against rather than inheriting it from a helper.
    /// </summary>
    private static string Limits(string entries) => $$"""{"limits":[{{entries}}]}""";

    private const string SessionEntry = """
        {"kind":"session","group":"session","percent":12,"severity":"normal",
         "resets_at":"2026-08-30T21:30:00+00:00","scope":null,"is_active":false}
        """;

    [Fact]
    public void AnEntryCarryingAScopeIsAPerModelCap()
    {
        var body = Limits("""
            {"kind":"weekly_scoped","group":"weekly","percent":40,"severity":"normal",
             "resets_at":null,"scope":"claude-opus-4","is_active":true}
            """);

        var window = Assert.Single(Parse(body).Windows);

        Assert.Equal(WindowKind.WeeklyScoped, window.Kind);
    }

    [Fact]
    public void APerModelCapIsLabelledWithTheScopeTheServerSent()
    {
        var body = Limits("""
            {"kind":"weekly_scoped","group":"weekly","percent":40,"severity":"normal",
             "resets_at":null,"scope":"claude-opus-4","is_active":true}
            """);

        var window = Assert.Single(Parse(body).Windows);

        Assert.Equal("claude-opus-4", window.Scope);
    }

    [Fact]
    public void AScopeIsEnoughOnItsOwnEvenWhenTheKindIsOneBingoDoesNotKnow()
    {
        // The kind string for a scoped cap has never been observed. Recognizing the entry by its
        // scope alone is what stops an unforeseen kind from dropping the cap silently.
        var body = Limits("""
            {"kind":"a_kind_from_2027","group":"weekly","percent":40,"severity":"normal",
             "resets_at":null,"scope":"claude-opus-4","is_active":true}
            """);

        var window = Assert.Single(Parse(body).Windows);

        Assert.Equal(WindowKind.WeeklyScoped, window.Kind);
    }

    [Fact]
    public void APerModelCapKeepsItsPercentageAndResetTime()
    {
        var body = Limits("""
            {"kind":"weekly_scoped","group":"weekly","percent":40,"severity":"normal",
             "resets_at":"2026-09-05T01:00:00+00:00","scope":"claude-opus-4","is_active":true}
            """);

        var window = Assert.Single(Parse(body).Windows);

        Assert.Equal(40, window.UsedPercent);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 5, 1, 0, 0, TimeSpan.Zero),
            window.ResetsAt);
    }

    [Fact]
    public void AnUnknownKindWithNoScopeIsStillSkipped()
    {
        // The tolerance rule survives. A window this version does not know and that names no
        // model is not a per-model cap; it is a feature that does not exist yet.
        var body = Limits($$"""
            {{SessionEntry}},
            {"kind":"a_kind_from_2027","group":"weekly","percent":40,"severity":"normal",
             "resets_at":null,"scope":null,"is_active":true}
            """);

        var window = Assert.Single(Parse(body).Windows);

        Assert.Equal(WindowKind.Session, window.Kind);
    }

    [Fact]
    public void TheWindowsTheHudShowsCarryNoScope()
    {
        var snapshot = Parse(Fixtures.Read(Fixtures.Baseline));

        Assert.All(snapshot.Windows, w => Assert.Null(w.Scope));
    }

    [Fact]
    public void AnEmptyScopeStringNamesNoModelAndIsNotACap()
    {
        // "" identifies nothing. Treating it as a cap would put an unlabelled row in the panel.
        // Paired with a session entry because a response reporting no windows at all is
        // unreadable rather than empty, and that rule is not what this test is about.
        var body = Limits($$"""
            {{SessionEntry}},
            {"kind":"a_kind_from_2027","group":"weekly","percent":40,"severity":"normal",
             "resets_at":null,"scope":"","is_active":true}
            """);

        var window = Assert.Single(Parse(body).Windows);

        Assert.Equal(WindowKind.Session, window.Kind);
    }
}
