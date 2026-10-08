using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoVenueVectorTests
{
    private const string ExpectedHash = "62f91113fc1d4b1c66a7a8ec852791327afd4d62c77b3fbc1b9089ef436431fe";

    [Fact]
    public void TheVenueVectorsAreTheBackendFileByteForByte()
    {
        var text = File.ReadAllText(VectorPath()).Replace("\r", string.Empty, StringComparison.Ordinal);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

        Assert.Equal(ExpectedHash, hash);
    }

    [Fact]
    public void TheVenueVectorsCoverEveryRoomKind()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(VectorPath()));
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.True(root.GetProperty("dice").GetArrayLength() > 0);
        Assert.True(root.GetProperty("deathroll").GetArrayLength() > 0);
        Assert.True(root.GetProperty("raffle").GetArrayLength() > 0);
    }

    private static string VectorPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Vectors", "venue.json");
    }
}
