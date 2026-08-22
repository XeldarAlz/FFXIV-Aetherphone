using System.Text.Json;
using Aetherphone.Core.Hunts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HuntSpawnEntryTimestampTests
{
    [Fact]
    public void DeserializesTheTimestampFieldFromTheDataCenterSpawnsArray()
    {
        const string json = "{\"mobId2\":\"gunitt\",\"worldId2\":\"cactuar\",\"zoneInstance\":0,\"isScheduled\":false,\"timestamp\":\"2026-08-20T16:24:23.094Z\"}";

        var entry = JsonSerializer.Deserialize<HuntSpawnEntryDto>(json);

        Assert.NotNull(entry);
        Assert.Equal(DateTimeOffset.Parse("2026-08-20T16:24:23.094Z"), entry!.Timestamp);
    }

    [Fact]
    public void TimestampIsNullWhenTheFieldIsAbsent()
    {
        const string json = "{\"mobId2\":\"gunitt\",\"worldId2\":\"cactuar\",\"zoneInstance\":0,\"isScheduled\":false}";

        var entry = JsonSerializer.Deserialize<HuntSpawnEntryDto>(json);

        Assert.NotNull(entry);
        Assert.Null(entry!.Timestamp);
    }
}
