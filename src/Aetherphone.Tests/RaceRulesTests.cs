using System.Security.Cryptography;
using System.Text;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RaceRulesTests
{
    [Fact]
    public void TheVectorFileIsTheBackendCopyByteForByte()
    {
        var text = File.ReadAllText(RaceVectors.Path).Replace("\r", string.Empty, StringComparison.Ordinal);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        Assert.Equal(RaceVectors.FileHash, hash);
    }

    [Fact]
    public void TheConstantsMirrorTheWireDocument()
    {
        Assert.Equal(8, RaceRules.FieldSize);
        Assert.Equal(24, RaceRules.BirdBank);
        Assert.Equal(60, RaceRules.StrengthBase);
        Assert.Equal(140, RaceRules.StrengthSpread);
        Assert.Equal(10200, RaceRules.OverroundBasisPoints);
        Assert.Equal(130, RaceRules.MinOddsHundredths);
        Assert.Equal(4000, RaceRules.MaxOddsHundredths);
        Assert.Equal(110, RaceRules.MinPlaceOddsHundredths);
        Assert.Equal(4000, RaceRules.MaxPlaceOddsHundredths);
        Assert.Equal(100, RaceRules.MinBet);
        Assert.Equal(6, RaceRules.MaxTickets);
        Assert.Equal(40, RaceRules.MillisecondsPerTick);
        Assert.Equal(25, RaceScript.TicksPerSecond);
        Assert.Equal(800, RaceScript.RaceTicks);
        Assert.Equal(256, RaceScript.SubTicks);
        Assert.Equal(1_000_000, RaceScript.TrackUnits);
        Assert.Equal(960, RaceScript.MinGapSubTicks);
        Assert.Equal(5760, RaceScript.MaxGapSubTicks);
        Assert.Equal(1280, RaceScript.PhotoGapSubTicks);
        Assert.Equal(0, RaceRules.KindWin);
        Assert.Equal(1, RaceRules.KindPlace);
        Assert.Equal(2, RaceRules.KindForecast);
        Assert.Equal(3, RaceRules.KindReverse);
        Assert.Equal(60, Aetherphone.Core.Casino.CasinoRoomCadence.RaceOpenSeconds);
        Assert.Equal(35, Aetherphone.Core.Casino.CasinoRoomCadence.RaceLockedSeconds);
        Assert.Equal(15, Aetherphone.Core.Casino.CasinoRoomCadence.RaceResultSeconds);
    }

    [Fact]
    public void WinOddsFollowTheFlooredHundredthsFormulaOnEveryVector()
    {
        var vectors = RaceVectors.Load();
        for (var race = 0; race < vectors.Length; race++)
        {
            var vector = vectors[race];
            var sum = 0;
            for (var slot = 0; slot < RaceRules.FieldSize; slot++)
            {
                sum += vector.Strengths[slot];
            }

            for (var slot = 0; slot < RaceRules.FieldSize; slot++)
            {
                Assert.Equal(vector.OddsHundredths[slot], RaceRules.WinOddsHundredths(sum, vector.Strengths[slot]));
            }
        }
    }

    [Fact]
    public void ForecastAndReversePayMatchTheBackendForTheFirstTwoHome()
    {
        var vectors = RaceVectors.Load();
        for (var race = 0; race < vectors.Length; race++)
        {
            var vector = vectors[race];
            var first = vector.OddsHundredths[vector.Order[0]];
            var second = vector.OddsHundredths[vector.Order[1]];
            Assert.Equal(vector.ForecastPayHundredths, RaceRules.ForecastPayHundredths(first, second));
            Assert.Equal(vector.ReversePayHundredths, RaceRules.ReversePayHundredths(first, second));
        }
    }

    [Fact]
    public void RatingsSpanOneToFiveAcrossTheStrengthBand()
    {
        Assert.Equal(1, RaceRules.Rating(60));
        Assert.Equal(5, RaceRules.Rating(199));
        Assert.Equal(3, RaceRules.Rating(118));
        for (var strength = RaceRules.StrengthBase; strength < RaceRules.StrengthBase + RaceRules.StrengthSpread; strength++)
        {
            Assert.InRange(RaceRules.Rating(strength), RaceRules.MinRating, RaceRules.MaxRating);
        }
    }

    [Fact]
    public void TicketsNeedADistinctSecondRunnerOnlyForPairKinds()
    {
        Assert.True(RaceRules.IsTicket(RaceRules.KindWin, 3, RaceRules.NoRunner));
        Assert.False(RaceRules.IsTicket(RaceRules.KindWin, 3, 4));
        Assert.True(RaceRules.IsTicket(RaceRules.KindPlace, 0, RaceRules.NoRunner));
        Assert.True(RaceRules.IsTicket(RaceRules.KindForecast, 2, 5));
        Assert.False(RaceRules.IsTicket(RaceRules.KindForecast, 2, 2));
        Assert.False(RaceRules.IsTicket(RaceRules.KindReverse, 2, RaceRules.NoRunner));
        Assert.False(RaceRules.IsTicket(RaceRules.KindCount, 2, RaceRules.NoRunner));
        Assert.False(RaceRules.IsTicket(RaceRules.KindWin, RaceRules.FieldSize, RaceRules.NoRunner));
    }

    [Fact]
    public void EveryTicketHasItsOwnKey()
    {
        var seen = new HashSet<int>();
        for (var kind = 0; kind < RaceRules.KindCount; kind++)
        {
            for (var runner = 0; runner < RaceRules.FieldSize; runner++)
            {
                for (var runnerB = RaceRules.NoRunner; runnerB < RaceRules.FieldSize; runnerB++)
                {
                    if (RaceRules.IsTicket(kind, runner, runnerB))
                    {
                        Assert.True(seen.Add(RaceRules.TicketKey(kind, runner, runnerB)));
                    }
                }
            }
        }
    }

    [Fact]
    public void PayoutsFloorToWholeChips()
    {
        Assert.Equal(19600, RaceRules.Payout(2500, 784));
        Assert.Equal(232, RaceRules.Payout(100, 232));
        Assert.Equal(1, RaceRules.Payout(101, 110) - 110);
        Assert.Equal(0, RaceRules.Payout(0, 784));
    }

    [Fact]
    public void PotentialPayNamesTheBestOrderForAReverseTicket()
    {
        Assert.Equal(697, RaceRules.PotentialPayHundredths(RaceRules.KindWin, 697, 232, 0));
        Assert.Equal(232, RaceRules.PotentialPayHundredths(RaceRules.KindPlace, 697, 232, 0));
        Assert.Equal(RaceRules.ForecastPayHundredths(613, 555),
            RaceRules.PotentialPayHundredths(RaceRules.KindForecast, 613, 210, 555));
        var reverse = Math.Max(RaceRules.ReversePayHundredths(613, 555), RaceRules.ReversePayHundredths(555, 613));
        Assert.Equal(reverse, RaceRules.PotentialPayHundredths(RaceRules.KindReverse, 613, 210, 555));
    }

    [Fact]
    public void TheRaceClockRunsAtFortyMillisecondsATick()
    {
        Assert.Equal(0, RaceRules.ElapsedSubTicks(1_000, 2_000));
        Assert.Equal(RaceScript.SubTicks, RaceRules.ElapsedSubTicks(2_040, 2_000));
        Assert.Equal(25L * RaceScript.SubTicks, RaceRules.ElapsedSubTicks(3_000, 2_000));
        Assert.Equal(1000, RaceRules.MillisecondsOf(25L * RaceScript.SubTicks));
    }

    [Fact]
    public void TheScriptRebuildsEveryVectorRaceExactly()
    {
        var vectors = RaceVectors.Load();
        Assert.Equal(16, vectors.Length);
        for (var race = 0; race < vectors.Length; race++)
        {
            var vector = vectors[race];
            var plan = RaceScript.Build(vector.Order, vector.Seed);
            Assert.Equal(vector.FinishSubTicks, plan.FinishSubTicks);
            Assert.Equal(vector.PhotoFinish, plan.PhotoFinish);
            Assert.Equal(vector.FadeTick, plan.FadeTick);
            for (var slot = 0; slot < RaceRules.FieldSize; slot++)
            {
                Assert.Equal(vector.Surges[slot], plan.Surges[slot]);
                for (var sample = 0; sample < vector.SampleTicks.Length; sample++)
                {
                    Assert.Equal(vector.Positions[slot][sample],
                        RaceScript.PositionAtTick(plan, slot, vector.SampleTicks[sample]));
                }
            }

            for (var sample = 0; sample < vector.SampleTicks.Length; sample++)
            {
                Assert.Equal(vector.Leaders[sample],
                    RaceScript.LeaderAt(plan, (long)vector.SampleTicks[sample] * RaceScript.SubTicks));
            }
        }
    }

    [Fact]
    public void FinishGapsAreHonestToTheDrawnOrder()
    {
        var vectors = RaceVectors.Load();
        for (var race = 0; race < vectors.Length; race++)
        {
            var vector = vectors[race];
            var plan = RaceScript.Build(vector.Order, vector.Seed);
            for (var place = 1; place < RaceRules.FieldSize; place++)
            {
                var gap = plan.FinishSubTicks[vector.Order[place]] - plan.FinishSubTicks[vector.Order[place - 1]];
                Assert.InRange(gap, RaceScript.MinGapSubTicks, RaceScript.MaxGapSubTicks);
            }

            var firstGap = plan.FinishSubTicks[vector.Order[1]] - plan.FinishSubTicks[vector.Order[0]];
            Assert.Equal(plan.PhotoFinish, firstGap < RaceScript.PhotoGapSubTicks);
            Assert.True(plan.FinishSubTicks[vector.Order[RaceRules.FieldSize - 1]]
                        <= RaceScript.RaceTicks * RaceScript.SubTicks);
        }
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var vector = RaceVectors.Load()[3];
        var first = RaceScript.Build(vector.Order, vector.Seed);
        var second = RaceScript.Build(vector.Order, vector.Seed);
        Assert.Equal(first.FinishSubTicks, second.FinishSubTicks);
        for (var slot = 0; slot < RaceRules.FieldSize; slot++)
        {
            Assert.Equal(first.Speeds[slot], second.Speeds[slot]);
            Assert.Equal(first.Covered[slot], second.Covered[slot]);
        }
    }

    [Fact]
    public void ThePhotoFinishRateSitsNearOneInFour()
    {
        var order = new[] { 0, 1, 2, 3, 4, 5, 6, 7 };
        var photos = 0;
        const int Races = 2000;
        var seed = new byte[32];
        for (var race = 0; race < Races; race++)
        {
            BitConverter.TryWriteBytes(seed, (long)race * 7919 + 17);
            var hex = Convert.ToHexStringLower(SHA256.HashData(seed));
            if (RaceScript.Build(order, hex).PhotoFinish)
            {
                photos++;
            }
        }

        Assert.InRange(photos / (double)Races, 0.21, 0.29);
    }

    [Fact]
    public void TheVerifierReplaysTheFieldTheStrengthsAndTheOrder()
    {
        var vectors = RaceVectors.Load();
        Span<int> birds = stackalloc int[RaceRules.FieldSize];
        Span<int> order = stackalloc int[RaceRules.FieldSize];
        for (var race = 0; race < vectors.Length; race++)
        {
            var vector = vectors[race];
            var seed = Convert.FromHexString(vector.Seed);
            Assert.True(CasinoVerifier.ReplaysRaceLog(seed, vector.StreamBinding, vector.DrawLog, birds, order));
            Assert.Equal(vector.Birds, birds.ToArray());
            Assert.Equal(vector.Order, order.ToArray());
            var commit = Convert.ToHexStringLower(SHA256.HashData(seed));
            Assert.Equal(CasinoRoundVerdict.Match, CasinoVerifier.Verify(CasinoWire.RaceKind, vector.Seed, commit,
                "r-" + race, vector.DrawLog, vector.StreamBinding));
        }
    }

    [Fact]
    public void ATamperedRaceLogFailsVerification()
    {
        var vector = RaceVectors.Load()[0];
        var seed = Convert.FromHexString(vector.Seed);
        var commit = Convert.ToHexStringLower(SHA256.HashData(seed));
        var tampered = vector.DrawLog.Replace("runner:226", "runner:227", StringComparison.Ordinal);
        Assert.Equal(CasinoRoundVerdict.Mismatch, CasinoVerifier.Verify(CasinoWire.RaceKind, vector.Seed, commit,
            "r-0", tampered, vector.StreamBinding));
        var truncated = vector.DrawLog[..vector.DrawLog.LastIndexOf(';')];
        Assert.Equal(CasinoRoundVerdict.Mismatch, CasinoVerifier.Verify(CasinoWire.RaceKind, vector.Seed, commit,
            "r-0", truncated, vector.StreamBinding));
        Assert.Equal(CasinoRoundVerdict.Mismatch, CasinoVerifier.Verify(CasinoWire.RaceKind, vector.Seed, commit,
            "r-0", vector.DrawLog, "race-track#99"));
    }
}
