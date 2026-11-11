using System.Text.Json;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// Pins the <c>limits</c> array of the 2026-11-11 capture, the first to carry a per-model cap.
///
/// <para>
/// Narrower than <see cref="BaselineFixtureContractTests"/> on purpose. The top level of this
/// capture carries a long list of codename keys that come and go, and pinning them would make
/// this test fail on churn that Bingo does not read. What Bingo does read is the <c>limits</c>
/// array, and the scope shape inside it is the thing that moved last time.
/// </para>
/// <para>
/// <b>When this fails, the payload moved.</b> Recapture with <c>scripts/capture-usage.js</c> and
/// update these expectations; do not loosen the parser to accommodate whatever arrived.
/// </para>
/// </summary>
public class ScopedCapFixtureContractTests
{
    private static readonly DateTimeOffset ObservedAt =
        new(2026, 11, 11, 15, 38, 39, TimeSpan.Zero);

    private static JsonElement Root => JsonDocument.Parse(Fixtures.Read(Fixtures.ScopedCap))
        .RootElement.Clone();

    private static JsonElement[] Limits => Root.GetProperty("limits").EnumerateArray().ToArray();

    private static JsonElement ScopedEntry =>
        Limits.Single(limit => limit.GetProperty("kind").GetString() == "weekly_scoped");

    private static string[] KeysOf(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order().ToArray();

    [Fact]
    public void TheLimitsArrayCarriesTheSessionTheWeekAndOneScopedCap()
    {
        var kindsAndGroups = Limits.Select(limit =>
            (limit.GetProperty("kind").GetString(), limit.GetProperty("group").GetString()));

        Assert.Equal(
            [("session", "session"), ("weekly_all", "weekly"), ("weekly_scoped", "weekly")],
            kindsAndGroups);
    }

    [Fact]
    public void EveryLimitCarriesTheSameKeys()
    {
        foreach (var limit in Limits)
        {
            Assert.Equal(
                ["group", "is_active", "kind", "percent", "resets_at", "scope", "severity"],
                KeysOf(limit));
        }
    }

    [Fact]
    public void OnlyTheScopedCapCarriesAScope()
    {
        foreach (var limit in Limits.Where(l => l.GetProperty("kind").GetString() != "weekly_scoped"))
        {
            Assert.Equal(JsonValueKind.Null, limit.GetProperty("scope").ValueKind);
        }
    }

    [Fact]
    public void TheScopeIsAnObjectNamingAModelAndNoSurface()
    {
        var scope = ScopedEntry.GetProperty("scope");

        Assert.Equal(["model", "surface"], KeysOf(scope));
        Assert.Equal(JsonValueKind.Null, scope.GetProperty("surface").ValueKind);
    }

    [Fact]
    public void TheModelCarriesADisplayNameAndANullId()
    {
        // The id being null is why the label comes from display_name. The day this fails
        // because an id arrived, the label does not change: display_name is still preferred.
        var model = ScopedEntry.GetProperty("scope").GetProperty("model");

        Assert.Equal(["display_name", "id"], KeysOf(model));
        Assert.Equal(JsonValueKind.Null, model.GetProperty("id").ValueKind);
        Assert.Equal("Fable", model.GetProperty("display_name").GetString());
    }

    [Fact]
    public void TheParserReadsTheSessionTheWeekAndTheFableCap()
    {
        var outcome = UsageNormalizer.Normalize(Fixtures.Read(Fixtures.ScopedCap), ObservedAt);

        var windows = Assert.IsType<FetchOutcome.Success>(outcome).Snapshot.Windows;

        Assert.Collection(
            windows,
            session =>
            {
                Assert.Equal(WindowKind.Session, session.Kind);
                Assert.Equal(3, session.UsedPercent);
                Assert.Null(session.Scope);
            },
            week =>
            {
                Assert.Equal(WindowKind.WeeklyAll, week.Kind);
                Assert.Equal(3, week.UsedPercent);
                Assert.Null(week.Scope);
            },
            cap =>
            {
                Assert.Equal(WindowKind.WeeklyScoped, cap.Kind);
                Assert.Equal("Fable", cap.Scope);
                Assert.Equal(0, cap.UsedPercent);
                Assert.Equal(new DateTimeOffset(2026, 11, 14, 1, 0, 0, TimeSpan.Zero), cap.ResetsAt);
                Assert.Equal(ServerSeverity.Normal, cap.Severity);
            });
    }
}
