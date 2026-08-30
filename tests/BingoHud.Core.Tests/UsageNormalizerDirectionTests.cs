using System.Text.Json;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// Pins the direction of every percentage Core produces: consumed, exactly as the server
/// reported it.
///
/// <para>
/// Direction is the one error in this codebase that cannot be spotted by looking at the screen.
/// The display direction is a user setting, so a reader who sees "12%" cannot tell from the
/// number alone whether the inversion happened once, twice, or in the wrong place. The rule
/// that makes it checkable is that inversion happens nowhere in Core — it happens once, at
/// render — and these tests are the fence around that rule.
/// </para>
/// <para>
/// Verified by mutation: with <c>UsageNormalizer</c> temporarily changed to store
/// <c>100 - percent</c>, all four of these tests failed, along with the two percentage
/// assertions in <see cref="UsageNormalizerLimitsPathTests"/>. Six failures, no others.
/// </para>
/// </summary>
public class UsageNormalizerDirectionTests
{
    private static readonly DateTimeOffset ObservedAt =
        new(2026, 8, 30, 10, 9, 23, TimeSpan.FromHours(-7));

    /// <summary>
    /// A minimal payload carrying one session window at a chosen utilization. Written by hand
    /// rather than captured, because the states that make direction unmistakable — nearly
    /// exhausted, barely touched — are exactly the ones no capture has caught the account in.
    /// </summary>
    private static string PayloadWithSessionAt(int percent) =>
        $$"""
        {
          "limits": [
            {
              "kind": "session",
              "group": "session",
              "percent": {{percent}},
              "severity": "normal",
              "resets_at": "2026-08-30T21:30:00.972286+00:00",
              "scope": null,
              "is_active": true
            }
          ]
        }
        """;

    private static QuotaSnapshot Parse(string body)
    {
        var outcome = UsageNormalizer.Normalize(body, ObservedAt);

        return Assert.IsType<FetchOutcome.Success>(outcome).Snapshot;
    }

    [Fact]
    public void ANearlyExhaustedWindowReportsAHighUsedPercent()
    {
        // 88 consumed inverts to 12, which is the baseline fixture's own value and would look
        // entirely plausible on screen. That is what makes this the case worth pinning.
        var snapshot = Parse(PayloadWithSessionAt(88));

        Assert.Equal(88, snapshot.Windows.Single().UsedPercent);
    }

    [Fact]
    public void ABarelyTouchedWindowReportsALowUsedPercent()
    {
        var snapshot = Parse(PayloadWithSessionAt(3));

        Assert.Equal(3, snapshot.Windows.Single().UsedPercent);
    }

    [Fact]
    public void AnUntouchedWindowReportsZeroUsedRatherThanZeroRemaining()
    {
        // The degenerate case: 0 and 100 are the two values where an inverted reading is at its
        // most dangerous, because "0%" and "100%" both read as normal-looking numbers.
        var snapshot = Parse(PayloadWithSessionAt(0));

        Assert.Equal(0, snapshot.Windows.Single().UsedPercent);
    }

    [Fact]
    public void EveryWindowInTheBaselineMatchesThePercentTheBodyCarries()
    {
        // Reads the expected values out of the fixture instead of restating them, so this
        // stays true if the fixture is recaptured at different utilizations. It asserts
        // "unchanged", not "12 and 37".
        var body = Fixtures.Read(Fixtures.Baseline);
        var snapshot = Parse(body);

        using var document = JsonDocument.Parse(body);
        var reported = document.RootElement
            .GetProperty("limits")
            .EnumerateArray()
            .Select(limit => limit.GetProperty("percent").GetDouble())
            .ToArray();

        Assert.Equal(reported, snapshot.Windows.Select(w => w.UsedPercent));
    }
}
