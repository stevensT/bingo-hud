using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// A window can report a utilization and no reset time. That combination is observed, ordinary,
/// and easy to get wrong in two opposite directions.
///
/// <para>
/// Dropping such a window loses a real percentage. Treating it as malformed rejects a response
/// the server considers perfectly valid. Filling the gap with a guessed reset time would be the
/// worst of the three: a countdown nothing backs, which is exactly what this project refuses to
/// display. The window keeps its percentage and carries no reset, and the readout simply shows
/// no countdown beside it.
/// </para>
/// <para>
/// Verified by mutation: with the limits reader changed to skip any entry whose <c>resets_at</c>
/// is not a string, exactly the four limits-path tests below failed and nothing else in the
/// suite did. The two flat-path tests are untouched by that mutation, which is the point of
/// having both — the two paths drop windows independently.
/// </para>
/// </summary>
public class UsageNormalizerNullResetTests
{
    private static readonly DateTimeOffset ObservedAt =
        new(2026, 8, 30, 10, 9, 23, TimeSpan.FromHours(-7));

    private static QuotaSnapshot Parse(string body)
    {
        var outcome = UsageNormalizer.Normalize(body, ObservedAt);

        return Assert.IsType<FetchOutcome.Success>(outcome).Snapshot;
    }

    private const string LimitsWindowWithNullReset =
        """
        {
          "limits": [
            {
              "kind": "session",
              "group": "session",
              "percent": 61,
              "severity": "normal",
              "resets_at": null,
              "scope": null,
              "is_active": true
            }
          ]
        }
        """;

    private const string LimitsWindowWithNoResetKey =
        """
        {
          "limits": [
            { "kind": "session", "group": "session", "percent": 61, "severity": "normal" }
          ]
        }
        """;

    private const string FlatWindowWithNullReset =
        """
        {
          "five_hour": { "utilization": 61, "resets_at": null }
        }
        """;

    [Fact]
    public void ALimitsWindowWithANullResetIsNotDropped()
    {
        var snapshot = Parse(LimitsWindowWithNullReset);

        Assert.Single(snapshot.Windows);
    }

    [Fact]
    public void ALimitsWindowWithANullResetKeepsItsPercentage()
    {
        var snapshot = Parse(LimitsWindowWithNullReset);

        Assert.Equal(61, snapshot.Windows.Single().UsedPercent);
    }

    [Fact]
    public void ALimitsWindowWithANullResetCarriesNoResetTime()
    {
        var snapshot = Parse(LimitsWindowWithNullReset);

        Assert.Null(snapshot.Windows.Single().ResetsAt);
    }

    [Fact]
    public void AWindowWithNoResetKeyAtAllIsTreatedTheSameAsANullOne()
    {
        // Absent and null both mean "no reset time was reported". Distinguishing them would
        // create a second empty state that renders identically.
        var snapshot = Parse(LimitsWindowWithNoResetKey);

        Assert.Null(snapshot.Windows.Single().ResetsAt);
    }

    [Fact]
    public void AFlatWindowWithANullResetIsNotDropped()
    {
        var snapshot = Parse(FlatWindowWithNullReset);

        Assert.Equal(61, snapshot.Windows.Single().UsedPercent);
    }

    [Fact]
    public void AFlatWindowWithANullResetCarriesNoResetTime()
    {
        var snapshot = Parse(FlatWindowWithNullReset);

        Assert.Null(snapshot.Windows.Single().ResetsAt);
    }
}
