using Aetherphone.Core.Net;

namespace Aetherphone.Core.VenueSync;

internal sealed class VenueSyncApiClient
{
    private readonly HttpService http;
    private readonly Configuration configuration;

    public VenueSyncApiClient(HttpService http, Configuration configuration)
    {
        this.http = http;
        this.configuration = configuration;
    }

    private string BaseUrl => string.IsNullOrEmpty(configuration.VenueSyncServerUrl)
        ? "https://xivvenuemanager.com"
        : configuration.VenueSyncServerUrl;

    private string? ApiKey => string.IsNullOrEmpty(configuration.VenueSyncApiKey)
        ? null
        : configuration.VenueSyncApiKey;

    public Task<VenueSyncVenuesResponse?> GetVenuesAsync(CancellationToken token) =>
        http.GetJsonAsync($"{BaseUrl}/api/plugin/venues", VenueSyncJsonContext.Default.VenueSyncVenuesResponse,
            null, token, appScope: "venue-sync", apiKey: ApiKey);

    public Task<VenueSyncShiftsResponse?> GetShiftsAsync(string venueId, CancellationToken token) =>
        http.GetJsonAsync($"{BaseUrl}/api/plugin/shifts?venueId={Uri.EscapeDataString(venueId)}",
            VenueSyncJsonContext.Default.VenueSyncShiftsResponse, null, token, appScope: "venue-sync", apiKey: ApiKey);

    public Task<VenueSyncClockResult?> ClockInAsync(string shiftId, CancellationToken token) =>
        http.PostJsonAsync($"{BaseUrl}/api/plugin/shifts/clock-in", new VenueSyncShiftIdRequest { ShiftId = shiftId },
            VenueSyncJsonContext.Default.VenueSyncShiftIdRequest, VenueSyncJsonContext.Default.VenueSyncClockResult,
            null, token, appScope: "venue-sync", apiKey: ApiKey);

    public Task<VenueSyncClockResult?> ClockOutAsync(string shiftId, CancellationToken token) =>
        http.PostJsonAsync($"{BaseUrl}/api/plugin/shifts/clock-out", new VenueSyncShiftIdRequest { ShiftId = shiftId },
            VenueSyncJsonContext.Default.VenueSyncShiftIdRequest, VenueSyncJsonContext.Default.VenueSyncClockResult,
            null, token, appScope: "venue-sync", apiKey: ApiKey);

    public Task<VenueSyncClockResult?> ClaimShiftAsync(string shiftId, CancellationToken token) =>
        http.PostJsonAsync($"{BaseUrl}/api/plugin/shifts/claim", new VenueSyncShiftIdRequest { ShiftId = shiftId },
            VenueSyncJsonContext.Default.VenueSyncShiftIdRequest, VenueSyncJsonContext.Default.VenueSyncClockResult,
            null, token, appScope: "venue-sync", apiKey: ApiKey);

    public Task<VenueSyncServicesResponse?> GetServicesAsync(string venueId, CancellationToken token) =>
        http.GetJsonAsync($"{BaseUrl}/api/plugin/services?venueId={Uri.EscapeDataString(venueId)}",
            VenueSyncJsonContext.Default.VenueSyncServicesResponse, null, token, appScope: "venue-sync", apiKey: ApiKey);

    public Task<VenueSyncTransactionResult?> LogTransactionAsync(VenueSyncTransactionRequest request,
        CancellationToken token) =>
        http.PostJsonAsync($"{BaseUrl}/api/plugin/transactions", request,
            VenueSyncJsonContext.Default.VenueSyncTransactionRequest,
            VenueSyncJsonContext.Default.VenueSyncTransactionResult, null, token, appScope: "venue-sync", apiKey: ApiKey);

    public Task<VenueSyncLinkCharacterResponse?> LinkCharacterAsync(string characterName, string world,
        CancellationToken token) =>
        http.PostJsonAsync($"{BaseUrl}/api/plugin/characters",
            new VenueSyncLinkCharacterRequest { CharacterName = characterName, World = world },
            VenueSyncJsonContext.Default.VenueSyncLinkCharacterRequest,
            VenueSyncJsonContext.Default.VenueSyncLinkCharacterResponse, null, token, appScope: "venue-sync", apiKey: ApiKey);

    public Task<VenueSyncPatronVisitResult?> PostPatronVisitAsync(VenueSyncPatronVisitRequest request,
        CancellationToken token) =>
        http.PostJsonAsync($"{BaseUrl}/api/plugin/patron-visits", request,
            VenueSyncJsonContext.Default.VenueSyncPatronVisitRequest,
            VenueSyncJsonContext.Default.VenueSyncPatronVisitResult, null, token, appScope: "venue-sync",
            apiKey: ApiKey);

    public Task<VenueSyncActiveEventResponse?> GetActiveEventAsync(string venueId, CancellationToken token) =>
        http.GetJsonAsync($"{BaseUrl}/api/plugin/events/active?venueId={Uri.EscapeDataString(venueId)}",
            VenueSyncJsonContext.Default.VenueSyncActiveEventResponse, null, token, appScope: "venue-sync",
            apiKey: ApiKey);
}
