using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DealerHoldemVectorTests
{
    private const string ExpectedHash = "0147655805c92995a52a12f6ced466d22dff3016b7277a569d6af489b9b94b09";
    private const int DealtCards = 9;

    [Fact]
    public void TheVectorsAreTheBackendFileByteForByte()
    {
        var text = File.ReadAllText(VectorPath()).Replace("\r", string.Empty, StringComparison.Ordinal);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        Assert.Equal(ExpectedHash, hash);
    }

    [Fact]
    public void EveryHandDealsFromTheHoldemShuffleAndVerifies()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(VectorPath()));
        var hands = document.RootElement.GetProperty("hands");
        Assert.True(hands.GetArrayLength() > 0);
        foreach (var hand in hands.EnumerateArray())
        {
            var seedHex = hand.GetProperty("seed").GetString()!;
            var seed = Convert.FromHexString(seedHex);
            var roundId = hand.GetProperty("roundId").GetString()!;
            var stream = new CasinoVerifier.DrawStream(seed, roundId);
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

            Assert.Equal(Ints(hand.GetProperty("dealt")), deck[..DealtCards]);
            var commit = Convert.ToHexStringLower(SHA256.HashData(seed));
            Assert.Equal(CasinoRoundVerdict.Match, CasinoVerifier.Verify(DealerHoldemRules.Kind, seedHex, commit,
                roundId, hand.GetProperty("drawLog").GetString()!));
        }
    }

    [Fact]
    public void TheStrengthsAndEveryPayoutMatchTheRulesMirror()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(VectorPath()));
        var sawNoQualify = false;
        var sawFold = false;
        foreach (var hand in document.RootElement.GetProperty("hands").EnumerateArray())
        {
            var dealt = Ints(hand.GetProperty("dealt"));
            int[] player = { dealt[0], dealt[1], dealt[4], dealt[5], dealt[6], dealt[7], dealt[8] };
            int[] dealer = { dealt[2], dealt[3], dealt[4], dealt[5], dealt[6], dealt[7], dealt[8] };
            var playerStrength = HoldemHands.Evaluate(player);
            var dealerStrength = HoldemHands.Evaluate(dealer);
            Assert.Equal(hand.GetProperty("playerStrength").GetInt32(), playerStrength);
            Assert.Equal(hand.GetProperty("dealerStrength").GetInt32(), dealerStrength);
            var qualifies = DealerHoldemRules.Qualifies(dealerStrength);
            Assert.Equal(hand.GetProperty("dealerQualifies").GetBoolean(), qualifies);

            var ante = hand.GetProperty("ante").GetInt64();
            var trips = hand.GetProperty("trips").GetInt64();
            var multiple = hand.GetProperty("playMultiple").GetInt32();
            var moves = hand.GetProperty("moves");
            var folded = string.Equals(moves[moves.GetArrayLength() - 1].GetString(), "f", StringComparison.Ordinal);
            var compare = Math.Sign(playerStrength - dealerStrength);
            var play = ante * multiple;
            var category = HoldemHands.DisplayCategoryOf(playerStrength);
            long antePayout;
            long blindPayout;
            long playPayout;
            int outcome;
            if (folded)
            {
                antePayout = 0;
                blindPayout = 0;
                playPayout = 0;
                outcome = DealerHoldemRules.OutcomeFolded;
                sawFold = true;
            }
            else
            {
                playPayout = compare > 0 ? play * 2 : compare == 0 ? play : 0;
                antePayout = !qualifies ? ante : compare > 0 ? ante * 2 : compare == 0 ? ante : 0;
                blindPayout = compare > 0 ? DealerHoldemRules.BlindReturn(ante, category) : compare == 0 ? ante : 0;
                outcome = compare > 0 ? DealerHoldemRules.OutcomeWin
                    : compare == 0 ? DealerHoldemRules.OutcomeTie : DealerHoldemRules.OutcomeDealer;
                sawNoQualify |= !qualifies;
            }

            var tripsPayout = DealerHoldemRules.TripsReturn(trips, category);
            Assert.Equal(hand.GetProperty("outcome").GetInt32(), outcome);
            Assert.Equal(hand.GetProperty("antePayout").GetInt64(), antePayout);
            Assert.Equal(hand.GetProperty("blindPayout").GetInt64(), blindPayout);
            Assert.Equal(hand.GetProperty("playPayout").GetInt64(), playPayout);
            Assert.Equal(hand.GetProperty("tripsPayout").GetInt64(), tripsPayout);
            Assert.Equal(hand.GetProperty("payout").GetInt64(), antePayout + blindPayout + playPayout + tripsPayout);
            Assert.Equal(hand.GetProperty("stake").GetInt64(), DealerHoldemRules.StakeAtDeal(ante, trips) + play);
        }

        Assert.True(sawNoQualify);
        Assert.True(sawFold);
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

    private static string VectorPath() => Path.Combine(AppContext.BaseDirectory, "Vectors", "dealer-holdem.json");
}
