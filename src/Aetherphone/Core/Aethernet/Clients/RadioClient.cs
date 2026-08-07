using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class RadioClient
{
    private readonly AethernetTransport net;

    public RadioClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<CommunityStationPage?> StationsAsync(CancellationToken token)
    {
        return net.GetAsync("/radio/stations", AethernetJsonContext.Default.CommunityStationPage, token);
    }

    public Task<CommunityStationDto?> StationAsync(string stationId, CancellationToken token)
    {
        return net.GetAsync($"/radio/stations/{Uri.EscapeDataString(stationId)}",
            AethernetJsonContext.Default.CommunityStationDto, token);
    }

    public Task<MyCommunityStationDto?> MineAsync(CancellationToken token)
    {
        return net.GetAsync("/radio/mine", AethernetJsonContext.Default.MyCommunityStationDto, token);
    }

    public Task<MyCommunityStationDto?> UpdateMineAsync(UpdateCommunityStationRequest request, CancellationToken token,
        Action<int>? statusSink = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, "/radio/mine", request,
            AethernetJsonContext.Default.UpdateCommunityStationRequest, AethernetJsonContext.Default.MyCommunityStationDto,
            token, statusSink);
    }

    public Task<RadioTrackPage?> TracksAsync(string stationId, CancellationToken token)
    {
        return net.GetAsync($"/radio/stations/{Uri.EscapeDataString(stationId)}/tracks",
            AethernetJsonContext.Default.RadioTrackPage, token);
    }

    public Task<RadioFollowResultDto?> FollowAsync(string stationId, bool follow, CancellationToken token)
    {
        var method = follow ? HttpMethod.Post : HttpMethod.Delete;
        return net.RequestAsync(method, $"/radio/stations/{Uri.EscapeDataString(stationId)}/follow",
            AethernetJsonContext.Default.RadioFollowResultDto, token);
    }
}
