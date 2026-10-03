using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class AdDtoWireTests
{
    private const string ServerAd =
        "{\"id\":\"ad1\",\"ownerId\":\"u1\",\"ownerName\":\"Owner\",\"ownerHandle\":\"owner\",\"ownerAvatarUrl\":\"\","
        + "\"archetype\":0,\"category\":0,\"title\":\"Night\",\"body\":\"Body\",\"tags\":[],\"region\":2,"
        + "\"dataCenterId\":7,\"worldId\":49,\"territoryId\":341,\"mapId\":83,\"mapX\":10.5,\"mapY\":11.9,"
        + "\"ward\":14,\"plot\":30,\"addressNote\":\"\","
        + "\"schedule\":[{\"day\":5,\"startMinute\":1200,\"durationMinutes\":180},"
        + "{\"day\":6,\"startMinute\":1230,\"durationMinutes\":90,\"everyWeeks\":2,\"firstUnix\":1791000000}],"
        + "\"openUntilUnix\":0,\"priceMode\":0,\"priceGil\":0,\"turnaround\":\"\",\"slotsLine\":\"\","
        + "\"requirements\":\"\",\"linkUrl\":\"\",\"afterDark\":false,\"mediaUrl\":null,\"mediaUrls\":[],"
        + "\"views\":0,\"saved\":false,\"status\":\"live\",\"createdAtUnix\":1,\"renewedAtUnix\":1,\"expiresAtUnix\":2}";

    [Fact]
    public void AddressFieldsSurviveTheWire()
    {
        var ad = JsonSerializer.Deserialize(ServerAd, AethernetJsonContext.Default.AdDto);

        Assert.NotNull(ad);
        Assert.Equal(341, ad.TerritoryId);
        Assert.Equal(83, ad.MapId);
        Assert.Equal(49, ad.WorldId);
        Assert.Equal(14, ad.Ward);
        Assert.Equal(30, ad.Plot);
    }

    [Fact]
    public void ScheduleSlotsDefaultToWeeklyWhenTheServerSendsNoCadence()
    {
        var ad = JsonSerializer.Deserialize(ServerAd, AethernetJsonContext.Default.AdDto);

        Assert.NotNull(ad);
        Assert.Equal(new AdScheduleSlot(5, 1200, 180), ad.Schedule[0]);
        Assert.Equal(new AdScheduleSlot(6, 1230, 90, 2, 1791000000L), ad.Schedule[1]);
    }

    [Fact]
    public void RepeatingSlotsAreSentWithTheirCadence()
    {
        var slot = new AdScheduleSlot(5, 1200, 180, 3, 1791000000L);

        var json = JsonSerializer.Serialize(slot, AethernetJsonContext.Default.AdScheduleSlot);

        Assert.Contains("\"everyWeeks\":3", json);
        Assert.Contains("\"firstUnix\":1791000000", json);
    }
}
