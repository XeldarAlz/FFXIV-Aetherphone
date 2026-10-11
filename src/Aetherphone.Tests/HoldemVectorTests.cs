using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HoldemVectorTests
{
    private const string ExpectedHash = "9552d0642ed01bc4d146228c93ad0ef20973467c82087c31c22b6accc413f9f4";

    [Fact]
    public void TheHoldemVectorsAreTheBackendFileByteForByte()
    {
        var text = File.ReadAllText(VectorPath()).Replace("\r", string.Empty, StringComparison.Ordinal);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

        Assert.Equal(ExpectedHash, hash);
    }

    [Fact]
    public void TheEvaluatorMatchesEveryVector()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(VectorPath()));
        var vectors = document.RootElement.GetProperty("evaluator");
        Assert.True(vectors.GetArrayLength() > 0);
        foreach (var vector in vectors.EnumerateArray())
        {
            var cards = Ints(vector.GetProperty("cards"));
            var strength = vector.GetProperty("strength").GetInt32();
            Assert.Equal(strength, HoldemHands.Evaluate(cards));
            Assert.Equal(vector.GetProperty("category").GetInt32(), HoldemHands.CategoryOf(strength));
            var best = new int[HoldemHands.HandSize];
            Assert.Equal(strength, HoldemHands.Best5(cards, best));
            Assert.Equal(Ints(vector.GetProperty("best5")), best);
            if (cards.Length == HoldemHands.HandSize)
            {
                Assert.Equal(strength, HoldemHands.Evaluate5(cards[0], cards[1], cards[2], cards[3], cards[4]));
            }
        }
    }

    [Fact]
    public void TheDeckVectorsShuffleAndVerifyLikeTheServer()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(VectorPath()));
        foreach (var vector in document.RootElement.GetProperty("deck").EnumerateArray())
        {
            var seed = Convert.FromHexString(vector.GetProperty("seed").GetString()!);
            var binding = string.Concat(vector.GetProperty("roomId").GetString(), "#",
                vector.GetProperty("handIndex").GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture));
            var stream = new CasinoVerifier.DrawStream(seed, binding);
            var deck = new int[52];
            for (var index = 0; index < deck.Length; index++)
            {
                deck[index] = index;
            }

            for (var index = 51; index >= 1; index--)
            {
                var swap = (int)stream.NextBelow((uint)(index + 1));
                (deck[index], deck[swap]) = (deck[swap], deck[index]);
            }

            Assert.Equal(Ints(vector.GetProperty("deck")), deck);
            Assert.True(CasinoVerifier.ReplaysDrawLog(HoldemRules.Kind, seed, binding,
                vector.GetProperty("drawLog").GetString()!));
        }
    }

    [Fact]
    public void TheRakeVectorsMatchTheMirror()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(VectorPath()));
        foreach (var vector in document.RootElement.GetProperty("rake").EnumerateArray())
        {
            Assert.Equal(vector.GetProperty("rake").GetInt64(), HoldemRules.Rake(vector.GetProperty("pot").GetInt64(),
                vector.GetProperty("bigBlind").GetInt64(), vector.GetProperty("sawFlop").GetBoolean()));
        }
    }

    [Fact]
    public void ShowdownStrengthsInTheHandVectorsMatchTheEvaluator()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(VectorPath()));
        var checkedAny = false;
        foreach (var hand in document.RootElement.GetProperty("hands").EnumerateArray())
        {
            var board = Ints(hand.GetProperty("board"));
            var holes = hand.GetProperty("holeCards");
            var payouts = hand.GetProperty("payouts");
            var seat = 0;
            foreach (var payout in payouts.EnumerateArray())
            {
                var strength = payout.GetProperty("strength").GetInt32();
                var hole = Ints(holes[seat]);
                seat++;
                if (strength < 0 || board.Length < HoldemRules.BoardSize)
                {
                    continue;
                }

                var cards = new int[hole.Length + board.Length];
                hole.CopyTo(cards, 0);
                board.CopyTo(cards, hole.Length);
                Assert.Equal(strength, HoldemHands.Evaluate(cards));
                Assert.False(string.IsNullOrEmpty(HoldemHandNames.Build(strength)));
                checkedAny = true;
            }
        }

        Assert.True(checkedAny);
    }

    [Fact]
    public void AWrongHoldemDrawLogFails()
    {
        var seed = new byte[32];
        Assert.False(CasinoVerifier.ReplaysDrawLog(HoldemRules.Kind, seed, "holdem-low#0", "shuffle:52"));
    }

    private static int[] Ints(JsonElement array)
    {
        var values = new int[array.GetArrayLength()];
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            values[index++] = item.GetInt32();
        }

        return values;
    }

    private static string VectorPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Vectors", "holdem.json");
    }
}
