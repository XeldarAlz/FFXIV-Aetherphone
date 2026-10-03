using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Runtime;
using Aetherphone.Core.Timers;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Notifications;

internal sealed class TimerNotifier : IDisposable
{
    private const long TickIntervalMilliseconds = 1000;
    private const string AppId = "timers";
    private const string ResetsGroup = "timers:resets";
    private const string VenturesGroup = "timers:ventures";
    private const string VoyagesGroup = "timers:voyages";
    private const string EventsGroup = "timers:events";
    private const string MapGroup = "timers:map";
    private static readonly Vector4 Accent = AppAccents.For(AppId);
    private readonly Configuration configuration;
    private readonly FrameworkTicker ticker;
    private readonly NotificationService notifications;
    private readonly GameTimers timers;
    private readonly AppGate gate;
    private long lastUnix;
    private bool gateWasOpen = true;

    public TimerNotifier(Configuration configuration, IFramework framework, NotificationService notifications,
        GameTimers timers, AppGate gate)
    {
        this.configuration = configuration;
        this.notifications = notifications;
        this.timers = timers;
        this.gate = gate;
        lastUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        ticker = new FrameworkTicker(framework, TickIntervalMilliseconds, OnTick);
    }

    public void Dispose()
    {
        ticker.Dispose();
    }

    private void OnTick()
    {
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!gate.Open)
        {
            gateWasOpen = false;
            return;
        }

        if (!gateWasOpen)
        {
            gateWasOpen = true;
            lastUnix = nowUnix;
            return;
        }

        var previousUnix = lastUnix;
        lastUnix = nowUnix;
        if (nowUnix <= previousUnix)
        {
            return;
        }

        var previousUtc = DateTimeOffset.FromUnixTimeSeconds(previousUnix).UtcDateTime;
        CheckSchedule(previousUtc, nowUnix);
        CheckRetainers(previousUnix, nowUnix);
        CheckVoyages(previousUnix, nowUnix);
        CheckMaps(previousUnix, nowUnix);
    }

    private void CheckSchedule(DateTime previousUtc, long nowUnix)
    {
        if (configuration.NotifyDailyReset && Due(GameSchedule.NextDailyReset(previousUtc), nowUnix))
        {
            Notify(Loc.T(L.Timers.DailyReset), Loc.T(L.Timers.ResetNotice), ResetsGroup);
        }

        if (configuration.NotifyGrandCompanyReset && Due(GameSchedule.NextGrandCompanyReset(previousUtc), nowUnix))
        {
            Notify(Loc.T(L.Timers.GrandCompanyReset), Loc.T(L.Timers.ResetNotice), ResetsGroup);
        }

        if (configuration.NotifyWeeklyReset && Due(GameSchedule.NextWeeklyReset(previousUtc), nowUnix))
        {
            Notify(Loc.T(L.Timers.WeeklyReset), Loc.T(L.Timers.ResetNotice), ResetsGroup);
        }

        if (configuration.NotifyFashionReport && Due(GameSchedule.NextFashionReportOpen(previousUtc), nowUnix))
        {
            Notify(Loc.T(L.Timers.FashionReport), Loc.T(L.Timers.FashionNotice), EventsGroup);
        }

        if (configuration.NotifyJumboCactpot &&
            Due(GameSchedule.NextJumboCactpot(previousUtc, timers.RegionCode), nowUnix))
        {
            Notify(Loc.T(L.Timers.JumboCactpot), Loc.T(L.Timers.CactpotNotice), EventsGroup);
        }
    }

    private void CheckRetainers(long previousUnix, long nowUnix)
    {
        if (!configuration.NotifyRetainerVentures)
        {
            return;
        }

        var characters = timers.Characters;
        for (var characterIndex = 0; characterIndex < characters.Count; characterIndex++)
        {
            var retainers = characters[characterIndex].Retainers;
            for (var index = 0; index < retainers.Count; index++)
            {
                var retainer = retainers[index];
                if (TimerLedger.Crossed(previousUnix, nowUnix, retainer.CompleteUnix))
                {
                    Notify(retainer.Name, Loc.T(L.Timers.VentureComplete), VenturesGroup);
                }
            }
        }
    }

    private void CheckVoyages(long previousUnix, long nowUnix)
    {
        if (!configuration.NotifyVoyages)
        {
            return;
        }

        var workshops = timers.Workshops;
        for (var workshopIndex = 0; workshopIndex < workshops.Count; workshopIndex++)
        {
            var vessels = workshops[workshopIndex].Vessels;
            for (var index = 0; index < vessels.Count; index++)
            {
                var vessel = vessels[index];
                if (TimerLedger.Crossed(previousUnix, nowUnix, vessel.ReturnUnix))
                {
                    Notify(vessel.Name, Loc.T(L.Timers.VoyageComplete), VoyagesGroup);
                }
            }
        }
    }

    private void CheckMaps(long previousUnix, long nowUnix)
    {
        if (!configuration.NotifyMapAllowance)
        {
            return;
        }

        var characters = timers.Characters;
        for (var index = 0; index < characters.Count; index++)
        {
            var character = characters[index];
            if (TimerLedger.Crossed(previousUnix, nowUnix, character.MapAllowanceUnix))
            {
                Notify(Loc.T(L.Timers.TreasureMap), Loc.T(L.Timers.MapNotice, character.Name), MapGroup);
            }
        }
    }

    private static bool Due(DateTime momentUtc, long nowUnix) =>
        new DateTimeOffset(momentUtc, TimeSpan.Zero).ToUnixTimeSeconds() <= nowUnix;

    private void Notify(string title, string body, string group)
    {
        notifications.Notify(new PhoneNotification(AppId, title, body, DateTime.Now, Accent, group));
    }
}
