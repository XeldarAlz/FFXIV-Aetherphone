using System.Security.Cryptography;
using System.Text.Json;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class VenueRulesTests
{
    [Fact]
    public void TheRoomKindsAreTheWireStrings()
    {
        Assert.Equal("casino.dice-table", VenueKinds.DiceTable);
        Assert.Equal("casino.deathroll", VenueKinds.Deathroll);
        Assert.Equal("casino.raffle", VenueKinds.Raffle);
        Assert.Equal(VenueRoomKind.Dice, VenueKinds.Of("casino.dice-table"));
        Assert.Equal(VenueRoomKind.None, VenueKinds.Of(CasinoWire.BlackjackKind));
        Assert.Equal(VenueKinds.Raffle, VenueKinds.WireKind(VenueRoomKind.Raffle));
    }

    [Fact]
    public void TheBoundsMirrorTheBackendConstants()
    {
        Assert.Equal(2, VenueRules.MinSides);
        Assert.Equal(1_000_000, VenueRules.MaxSides);
        Assert.Equal(1000, VenueRules.DefaultSides);
        Assert.Equal(15, VenueRules.MinRoundSeconds);
        Assert.Equal(600, VenueRules.MaxRoundSeconds);
        Assert.Equal(60, VenueRules.DefaultRoundSeconds);
        Assert.Equal(2, VenueRules.MinStartAt);
        Assert.Equal(1_000_000, VenueRules.MaxStartAt);
        Assert.Equal(10_000, VenueRules.DefaultStake);
        Assert.Equal(60, VenueRules.DuelTurnSeconds);
        Assert.Equal(120, VenueRules.ChallengeLapseSeconds);
        Assert.Equal(60, VenueRules.MinRaffleSeconds);
        Assert.Equal(86_400, VenueRules.MaxRaffleSeconds);
        Assert.Equal(100, VenueRules.MaxTicketsPerPerson);
        Assert.Equal(10, VenueRules.MaxWinners);
        Assert.Equal(100, VenueRules.MaxEntrants);
        Assert.Equal(48, VenueRules.TitleMaxLength);
        Assert.Equal(50, VenueRules.RollLogLimit);
    }

    [Fact]
    public void VenueRoomsRefuseHouseChips()
    {
        var config = new CasinoTableConfigDto(GameKind: VenueKinds.DiceTable, Currency: CasinoCurrencies.Chips);
        Assert.Equal(CasinoReasons.PracticeOnly, VenueRules.Check(config));
    }

    [Fact]
    public void ADefaultPracticeDiceTablePasses()
    {
        var config = new CasinoTableConfigDto(GameKind: VenueKinds.DiceTable, Seats: 0, Practice: true,
            Currency: CasinoCurrencies.Practice, Dice: new CasinoDiceTableOptionsDto());
        Assert.Equal(string.Empty, VenueRules.Check(config));
    }

    [Fact]
    public void OutOfRangeSidesAndClocksAreInvalid()
    {
        var sides = new CasinoTableConfigDto(GameKind: VenueKinds.DiceTable, Currency: CasinoCurrencies.Practice,
            Dice: new CasinoDiceTableOptionsDto(Sides: 1));
        var clock = new CasinoTableConfigDto(GameKind: VenueKinds.DiceTable, Currency: CasinoCurrencies.Gil,
            Dice: new CasinoDiceTableOptionsDto(RoundSeconds: 5));
        Assert.Equal(CasinoReasons.ConfigInvalid, VenueRules.Check(sides));
        Assert.Equal(CasinoReasons.ConfigInvalid, VenueRules.Check(clock));
    }

    [Fact]
    public void APracticeDeathrollStakeMustFitTheStack()
    {
        var config = new CasinoTableConfigDto(GameKind: VenueKinds.Deathroll, Currency: CasinoCurrencies.Practice,
            PracticeStack: 5_000, Deathroll: new CasinoDeathrollOptionsDto(1000, 10_000));
        Assert.Equal(CasinoReasons.ConfigInvalid, VenueRules.Check(config));
        Assert.Equal(string.Empty,
            VenueRules.Check(config with { Deathroll = new CasinoDeathrollOptionsDto(1000, 5_000) }));
    }

    [Fact]
    public void RaffleSettingsAreBoundedLikeTheServer()
    {
        Assert.True(VenueRules.IsRaffle("Bar tab", 3, 1, 600));
        Assert.False(VenueRules.IsRaffle("  ", 3, 1, 600));
        Assert.False(VenueRules.IsRaffle(new string('a', 49), 3, 1, 600));
        Assert.False(VenueRules.IsRaffle("Bar tab", 101, 1, 600));
        Assert.False(VenueRules.IsRaffle("Bar tab", 3, 11, 600));
        Assert.False(VenueRules.IsRaffle("Bar tab", 3, 1, 59));
    }

    [Fact]
    public void OnlyAOneLosesTheDuel()
    {
        Assert.True(VenueRules.Loses(1));
        Assert.False(VenueRules.Loses(2));
    }
}

