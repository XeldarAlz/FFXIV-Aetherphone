using System.Globalization;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.VenueSync;

internal readonly record struct VenueGuestKey(string Name, string World);

internal readonly record struct VenueGuestState(bool InVenue, bool Synced, long LastSeenAtMs);

internal sealed class VenuePatronTracker : IDisposable
{
    private const long TickIntervalMilliseconds = 1000;
    private const long EventCacheTtlMilliseconds = 60_000;
    private const long DepartureDebounceMilliseconds = TickIntervalMilliseconds * 3;

    private readonly IFramework framework;
    private readonly IObjectTable objectTable;
    private readonly Configuration configuration;
    private readonly GameData gameData;
    private readonly VenueSyncApiClient client;
    private readonly PhoneVisibility visibility;
    private readonly FrameworkTicker ticker;
    private readonly Dictionary<VenueGuestKey, VenueGuestState> guests = new();
    private readonly HashSet<VenueGuestKey> guestPostInFlight = new();
    private readonly Dictionary<string, (bool Active, long FetchedAtMs)> eventPresence = new();
    private readonly HashSet<string> eventPresenceInFlight = new();
    private long currentHouseId;

    public VenuePatronTracker(IFramework framework, IObjectTable objectTable, Configuration configuration,
        GameData gameData, VenueSyncApiClient client, PhoneVisibility visibility, AppGate gate)
    {
        this.framework = framework;
        this.objectTable = objectTable;
        this.configuration = configuration;
        this.gameData = gameData;
        this.client = client;
        this.visibility = visibility;
        ticker = new FrameworkTicker(framework, TickIntervalMilliseconds, OnTick, gate);
    }

    private void OnTick()
    {
        if (!visibility.IsVisible)
        {
            ClearGuests();
            return;
        }

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
        bool active;
        try
        {
            var response = await client.GetActiveEventAsync(venueId, CancellationToken.None).ConfigureAwait(false);
            active = response?.Active ?? false;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[VenueSync/Patron] Event presence check failed: {exception.Message}");
            await framework.RunOnFrameworkThread(() => eventPresenceInFlight.Remove(venueId)).ConfigureAwait(false);
            return;
        }

        var fetchedAtMs = Environment.TickCount64;
        await framework.RunOnFrameworkThread(() =>
        {
            eventPresence[venueId] = (active, fetchedAtMs);
            eventPresenceInFlight.Remove(venueId);
        }).ConfigureAwait(false);
    }

    private void ScanGuests(string venueId)
    {
        var nowMs = Environment.TickCount64;
        var seen = new HashSet<VenueGuestKey>();
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

            var key = new VenueGuestKey(name, world);
            seen.Add(key);
            if (!guests.TryGetValue(key, out var existing) || !existing.InVenue || !existing.Synced)
            {
                guests[key] = new VenueGuestState(true, false, nowMs);
                if (guestPostInFlight.Add(key))
                {
                    _ = PostVisitAsync(venueId, key, true, nowMs);
                }
            }
            else
            {
                guests[key] = existing with { LastSeenAtMs = nowMs };
            }
        }

        var departedKeys = new List<VenueGuestKey>();
        foreach (var pair in guests)
        {
            if (pair.Value.InVenue && !seen.Contains(pair.Key) &&
                nowMs - pair.Value.LastSeenAtMs >= DepartureDebounceMilliseconds)
            {
                departedKeys.Add(pair.Key);
            }
        }

        for (var index = 0; index < departedKeys.Count; index++)
        {
            var key = departedKeys[index];
            guests[key] = new VenueGuestState(false, false, nowMs);
            if (guestPostInFlight.Add(key))
            {
                _ = PostVisitAsync(venueId, key, false, nowMs);
            }
        }
    }

    private async Task PostVisitAsync(string venueId, VenueGuestKey key, bool expectedInVenue, long stateStampMs)
    {
        var action = expectedInVenue ? "enter" : "leave";
        try
        {
            var request = new VenueSyncPatronVisitRequest
            {
                VenueId = venueId,
                CharacterName = key.Name,
                World = key.World,
                Action = action,
                Timestamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            };
            var result = await client.PostPatronVisitAsync(request, CancellationToken.None).ConfigureAwait(false);
            if (result is not { Success: true })
            {
                AepLog.Warning($"[VenueSync/Patron] Server rejected {action} for {key.Name}: " +
                    $"{result?.Error ?? "no response"}");
                await framework.RunOnFrameworkThread(() => guestPostInFlight.Remove(key)).ConfigureAwait(false);
                return;
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[VenueSync/Patron] Failed to log {action} for {key.Name}: {exception.Message}");
            await framework.RunOnFrameworkThread(() => guestPostInFlight.Remove(key)).ConfigureAwait(false);
            return;
        }

        await framework.RunOnFrameworkThread(() =>
        {
            if (guests.TryGetValue(key, out var current) && current.InVenue == expectedInVenue &&
                current.LastSeenAtMs == stateStampMs)
            {
                guests[key] = current with { Synced = true };
            }

            guestPostInFlight.Remove(key);
        }).ConfigureAwait(false);
    }

    public void Dispose()
    {
        ticker.Dispose();
    }
}
