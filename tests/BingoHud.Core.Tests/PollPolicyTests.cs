using BingoHud.Core.Polling;

namespace BingoHud.Core.Tests;

/// <summary>
/// The poll cadence, as a table.
///
/// <para>
/// Two things make this testable without waiting for a single real interval: the policy is a
/// pure function over <see cref="PollSignals"/>, and it returns a named reason alongside the
/// delay. The reason is not decoration — it is shown in the detail panel, so a user who wonders
/// why the number has not moved in twenty minutes can be told, rather than left guessing at an
/// invisible timer.
/// </para>
/// <para>
/// The floor is the important half. This is someone else's undocumented service, utilization
/// only moves while Claude Code is working, and polling harder cannot make a number arrive
/// sooner than the server changes it — it can only cost the user the rate limit they are trying
/// to watch.
/// </para>
/// </summary>
public class PollPolicyTests
{
    [Fact]
    public void TheFloorIsTwoMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), PollPolicy.Floor);
    }

    [Fact]
    public void TheCeilingIsThirtyMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(30), PollPolicy.Ceiling);
    }

    public static TheoryData<PollSignals> EverySignalCombination
    {
        get
        {
            var data = new TheoryData<PollSignals>();
            TimeSpan?[] spans = [null, TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromHours(2)];

            foreach (var power in new[] { true, false })
            foreach (var panel in spans)
            foreach (var activity in spans)
            foreach (var failed in new[] { true, false })
            {
                data.Add(new PollSignals(power, panel, activity, failed));
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(EverySignalCombination))]
    public void NoCombinationOfSignalsPollsFasterThanTheFloor(PollSignals signals)
    {
        // AC-25 stated as a property rather than as a row, so it holds for combinations nobody
        // thought to write a row for.
        var (delay, _) = PollPolicy.NextDelay(signals);

        Assert.True(delay >= PollPolicy.Floor, $"Delay was {delay}, below the {PollPolicy.Floor} floor.");
    }

    [Theory]
    [MemberData(nameof(EverySignalCombination))]
    public void NoCombinationOfSignalsExceedsTheCeiling(PollSignals signals)
    {
        var (delay, _) = PollPolicy.NextDelay(signals);

        Assert.True(delay <= PollPolicy.Ceiling, $"Delay was {delay}, above the {PollPolicy.Ceiling} ceiling.");
    }

    [Theory]
    [MemberData(nameof(EverySignalCombination))]
    public void EveryDecisionCarriesAReason(PollSignals signals)
    {
        var (_, reason) = PollPolicy.NextDelay(signals);

        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public void AServerRetryAfterIsHonouredExactly()
    {
        var signals = new PollSignals(ServerRetryAfter: TimeSpan.FromMinutes(7));

        var (delay, reason) = PollPolicy.NextDelay(signals);

        Assert.Equal(TimeSpan.FromMinutes(7), delay);
        Assert.Equal(PollPolicy.Reasons.ServerAskedToWait, reason);
    }

    [Fact]
    public void AServerRetryAfterBeyondTheCeilingIsStillHonoured()
    {
        // The ceiling is Bingo's own restraint. It is not a licence to poll sooner than the
        // service asked.
        var signals = new PollSignals(ServerRetryAfter: TimeSpan.FromHours(2));

        var (delay, _) = PollPolicy.NextDelay(signals);

        Assert.Equal(TimeSpan.FromHours(2), delay);
    }

    [Fact]
    public void AServerRetryAfterBelowTheFloorIsRaisedToTheFloor()
    {
        var signals = new PollSignals(ServerRetryAfter: TimeSpan.FromSeconds(5));

        var (delay, _) = PollPolicy.NextDelay(signals);

        Assert.Equal(PollPolicy.Floor, delay);
    }

    [Fact]
    public void AServerRetryAfterOutranksEveryOtherSignal()
    {
        var signals = new PollSignals(
            PowerConstrained: false,
            SinceUserOpenedPanel: TimeSpan.Zero,
            SinceLocalTranscriptActivity: TimeSpan.Zero,
            LastAttemptFailed: false,
            ServerRetryAfter: TimeSpan.FromMinutes(20));

        var (delay, reason) = PollPolicy.NextDelay(signals);

        Assert.Equal(TimeSpan.FromMinutes(20), delay);
        Assert.Equal(PollPolicy.Reasons.ServerAskedToWait, reason);
    }

    [Fact]
    public void AFailedAttemptBacksOffToTheCeiling()
    {
        // No attempt counter is available here, so there is no exponential curve to ride. When
        // an undocumented endpoint is not answering, the slowest cadence is both the politest
        // guess and the cheapest one.
        var signals = new PollSignals(LastAttemptFailed: true, SinceUserOpenedPanel: TimeSpan.Zero);

        var (delay, reason) = PollPolicy.NextDelay(signals);

        Assert.Equal(PollPolicy.Ceiling, delay);
        Assert.Equal(PollPolicy.Reasons.LastAttemptFailed, reason);
    }

    [Fact]
    public void BatteryPowerBacksOffToTheCeiling()
    {
        var signals = new PollSignals(PowerConstrained: true, SinceLocalTranscriptActivity: TimeSpan.Zero);

        var (delay, reason) = PollPolicy.NextDelay(signals);

        Assert.Equal(PollPolicy.Ceiling, delay);
        Assert.Equal(PollPolicy.Reasons.PowerConstrained, reason);
    }

    [Fact]
    public void AnOpenPanelPollsAtTheFloor()
    {
        // Someone is looking at the numbers. This is the only situation where a fast cadence
        // buys anything at all.
        var signals = new PollSignals(SinceUserOpenedPanel: TimeSpan.FromSeconds(30));

        var (delay, reason) = PollPolicy.NextDelay(signals);

        Assert.Equal(PollPolicy.Floor, delay);
        Assert.Equal(PollPolicy.Reasons.UserIsWatching, reason);
    }

    [Fact]
    public void APanelClosedLongAgoNoLongerCountsAsWatching()
    {
        var signals = new PollSignals(SinceUserOpenedPanel: TimeSpan.FromHours(3));

        var (_, reason) = PollPolicy.NextDelay(signals);

        Assert.NotEqual(PollPolicy.Reasons.UserIsWatching, reason);
    }

    [Fact]
    public void RecentClaudeCodeActivityPollsAtTheWorkingCadence()
    {
        // Utilization only moves while Claude Code is working, so this is the window in which
        // there is anything new to fetch.
        var signals = new PollSignals(SinceLocalTranscriptActivity: TimeSpan.FromMinutes(3));

        var (delay, reason) = PollPolicy.NextDelay(signals);

        Assert.Equal(TimeSpan.FromMinutes(5), delay);
        Assert.Equal(PollPolicy.Reasons.ClaudeCodeIsWorking, reason);
    }

    [Fact]
    public void AnIdleMachinePollsAtTheCeiling()
    {
        var signals = new PollSignals(SinceLocalTranscriptActivity: TimeSpan.FromHours(4));

        var (delay, reason) = PollPolicy.NextDelay(signals);

        Assert.Equal(PollPolicy.Ceiling, delay);
        Assert.Equal(PollPolicy.Reasons.NothingIsHappening, reason);
    }

    [Fact]
    public void KnowingNothingAtAllPollsAtTheCeiling()
    {
        // All signals null or false: no panel, no transcript information, no failure. Absence
        // of evidence is not a reason to poll hard.
        var (delay, reason) = PollPolicy.NextDelay(new PollSignals());

        Assert.Equal(PollPolicy.Ceiling, delay);
        Assert.Equal(PollPolicy.Reasons.NothingIsHappening, reason);
    }

    [Fact]
    public void AWatchingUserOutranksRecentActivity()
    {
        var signals = new PollSignals(
            SinceUserOpenedPanel: TimeSpan.Zero,
            SinceLocalTranscriptActivity: TimeSpan.Zero);

        var (delay, reason) = PollPolicy.NextDelay(signals);

        Assert.Equal(PollPolicy.Floor, delay);
        Assert.Equal(PollPolicy.Reasons.UserIsWatching, reason);
    }

    [Fact]
    public void AWatchingUserOutranksBatteryPower()
    {
        // Found by mutation: nothing pinned this, so demoting the battery rule below the panel
        // rule broke no test. Deciding it explicitly — power saving is for when the app is
        // unattended, and someone with the panel open is attending and asking. The cost is
        // bounded because they close the panel.
        var signals = new PollSignals(
            PowerConstrained: true,
            SinceUserOpenedPanel: TimeSpan.FromSeconds(30));

        var (delay, reason) = PollPolicy.NextDelay(signals);

        Assert.Equal(PollPolicy.Floor, delay);
        Assert.Equal(PollPolicy.Reasons.UserIsWatching, reason);
    }

    [Fact]
    public void BatteryPowerOutranksARecentlyClosedPanel()
    {
        // The other side of the same rule: once the user stops watching, the battery wins.
        var signals = new PollSignals(
            PowerConstrained: true,
            SinceUserOpenedPanel: TimeSpan.FromHours(1));

        var (delay, reason) = PollPolicy.NextDelay(signals);

        Assert.Equal(PollPolicy.Ceiling, delay);
        Assert.Equal(PollPolicy.Reasons.PowerConstrained, reason);
    }

    [Fact]
    public void AFailedAttemptOutranksAWatchingUser()
    {
        // Backing off matters more than responsiveness: the panel being open is exactly when a
        // user is most likely to keep retrying something that is already failing.
        var signals = new PollSignals(
            SinceUserOpenedPanel: TimeSpan.Zero,
            LastAttemptFailed: true);

        var (_, reason) = PollPolicy.NextDelay(signals);

        Assert.Equal(PollPolicy.Reasons.LastAttemptFailed, reason);
    }
}
