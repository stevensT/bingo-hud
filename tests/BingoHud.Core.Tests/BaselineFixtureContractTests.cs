using System.Text.Json;
using System.Text.RegularExpressions;

namespace BingoHud.Core.Tests;

/// <summary>
/// Pins the shape of the 2026-08-30 baseline capture, field by field.
///
/// <para>
/// Every other test in this suite asserts what the parser does with that payload. This one
/// asserts what the payload is. The difference matters because the parser is deliberately
/// tolerant: it ignores keys it does not know, so it would go on producing plausible-looking
/// windows through a good deal of upstream change without complaining. This is the test that
/// complains.
/// </para>
/// <para>
/// <b>When this fails, the payload moved.</b> The response is fetched, and recapturing it with
/// <c>scripts/capture-usage.js</c> then updating these expectations is the correct fix.
/// Loosening the parser to accommodate whatever arrived is not — that trades a loud failure for
/// a quiet misreading, which is the failure this project can least afford.
/// </para>
/// <para>
/// The assertions are strict on purpose. A fixture is a committed file, so nothing here can
/// break on its own; it breaks only when someone changes the capture, and at that moment being
/// told exactly what changed is worth more than being spared the noise.
/// </para>
/// <para>
/// Verified by mutation: with <c>limits</c> renamed to <c>quota_limits</c> in the fixture —
/// the shape of a real upstream rename — fourteen assertions here failed, while the parser
/// tests very largely did not. The parser had quietly fallen back to the flat keys and gone on
/// producing correct-looking windows, which is precisely the drift nothing else in the suite
/// notices.
/// </para>
/// </summary>
public class BaselineFixtureContractTests
{
    /// <summary>
    /// ISO-8601 with six fractional digits and an explicit <c>+00:00</c> offset, which is what
    /// the endpoint sends. Not a <c>Z</c>, and not whole seconds.
    /// </summary>
    private static readonly Regex ResetTimestamp =
        new(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{6}\+00:00$");

    private static JsonElement Root => JsonDocument.Parse(Fixtures.Read(Fixtures.Baseline))
        .RootElement.Clone();

    [Fact]
    public void TheTopLevelKeysAreExactlyTheOnesCaptured()
    {
        // Includes the ten keys Bingo does not consume. They are pinned so that one appearing
        // or vanishing is visible, not so that Bingo does anything with them.
        //
        // Membership is pinned, not order: JSON object order carries no meaning, and a
        // reordered payload is not a payload that moved.
        string[] expected =
        [
            "five_hour",
            "seven_day",
            "limits",
            "seven_day_oauth_apps",
            "seven_day_opus",
            "seven_day_sonnet",
            "seven_day_cowork",
            "seven_day_omelette",
            "tangelo",
            "iguana_necktie",
            "omelette_promotional",
            "nimbus_quill",
            "cinder_cove",
            "amber_ladder",
            "juniper_tide",
            "extra_usage",
            "spend",
            "member_dashboard_available",
        ];

        Assert.Equal(
            expected.Order(),
            Root.EnumerateObject().Select(p => p.Name).Order());
    }

    [Fact]
    public void TheLimitsArrayHoldsExactlyTheSessionAndWeeklyAllWindows()
    {
        var kinds = Root.GetProperty("limits")
            .EnumerateArray()
            .Select(limit => limit.GetProperty("kind").GetString());

        Assert.Equal(["session", "weekly_all"], kinds);
    }

    [Theory]
    [InlineData("kind", JsonValueKind.String)]
    [InlineData("group", JsonValueKind.String)]
    [InlineData("percent", JsonValueKind.Number)]
    [InlineData("severity", JsonValueKind.String)]
    [InlineData("resets_at", JsonValueKind.String)]
    [InlineData("scope", JsonValueKind.Null)]
    public void EveryLimitCarriesTheFieldTypeTheParserExpects(string field, JsonValueKind kind)
    {
        foreach (var limit in Root.GetProperty("limits").EnumerateArray())
        {
            Assert.Equal(kind, limit.GetProperty(field).ValueKind);
        }
    }

    [Fact]
    public void EveryLimitReportsASeverityOfNormal()
    {
        // The only severity ever observed. When this fails because a window escalated, the
        // right response is to keep the capture: an observed warning or critical is a fixture
        // the project has been waiting for.
        foreach (var limit in Root.GetProperty("limits").EnumerateArray())
        {
            Assert.Equal("normal", limit.GetProperty("severity").GetString());
        }
    }

    [Fact]
    public void EveryResetTimeUsesMicrosecondsAndAnExplicitUtcOffset()
    {
        foreach (var limit in Root.GetProperty("limits").EnumerateArray())
        {
            Assert.Matches(ResetTimestamp, limit.GetProperty("resets_at").GetString()!);
        }
    }

    [Theory]
    [InlineData("five_hour", "session")]
    [InlineData("seven_day", "weekly_all")]
    public void TheFlatKeysAgreeExactlyWithTheLimitsArray(string flatKey, string limitKind)
    {
        // The whole case for treating the flat keys as a fallback rather than a separate
        // source rests on this agreement. If the two forms ever diverge, the fallback stops
        // being a fallback and the decision in the plan needs revisiting.
        var flat = Root.GetProperty(flatKey);
        var limit = Root.GetProperty("limits")
            .EnumerateArray()
            .Single(l => l.GetProperty("kind").GetString() == limitKind);

        Assert.Equal(limit.GetProperty("percent").GetDouble(), flat.GetProperty("utilization").GetDouble());
        Assert.Equal(limit.GetProperty("resets_at").GetString(), flat.GetProperty("resets_at").GetString());
    }

    [Theory]
    [InlineData("seven_day_opus")]
    [InlineData("seven_day_sonnet")]
    public void ThePerModelWeeklyKeysAreStillNull(string key)
    {
        // No per-model cap has ever been observed. The detail panel's empty state is built on
        // that, so the day this fails is the day the empty state stops being the common case
        // and a real fixture becomes available for it.
        Assert.Equal(JsonValueKind.Null, Root.GetProperty(key).ValueKind);
    }

    [Fact]
    public void NoLimitCarriesAModelScope()
    {
        foreach (var limit in Root.GetProperty("limits").EnumerateArray())
        {
            Assert.Equal(JsonValueKind.Null, limit.GetProperty("scope").ValueKind);
        }
    }

    [Fact]
    public void TheCaptureStillReportsTheUtilizationsTheseTestsWereWrittenAgainst()
    {
        // Named separately from the shape assertions because it is the one thing here that a
        // recapture is expected to change. It exists so that a recapture is a deliberate act
        // with visible consequences, not a silent swap underneath the suite.
        var percents = Root.GetProperty("limits")
            .EnumerateArray()
            .Select(limit => limit.GetProperty("percent").GetInt32());

        Assert.Equal([12, 37], percents);
    }
}
