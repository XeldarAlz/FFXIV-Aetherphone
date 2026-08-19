using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.VenueSync;

internal readonly record struct VenueGuestState(string World, bool InVenue);

internal sealed class VenuePatronTracker : IDisposable
{
    private const long TickIntervalMilliseconds = 1000;
    private const long EventCacheTtlMilliseconds = 60_000;

    private readonly IObjectTable objectTable;
    private readonly Configuration configuration;
    private readonly GameData gameData;
    private readonly VenueSyncApiClient client;
    private readonly FrameworkTicker ticker;
    private readonly Dictionary<string, VenueGuestState> guests = new();
    private readonly Dictionary<string, (bool Active, long FetchedAtMs)> eventPresence = new();
    private readonly HashSet<string> eventPresenceInFlight = new();
    private long currentHouseId;

    public VenuePatronTracker(IFramework framework, IObjectTable objectTable, Configuration configuration,
        GameData gameData, VenueSyncApiClient client, AppGate gate)
    {
        this.objectTable = objectTable;
        this.configuration = configuration;
        this.gameData = gameData;
        this.client = client;
        ticker = new FrameworkTicker(framework, TickIntervalMilliseconds, OnTick, gate);
    }

    private void OnTick()
    {
        if (!configuration.VenueSyncPatronTrackingEnabled || string.IsNullOrEmpty(configuration.VenueSyncApiKey))
        {
            return;
        }

        var currentHouse = VenueSyncHouseDetector.Current(gameData);
        if (currentHouse is null)
        {
            ClearGuests();
            return;
        }

        if (!configuration.VenueSyncHouseLinks.TryGetValue(currentHouse.Value.HouseId, out var venueId) ||
            string.IsNullOrEmpty(venueId))
        {
            ClearGuests();
            return;
        }

        if (currentHouse.Value.HouseId != currentHouseId)
        {
            guests.Clear();
            currentHouseId = currentHouse.Value.HouseId;
        }

        if (configuration.VenueSyncPatronTrackingOnlyDuringEvents && !IsEventActive(venueId))
        {
            return;
        }

        ScanGuests(venueId);
    }

    private void ClearGuests()
    {
        guests.Clear();
        currentHouseId = 0;
    }

    private bool IsEventActive(string venueId)
    {
        var nowMs = Environment.TickCount64;
        if (eventPresence.TryGetValue(venueId, out var cached) &&
            nowMs - cached.FetchedAtMs < EventCacheTtlMilliseconds)
        {
            return cached.Active;
        }

        if (eventPresenceInFlight.Add(venueId))
        {
            _ = RefreshEventPresenceAsync(venueId);
        }

        return false;
    }

    private async Task RefreshEventPresenceAsync(string venueId)
    {
        try
        {
            var response = await client.GetActiveEventAsync(venueId, CancellationToken.None).ConfigureAwait(false);
            eventPresence[venueId] = (response?.Active ?? false, Environment.TickCount64);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[VenueSync/Patron] Event presence check failed: {exception.Message}");
        }
        finally
        {
            eventPresenceInFlight.Remove(venueId);
        }
    }

    private void ScanGuests(string venueId)
    {
        var seen = new HashSet<string>();
        foreach (var gameObject in objectTable)
        {
            if (gameObject is not IPlayerCharacter character)
            {
                continue;
            }

            var name = character.Name.TextValue;
            if (name.Length == 0)
            {
                continue;
            }

            var world = gameData.WorldName(character.HomeWorld.RowId);
            if (world.Length == 0)
            {
                world = gameData.WorldName(character.CurrentWorld.RowId);
            }

            seen.Add(name);
            if (!guests.TryGetValue(name, out var state) || !state.InVenue)
            {
                guests[name] = new VenueGuestState(world, true);
                _ = PostVisitAsync(venueId, name, world, "enter");
            }
        }

        var departedNames = new List<string>();
        foreach (var pair in guests)
        {
            if (pair.Value.InVenue && !seen.Contains(pair.Key))
            {
                departedNames.Add(pair.Key);
            }
        }

        for (var index = 0; index < departedNames.Count; index++)
        {
            var name = departedNames[index];
            var world = guests[name].World;
            guests[name] = new VenueGuestState(world, false);
            _ = PostVisitAsync(venueId, name, world, "leave");
        }
    }

    private async Task PostVisitAsync(string venueId, string characterName, string world, string action)
    {
        try
        {
            var request = new VenueSyncPatronVisitRequest
            {
                VenueId = venueId,
                CharacterName = characterName,
                World = world,
                Action = action,
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            };
            var result = await client.PostPatronVisitAsync(request, CancellationToken.None).ConfigureAwait(false);
            if (result is not { Success: true })
            {
                AepLog.Warning($"[VenueSync/Patron] Server rejected {action} for {characterName}: " +
                    $"{result?.Error ?? "no response"}");
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[VenueSync/Patron] Failed to log {action} for {characterName}: {exception.Message}");
        }
    }

    public void Dispose()
    {
        ticker.Dispose();
    }
}