public sealed class VenueDrawVectorTests
{
    private static JsonElement Root()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Vectors", "venue.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }

    [Fact]
    public void EveryDiceVectorRollsTheSameValue()
    {
        foreach (var vector in Root().GetProperty("dice").EnumerateArray())
        {
            var seed = Convert.FromHexString(vector.GetProperty("seed").GetString()!);
            var value = VenueDraws.Roll(seed, vector.GetProperty("roomId").GetString()!,
                vector.GetProperty("seq").GetInt64(), vector.GetProperty("sides").GetInt64());
            Assert.Equal(vector.GetProperty("value").GetInt64(), value);
        }
    }

    [Fact]
    public void EveryDeathrollVectorRollsTheSameValue()
    {
        foreach (var vector in Root().GetProperty("deathroll").EnumerateArray())
        {
            var seed = Convert.FromHexString(vector.GetProperty("seed").GetString()!);
            var value = VenueDraws.Deathroll(seed, vector.GetProperty("roomId").GetString()!,
                vector.GetProperty("seq").GetInt64(), vector.GetProperty("bound").GetInt64());
            Assert.Equal(vector.GetProperty("value").GetInt64(), value);
        }
    }

    [Fact]
    public void EveryRaffleVectorDrawsTheSameWinners()
    {
        foreach (var vector in Root().GetProperty("raffle").EnumerateArray())
        {
            var seed = Convert.FromHexString(vector.GetProperty("seed").GetString()!);
            var tickets = new List<int>();
            foreach (var ticket in vector.GetProperty("tickets").EnumerateArray())
            {
                tickets.Add(ticket.GetInt32());
            }

            var expected = new List<int>();
            foreach (var index in vector.GetProperty("drawn").EnumerateArray())
            {
                expected.Add(index.GetInt32());
            }

            var winners = vector.GetProperty("winners").GetInt32();
            var drawn = new int[winners];
            var count = VenueDraws.Raffle(seed, vector.GetProperty("roomId").GetString()!,
                vector.GetProperty("seq").GetInt64(), tickets.ToArray(), winners, drawn);
            Assert.Equal(expected, drawn[..count]);
        }
    }

    [Fact]
    public void TheStreamBindingIsRoomHashSeq()
    {
        Assert.Equal("venue-x#42", VenueDraws.StreamBinding("venue-x", 42));
    }
}

public sealed class VenueVerifierTests
{
    private const string RoomId = "venue-vectors";

    private static (string Seed, long Seq, long Sides, long Value) FirstDice()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Vectors", "venue.json");
        var vector = JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("dice")[4];
        return (vector.GetProperty("seed").GetString()!, vector.GetProperty("seq").GetInt64(),
            vector.GetProperty("sides").GetInt64(), vector.GetProperty("value").GetInt64());
    }

    private static CasinoRoundVerifyDto Proof(string seedHex)
    {
        var commit = Convert.ToHexStringLower(SHA256.HashData(Convert.FromHexString(seedHex)));
        return new CasinoRoundVerifyDto(Granted: true, SeedRevealed: seedHex, SeedCommitHash: commit);
    }

    [Fact]
    public void ARevealedRollMatchesWhatTheLogShowed()
    {
        var (seed, seq, sides, value) = FirstDice();
        Assert.Equal(CasinoRoundVerdict.Match, VenueVerifier.VerifyRoll(Proof(seed), RoomId, seq, sides, value, false));
    }

    [Fact]
    public void AnEditedValueIsAMismatch()
    {
        var (seed, seq, sides, value) = FirstDice();
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            VenueVerifier.VerifyRoll(Proof(seed), RoomId, seq, sides, value == 1 ? 2 : value - 1, false));
    }

    [Fact]
    public void ABrokenCommitIsAMismatchAndAnUnrevealedSeedWaits()
    {
        var (seed, seq, sides, value) = FirstDice();
        var forged = Proof(seed) with { SeedCommitHash = new string('0', 64) };
        Assert.Equal(CasinoRoundVerdict.Mismatch, VenueVerifier.VerifyRoll(forged, RoomId, seq, sides, value, false));
        Assert.Equal(CasinoRoundVerdict.Unrevealed,
            VenueVerifier.VerifyRoll(new CasinoRoundVerifyDto(Granted: true), RoomId, seq, sides, value, false));
    }

    [Fact]
    public void ARaffleVerifiesItsWinnersInOrder()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Vectors", "venue.json");
        var vector = JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("raffle")[3];
        var tickets = new List<int>();
        foreach (var ticket in vector.GetProperty("tickets").EnumerateArray())
        {
            tickets.Add(ticket.GetInt32());
        }

        var entrants = new CasinoRaffleEntrantDto[tickets.Count];
        for (var index = 0; index < entrants.Length; index++)
        {
            entrants[index] = new CasinoRaffleEntrantDto("u" + index, "Player " + index, tickets[index]);
        }

        var winners = new List<CasinoRaffleEntrantDto>();
        foreach (var drawn in vector.GetProperty("drawn").EnumerateArray())
        {
            winners.Add(entrants[drawn.GetInt32()]);
        }

        var raffle = new CasinoRaffleDto(Seq: 1, Winners: vector.GetProperty("winners").GetInt32(),
            Entrants: entrants, Drawn: true, DrawSeq: vector.GetProperty("seq").GetInt64(),
            WinnersDrawn: winners.ToArray());
        var proof = Proof(vector.GetProperty("seed").GetString()!);
        Assert.Equal(CasinoRoundVerdict.Match, VenueVerifier.VerifyRaffle(proof, RoomId, raffle));
        winners.Reverse();
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            VenueVerifier.VerifyRaffle(proof, RoomId, raffle with { WinnersDrawn = winners.ToArray() }));
    }
}
