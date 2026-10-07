using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.MoogleClicker;
using Aetherphone.Core.MoogleClicker;
using Newtonsoft.Json;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MoogleClickerBoardTests
{
    private const float Step = 0.05f;
    private const long Epoch = 1_700_000_000_000L;
    private const long Second = 1000L;
    private const double Tolerance = 1e-9;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Replay(4242);
        var second = Replay(4242);
        var other = Replay(7);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.Contains("catch", first, StringComparison.Ordinal);
    }

    [Fact]
    public void SlowTapsNeverBuildAComboOrACritical()
    {
        for (var seed = 1UL; seed <= 20UL; seed++)
        {
            var board = new MoogleClickerBoard();
            board.Reset(GameRandom.FromSeed(seed));
            for (var tap = 0; tap < 50; tap++)
            {
                var outcome = board.Tap();
                Assert.Equal(1, outcome.Multiplier);
                Assert.False(outcome.Critical);
                board.Update(MoogleClickerBoard.ComboWindowSeconds + 0.1f);
            }
        }
    }

    [Fact]
    public void FastTapsClimbTheComboTiersAndLandCriticals()
    {
        var board = new MoogleClickerBoard();
        board.Reset(GameRandom.FromSeed(99));
        var highest = 1;
        var tierUps = 0;
        for (var tap = 0; tap < 200; tap++)
        {
            var outcome = board.Tap();
            highest = Math.Max(highest, outcome.Multiplier);
            tierUps += outcome.TierUp ? 1 : 0;
            if (outcome.Critical)
            {
                Assert.True(outcome.Multiplier >= 2);
                Assert.Equal(outcome.Multiplier * MoogleClickerBoard.CriticalMultiplier, outcome.KupoMultiplier);
            }

            board.Update(Step);
        }

        Assert.Equal(ComboMeter.MaxMultiplier, highest);
        Assert.Equal(4, tierUps);
        Assert.True(board.Criticals > 0);
        Assert.Equal(200, board.BestCombo);
    }

    [Fact]
    public void CriticalChanceGrowsWithTheComboTier()
    {
        Assert.Equal(0f, MoogleClickerBoard.CriticalChance(1));
        var previous = 0f;
        foreach (var multiplier in new[] { 2, 3, 5, 8 })
        {
            var chance = MoogleClickerBoard.CriticalChance(multiplier);
            Assert.True(chance > previous);
            previous = chance;
        }
    }

    [Fact]
    public void TheFirstMinionArrivesInsideItsWindowAndAZeroDeltaFreezesIt()
    {
        for (var seed = 1UL; seed <= 30UL; seed++)
        {
            var board = new MoogleClickerBoard();
            board.Reset(GameRandom.FromSeed(seed));
            var countdown = board.MinionCountdown;
            board.Update(0f);
            Assert.Equal(countdown, board.MinionCountdown);
            var elapsed = 0f;
            while (!board.MinionActive && elapsed < 100f)
            {
                board.Update(Step);
                elapsed += Step;
            }

            Assert.True(board.MinionActive);
            Assert.InRange(elapsed, MoogleClickerBoard.FirstMinionMinSeconds - Step,
                MoogleClickerBoard.FirstMinionMaxSeconds + Step);
        }
    }

    [Fact]
    public void AnUncaughtMinionCrossesTheStageAndTheNextOneIsScheduled()
    {
        var board = ActiveMinion(5);
        var travelled = 0f;
        while (board.MinionActive)
        {
            var point = board.MinionPoint;
            Assert.InRange(point.X, -MoogleClickerBoard.MinionEdge - 0.001f, 1f + MoogleClickerBoard.MinionEdge + 0.001f);
            Assert.InRange(point.Y, MoogleClickerBoard.MinionTop - 0.001f, MoogleClickerBoard.MinionBottom + 0.001f);
            board.Update(Step);
            travelled += Step;
        }

        Assert.InRange(travelled, MoogleClickerBoard.MinionTravelSeconds - Step,
            MoogleClickerBoard.MinionTravelSeconds + Step);
        Assert.InRange(board.MinionCountdown, MoogleClickerBoard.MinionMinSeconds, MoogleClickerBoard.MinionMaxSeconds);
    }

    [Fact]
    public void CatchingGrantsARewardOnlyWhileAMinionFlies()
    {
        var idle = new MoogleClickerBoard();
        idle.Reset(GameRandom.FromSeed(3));
        Assert.Equal(KupoReward.None, idle.Catch());

        var seen = new HashSet<KupoReward>();
        for (var seed = 1UL; seed <= 60UL; seed++)
        {
            var board = ActiveMinion(seed);
            var reward = board.Catch();
            Assert.NotEqual(KupoReward.None, reward);
            Assert.False(board.MinionActive);
            Assert.Equal(KupoReward.None, board.Catch());
            seen.Add(reward);
        }

        Assert.Equal(3, seen.Count);
    }

    [Fact]
    public void BuildingCostsGrowFifteenPercentPerOwned()
    {
        Assert.Equal(15d, KupoBuildings.Cost(0, 0), Tolerance);
        Assert.Equal(17.25d, KupoBuildings.Cost(0, 1), Tolerance);
        for (var building = 0; building < KupoBuildings.Count; building++)
        {
            Assert.Equal(KupoBuildings.BaseCost(building), KupoBuildings.Cost(building, 0));
            for (var owned = 0; owned < 120; owned += 17)
            {
                var ratio = KupoBuildings.Cost(building, owned + 1) / KupoBuildings.Cost(building, owned);
                Assert.Equal(KupoBuildings.Growth, ratio, Tolerance);
            }

            if (building > 0)
            {
                Assert.True(KupoBuildings.BaseCost(building) > KupoBuildings.BaseCost(building - 1));
                Assert.True(KupoBuildings.BaseRate(building) > KupoBuildings.BaseRate(building - 1));
            }
        }
    }

    [Fact]
    public void BulkCostIsTheSumOfSingleCosts()
    {
        foreach (var building in new[] { 0, 5, 11 })
        {
            foreach (var owned in new[] { 0, 7, 40 })
            {
                foreach (var count in new[] { 1, 10, 37 })
                {
                    var sum = 0d;
                    for (var index = 0; index < count; index++)
                    {
                        sum += KupoBuildings.Cost(building, owned + index);
                    }

                    Assert.Equal(1d, KupoBuildings.BulkCost(building, owned, count) / sum, Tolerance);
                }
            }
        }

        Assert.Equal(0d, KupoBuildings.BulkCost(3, 4, 0));
    }

    [Fact]
    public void MaxPurchaseBuysEverythingTheBankCovers()
    {
        var exact = KupoBuildings.BulkCost(0, 3, 12);
        Assert.Equal(12, KupoBuildings.MaxAffordable(0, 3, exact));
        Assert.Equal(11, KupoBuildings.MaxAffordable(0, 3, exact * 0.999));
        Assert.Equal(0, KupoBuildings.MaxAffordable(0, 3, KupoBuildings.Cost(0, 3) * 0.5));
        Assert.Equal(0, KupoBuildings.MaxAffordable(0, KupoBuildings.MaxOwned, 1e300));

        var workshop = Workshop(Epoch, 3);
        var save = Saved(workshop);
        save.Kupo = exact + 1d;
        workshop.Load(save);
        Assert.Equal(12, workshop.BuyMax(0));
        Assert.Equal(15, workshop.Owned(0));
        Assert.InRange(workshop.Kupo, 0d, 1d + 1e-6);
        Assert.Equal(0, workshop.BuyMax(0));
    }

    [Fact]
    public void BuyingRefusesWhatTheBankCannotCover()
    {
        var workshop = Workshop(Epoch);
        Assert.Equal(0, workshop.Buy(1, 1));
        Assert.Equal(0, workshop.Owned(1));
        Assert.Equal(0, workshop.Buy(-1, 1));
        Assert.Equal(0, workshop.Buy(KupoBuildings.Count, 1));

        var save = Saved(workshop);
        save.Kupo = KupoBuildings.BulkCost(1, 0, 10);
        workshop.Load(save);
        Assert.Equal(0, workshop.Buy(1, 11));
        Assert.Equal(10, workshop.Buy(1, 10));
        Assert.Equal(10, workshop.Owned(1));
        Assert.Equal(0d, workshop.Kupo, 1e-6);
    }

    [Fact]
    public void AccrualFollowsTheWallClockGap()
    {
        var workshop = Workshop(Epoch, 10, 4, 2);
        var rate = workshop.BaseKupoPerSecond;
        Assert.Equal(10 * 0.1d + 4 * 1d + 2 * 8d, rate, Tolerance);

        var advance = workshop.Advance(Epoch + 10 * Second);
        Assert.Equal(10d, advance.Seconds, Tolerance);
        Assert.Equal(rate * 10d, advance.Kupo, Tolerance);
        Assert.Equal(rate * 10d, workshop.Kupo, Tolerance);
        Assert.Equal(workshop.Kupo, workshop.LedgerKupo, Tolerance);
        Assert.Equal(workshop.Kupo, workshop.LifetimeKupo, Tolerance);
        Assert.False(advance.Away);
        Assert.False(workshop.HasAwayReport);

        var again = workshop.Advance(Epoch + 10 * Second);
        Assert.Equal(0d, again.Kupo);

        var tickByTick = Workshop(Epoch, 10, 4, 2);
        for (var tick = 1; tick <= 10; tick++)
        {
            tickByTick.Advance(Epoch + tick * Second);
        }

        Assert.Equal(workshop.Kupo, tickByTick.Kupo, 1e-9);
    }

    [Fact]
    public void OfflineCatchUpIsCappedAtEightHoursAndReported()
    {
        var workshop = Workshop(Epoch, 0, 5);
        var rate = workshop.BaseKupoPerSecond;
        var advance = workshop.Advance(Epoch + 20L * 3600L * Second);

        Assert.Equal(KupoWorkshop.OfflineCapSeconds, advance.Seconds, Tolerance);
        Assert.Equal(rate * KupoWorkshop.OfflineCapSeconds, workshop.Kupo, Tolerance);
        Assert.True(advance.Away);
        Assert.True(workshop.HasAwayReport);
        Assert.True(workshop.AwayCapped);
        Assert.Equal(KupoWorkshop.OfflineCapSeconds, workshop.AwaySeconds, Tolerance);
        Assert.Equal(workshop.Kupo, workshop.AwayKupo, Tolerance);

        workshop.DismissAway();
        Assert.False(workshop.HasAwayReport);
        Assert.False(workshop.AwayCapped);
    }

    [Fact]
    public void OnlyGapsPastTheThresholdRaiseTheAwayCard()
    {
        var workshop = Workshop(Epoch, 0, 1);
        var now = Epoch;
        for (var tick = 0; tick < 30; tick++)
        {
            now += Second;
            workshop.Advance(now);
        }

        Assert.False(workshop.HasAwayReport);

        now += 120L * Second;
        workshop.Advance(now);
        Assert.True(workshop.HasAwayReport);
        Assert.False(workshop.AwayCapped);
        Assert.Equal(120d, workshop.AwaySeconds, Tolerance);
    }

    [Fact]
    public void AClockThatRunsBackwardsEarnsNothing()
    {
        var workshop = Workshop(Epoch, 0, 3);
        var back = workshop.Advance(Epoch - 5 * Second);

        Assert.Equal(0d, back.Kupo);
        Assert.Equal(0d, workshop.Kupo);
        Assert.Equal(Epoch - 5 * Second, workshop.LastTickUnixMilliseconds);
        workshop.Advance(Epoch);
        Assert.Equal(workshop.BaseKupoPerSecond * 5d, workshop.Kupo, Tolerance);
    }

    [Fact]
    public void AFrenzyMultipliesOnlyTheSecondsItCovers()
    {
        var workshop = Workshop(Epoch, 0, 2);
        var rate = workshop.BaseKupoPerSecond;
        Assert.Equal(0d, workshop.Reward(KupoReward.Frenzy, Epoch));
        Assert.True(workshop.FrenzyActive(Epoch + Second));
        Assert.Equal(rate * KupoWorkshop.FrenzyMultiplier, workshop.KupoPerSecond(Epoch + Second), Tolerance);

        workshop.Advance(Epoch + 100 * Second);
        var expected = rate * 100d + rate * (KupoWorkshop.FrenzyMultiplier - 1d) * KupoWorkshop.FrenzySeconds;
        Assert.Equal(expected, workshop.Kupo, 1e-6);
        Assert.False(workshop.BuffActive(Epoch + 100 * Second));
        Assert.Equal(KupoReward.None, workshop.Buff);
        Assert.Equal(1, workshop.MinionsCaught);
    }

    [Fact]
    public void ALumpPaysFromTheBankAndTheProductionRate()
    {
        var workshop = Workshop(Epoch, 0, 1);
        var save = Saved(workshop);
        save.Kupo = 1_000_000d;
        workshop.Load(save);
        var gained = workshop.Reward(KupoReward.Lump, Epoch);

        Assert.Equal(1d * KupoWorkshop.LumpProductionSeconds + KupoWorkshop.LumpFloor, gained, Tolerance);
        Assert.Equal(1_000_000d + gained, workshop.Kupo, Tolerance);
        Assert.Equal(0d, workshop.Reward(KupoReward.None, Epoch));
        Assert.Equal(1, workshop.MinionsCaught);
    }

    [Fact]
    public void TapUpgradesDoubleTapsAndShareTheProductionRate()
    {
        var workshop = Workshop(Epoch, 0, 10);
        Assert.Equal(1d, workshop.TapValue(Epoch), Tolerance);
        Assert.Equal(1d, workshop.Tap(Epoch, 0.5d), Tolerance);
        Assert.Equal(3d, workshop.Tap(Epoch, 3d), Tolerance);
        Assert.Equal(2, workshop.Taps);
        Assert.Equal(4d, workshop.TapKupo, Tolerance);

        var save = Saved(workshop);
        for (var tapIndex = 0; tapIndex < KupoUpgrades.TapUpgrades; tapIndex++)
        {
            save.Upgrades |= unchecked((long)KupoUpgrades.Bit(KupoUpgrades.ForTap(tapIndex)));
        }

        workshop.Load(save);
        var rate = workshop.KupoPerSecond(Epoch);
        Assert.Equal(4d + rate * 0.03d, workshop.TapValue(Epoch), Tolerance);
        workshop.Reward(KupoReward.TapFrenzy, Epoch);
        Assert.Equal((4d + rate * 0.03d) * KupoWorkshop.TapFrenzyMultiplier, workshop.TapValue(Epoch + Second),
            Tolerance);
    }

    [Fact]
    public void BuildingUpgradesUnlockAtTheirThresholdsAndDoubleOutput()
    {
        var workshop = Workshop(Epoch, 0, 0, 5);
        Assert.True(workshop.IsUnlocked(KupoUpgrades.ForBuilding(2, 0)));
        Assert.True(workshop.IsUnlocked(KupoUpgrades.ForBuilding(2, 1)));
        Assert.False(workshop.IsUnlocked(KupoUpgrades.ForBuilding(2, 2)));
        Assert.False(workshop.IsUnlocked(KupoUpgrades.ForBuilding(1, 0)));
        Assert.False(workshop.BuyUpgrade(KupoUpgrades.ForBuilding(2, 0)));

        var single = workshop.UnitRate(2);
        var save = Saved(workshop);
        save.Kupo = KupoUpgrades.Cost(KupoUpgrades.ForBuilding(2, 0)) + KupoUpgrades.Cost(KupoUpgrades.ForBuilding(2, 1));
        workshop.Load(save);
        Assert.True(workshop.BuyUpgrade(KupoUpgrades.ForBuilding(2, 0)));
        Assert.False(workshop.BuyUpgrade(KupoUpgrades.ForBuilding(2, 0)));
        Assert.Equal(single * 2d, workshop.UnitRate(2), Tolerance);
        Assert.True(workshop.BuyUpgrade(KupoUpgrades.ForBuilding(2, 1)));
        Assert.Equal(single * 4d, workshop.UnitRate(2), Tolerance);
        Assert.Equal(2, workshop.UpgradeCount);
        Assert.Equal(0d, workshop.Kupo, 1e-6);
    }

    [Fact]
    public void NextUpgradesListsTheCheapestUnlockedOnesFirst()
    {
        var workshop = Workshop(Epoch, 30, 30, 30, 30);
        var save = Saved(workshop);
        save.LedgerKupo = 2_000d;
        save.Upgrades = unchecked((long)KupoUpgrades.Bit(KupoUpgrades.ForBuilding(0, 0)));
        workshop.Load(save);
        Span<int> picks = stackalloc int[6];
        var count = workshop.NextUpgrades(picks);

        Assert.Equal(6, count);
        for (var index = 0; index < count; index++)
        {
            Assert.False(workshop.HasUpgrade(picks[index]));
            Assert.True(workshop.IsUnlocked(picks[index]));
            if (index > 0)
            {
                Assert.True(KupoUpgrades.Cost(picks[index]) >= KupoUpgrades.Cost(picks[index - 1]));
            }
        }

        var cheapest = double.MaxValue;
        for (var upgrade = 0; upgrade < KupoUpgrades.Count; upgrade++)
        {
            if (!workshop.HasUpgrade(upgrade) && workshop.IsUnlocked(upgrade))
            {
                cheapest = Math.Min(cheapest, KupoUpgrades.Cost(upgrade));
            }
        }

        Assert.Equal(cheapest, KupoUpgrades.Cost(picks[0]));
        Assert.Equal(0, Workshop(Epoch).NextUpgrades(picks));
    }

    [Fact]
    public void StampsFollowTheCubeRootOfLifetimeKupo()
    {
        Assert.Equal(0d, KupoLedger.StampsFor(0d));
        Assert.Equal(0d, KupoLedger.StampsFor(double.NaN));
        Assert.Equal(0d, KupoLedger.StampsFor(KupoLedger.KupoPerStampCube - 1d));
        Assert.Equal(1d, KupoLedger.StampsFor(KupoLedger.KupoPerStampCube));
        Assert.Equal(2d, KupoLedger.StampsFor(8e9));
        Assert.Equal(3d, KupoLedger.StampsFor(2.7e10));
        Assert.Equal(10d, KupoLedger.StampsFor(1e12));
        Assert.Equal(8e9, KupoLedger.KupoForStamps(2d), Tolerance);
        Assert.Equal(1.04d, KupoLedger.Multiplier(2d), Tolerance);
        Assert.Equal(KupoLedger.LevelCap, KupoLedger.Level(1e9));
        Assert.Equal(4, KupoLedger.BonusPercent(2d));
    }

    [Fact]
    public void ClosingTheLedgerResetsTheWorkshopButKeepsStampsAndTheirMultiplier()
    {
        var workshop = Workshop(Epoch, 3, 2);
        var save = Saved(workshop);
        save.Kupo = 5e8;
        save.LedgerKupo = 8e9;
        save.LifetimeKupo = 8e9;
        save.Upgrades = unchecked((long)KupoUpgrades.Bit(KupoUpgrades.ForBuilding(0, 0)));
        save.Taps = 42;
        workshop.Load(save);
        workshop.Reward(KupoReward.Frenzy, Epoch);

        Assert.Equal(2d, workshop.PendingStamps);
        Assert.Equal(2d, workshop.CloseLedger());
        Assert.Equal(2d, workshop.Stamps);
        Assert.Equal(2, workshop.LedgerLevel);
        Assert.Equal(1, workshop.LedgerPages);
        Assert.Equal(0d, workshop.Kupo);
        Assert.Equal(0d, workshop.LedgerKupo);
        Assert.Equal(8e9, workshop.LifetimeKupo);
        Assert.Equal(0, workshop.BuildingCount);
        Assert.Equal(0UL, workshop.UpgradeMask);
        Assert.Equal(KupoReward.None, workshop.Buff);
        Assert.Equal(42, workshop.Taps);
        Assert.Equal(0d, workshop.PendingStamps);
        Assert.Equal(0d, workshop.CloseLedger());
        Assert.Equal(1.04d, workshop.Multiplier, Tolerance);
        Assert.Equal(1.04d, workshop.TapValue(Epoch), Tolerance);

        var relaunched = Saved(workshop);
        relaunched.Owned[0] = 1;
        workshop.Load(relaunched);
        Assert.Equal(0.1d * 1.04d, workshop.BaseKupoPerSecond, Tolerance);
        Assert.Equal(KupoLedger.KupoForStamps(3d), workshop.NextStampKupo, Tolerance);
    }

    [Fact]
    public void TheFormatterStaysCompactAtEveryBoundary()
    {
        Assert.Equal("0", KupoFormat.Amount(0d));
        Assert.Equal("0", KupoFormat.Amount(-5d));
        Assert.Equal("0", KupoFormat.Amount(double.NaN));
        Assert.Equal("0", KupoFormat.Amount(0.9d));
        Assert.Equal("1", KupoFormat.Amount(1d));
        Assert.Equal("999", KupoFormat.Amount(999d));
        Assert.Equal("999", KupoFormat.Amount(999.99d));
        Assert.Equal("1K", KupoFormat.Amount(1_000d));
        Assert.Equal("1.05K", KupoFormat.Amount(1_050d));
        Assert.Equal("1.23K", KupoFormat.Amount(1_234d));
        Assert.Equal("1.5K", KupoFormat.Amount(1_500d));
        Assert.Equal("12.3K", KupoFormat.Amount(12_345d));
        Assert.Equal("120K", KupoFormat.Amount(120_000d));
        Assert.Equal("999K", KupoFormat.Amount(999_999d));
        Assert.Equal("1M", KupoFormat.Amount(1e6));
        Assert.Equal("1B", KupoFormat.Amount(1e9));
        Assert.Equal("1T", KupoFormat.Amount(1e12));
        Assert.Equal("999T", KupoFormat.Amount(999.999e12));
        Assert.Equal("1aa", KupoFormat.Amount(1e15));
        Assert.Equal("1ab", KupoFormat.Amount(1e18));
        Assert.Equal("4.56az", KupoFormat.Amount(4.567e90));
        Assert.Equal("1ba", KupoFormat.Amount(1e93));
        Assert.Equal("1dr", KupoFormat.Amount(1e300));
        Assert.Equal("999dr", KupoFormat.Amount(double.PositiveInfinity));
        Assert.Equal("999dr", KupoFormat.Amount(double.MaxValue));
        Assert.Equal("0", KupoFormat.Rate(0d));
        Assert.Equal("0.1", KupoFormat.Rate(0.1d));
        Assert.Equal("2.5", KupoFormat.Rate(2.5d));
        Assert.Equal("3", KupoFormat.Rate(3d));
        Assert.Equal("99.9", KupoFormat.Rate(99.95d));
        Assert.Equal("150", KupoFormat.Rate(150d));
        Assert.Equal("1.5K", KupoFormat.Rate(1_500d));
        Assert.Same(KupoFormat.Amount(1_234d), KupoFormat.Amount(1_234.5d));
        Assert.Same(KupoFormat.Amount(42d), KupoFormat.Amount(42.7d));
    }

    [Fact]
    public void TheSaveRoundTripsThroughConfiguration()
    {
        var workshop = Workshop(Epoch, 7, 3, 0, 0, 1, 0, 0, 0, 0, 0, 0, 2);
        var save = Saved(workshop);
        save.Kupo = 123_456.5d;
        save.LedgerKupo = 9e9;
        save.LifetimeKupo = 3e10;
        save.TapKupo = 77d;
        save.Taps = 12;
        save.Stamps = 2d;
        save.LedgerPages = 1;
        save.Upgrades = unchecked((long)(KupoUpgrades.Bit(KupoUpgrades.Count - 1) | KupoUpgrades.Bit(0)));
        workshop.Load(save);
        workshop.Advance(Epoch + 2L * 3600L * Second);
        workshop.Reward(KupoReward.Frenzy, Epoch + 2L * 3600L * Second);

        var configuration = new Configuration();
        workshop.Store(configuration.MoogleClicker);
        var json = JsonConvert.SerializeObject(configuration);
        var restored = JsonConvert.DeserializeObject<Configuration>(json)!;
        var loaded = new KupoWorkshop();
        loaded.Load(restored.MoogleClicker);

        Assert.Equal(workshop.Kupo, loaded.Kupo);
        Assert.Equal(workshop.LedgerKupo, loaded.LedgerKupo);
        Assert.Equal(workshop.LifetimeKupo, loaded.LifetimeKupo);
        Assert.Equal(workshop.TapKupo, loaded.TapKupo);
        Assert.Equal(workshop.Taps, loaded.Taps);
        Assert.Equal(workshop.Stamps, loaded.Stamps);
        Assert.Equal(workshop.LedgerPages, loaded.LedgerPages);
        Assert.Equal(workshop.MinionsCaught, loaded.MinionsCaught);
        Assert.Equal(workshop.UpgradeMask, loaded.UpgradeMask);
        Assert.True(loaded.HasUpgrade(KupoUpgrades.Count - 1));
        Assert.Equal(workshop.Buff, loaded.Buff);
        Assert.Equal(workshop.BuffEndsUnixMilliseconds, loaded.BuffEndsUnixMilliseconds);
        Assert.Equal(workshop.LastTickUnixMilliseconds, loaded.LastTickUnixMilliseconds);
        Assert.Equal(workshop.AwaySeconds, loaded.AwaySeconds);
        Assert.Equal(workshop.AwayKupo, loaded.AwayKupo);
        Assert.True(loaded.HasAwayReport);
        for (var building = 0; building < KupoBuildings.Count; building++)
        {
            Assert.Equal(workshop.Owned(building), loaded.Owned(building));
        }
    }

    [Fact]
    public void ADamagedSaveLoadsSafely()
    {
        var save = new MoogleClickerSave
        {
            Kupo = double.NaN,
            LedgerKupo = double.PositiveInfinity,
            LifetimeKupo = -4d,
            Owned = new[] { 1, KupoBuildings.MaxOwned + 9, -5 },
            Buff = (KupoReward)200,
            BuffEndsUnixMilliseconds = Epoch,
            Stamps = 2.7d,
        };
        var workshop = new KupoWorkshop();
        workshop.Load(save);

        Assert.Equal(0d, workshop.Kupo);
        Assert.Equal(0d, workshop.LedgerKupo);
        Assert.Equal(0d, workshop.LifetimeKupo);
        Assert.Equal(1, workshop.Owned(0));
        Assert.Equal(KupoBuildings.MaxOwned, workshop.Owned(1));
        Assert.Equal(0, workshop.Owned(2));
        Assert.Equal(0, workshop.Owned(KupoBuildings.Count - 1));
        Assert.Equal(KupoReward.None, workshop.Buff);
        Assert.Equal(2d, workshop.Stamps);
        Assert.Equal(default, workshop.Advance(Epoch));
        Assert.Equal(Epoch, workshop.LastTickUnixMilliseconds);
    }

    private static string Replay(ulong seed)
    {
        var board = new MoogleClickerBoard();
        board.Reset(GameRandom.FromSeed(seed));
        var trace = new StringBuilder();
        var elapsed = 0f;
        var catches = 0;
        for (var frame = 0; frame < 4000; frame++)
        {
            board.Update(Step);
            elapsed += Step;
            if (frame % 3 == 0 && frame % 400 < 200)
            {
                var outcome = board.Tap();
                trace.Append(outcome.Multiplier).Append(outcome.Critical ? '!' : '.');
            }

            if (!board.MinionActive || board.MinionProgress < 0.3f)
            {
                continue;
            }

            var point = board.MinionPoint;
            trace.Append(" catch ").Append(board.Catch()).Append('@')
                .Append(point.X.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(point.Y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append(' ');
            catches++;
        }

        trace.Append(catches).Append(' ').Append(board.Criticals).Append(' ').Append(board.BestCombo);
        return trace.ToString();
    }

    private static MoogleClickerBoard ActiveMinion(ulong seed)
    {
        var board = new MoogleClickerBoard();
        board.Reset(GameRandom.FromSeed(seed));
        while (!board.MinionActive)
        {
            board.Update(Step);
        }

        return board;
    }

    private static KupoWorkshop Workshop(long lastTick, params int[] owned)
    {
        var save = new MoogleClickerSave { LastTickUnixMilliseconds = lastTick };
        Array.Copy(owned, save.Owned, Math.Min(owned.Length, save.Owned.Length));
        var workshop = new KupoWorkshop();
        workshop.Load(save);
        return workshop;
    }

    private static MoogleClickerSave Saved(KupoWorkshop workshop)
    {
        var save = new MoogleClickerSave();
        workshop.Store(save);
        return save;
    }
}
