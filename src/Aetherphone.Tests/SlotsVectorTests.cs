using System.Security.Cryptography;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SlotsVectorTests
{
    [Theory]
    [InlineData("slots-bird.json", "27af33308a5db47615b279986c2e5d5d7dee721382c24d91aa7ddf4847e50de5")]
    [InlineData("slots-cascade.json", "3fc7a407767543e6faa9eb925d11f1d5a8c8866cc06bf3287a13f4437e485e11")]
    [InlineData("slots-moogle.json", "374b668830e9fdb6594694132de054d0cbd326789ea4bb862ee86a48c0d1b6e7")]
    public void TheVectorFileIsTheBackendCopy(string name, string hash)
    {
        var bytes = File.ReadAllBytes(SlotsVectorFiles.PathOf(name));
        Assert.Equal(hash, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    [Fact]
    public void EveryDrawLogVerifiesAsASlotsRound()
    {
        var checkedRounds = 0;
        foreach (var (_, round) in SlotsVectorFiles.Rounds())
        {
            var seedHex = round.GetProperty("seed").GetString()!;
            var roundId = round.GetProperty("roundId").GetString()!;
            var drawLog = round.GetProperty("drawLog").GetString()!;
            var commit = Convert.ToHexStringLower(SHA256.HashData(Convert.FromHexString(seedHex)));
            Assert.Equal(CasinoRoundVerdict.Match,
                CasinoVerifier.Verify(CasinoWire.SlotsKind, seedHex, commit, roundId, drawLog));
            Assert.Equal(CasinoRoundVerdict.Mismatch,
                CasinoVerifier.Verify(CasinoWire.SlotsKind, seedHex, commit, roundId + "x", drawLog));
            checkedRounds++;
        }

        Assert.Equal(80, checkedRounds);
    }

    [Fact]
    public void EachRoundReplaysUnderItsOwnMachineProfile()
    {
        foreach (var (name, round) in SlotsVectorFiles.Rounds())
        {
            var seed = Convert.FromHexString(round.GetProperty("seed").GetString()!);
            var roundId = round.GetProperty("roundId").GetString()!;
            var drawLog = round.GetProperty("drawLog").GetString()!;
            var profile = ProfileOf(name, round.GetProperty("mode").GetString()!);
            Assert.True(SlotsDrawLog.ReplaysAs(profile, seed, roundId, drawLog), roundId);
            Assert.False(SlotsDrawLog.ReplaysAs(SlotsDrawProfile.Legacy, seed, roundId, drawLog), roundId);
        }
    }

    [Fact]
    public void ATamperedDrawFailsTheVerifier()
    {
        foreach (var (_, round) in SlotsVectorFiles.Rounds().Take(6))
        {
            var seedHex = round.GetProperty("seed").GetString()!;
            var roundId = round.GetProperty("roundId").GetString()!;
            var drawLog = round.GetProperty("drawLog").GetString()!;
            var commit = Convert.ToHexStringLower(SHA256.HashData(Convert.FromHexString(seedHex)));
            var colon = drawLog.IndexOf(':');
            var end = drawLog.IndexOf(';');
            var value = uint.Parse(drawLog[(colon + 1)..end]);
            var tampered = drawLog[..(colon + 1)] + (value == 0 ? 1 : value - 1) + drawLog[end..];
            Assert.Equal(CasinoRoundVerdict.Mismatch,
                CasinoVerifier.Verify(CasinoWire.SlotsKind, seedHex, commit, roundId, tampered));
        }
    }

    [Fact]
    public void StepRunningTotalsReachTheRoundWin()
    {
        foreach (var (_, round) in SlotsVectorFiles.Rounds())
        {
            var steps = round.GetProperty("steps").EnumerateArray().ToArray();
            var sum = steps.Sum(step => step.GetProperty("pay").GetInt64());
            var last = steps[^1].GetProperty("running").GetInt64();
            Assert.Equal(last, sum);
            if (!round.GetProperty("capApplied").GetBoolean())
            {
                Assert.Equal(round.GetProperty("totalWin").GetInt64(), last);
            }
        }
    }

    [Fact]
    public void TheGambleCardIsAFairTwoWayDraw()
    {
        const string seed = "8c149ac526b6f9b67a853867de9ddb8a3f7595c93eb0ad46ea2d790627c777ff";
        var commit = Convert.ToHexStringLower(SHA256.HashData(Convert.FromHexString(seed)));
        var stream = new CasinoVerifier.DrawStream(Convert.FromHexString(seed), "gamble-round");
        var card = stream.NextBelow(2);
        Assert.Equal(CasinoRoundVerdict.Match,
            CasinoVerifier.Verify(CasinoWire.SlotsGambleKind, seed, commit, "gamble-round", "gamble:" + card));
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.SlotsGambleKind, seed, commit, "gamble-round", "gamble:" + (1 - card)));
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.SlotsGambleKind, seed, commit, "gamble-round", "gamble:2"));
    }

    private static SlotsDrawProfile ProfileOf(string name, string mode) => name switch
    {
        "slots-cascade.json" => mode switch
        {
            "ante" => SlotsDrawProfile.CascadeAnte,
            "buy" => SlotsDrawProfile.CascadeBuy,
            _ => SlotsDrawProfile.CascadeBase,
        },
        "slots-moogle.json" => SlotsDrawProfile.Moogle,
        _ => SlotsDrawProfile.Bird,
    };
}
