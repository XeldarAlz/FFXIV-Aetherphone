using Aetherphone.Apps.Casino;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoLobbyTests
{
    private const long Day = 86_400;

    [Fact]
    public void LimitBoundsMirrorTheServerInChips()
    {
        Assert.Equal(50_000, CasinoLimits.SelfLimitFloor);
        Assert.Equal(25_000_000, CasinoLimits.FallbackCeiling);
        Assert.Equal(25_000_000, CasinoLimitPicker.CeilingFor(0));
        Assert.Equal(25_000_000, CasinoLimitPicker.CeilingFor(25_000_000));
    }

    [Fact]
    public void ZeroLimitOnTheWireMeansNoLimit()
    {
        var tonight = CasinoTonight.From(new CasinoStateDto(LossLimit: 0, LossHeadroom: 0, NetLossToday: 12_000));
        Assert.False(tonight.HasLimit);
        Assert.False(tonight.Reached);
        Assert.Equal(0f, tonight.Fraction);
        Assert.Equal(TonightTone.Calm, tonight.Tone);
    }

    [Fact]
    public void TheGaugeFillsWithWhatTheLimitHasUsed()
    {
        var calm = CasinoTonight.From(new CasinoStateDto(LossLimit: 50_000, LossHeadroom: 40_000));
        Assert.Equal(0.2f, calm.Fraction, 3);
        Assert.Equal(TonightTone.Calm, calm.Tone);

        var close = CasinoTonight.From(new CasinoStateDto(LossLimit: 50_000, LossHeadroom: 5_000));
        Assert.Equal(TonightTone.Close, close.Tone);

        var reached = CasinoTonight.From(new CasinoStateDto(LossLimit: 50_000, LossHeadroom: 0));
        Assert.True(reached.Reached);
        Assert.Equal(1f, reached.Fraction);
        Assert.Equal(TonightTone.Reached, reached.Tone);
    }

    [Theory]
    [InlineData(1, 50_000)]
    [InlineData(54_000, 50_000)]
    [InlineData(56_000, 60_000)]
    [InlineData(640_000, 600_000)]
    [InlineData(650_000, 700_000)]
    [InlineData(7_400_000, 7_000_000)]
    [InlineData(90_000_000, 25_000_000)]
    public void ChosenLimitsSnapToStepsThatGrowWithTheAmount(long raw, long expected)
    {
        Assert.Equal(expected, CasinoLimitPicker.Snap(raw, 25_000_000));
    }

    [Fact]
    public void TheSliderEndsLandOnTheBounds()
    {
        Assert.Equal(CasinoLimitPicker.Floor, CasinoLimitPicker.FromFraction(0f, 25_000_000));
        Assert.Equal(25_000_000, CasinoLimitPicker.FromFraction(1f, 25_000_000));
        Assert.Equal(0f, CasinoLimitPicker.FractionOf(CasinoLimitPicker.Floor, 25_000_000));
        Assert.Equal(1f, CasinoLimitPicker.FractionOf(25_000_000, 25_000_000), 4);
    }

    [Fact]
    public void TheSliderRoundTripsASnappedValue()
    {
        const long chosen = 500_000;
        var fraction = CasinoLimitPicker.FractionOf(chosen, 25_000_000);
        Assert.Equal(chosen, CasinoLimitPicker.FromFraction(fraction, 25_000_000));
    }

    [Fact]
    public void SteppersCrossStepBoundariesCleanly()
    {
        Assert.Equal(600_000, CasinoLimitPicker.Nudge(500_000, 1, 25_000_000));
        Assert.Equal(490_000, CasinoLimitPicker.Nudge(500_000, -1, 25_000_000));
        Assert.Equal(50_000, CasinoLimitPicker.Nudge(50_000, -1, 25_000_000));
        Assert.Equal(25_000_000, CasinoLimitPicker.Nudge(25_000_000, 1, 25_000_000));
    }

    [Fact]
    public void LoweringStartsNowAndRaisingWaitsForTheNextDay()
    {
        Assert.Equal(LimitChange.StartsNow, CasinoLimitPicker.ChangeOf(40_000, 50_000, false, null));
        Assert.Equal(LimitChange.NextDay, CasinoLimitPicker.ChangeOf(60_000, 50_000, false, null));
        Assert.Equal(LimitChange.StartsNow, CasinoLimitPicker.ChangeOf(60_000, null, false, null));
        Assert.Equal(LimitChange.NextDay, CasinoLimitPicker.ChangeOf(null, 50_000, false, null));
        Assert.Equal(LimitChange.None, CasinoLimitPicker.ChangeOf(50_000, 50_000, false, null));
        Assert.Equal(LimitChange.None, CasinoLimitPicker.ChangeOf(80_000, 50_000, true, 80_000));
        Assert.Equal(LimitChange.None, CasinoLimitPicker.ChangeOf(null, 50_000, true, null));
    }

    [Fact]
    public void RecentGamesAreDistinctNewestFirstAndSkipUnknownKinds()
    {
        var rounds = new[]
        {
            Round("casino.slots", 10),
            Round("casino.dailyspin", 9),
            Round("casino.slots", 8),
            Round("casino.wheel", 7),
            Round("poker", 6),
            Round("casino.bartender", 5),
        };
        string[] playable = { "blackjack", "slots", "scratch", "bartender", "bingo", "wheel" };
        Span<int> picked = stackalloc int[4];
        var count = CasinoRecentGames.Collect(rounds, playable, picked);
        Assert.Equal(3, count);
        Assert.Equal(1, picked[0]);
        Assert.Equal(5, picked[1]);
        Assert.Equal(3, picked[2]);
    }

    [Fact]
    public void RecentGamesStopAtTheShelfSize()
    {
        var rounds = new[]
        {
            Round("casino.slots", 6), Round("casino.wheel", 5), Round("casino.bingo", 4),
            Round("casino.scratch", 3), Round("casino.blackjack", 2),
        };
        string[] playable = { "blackjack", "slots", "scratch", "bartender", "bingo", "wheel" };
        Span<int> picked = stackalloc int[2];
        Assert.Equal(2, CasinoRecentGames.Collect(rounds, playable, picked));
    }

    [Fact]
    public void HistoryDaysTotalOnlySettledRoundsAndFlagTheTruncatedDay()
    {
        var rounds = new[]
        {
            Round("casino.slots", Day * 3 + 50, stake: 100, payout: 300),
            Round("casino.slots", Day * 3 + 10, stake: 100, payout: 0, state: CasinoRoundStates.Voided),
            Round("casino.wheel", Day * 2 + 40, stake: 200, payout: 0),
            Round("casino.wheel", Day * 2 + 20, stake: 200, payout: 0, state: CasinoRoundStates.Open),
        };
        var days = new List<HistoryDay>();
        CasinoHistoryDays.Group(rounds, true, SameUtcDay, days);
        Assert.Equal(2, days.Count);
        Assert.Equal(new HistoryDay(0, 2, 200, true, true), days[0]);
        Assert.Equal(new HistoryDay(2, 4, -200, false, true), days[1]);

        CasinoHistoryDays.Group(rounds, false, SameUtcDay, days);
        Assert.True(days[1].Complete);
    }

    private static bool SameUtcDay(long first, long second) => first / Day == second / Day;

    private static CasinoRoundHistoryDto Round(string kind, long createdAt, long stake = 100, long payout = 0,
        int state = CasinoRoundStates.Settled) =>
        new(RoundId: kind + createdAt, GameKind: kind, Stake: stake, Payout: payout, State: state,
            CreatedAtUnix: createdAt);
}
