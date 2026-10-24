using Aetherphone.Core.Glamour;
using Newtonsoft.Json;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GlamourerStainListTests
{
    [Fact]
    public void StainsSurviveTheJsonRoundTripDalamudUsesForIpcArguments()
    {
        var json = JsonConvert.SerializeObject(GlamourerBridge.StainList(12, 87));

        var stains = JsonConvert.DeserializeObject<IReadOnlyList<byte>>(json);

        Assert.NotNull(stains);
        Assert.Equal(new byte[] { 12, 87 }, stains);
    }

    [Fact]
    public void AByteArrayCannotCrossThatRoundTrip()
    {
        var json = JsonConvert.SerializeObject(new byte[] { 12, 87 });

        Assert.ThrowsAny<Exception>(() => JsonConvert.DeserializeObject<IReadOnlyList<byte>>(json));
    }
}
