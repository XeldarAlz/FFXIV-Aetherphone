using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Runtime;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Aetherphone.Core.Fishing;

internal enum FishingIslandKind : byte
{
    None,
    Voyage,
    Fish,
}

internal readonly record struct FishingIslandStatus(
    FishingIslandKind Kind,
    string Title,
    long TargetUnix,
    bool Open)
{
    public static readonly FishingIslandStatus None = new(FishingIslandKind.None, string.Empty, 0, false);
}

internal sealed class FishingAlerts : IDisposable
{
    public const long FishLeadSeconds = 300;
    public const long VoyageLeadSeconds = 600;
    public const long BoardingWindowSeconds = 900;
    private const long TickIntervalMilliseconds = 1000;
    private const long CaughtRefreshMilliseconds = 5000;
    private const int WindowRefreshBudget = 24;
    private const string FishGroupPrefix = "fishing.fish.";
    private const string VoyageGroupKey = "fishing.voyage";
    private static readonly Vector4 Accent = AppAccents.For(FishingCatalog.AppId);

    private readonly Configuration configuration;
    private readonly FishingCatalog catalog;
    private readonly NotificationService notifications;
    private readonly IClientState clientState;
    private readonly FrameworkTicker ticker;
    private readonly Dictionary<uint, long> notifiedWindowStart = new();
    private readonly HashSet<uint> caughtItems = new();
    private readonly OceanVoyageSlot[] voyageProbe = new OceanVoyageSlot[1];
    private long lastCaughtRefresh = long.MinValue;

    public FishingAlerts(Configuration configuration, FishingCatalog catalog, NotificationService notifications,
        IClientState clientState, IFramework framework, AppGate gate)
    {
        this.configuration = configuration;
        this.catalog = catalog;
        this.notifications = notifications;
        this.clientState = clientState;
        ticker = new FrameworkTicker(framework, TickIntervalMilliseconds, OnTick, gate);
    }

    public FishingIslandStatus Island { get; private set; } = FishingIslandStatus.None;

    public bool CaughtKnown { get; private set; }

    public int CaughtVersion { get; private set; }

    public bool IsCaught(uint itemId) => CaughtKnown && caughtItems.Contains(itemId);

    public bool HasAlarm(uint itemId) => configuration.FishingAlarms.Contains(itemId);

    public int AlarmCount => configuration.FishingAlarms.Count;

    public bool ToggleAlarm(uint itemId)
    {
        var alarms = configuration.FishingAlarms;
        var enabled = !alarms.Remove(itemId);
        if (enabled)
        {
            alarms.Add(itemId);
            notifiedWindowStart.Remove(itemId);
        }

        configuration.Save();
        return enabled;
    }

    public bool HasVoyageReminder(long boardingUnix) => FindReminder(boardingUnix) >= 0;

    public bool ToggleVoyageReminder(long boardingUnix, OceanRoute route)
    {
        var reminders = configuration.FishingVoyageReminders;
        var existing = FindReminder(boardingUnix);
        if (existing >= 0)
        {
            reminders.RemoveAt(existing);
            configuration.Save();
            return false;
        }

        reminders.Add(new FishingVoyageReminder { BoardingUnix = boardingUnix, Route = (int)route });
        configuration.Save();
        return true;
    }

    public void Dispose()
    {
        ticker.Dispose();
    }

    private int FindReminder(long boardingUnix)
    {
        var reminders = configuration.FishingVoyageReminders;
        for (var index = 0; index < reminders.Count; index++)
        {
            if (reminders[index].BoardingUnix == boardingUnix)
            {
                return index;
            }
        }

        return -1;
    }

    private void OnTick()
    {
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var hasFishAlarms = configuration.FishingAlarms.Count > 0;
        if (hasFishAlarms)
        {
            catalog.EnsureLoaded();
            catalog.RefreshWindows(nowUnix, WindowRefreshBudget);
        }

        var island = FishingIslandStatus.None;
        var dirty = TickVoyages(nowUnix, ref island);
        if (hasFishAlarms)
        {
            TickFish(nowUnix, ref island);
        }

        Island = island;
        RefreshCaught();
        if (dirty)
        {
            configuration.Save();
        }
    }

    private bool TickVoyages(long nowUnix, ref FishingIslandStatus island)
    {
        var reminders = configuration.FishingVoyageReminders;
        var dirty = false;
        for (var index = reminders.Count - 1; index >= 0; index--)
        {
            var reminder = reminders[index];
            var closesUnix = reminder.BoardingUnix + BoardingWindowSeconds;
            if (nowUnix >= closesUnix)
            {
                reminders.RemoveAt(index);
                dirty = true;
                continue;
            }

            if (nowUnix < reminder.BoardingUnix - VoyageLeadSeconds)
            {
                continue;
            }

            var title = VoyageTitle(reminder);
            var open = nowUnix >= reminder.BoardingUnix;
            Offer(ref island, new FishingIslandStatus(FishingIslandKind.Voyage, title,
                open ? closesUnix : reminder.BoardingUnix, open));
            if (reminder.Notified)
            {
                continue;
            }

            reminder.Notified = true;
            dirty = true;
            var body = open
                ? Loc.T(L.Fishing.BoardingOpenBody)
                : Loc.T(L.Fishing.BoardingSoonBody,
                    TimeText.Clock(DateTimeOffset.FromUnixTimeSeconds(reminder.BoardingUnix).LocalDateTime));
            notifications.Notify(new PhoneNotification(FishingCatalog.AppId, title, body, DateTime.Now, Accent,
                VoyageGroupKey));
        }

        return dirty;
    }

