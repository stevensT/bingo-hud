using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// The fallback parse path: the flat window keys, read only when <c>limits</c> is absent.
///
/// <para>
/// This path has never been observed on the live account — the endpoint has returned
/// <c>limits[]</c> on every capture. It is retained because it is what the prior art documents
/// and what an older account may still return, and because it is the only thing standing
/// between a payload that drops <c>limits[]</c> and a HUD showing nothing. Its fixture is
/// derived from the baseline capture rather than recorded, for the same reason.
/// </para>
/// </summary>
public class UsageNormalizerFlatFallbackTests
{
    private static readonly DateTimeOffset ObservedAt =
        new(2026, 8, 30, 10, 9, 23, TimeSpan.FromHours(-7));

    private static QuotaSnapshot Parse(string body)
    {
        var outcome = UsageNormalizer.Normalize(body, ObservedAt);

        return Assert.IsType<FetchOutcome.Success>(outcome).Snapshot;
    }

    /// <summary>
    /// A payload carrying exactly one flat window under a chosen key name, and no
    /// <c>limits</c> array.
    /// </summary>
    private static string PayloadWithFlatKey(string key) =>
        $$"""
        {
          "{{key}}": {
            "utilization": 44,
            "resets_at": "2026-08-30T21:30:00.972286+00:00",
            "limit_dollars": null,
            "used_dollars": null,
            "remaining_dollars": null,
            "locked_reason": null
          }
        }
        """;

    [Fact]
    public void TheDerivedFixtureYieldsTheSameTwoWindowsAsTheLimitsPath()
    {
        // The point of the fallback is that it agrees with the primary path. Both fixtures
        // carry the same numbers because one was derived from the other, so any disagreement
        // here is the two code paths disagreeing, which is the bug worth catching.
        var viaLimits = Parse(Fixtures.Read(Fixtures.Baseline));
        var viaFlatKeys = Parse(Fixtures.Read(Fixtures.DerivedFlatOnly));

        Assert.Equal(viaLimits.Windows, viaFlatKeys.Windows);
    }

    [Fact]
    public void TheDerivedFixtureYieldsExactlyTwoWindows()
    {
        // The derived fixture still carries `nimbus_quill`, an unreleased key whose value is
        // window-shaped: a utilization of 0 and a null reset. A fallback that recognized
        // windows by their shape rather than by name would produce a third window here and put
        // an invented 0% on screen. Known keys only.
        var snapshot = Parse(Fixtures.Read(Fixtures.DerivedFlatOnly));

        Assert.Equal(2, snapshot.Windows.Count);
    }

    [Theory]
    [InlineData("five_hour")]
    [InlineData("5_hour")]
    [InlineData("session")]
    [InlineData("primary")]
    public void SessionAliasesAllMapToTheSessionWindow(string key)
    {
        var snapshot = Parse(PayloadWithFlatKey(key));

        Assert.Equal(WindowKind.Session, snapshot.Windows.Single().Kind);
    }

    [Theory]
    [InlineData("seven_day")]
    [InlineData("7_day")]
    [InlineData("weekly")]
    [InlineData("week")]
    [InlineData("secondary")]
    public void WeeklyAliasesAllMapToTheWeeklyAllWindow(string key)
    {
        var snapshot = Parse(PayloadWithFlatKey(key));

        Assert.Equal(WindowKind.WeeklyAll, snapshot.Windows.Single().Kind);
    }

    [Fact]
    public void AFlatWindowCarriesItsUtilizationUnchanged()
    {
        var snapshot = Parse(PayloadWithFlatKey("five_hour"));

        Assert.Equal(44, snapshot.Windows.Single().UsedPercent);
    }

    [Fact]
    public void AFlatWindowCarriesItsResetInstant()
    {
        var expected = new DateTimeOffset(2026, 8, 30, 21, 30, 0, TimeSpan.Zero)
            .AddTicks(9_722_860);

        var snapshot = Parse(PayloadWithFlatKey("five_hour"));

        Assert.Equal(expected, snapshot.Windows.Single().ResetsAt);
    }

    [Fact]
    public void TheFlatKeysAreIgnoredWhenLimitsIsPresent()
    {
        // Both forms are present in every real payload observed so far. Reading both would
        // double every window; the flat keys are a fallback, not a supplement.
        var snapshot = Parse(Fixtures.Read(Fixtures.Baseline));

        Assert.Equal(2, snapshot.Windows.Count);
    }
}
