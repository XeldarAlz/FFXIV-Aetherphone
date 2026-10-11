using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BlackjackSideBetVectorTests
{
    private const string ExpectedHash = "62586b19ff1ce9d236396bc1e52b8ab8b1581317a65870008d6305ee36ae4291";

    [Fact]
    public void TheSideBetVectorsAreTheBackendFileByteForByte()
    {
        var text = File.ReadAllText(VectorPath()).Replace("\r", string.Empty, StringComparison.Ordinal);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

        Assert.Equal(ExpectedHash, hash);
    }

    [Fact]
    public void ThePayTablesAndReturnsMatchTheVectorFile()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(VectorPath()));
        var root = document.RootElement;

        Assert.Equal(BlackjackSideBets.PerfectPairsPays, Ints(root.GetProperty("perfectPairsPays")));
        Assert.Equal(BlackjackSideBets.TwentyOnePlusThreePays, Ints(root.GetProperty("twentyOnePlusThreePays")));
        var pairs = root.GetProperty("perfectPairsReturn");
        Assert.Equal(BlackjackSideBets.PerfectPairsReturnNumerator, pairs.GetProperty("numerator").GetInt64());
        Assert.Equal(BlackjackSideBets.PerfectPairsReturnDenominator, pairs.GetProperty("denominator").GetInt64());
        Assert.Equal(BlackjackSideBets.PerfectPairsReturnBasisPoints, pairs.GetProperty("basisPoints").GetInt32());
        var three = root.GetProperty("twentyOnePlusThreeReturn");
        Assert.Equal(BlackjackSideBets.TwentyOnePlusThreeReturnNumerator, three.GetProperty("numerator").GetInt64());
        Assert.Equal(BlackjackSideBets.TwentyOnePlusThreeReturnDenominator,
            three.GetProperty("denominator").GetInt64());
        Assert.Equal(BlackjackSideBets.TwentyOnePlusThreeReturnBasisPoints, three.GetProperty("basisPoints").GetInt32());
    }

    [Fact]
    public void EveryCaseRanksTheSameOnTheClient()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(VectorPath()));
        foreach (var entry in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var hero = Ints(entry.GetProperty("hero"));
            var upCard = entry.GetProperty("upCard").GetInt32();
            Assert.Equal(entry.GetProperty("perfectPairsKind").GetInt32(),
                BlackjackSideBets.PerfectPairsKind(hero[0], hero[1]));
            Assert.Equal(entry.GetProperty("twentyOnePlusThreeKind").GetInt32(),
                BlackjackSideBets.TwentyOnePlusThreeKind(hero[0], hero[1], upCard));
        }
    }

    [Fact]
    public void EverySeededHandDealsTheSameShoeOnTheClient()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(VectorPath()));
        var hands = document.RootElement.GetProperty("hands");
        Assert.True(hands.GetArrayLength() > 0);
        foreach (var entry in hands.EnumerateArray())
        {
            var shoe = Shoe(entry.GetProperty("seed").GetString()!, entry.GetProperty("binding").GetString()!);
            var hero = Ints(entry.GetProperty("hero"));
            Assert.Equal(hero[0], shoe[0] % PlayingCards.DeckSize);
            Assert.Equal(entry.GetProperty("upCard").GetInt32(), shoe[1] % PlayingCards.DeckSize);
            Assert.Equal(hero[1], shoe[2] % PlayingCards.DeckSize);
            Assert.Equal(entry.GetProperty("perfectPairsKind").GetInt32(),
                BlackjackSideBets.PerfectPairsKind(shoe[0], shoe[2]));
            Assert.Equal(entry.GetProperty("twentyOnePlusThreeKind").GetInt32(),
                BlackjackSideBets.TwentyOnePlusThreeKind(shoe[0], shoe[2], shoe[1]));
        }
    }

    private static int[] Shoe(string seedHex, string binding)
    {
        var stream = new CasinoVerifier.DrawStream(Convert.FromHexString(seedHex), binding);
        var shoe = new int[BlackjackRules.ShoeCards];
        for (var index = 0; index < shoe.Length; index++)
        {
            shoe[index] = index;
        }

        for (var index = shoe.Length - 1; index >= 1; index--)
        {
            var swap = (int)stream.NextBelow((uint)(index + 1));
            (shoe[index], shoe[swap]) = (shoe[swap], shoe[index]);
        }

        return shoe;
    }

    private static int[] Ints(JsonElement array)
    {
        var values = new int[array.GetArrayLength()];
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            values[index] = item.GetInt32();
            index++;
        }

        return values;
    }

    private static string VectorPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Vectors", "blackjack-sidebets.json");
    }
}
