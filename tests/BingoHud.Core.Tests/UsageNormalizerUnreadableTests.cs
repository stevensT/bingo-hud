using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// What happens when a response yields nothing Bingo recognizes.
///
/// <para>
/// The answer is always the same, and it is the reason this project exists in the form it does:
/// say so, and show nothing. There is no partial success, no empty window list rendered as
/// zeroes, and no last-known value quietly standing in. A zero is a reading — it means the
/// window is untouched — so the absence of a reading must never be able to render as one.
/// </para>
/// <para>
/// <see cref="FetchOutcome.Unreadable"/> carries no snapshot at all, so this is enforced by the
/// type rather than by discipline. The tests below pin the inputs that must reach it.
/// </para>
/// </summary>
public class UsageNormalizerUnreadableTests
{
    private static readonly DateTimeOffset ObservedAt =
        new(2026, 8, 30, 10, 9, 23, TimeSpan.FromHours(-7));

    private static FetchOutcome.Unreadable ParseExpectingUnreadable(string body) =>
        Assert.IsType<FetchOutcome.Unreadable>(UsageNormalizer.Normalize(body, ObservedAt));

    [Fact]
    public void AnEmptyObjectIsUnreadable()
    {
        ParseExpectingUnreadable("{}");
    }

    [Fact]
    public void APayloadOfNothingButUnknownKeysIsUnreadable()
    {
        // Tolerance means an unknown key alongside a real window is ignored. It does not mean
        // a payload made entirely of unknown keys counts as a successful reading of zero
        // windows.
        var body =
            """
            {
              "tangelo": null,
              "nimbus_quill": { "utilization": 0, "resets_at": null },
              "member_dashboard_available": false
            }
            """;

        ParseExpectingUnreadable(body);
    }

    [Fact]
    public void AnEmptyLimitsArrayIsUnreadable()
    {
        // Present but empty. The flat keys are not consulted — limits[] was there, and it said
        // there are no windows.
        ParseExpectingUnreadable("""{ "limits": [] }""");
    }

    [Fact]
    public void ALimitsArrayOfOnlyUnrecognizedKindsIsUnreadable()
    {
        var body =
            """
            {
              "limits": [
                { "kind": "fortnightly_vibes", "percent": 12, "severity": "normal" }
              ]
            }
            """;

        ParseExpectingUnreadable(body);
    }

    [Fact]
    public void ABodyThatIsNotJsonIsUnreadable()
    {
        // An HTML error page from an intermediary is the realistic version of this: a proxy or
        // a captive portal answering instead of the API. It must not escape as an exception.
        ParseExpectingUnreadable("<html><body>502 Bad Gateway</body></html>");
    }

    [Fact]
    public void AnEmptyBodyIsUnreadable()
    {
        ParseExpectingUnreadable(string.Empty);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""["five_hour"]""")]
    [InlineData("\"a string\"")]
    [InlineData("42")]
    [InlineData("null")]
    public void ValidJsonThatIsNotAnObjectIsUnreadable(string body)
    {
        // Well-formed JSON of the wrong shape. This is what a payload redesign would most
        // likely look like, and it must land in the same explicit state as garbage.
        ParseExpectingUnreadable(body);
    }

    [Fact]
    public void AMalformedKnownWindowIsUnreadableRatherThanZero()
    {
        // The window is present and named, so it is not an unknown key to be skipped — its
        // percentage is simply unusable. Skipping it here would leave the session window
        // missing from a response that clearly meant to report one.
        var body =
            """
            {
              "limits": [
                { "kind": "session", "percent": "twelve percent", "severity": "normal" }
              ]
            }
            """;

        ParseExpectingUnreadable(body);
    }

    [Fact]
    public void AMalformedResetTimeIsUnreadableRatherThanIgnored()
    {
        // A reset time that cannot be parsed is not the same as one that was not reported.
        // Treating it as absent would silently drop a countdown the server did send.
        var body =
            """
            {
              "limits": [
                { "kind": "session", "percent": 12, "resets_at": "next Tuesdayish" }
              ]
            }
            """;

        ParseExpectingUnreadable(body);
    }

    [Fact]
    public void TheReasonGivenIsNonEmptySoTheDetailPanelHasSomethingToShow()
    {
        var unreadable = ParseExpectingUnreadable("{}");

        Assert.False(string.IsNullOrWhiteSpace(unreadable.Reason));
    }
}