    private string VoyageTitle(FishingVoyageReminder reminder)
    {
        var route = reminder.Route == (int)OceanRoute.Ruby ? OceanRoute.Ruby : OceanRoute.Indigo;
        var boarding = DateTimeOffset.FromUnixTimeSeconds(reminder.BoardingUnix).UtcDateTime;
        GameSchedule.UpcomingOceanVoyages(boarding, route, voyageProbe);
        var slot = voyageProbe[0];
        Span<OceanStop> stops = stackalloc OceanStop[OceanItinerary.StopCount];
        if (!OceanItinerary.TryStops(slot.Destination, slot.Time, stops))
        {
            return Loc.T(L.Timers.OceanFishing);
        }

        var name = catalog.OceanSpotName(stops[^1].SpotId);
        return name.Length > 0 ? name : Loc.T(L.Timers.OceanFishing);
    }

    private void TickFish(long nowUnix, ref FishingIslandStatus island)
    {
        if (catalog.State != FishingCatalogState.Ready)
        {
            return;
        }

        var alarms = configuration.FishingAlarms;
        var entries = catalog.Entries;
        for (var alarmIndex = 0; alarmIndex < alarms.Count; alarmIndex++)
        {
            var itemId = alarms[alarmIndex];
            if (!catalog.TryIndex(itemId, out var index))
            {
                continue;
            }

            var entry = entries[index];
            if (entry.Fish.Rule.AlwaysOpen)
            {
                continue;
            }

            var window = catalog.WindowAt(index);
            if (!window.Exists || nowUnix < window.StartUnix - FishLeadSeconds || nowUnix >= window.EndUnix)
            {
                continue;
            }

            var open = nowUnix >= window.StartUnix;
            Offer(ref island, new FishingIslandStatus(FishingIslandKind.Fish, entry.Name,
                open ? window.EndUnix : window.StartUnix, open));
            if (notifiedWindowStart.TryGetValue(itemId, out var notified) && notified == window.StartUnix)
            {
                continue;
            }

            notifiedWindowStart[itemId] = window.StartUnix;
            var place = entry.SpotName.Length > 0 ? entry.SpotName : entry.ZoneName;
            var body = open
                ? Loc.T(L.Fishing.WindowOpenBody, place)
                : Loc.T(L.Fishing.WindowSoonBody, place,
                    TimeText.Clock(DateTimeOffset.FromUnixTimeSeconds(window.StartUnix).LocalDateTime));
            notifications.Notify(new PhoneNotification(FishingCatalog.AppId, entry.Name, body, DateTime.Now, Accent,
                string.Concat(FishGroupPrefix, itemId.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        }
    }

    private static void Offer(ref FishingIslandStatus current, in FishingIslandStatus candidate)
    {
        if (current.Kind == FishingIslandKind.None || candidate.TargetUnix < current.TargetUnix)
        {
            current = candidate;
        }
    }

    private unsafe void RefreshCaught()
    {
        var now = Environment.TickCount64;
        if (now - lastCaughtRefresh < CaughtRefreshMilliseconds)
        {
            return;
        }

        lastCaughtRefresh = now;
        var playerState = clientState.IsLoggedIn ? PlayerState.Instance() : null;
        if (playerState == null || !playerState->IsLoaded)
        {
            if (CaughtKnown)
            {
                CaughtKnown = false;
                caughtItems.Clear();
                CaughtVersion++;
            }

            return;
        }

        var changed = !CaughtKnown;
        if (catalog.State == FishingCatalogState.Ready)
        {
            var entries = catalog.Entries;
            for (var index = 0; index < entries.Length; index++)
            {
                changed |= Track(playerState, entries[index].ItemId, entries[index].LogRow);
            }
        }

        var blueFish = OceanItinerary.AllBlueFish;
        for (var index = 0; index < blueFish.Length; index++)
        {
            var itemId = blueFish[index].ItemId;
            changed |= Track(playerState, itemId, catalog.LogRow(itemId));
        }

        CaughtKnown = true;
        if (changed)
        {
            CaughtVersion++;
        }
    }

    private unsafe bool Track(PlayerState* playerState, uint itemId, uint logRow)
    {
        if (logRow == 0)
        {
            return false;
        }

        var caught = playerState->IsFishCaught(logRow);
        return caught ? caughtItems.Add(itemId) : caughtItems.Remove(itemId);
    }
}
