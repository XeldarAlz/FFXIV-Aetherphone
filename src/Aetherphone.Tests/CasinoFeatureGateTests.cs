using Aetherphone.Apps.Casino;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoFeatureGateTests
{
    private static readonly string[] LegacyGames =
    {
        CasinoGames.Blackjack, CasinoGames.Wheel, CasinoGames.Bingo, CasinoGames.Scratch, CasinoGames.Barkeep,
        CasinoGames.DailySpin,
    };

    private static readonly string[] NewGames =
    {
        CasinoGames.Holdem, CasinoGames.SlotsBird, CasinoGames.SlotsCascade, CasinoGames.SlotsMoogle,
        CasinoGames.Plinko, CasinoGames.Mines, CasinoGames.Dice, CasinoGames.Limbo, CasinoGames.Keno,
        CasinoGames.HiLo, CasinoGames.Race, CasinoGames.DiceTable, CasinoGames.Deathroll, CasinoGames.Raffle,
    };

    [Fact]
    public void AnOldServerKeepsTheLegacyFloorOpenAndClosesEverythingNew()
    {
        var old = CasinoFeatureSet.From(null);
        Assert.True(old.IsEmpty);
        for (var index = 0; index < LegacyGames.Length; index++)
        {
            Assert.True(CasinoGameGate.IsOpen(old, LegacyGames[index]), LegacyGames[index]);
        }

        for (var index = 0; index < NewGames.Length; index++)
        {
            Assert.False(CasinoGameGate.IsOpen(old, NewGames[index]), NewGames[index]);
        }
    }

    [Fact]
    public void EachFlagOpensItsOwnGamesOnly()
    {
        var machines = CasinoFeatureSet.From(new[] { CasinoFeatures.Machines });
        Assert.True(CasinoGameGate.IsOpen(machines, CasinoGames.SlotsMoogle));
        Assert.False(CasinoGameGate.IsOpen(machines, CasinoGames.Plinko));
        var originals = CasinoFeatureSet.From(new[] { CasinoFeatures.Originals, CasinoFeatures.Plinko });
        Assert.True(CasinoGameGate.IsOpen(originals, CasinoGames.Keno));
        Assert.True(CasinoGameGate.IsOpen(originals, CasinoGames.Plinko));
        Assert.False(CasinoGameGate.IsOpen(originals, CasinoGames.Race));
    }

    [Fact]
    public void RoomsGateOnTheirKind()
    {
        var none = CasinoFeatureSet.Empty;
        Assert.True(CasinoGameGate.RoomOpen(none, CasinoWire.BlackjackKind));
        Assert.False(CasinoGameGate.RoomOpen(none, HoldemRules.Kind));
        Assert.False(CasinoGameGate.RoomOpen(none, CasinoWire.RaceKind));
        Assert.False(CasinoGameGate.RoomOpen(none, VenueKinds.Raffle));
        var all = CasinoFeatureSet.From(new[] { CasinoFeatures.Holdem, CasinoFeatures.Race, CasinoFeatures.Venue });
        Assert.True(CasinoGameGate.RoomOpen(all, HoldemRules.Kind));
        Assert.True(CasinoGameGate.RoomOpen(all, CasinoWire.RaceKind));
        Assert.True(CasinoGameGate.RoomOpen(all, VenueKinds.Deathroll));
    }

    [Fact]
    public void TheFeatureSetReadsKnownAndUnknownFlags()
    {
        var set = CasinoFeatureSet.From(new[] { "economy.v3", "missions", "wheel.v2" });
        Assert.True(set.Has(CasinoFeatures.EconomyV3));
        Assert.True(set.Has(CasinoFeatures.Missions));
        Assert.True(set.Has("wheel.v2"));
        Assert.False(set.Has(CasinoFeatures.Fame));
        Assert.False(set.Has("bingo.v2"));
        Assert.False(set.IsEmpty);
    }

    [Fact]
    public void EveryKnownFlagFitsTheMask()
    {
        Assert.True(CasinoFeatureSet.Known.Length <= 64);
        var all = CasinoFeatureSet.From(CasinoFeatureSet.Known);
        for (var index = 0; index < CasinoFeatureSet.Known.Length; index++)
        {
            Assert.True(all.Has(CasinoFeatureSet.Known[index]));
        }
    }
}
