using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// The tolerance rule: a top-level key this version does not know is ignored, and ignoring it
/// costs nothing else.
///
/// <para>
/// This is not a hypothetical robustness exercise. The 2026-08-30 capture carries ten top-level
/// keys that correspond to nothing Bingo displays — <c>tangelo</c>, <c>nimbus_quill</c>,
/// <c>iguana_necktie</c> and the rest, evidently features that do not exist yet. A parser that
/// treated an unrecognized key as a problem would reject every real response, so tolerance here
/// is what makes the app work at all.
/// </para>
/// <para>
/// The opposite failure is the dangerous one, and it is the one these tests are really guarding:
/// several of those keys carry window-shaped values, so a parser that is tolerant in the wrong
/// way invents windows instead of rejecting responses.
/// </para>
/// <para>
/// Verified by mutation: with the flat reader changed to recognize any top-level object holding
/// a numeric <c>utilization</c>, twelve tests failed — all ten window-shaped cases here, plus
/// both derived-fixture assertions in <see cref="UsageNormalizerFlatFallbackTests"/>. The
/// <c>spend</c> and <c>extra_usage</c> cases below survived that particular mutation, because
/// neither carries a numeric <c>utilization</c>; they guard a different one, a parser that
/// reached for a top-level <c>percent</c>.
/// </para>
/// </summary>
public class UsageNormalizerUnknownKeyTests
{
    private static readonly DateTimeOffset ObservedAt =
        new(2026, 8, 30, 10, 9, 23, TimeSpan.FromHours(-7));

    /// <summary>
    /// Every top-level key in the baseline capture that this version does not consume. Listed
    /// by name rather than computed, so that a key moving from unknown to known is a
    /// deliberate edit here rather than a silent change in behaviour.
    /// </summary>
    public static TheoryData<string> UnknownKeys =>
    [
        "seven_day_oauth_apps",
        "seven_day_cowork",
        "seven_day_omelette",
        "tangelo",
        "iguana_necktie",
        "omelette_promotional",
        "nimbus_quill",
        "cinder_cove",
        "amber_ladder",
        "juniper_tide",
    ];

    private static QuotaSnapshot Parse(string body)
    {
        var outcome = UsageNormalizer.Normalize(body, ObservedAt);

        return Assert.IsType<FetchOutcome.Success>(outcome).Snapshot;
    }

    /// <summary>
    /// One real window, plus one extra top-level key carrying a window-shaped value. If the
    /// extra key is ignored as it should be, this payload describes exactly one window.
    /// </summary>
    private static string PayloadWithExtraKey(string key, string value) =>
        $$"""
        {
          "five_hour": {
            "utilization": 44,
            "resets_at": "2026-08-30T21:30:00.972286+00:00"
          },
          "{{key}}": {{value}}
        }
        """;

    private const string WindowShapedValue =
        """
        {
          "utilization": 0,
          "resets_at": null,
          "limit_dollars": null,
          "used_dollars": null,
          "remaining_dollars": null,
          "locked_reason": null
        }
        """;

    [Fact]
    public void TheBaselineParsesDespiteTheKeysThisVersionDoesNotKnow()
    {
        var snapshot = Parse(Fixtures.Read(Fixtures.Baseline));

        Assert.Equal(2, snapshot.Windows.Count);
    }

    [Theory]
    [MemberData(nameof(UnknownKeys))]
    public void AnUnknownKeyCarryingAWindowShapedValueYieldsNoWindow(string key)
    {
        var snapshot = Parse(PayloadWithExtraKey(key, WindowShapedValue));

        Assert.Equal(WindowKind.Session, snapshot.Windows.Single().Kind);
    }

    [Theory]
    [MemberData(nameof(UnknownKeys))]
    public void AnUnknownKeyDoesNotPreventTheWindowsBesideItFromParsing(string key)
    {
        // Tolerance has to hold for values of a shape nobody anticipated, not just for the
        // null and window-shaped ones this capture happens to contain.
        var snapshot = Parse(PayloadWithExtraKey(key, """{ "surprise": [1, 2, 3] }"""));

        Assert.Equal(44, snapshot.Windows.Single().UsedPercent);
    }

    [Fact]
    public void SpendIsNotReadAsAWindow()
    {
        // `spend` is the closest thing in the payload to a trap: it carries a `percent` and a
        // `severity`, exactly like an entry of the limits array, and it is about money rather
        // than quota. Reading it would put a dollar figure on screen labelled as usage.
        var spend =
            """
            {
              "used": { "amount_minor": 273, "currency": "USD", "exponent": 2 },
              "limit": null,
              "percent": 0,
              "severity": "normal",
              "enabled": true
            }
            """;

        var snapshot = Parse(PayloadWithExtraKey("spend", spend));

        Assert.Equal(WindowKind.Session, snapshot.Windows.Single().Kind);
    }

    [Fact]
    public void ExtraUsageIsNotReadAsAWindow()
    {
        // Also carries a `utilization` key. Extra usage is a non-goal for this feature.
        var extraUsage =
            """
            {
              "is_enabled": true,
              "monthly_limit": null,
              "used_credits": 273,
              "utilization": null,
              "currency": "USD"
            }
            """;

        var snapshot = Parse(PayloadWithExtraKey("extra_usage", extraUsage));

        Assert.Equal(WindowKind.Session, snapshot.Windows.Single().Kind);
    }
}
