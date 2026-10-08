using System.Security.Cryptography;
using System.Text.Json;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PlinkoVectorTests
{
    private const string PinnedHash = "6f9373148211cc136152b41059c87908ce4336b8d4cc19298da1c9c070058a06";

    [Fact]
    public void TheVectorFileIsTheBackendCopyByteForByte()
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(VectorPath())));
        Assert.Equal(PinnedHash, hash);
    }

    [Fact]
    public void TheStripsMatchTheVectorFile()
    {
        using var vectors = Load();
        var strips = vectors.RootElement.GetProperty("strips");
        for (var rowsIndex = 0; rowsIndex < PlinkoRules.RowCounts.Length; rowsIndex++)
        {
            var rows = PlinkoRules.RowCounts[rowsIndex];
            var boards = strips.GetProperty(rows.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var risk = 0;
            foreach (var strip in boards.EnumerateArray())
            {
                Assert.Equal(Ints(strip), PlinkoRules.Strip(rows, risk).ToArray());
                risk++;
            }

            Assert.Equal(PlinkoRules.RiskCount, risk);
        }
    }

    [Fact]
    public void EveryDropReplaysItsPathSlotAndPayout()
    {
        using var vectors = Load();
        var checkedCases = 0;
        foreach (var entry in vectors.RootElement.GetProperty("drops").EnumerateArray())
        {
            var seedHex = entry.GetProperty("seed").GetString()!;
            var roundId = entry.GetProperty("roundId").GetString()!;
            var drawLog = entry.GetProperty("drawLog").GetString()!;
            var rows = entry.GetProperty("rows").GetInt32();
            var risk = entry.GetProperty("risk").GetInt32();
            var stake = entry.GetProperty("stake").GetInt64();
            var path = Ints(entry.GetProperty("path"));
            var slot = entry.GetProperty("slot").GetInt32();
            var stream = new CasinoVerifier.DrawStream(Convert.FromHexString(seedHex), roundId);
            for (var row = 0; row < rows; row++)
            {
                Assert.Equal((uint)path[row], stream.NextBelow(2));
            }

            Assert.True(PlinkoRules.IsPath(path, rows, slot));
            var tenths = PlinkoRules.MultiplierTenths(rows, risk, slot);
            Assert.Equal(entry.GetProperty("multiplierTenths").GetInt32(), tenths);
            Assert.Equal(entry.GetProperty("payout").GetInt64(), PlinkoRules.Payout(stake, tenths));
            var commit = Convert.ToHexStringLower(SHA256.HashData(Convert.FromHexString(seedHex)));
            Assert.Equal(CasinoRoundVerdict.Match,
                CasinoVerifier.Verify(CasinoWire.PlinkoKind, seedHex, commit, roundId, drawLog));
            Assert.Equal(CasinoRoundVerdict.Mismatch,
                CasinoVerifier.Verify(CasinoWire.PlinkoKind, seedHex, commit, roundId + "x", drawLog));
            checkedCases++;
        }

        Assert.Equal(36, checkedCases);
    }

    [Fact]
    public void ALogLongerThanTheTallestBoardIsRefused()
    {
        var seed = new byte[32];
        var seedHex = Convert.ToHexStringLower(seed);
        var commit = Convert.ToHexStringLower(SHA256.HashData(seed));
        var stream = new CasinoVerifier.DrawStream(seed, "r");
        var parts = new string[PlinkoRules.MaxRows + 1];
        for (var row = 0; row < parts.Length; row++)
        {
            parts[row] = "peg:" + stream.NextBelow(2).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(CasinoWire.PlinkoKind, seedHex, commit, "r", string.Join(';', parts)));
        Assert.Equal(CasinoRoundVerdict.Match, CasinoVerifier.Verify(CasinoWire.PlinkoKind, seedHex, commit, "r",
            string.Join(';', parts, 0, PlinkoRules.MaxRows)));
    }

    private static int[] Ints(JsonElement array) => array.EnumerateArray().Select(item => item.GetInt32()).ToArray();

    private static JsonDocument Load() => JsonDocument.Parse(File.ReadAllBytes(VectorPath()));

    private static string VectorPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Aetherphone.Tests", "Vectors", "plinko.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate src/Aetherphone.Tests/Vectors/plinko.json.");
    }
}
