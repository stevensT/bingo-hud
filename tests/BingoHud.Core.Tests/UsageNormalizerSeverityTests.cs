using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// The server reports a severity per limit, and Bingo carries it rather than deriving it.
///
/// <para>
/// Only <c>normal</c> has ever been observed on a live response. Every other spelling in this
/// file is inferred from prior art, so the mapping has to fail safe in one specific direction:
/// a string nobody anticipated becomes <see cref="ServerSeverity.Unknown"/>, never
/// <see cref="ServerSeverity.Normal"/>. Getting that backwards would mean the first time the
/// server escalates in an unfamiliar spelling — the exact moment the reading matters most — the
/// HUD reports that everything is fine.
/// </para>
/// </summary>
public class UsageNormalizerSeverityTests
{
    private static readonly DateTimeOffset ObservedAt =
        new(2026, 8, 30, 10, 9, 23, TimeSpan.FromHours(-7));

    private static QuotaWindow ParseSingleWindow(string body)
    {
        var outcome = UsageNormalizer.Normalize(body, ObservedAt);

        return Assert.IsType<FetchOutcome.Success>(outcome).Snapshot.Windows.Single();
    }

    /// <summary>
    /// A limits payload whose one window carries the given raw <c>severity</c> value. Takes a
    /// JSON fragment rather than a string so that null and non-string values can be tested
    /// through the same helper.
    /// </summary>
    private static string PayloadWithSeverity(string rawSeverityJson) =>
        $$"""
        {
          "limits": [
            {
              "kind": "session",
              "group": "session",
              "percent": 61,
              "severity": {{rawSeverityJson}},
              "resets_at": "2026-08-30T21:30:00.972286+00:00",
              "scope": null,
              "is_active": true
            }
          ]
        }
        """;

    [Fact]
    public void TheObservedNormalSeverityIsCarriedThrough()
    {
        Assert.Equal(
            ServerSeverity.Normal,
            ParseSingleWindow(PayloadWithSeverity("\"normal\"")).Severity);
    }

    [Theory]
    [InlineData("warning", ServerSeverity.Warning)]
    [InlineData("critical", ServerSeverity.Critical)]
    [InlineData("rejected", ServerSeverity.Rejected)]
    public void TheSpellingsInferredFromPriorArtAreRecognized(string raw, ServerSeverity expected)
    {
        // None of these has been seen on a live response. They are recognized on the strength
        // of prior art, and the test exists so that when one does arrive it is already handled.
        Assert.Equal(expected, ParseSingleWindow(PayloadWithSeverity($"\"{raw}\"")).Severity);
    }

    [Theory]
    [InlineData("\"exhausted\"")]
    [InlineData("\"NORMAL_BUT_LOUDER\"")]
    [InlineData("\"\"")]
    [InlineData("null")]
    [InlineData("3")]
    public void AnUnrecognizedSeverityIsUnknownRatherThanNormal(string rawSeverityJson)
    {
        Assert.Equal(
            ServerSeverity.Unknown,
            ParseSingleWindow(PayloadWithSeverity(rawSeverityJson)).Severity);
    }

    [Fact]
    public void AWindowWithNoSeverityFieldAtAllIsUnknown()
    {
        var body =
            """
            {
              "limits": [
                { "kind": "session", "group": "session", "percent": 61, "resets_at": null }
              ]
            }
            """;

        Assert.Equal(ServerSeverity.Unknown, ParseSingleWindow(body).Severity);
    }

    [Fact]
    public void AFlatWindowIsUnknownBecauseThatFormReportsNoSeverity()
    {
        // Not a gap to be filled in later: the flat payload genuinely carries no severity, and
        // the honest value for "the server did not say" is the same one used for "the server
        // said something we do not understand".
        var body = """{ "five_hour": { "utilization": 61, "resets_at": null } }""";

        Assert.Equal(ServerSeverity.Unknown, ParseSingleWindow(body).Severity);
    }

    [Fact]
    public void BothBaselineWindowsCarryTheNormalTheServerReported()
    {
        var outcome = UsageNormalizer.Normalize(Fixtures.Read(Fixtures.Baseline), ObservedAt);
        var snapshot = Assert.IsType<FetchOutcome.Success>(outcome).Snapshot;

        Assert.All(snapshot.Windows, w => Assert.Equal(ServerSeverity.Normal, w.Severity));
    }
}
