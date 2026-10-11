using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class OriginalsVectorTests
{
    private const string PinnedHash = "1c3c0519b76e15d19e60368124a81fb5f2698fd48689bab8f2ed0ea570c4eab9";

    [Fact]
    public void TheVectorFileIsTheBackendCopy()
    {
        var text = File.ReadAllText(VectorPath()).Replace("\r", string.Empty, StringComparison.Ordinal);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        Assert.Equal(PinnedHash, hash);
    }

    [Fact]
    public void EveryDrawLogVerifiesOnTheFloatStream()
    {
        using var vectors = Load();
        var checkedCases = 0;
        foreach (var (kind, section) in Sections())
        {
            foreach (var entry in vectors.RootElement.GetProperty(section).EnumerateArray())
            {
                var seedHex = entry.GetProperty("seed").GetString()!;
                var roundId = entry.GetProperty("roundId").GetString()!;
                var drawLog = entry.GetProperty("drawLog").GetString()!;
                var seed = Convert.FromHexString(seedHex);
                var commit = Convert.ToHexStringLower(SHA256.HashData(seed));
                Assert.Equal(CasinoRoundVerdict.Match, CasinoVerifier.Verify(kind, seedHex, commit, roundId, drawLog));
                Assert.Equal(CasinoRoundVerdict.Mismatch,
                    CasinoVerifier.Verify(kind, seedHex, commit, roundId + "x", drawLog));
                checkedCases++;
            }
        }

        Assert.Equal(23, checkedCases);
    }

    [Fact]
    public void MinesCasesReplayTheirMinesAndPayouts()
    {
        using var vectors = Load();
        foreach (var entry in vectors.RootElement.GetProperty("mines").EnumerateArray())
        {
            var stream = StreamFor(entry);
            var mines = entry.GetProperty("mines").GetInt32();
            var mineTiles = Ints(entry.GetProperty("mineTiles"));
            Assert.Equal(mineTiles, DrawDistinct(stream, OriginalsRules.MinesTiles, mines));
            var picks = Ints(entry.GetProperty("picks"));
            var stake = entry.GetProperty("stake").GetInt64();
            var safe = 0;
            var busted = false;
            for (var index = 0; index < picks.Length; index++)
            {
                if (Array.IndexOf(mineTiles, picks[index]) >= 0)
                {
                    busted = true;
                    break;
                }

                safe++;
            }

            var payout = busted ? 0 : OriginalsRules.MinesPayout(stake, mines, safe);
            Assert.Equal(entry.GetProperty("payout").GetInt64(), payout);
        }
    }

    [Fact]
    public void DiceCasesReplayTheirRollsAndPayouts()
    {
        using var vectors = Load();
        foreach (var entry in vectors.RootElement.GetProperty("dice").EnumerateArray())
        {
            var stream = StreamFor(entry);
            var roll = (int)stream.NextFloatBelow(OriginalsRules.DiceRollBound);
            var target = entry.GetProperty("target").GetInt32();
            var over = entry.GetProperty("over").GetBoolean();
            var stake = entry.GetProperty("stake").GetInt64();
            var won = OriginalsRules.DiceWins(roll, target, over);
            var chance = OriginalsRules.DiceChance(target, over);
            Assert.Equal(entry.GetProperty("roll").GetInt32(), roll);
            Assert.Equal(entry.GetProperty("won").GetBoolean(), won);
            Assert.Equal(entry.GetProperty("payout").GetInt64(), won ? OriginalsRules.DicePayout(stake, chance) : 0);
        }
    }

    [Fact]
    public void LimboCasesReplayTheirResultsAndPayouts()
    {
        using var vectors = Load();
        foreach (var entry in vectors.RootElement.GetProperty("limbo").EnumerateArray())
        {
            var stream = StreamFor(entry);
            var result = OriginalsRules.LimboResult(stream.NextFloatBelow(OriginalsRules.LimboBound));
            var target = entry.GetProperty("target").GetInt32();
            var stake = entry.GetProperty("stake").GetInt64();
            var won = OriginalsRules.LimboWins(result, target);
            Assert.Equal(entry.GetProperty("result").GetInt32(), result);
            Assert.Equal(entry.GetProperty("won").GetBoolean(), won);
            Assert.Equal(entry.GetProperty("payout").GetInt64(), won ? OriginalsRules.LimboPayout(stake, target) : 0);
        }
    }

    [Fact]
    public void KenoCasesReplayTheirDrawsHitsAndPayouts()
    {
        using var vectors = Load();
        foreach (var entry in vectors.RootElement.GetProperty("keno").EnumerateArray())
        {
            var stream = StreamFor(entry);
            var drawn = DrawDistinct(stream, OriginalsRules.KenoTiles, OriginalsRules.KenoDraws);
            var picks = Ints(entry.GetProperty("picks"));
            var risk = entry.GetProperty("risk").GetInt32();
            var stake = entry.GetProperty("stake").GetInt64();
            var hits = OriginalsRules.KenoHits(picks, drawn);
            Assert.Equal(Ints(entry.GetProperty("drawn")), drawn);
            Assert.Equal(entry.GetProperty("hits").GetInt32(), hits);
            Assert.Equal(entry.GetProperty("payout").GetInt64(),
                OriginalsRules.KenoPayout(stake, risk, picks.Length, hits));
        }
    }

    [Fact]
    public void HiLoCasesReplayTheirCardsChainsAndPayouts()
    {
        using var vectors = Load();
        foreach (var entry in vectors.RootElement.GetProperty("hiLo").EnumerateArray())
        {
            var stream = StreamFor(entry);
            var cards = new int[OriginalsRules.HiLoDeck];
            for (var index = 0; index < cards.Length; index++)
            {
                cards[index] = (int)stream.NextFloatBelow(OriginalsRules.HiLoDeck);
            }

            Assert.Equal(Ints(entry.GetProperty("cards")), cards);
            var moves = entry.GetProperty("moves").EnumerateArray().Select(move => move.GetString()!).ToArray();
            var numerator = BigInteger.One;
            var denominator = BigInteger.One;
            var busted = false;
            var won = false;
            for (var index = 0; index < moves.Length && !busted; index++)
            {
                Assert.True(OriginalsRules.TryParseCall(moves[index], out var call));
                if (call == HiLoCall.Skip)
                {
                    continue;
                }

                var shown = OriginalsRules.HiLoRank(cards[index]);
                Assert.True(OriginalsRules.IsLegalCall(call, shown));
                if (!OriginalsRules.CallWins(call, shown, OriginalsRules.HiLoRank(cards[index + 1])))
                {
                    busted = true;
                    continue;
                }

                won = true;
                numerator *= 1287;
                denominator *= 100 * OriginalsRules.WinningRanks(call, shown);
            }

            var stake = entry.GetProperty("stake").GetInt64();
            Assert.Equal(entry.GetProperty("busted").GetBoolean(), busted);
            Assert.Equal(entry.GetProperty("step").GetInt32(), moves.Length);
            Assert.Equal(entry.GetProperty("multiplierTenThousandths").GetInt64(),
                (long)(numerator * 10000 / denominator));
            var payout = busted || !won ? 0 : (long)(numerator * stake / denominator);
            Assert.Equal(entry.GetProperty("payout").GetInt64(), payout);
        }
    }

    private static (string Kind, string Section)[] Sections() =>
    [
        (CasinoWire.MinesKind, "mines"),
        (CasinoWire.DiceKind, "dice"),
        (CasinoWire.LimboKind, "limbo"),
        (CasinoWire.KenoKind, "keno"),
        (CasinoWire.HiLoKind, "hiLo"),
    ];

    private static CasinoVerifier.DrawStream StreamFor(JsonElement entry) =>
        new(Convert.FromHexString(entry.GetProperty("seed").GetString()!), entry.GetProperty("roundId").GetString()!);

    private static int[] DrawDistinct(CasinoVerifier.DrawStream stream, int population, int count)
    {
        var remaining = Enumerable.Range(0, population).ToList();
        var result = new int[count];
        for (var index = 0; index < count; index++)
        {
            var picked = (int)stream.NextFloatBelow((uint)(population - index));
            result[index] = remaining[picked];
            remaining.RemoveAt(picked);
        }

        return result;
    }

    private static int[] Ints(JsonElement array) => array.EnumerateArray().Select(item => item.GetInt32()).ToArray();

    private static JsonDocument Load() => JsonDocument.Parse(File.ReadAllText(VectorPath()));

    private static string VectorPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Aetherphone.Tests", "Vectors", "originals.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate src/Aetherphone.Tests/Vectors/originals.json.");
    }
}
