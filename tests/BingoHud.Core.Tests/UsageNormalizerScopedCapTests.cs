using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// Per-model weekly caps: the windows the detail panel exists to show (AC-23).
///
/// <para>
/// The rule is the smallest one available: an entry whose <c>scope</c> names a model is a cap
/// restricted to that model, whatever its <c>kind</c> says. <c>scope</c> was null in every
/// capture until 2026-11-11, when the server first sent one, as an object:
/// <c>{"model":{"id":null,"display_name":"Fable"},"surface":null}</c>. Recognizing a cap by its
/// scope rather than by a predicted <c>kind</c> string is what keeps a new kind from dropping
/// the cap silently, where the panel would show an empty state that looks exactly like the
/// honest one.
/// </para>
/// <para>
/// The label is the server's own: <c>display_name</c>, or <c>id</c> when there is no display
/// name, shown as sent. Bingo does not map it to a friendlier name, because a label invented
/// for a cap is the panel telling the user something the server did not say. Scope shapes that
/// have not been observed are unreadable; those cases live in
/// <see cref="UsageNormalizerUnreadableTests"/>.
/// </para>
/// </summary>
public class UsageNormalizerScopedCapTests
{
    private static readonly DateTimeOffset ObservedAt =
        new(2026, 11, 11, 7, 38, 39, TimeSpan.FromHours(-8));

    private static QuotaSnapshot Parse(string body) =>
        Assert.IsType<FetchOutcome.Success>(UsageNormalizer.Normalize(body, ObservedAt)).Snapshot;

    /// <summary>
    /// A limits array holding the given entries, written out so each test states the whole shape
    /// it is asserting against rather than inheriting it from a helper.
    /// </summary>
    private static string Limits(string entries) => $$"""{"limits":[{{entries}}]}""";

    private const string SessionEntry = """
        {"kind":"session","group":"session","percent":3,"severity":"normal",
         "resets_at":"2026-11-11T19:50:00.864498+00:00","scope":null,"is_active":true}
        """;

    /// <summary>The scope exactly as the 2026-11-11 capture carried it.</summary>
    private const string FableScope = """
        {"model":{"id":null,"display_name":"Fable"},"surface":null}
        """;

    [Fact]
    public void AnEntryCarryingAScopeIsAPerModelCap()
    {
        var body = Limits($$"""
            {"kind":"weekly_scoped","group":"weekly","percent":0,"severity":"normal",
             "resets_at":"2026-11-14T01:00:00+00:00","scope":{{FableScope}},"is_active":false}
            """);

        var window = Assert.Single(Parse(body).Windows);

        Assert.Equal(WindowKind.WeeklyScoped, window.Kind);
    }

    [Fact]
    public void APerModelCapIsLabelledWithTheDisplayNameTheServerSent()
    {
        var body = Limits($$"""
            {"kind":"weekly_scoped","group":"weekly","percent":0,"severity":"normal",
             "resets_at":null,"scope":{{FableScope}},"is_active":false}
            """);

        var window = Assert.Single(Parse(body).Windows);

        Assert.Equal("Fable", window.Scope);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    public void WithNoUsableDisplayNameTheCapIsLabelledWithTheModelId(string displayName)
    {
        // The capture's id was null, so the display name is the one that carries a value today.
        // The id is the server's word too, so it is the label when the display name says
        // nothing.
        var body = Limits($$"""
            {"kind":"weekly_scoped","group":"weekly","percent":0,"severity":"normal",
             "resets_at":null,
             "scope":{"model":{"id":"claude-fable-5","display_name":{{displayName}}},"surface":null},
             "is_active":false}
            """);

        var window = Assert.Single(Parse(body).Windows);

        Assert.Equal("claude-fable-5", window.Scope);
    }

    [Fact]
    public void WithNoDisplayNameKeyAtAllTheCapIsLabelledWithTheModelId()
    {
        var body = Limits("""
            {"kind":"weekly_scoped","group":"weekly","percent":0,"severity":"normal",
             "resets_at":null,"scope":{"model":{"id":"claude-fable-5"}},"is_active":false}
            """);

        var window = Assert.Single(Parse(body).Windows);

        Assert.Equal("claude-fable-5", window.Scope);
    }

    [Fact]
    public void AScopeIsEnoughOnItsOwnEvenWhenTheKindIsOneBingoDoesNotKnow()
    {
        // weekly_scoped has now been observed, but the kind is still not what makes the entry a
        // cap. A later rename of the kind must not drop the cap silently.
        var body = Limits($$"""
            {"kind":"a_kind_from_2027","group":"weekly","percent":40,"severity":"normal",
             "resets_at":null,"scope":{{FableScope}},"is_active":true}
            """);

        var window = Assert.Single(Parse(body).Windows);

        Assert.Equal(WindowKind.WeeklyScoped, window.Kind);
    }

    [Fact]
    public void APerModelCapKeepsItsPercentageResetTimeAndSeverity()
    {
        // The scoped entry's reset time has no fractional seconds, unlike the session and
        // weekly ones in the same capture. Both forms parse.
        var body = Limits($$"""
            {"kind":"weekly_scoped","group":"weekly","percent":40,"severity":"warning",
             "resets_at":"2026-11-14T01:00:00+00:00","scope":{{FableScope}},"is_active":false}
            """);

        var window = Assert.Single(Parse(body).Windows);

        Assert.Equal(40, window.UsedPercent);
        Assert.Equal(new DateTimeOffset(2026, 11, 14, 1, 0, 0, TimeSpan.Zero), window.ResetsAt);
        Assert.Equal(ServerSeverity.Warning, window.Severity);
    }

    [Fact]
    public void UnknownKeysInsideTheScopeAreIgnored()
    {
        // Tolerant in, as everywhere else in the payload. Noticing a new key is the contract
        // test's job; refusing a reading over one would fail every response the day it appears.
        var body = Limits("""
            {"kind":"weekly_scoped","group":"weekly","percent":0,"severity":"normal",
             "resets_at":null,
             "scope":{"model":{"id":null,"display_name":"Fable","family":"fable"},
                      "surface":null,"tier":"max"},
             "is_active":false}
            """);

        var window = Assert.Single(Parse(body).Windows);

        Assert.Equal("Fable", window.Scope);
    }

    [Fact]
    public void AScopeOfNullIsNoScope()
    {
        var window = Assert.Single(Parse(Limits(SessionEntry)).Windows);

        Assert.Equal(WindowKind.Session, window.Kind);
        Assert.Null(window.Scope);
    }

    [Fact]
    public void AnAbsentScopeIsNoScope()
    {
        var body = Limits("""
            {"kind":"weekly_all","group":"weekly","percent":3,"severity":"normal",
             "resets_at":"2026-11-14T01:00:00.864518+00:00","is_active":false}
            """);

        var window = Assert.Single(Parse(body).Windows);

        Assert.Equal(WindowKind.WeeklyAll, window.Kind);
        Assert.Null(window.Scope);
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
}
