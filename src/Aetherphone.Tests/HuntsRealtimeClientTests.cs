using System.Net.WebSockets;
using System.Reflection;
using Aetherphone.Core.Hunts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HuntsRealtimeClientTests
{
    private static readonly MethodInfo HandleFrameAsyncMethod = typeof(HuntsRealtimeClient).GetMethod(
        "HandleFrameAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static Task InvokeAsync(HuntsRealtimeClient client, string frame)
    {
        using var ws = new ClientWebSocket();
        return (Task)HandleFrameAsyncMethod.Invoke(client, new object[] { ws, frame, CancellationToken.None })!;
    }

    [Fact]
    public async Task SpawnFrameDispatchesMobReportReceived()
    {
        var client = new HuntsRealtimeClient(null!);
        HuntsSocketMobReport? received = null;
        client.MobReportReceived += report => received = report;

        var frame = "42[\"message\",{\"type\":\"mob\",\"subType\":\"report\",\"data\":{\"action\":\"spawn\",\"id\":{\"mobId\":\"arch_aethereater\",\"worldId\":\"zalera\",\"zoneId\":\"kozamauka\",\"phaseNum\":1},\"data\":{\"zoneId2\":\"kozamauka\",\"zonePoiIds\":[1193,1194,1195,1196],\"timestamp\":\"2026-08-20T14:10:40.521Z\",\"window\":1,\"stage\":1}}}]";

        await InvokeAsync(client, frame);

        Assert.NotNull(received);
        Assert.Equal("spawn", received!.Action);
        Assert.Equal("arch_aethereater", received.Id!.MobId);
    }

    [Fact]
    public async Task DeathFrameDispatchesMobReportReceived()
    {
        var client = new HuntsRealtimeClient(null!);
        HuntsSocketMobReport? received = null;
        client.MobReportReceived += report => received = report;

        var frame = "42[\"message\",{\"type\":\"mob\",\"subType\":\"report\",\"data\":{\"action\":\"death\",\"id\":{\"mobId\":\"arch_aethereater\",\"worldId\":\"zalera\",\"zoneId\":\"kozamauka\",\"phaseNum\":2},\"data\":{\"num\":1,\"startedAt\":\"2026-08-20T14:15:07.065Z\",\"prevStartedAt\":\"2026-08-14T05:10:46.691Z\"}}}]";

        await InvokeAsync(client, frame);

        Assert.NotNull(received);
        Assert.Equal("death", received!.Action);
    }

    [Fact]
    public async Task MobWorldKillFrameDispatchesMobWorldKillReceived()
    {
        var client = new HuntsRealtimeClient(null!);
        HuntLogEntryDto? received = null;
        client.MobWorldKillReceived += entry => received = entry;

        var frame = "42[\"message\",{\"type\":\"mobworldkill\",\"subType\":\"recentAdd\",\"data\":{\"id\":2368950,\"mobId2\":\"arch_aethereater\",\"worldId2\":\"zalera\",\"zoneInstance\":null,\"spawnedAt\":\"2026-08-20T14:10:40.521Z\",\"killedAt\":\"2026-08-20T14:15:07.065Z\",\"zoneId2\":\"kozamauka\",\"isFailed\":false}}]";

        await InvokeAsync(client, frame);

        Assert.NotNull(received);
        Assert.Equal("arch_aethereater", received!.MobId);
    }

    [Fact]
    public async Task MultiPhaseFRankSpawnFrameCarriesItsPhaseNum()
    {
        var client = new HuntsRealtimeClient(null!);
        HuntsSocketMobReport? received = null;
        client.MobReportReceived += report => received = report;

        var frame = "42[\"message\",{\"type\":\"mob\",\"subType\":\"report\",\"data\":{\"action\":\"spawn\",\"id\":{\"mobId\":\"behemoth\",\"worldId\":\"jenova\",\"phaseNum\":1},\"data\":{\"zoneId2\":\"coerthas_central_highlands\",\"zonePoiIds\":[491],\"timestamp\":\"2026-08-20T20:02:48.016Z\",\"reporters\":[{\"id\":44020,\"name\":\"Takeru Yamato\"}],\"window\":1,\"stage\":1}}}]";

        await InvokeAsync(client, frame);

        Assert.NotNull(received);
        Assert.Equal("behemoth", received!.Id!.MobId);
        Assert.Equal(1, received.Id!.PhaseNum);
    }

    [Fact]
    public async Task SRankSpawnFrameWithReportersAndNoPhaseDispatches()
    {
        var client = new HuntsRealtimeClient(null!);
        HuntsSocketMobReport? received = null;
        client.MobReportReceived += report => received = report;

        var frame = "42[\"message\",{\"type\":\"mob\",\"subType\":\"report\",\"data\":{\"action\":\"spawn\",\"id\":{\"mobId\":\"gunitt\",\"worldId\":\"cactuar\"},\"data\":{\"zoneId2\":\"the_tempest\",\"zonePoiIds\":[1026],\"timestamp\":\"2026-08-20T16:24:23.094Z\",\"reporters\":[{\"id\":36707,\"name\":\"Elpha Bits\"}],\"isScheduled\":false,\"scheduleDelay\":null,\"window\":1,\"stage\":null}}}]";

        await InvokeAsync(client, frame);

        Assert.NotNull(received);
        Assert.Equal("spawn", received!.Action);
        Assert.Equal("gunitt", received.Id!.MobId);
        Assert.Null(received.Id!.ZoneId);
        Assert.Null(received.Id!.PhaseNum);
        Assert.False(received.Data!.IsScheduled);
        Assert.Equal(1026, received.Data!.ZonePoiIds![0]);
        Assert.Equal(36707, received.Data!.Reporters![0].Id);
        Assert.Equal("Elpha Bits", received.Data!.Reporters![0].Name);
    }

    [Fact]
    public async Task SpawnClaimFrameWithReportersDispatches()
    {
        var client = new HuntsRealtimeClient(null!);
        HuntsSocketMobReport? received = null;
        client.MobReportReceived += report => received = report;

        var frame = "42[\"message\",{\"type\":\"mob\",\"subType\":\"report\",\"data\":{\"action\":\"spawn_claim\",\"id\":{\"mobId\":\"gunitt\",\"worldId\":\"cactuar\"},\"data\":{\"reporters\":[{\"id\":36707,\"name\":\"Elpha Bits\"},{\"id\":44020,\"name\":\"Takeru Yamato\"}]}}}]";

        await InvokeAsync(client, frame);

        Assert.NotNull(received);
        Assert.Equal("spawn_claim", received!.Action);
        Assert.Equal(2, received.Data!.Reporters!.Length);
        Assert.Equal("Takeru Yamato", received.Data!.Reporters![1].Name);
    }

    [Fact]
    public async Task SpawnFrameWithNullReportersDispatches()
    {
        var client = new HuntsRealtimeClient(null!);
        HuntsSocketMobReport? received = null;
        client.MobReportReceived += report => received = report;

        var frame = "42[\"message\",{\"type\":\"mob\",\"subType\":\"report\",\"data\":{\"action\":\"spawn\",\"id\":{\"mobId\":\"gunitt\",\"worldId\":\"cactuar\"},\"data\":{\"zoneId2\":\"the_tempest\",\"zonePoiIds\":[1026],\"timestamp\":\"2026-08-20T16:24:23.094Z\",\"reporters\":null,\"isScheduled\":false,\"window\":1,\"stage\":null}}}]";

        await InvokeAsync(client, frame);

        Assert.NotNull(received);
        Assert.Null(received!.Data!.Reporters);
    }

    [Fact]
    public async Task SRankDeathFrameWithNoPhaseDispatches()
    {
        var client = new HuntsRealtimeClient(null!);
        HuntsSocketMobReport? received = null;
        client.MobReportReceived += report => received = report;

        var frame = "42[\"message\",{\"type\":\"mob\",\"subType\":\"report\",\"data\":{\"action\":\"death\",\"id\":{\"mobId\":\"gunitt\",\"worldId\":\"cactuar\"},\"data\":{\"num\":1,\"startedAt\":\"2026-08-20T16:27:58.247Z\",\"prevStartedAt\":\"2026-08-16T16:39:36.506Z\"}}}]";

        await InvokeAsync(client, frame);

        Assert.NotNull(received);
        Assert.Equal("death", received!.Action);
        Assert.Null(received.Id!.WindowNum);
        Assert.Null(received.Id!.PhaseNum);
    }

    [Fact]
    public async Task SRankMobWorldKillFrameDispatchesMobWorldKillReceived()
    {
        var client = new HuntsRealtimeClient(null!);
        HuntLogEntryDto? received = null;
        client.MobWorldKillReceived += entry => received = entry;

        var frame = "42[\"message\",{\"type\":\"mobworldkill\",\"subType\":\"recentAdd\",\"data\":{\"id\":2369010,\"mobId2\":\"gunitt\",\"worldId2\":\"cactuar\",\"zoneInstance\":null,\"spawnedAt\":\"2026-08-20T16:24:23.094Z\",\"killedAt\":\"2026-08-20T16:27:58.247Z\",\"zoneId2\":\"the_tempest\",\"isFailed\":false}}]";

        await InvokeAsync(client, frame);

        Assert.NotNull(received);
        Assert.Equal("gunitt", received!.MobId);
        Assert.Equal("the_tempest", received.ZoneId);
    }

    [Fact]
    public async Task SSRankDeathFrameWithZoneInstanceDispatches()
    {
        var client = new HuntsRealtimeClient(null!);
        HuntsSocketMobReport? received = null;
        client.MobReportReceived += report => received = report;

        var frame = "42[\"message\",{\"type\":\"mob\",\"subType\":\"report\",\"data\":{\"action\":\"death\",\"id\":{\"mobId\":\"arch_aethereater\",\"worldId\":\"asura\",\"zoneInstance\":2,\"zoneId\":\"heritage_found\",\"phaseNum\":2},\"data\":{\"num\":1,\"startedAt\":\"2026-08-20T19:40:00.231Z\",\"prevStartedAt\":\"2025-08-29T20:55:18.099Z\"}}}]";

        await InvokeAsync(client, frame);

        Assert.NotNull(received);
        Assert.Equal("death", received!.Action);
        Assert.Equal("arch_aethereater", received.Id!.MobId);
        Assert.Equal(2, received.Id!.ZoneInstance);
        Assert.Equal("heritage_found", received.Id!.ZoneId);
        Assert.Equal(2, received.Id!.PhaseNum);
    }

    [Fact]
    public async Task UnknownMessageTypeIsIgnored()
    {
        var client = new HuntsRealtimeClient(null!);
        var reportFired = false;
        var killFired = false;
        client.MobReportReceived += _ => reportFired = true;
        client.MobWorldKillReceived += _ => killFired = true;

        var frame = "42[\"message\",{\"type\":\"presence\",\"subType\":\"userJoined\",\"data\":{}}]";

        await InvokeAsync(client, frame);

        Assert.False(reportFired);
        Assert.False(killFired);
    }
}
