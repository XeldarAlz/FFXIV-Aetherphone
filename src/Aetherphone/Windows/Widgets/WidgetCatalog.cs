using Aetherphone.Apps.Activity.Widgets;
using Aetherphone.Apps.AetherStream.Widgets;
using Aetherphone.Apps.Calendar;
using Aetherphone.Apps.Calendar.Widgets;
using Aetherphone.Apps.Casino.Widgets;
using Aetherphone.Apps.Clock.Widgets;
using Aetherphone.Apps.Coin.Widgets;
using Aetherphone.Apps.Dailies.Widgets;
using Aetherphone.Apps.Fishing.Widgets;
using Aetherphone.Apps.Games.Widgets;
using Aetherphone.Apps.Health.Widgets;
using Aetherphone.Apps.Housing.Widgets;
using Aetherphone.Apps.Hunts.Widgets;
using Aetherphone.Apps.Jobs.Widgets;
using Aetherphone.Apps.Linkpearl.Widgets;
using Aetherphone.Apps.Maps.Widgets;
using Aetherphone.Apps.Market.Widgets;
using Aetherphone.Apps.Message;
using Aetherphone.Apps.Message.Widgets;
using Aetherphone.Apps.Music.Widgets;
using Aetherphone.Apps.Muster.Widgets;
using Aetherphone.Apps.News.Widgets;
using Aetherphone.Apps.Notes.Widgets;
using Aetherphone.Apps.Notifications.Widgets;
using Aetherphone.Apps.Photos.Widgets;
using Aetherphone.Apps.Settings.Widgets;
using Aetherphone.Apps.Shortcuts.Widgets;
using Aetherphone.Apps.Skywatcher.Widgets;
using Aetherphone.Apps.Timers.Widgets;
using Aetherphone.Apps.Venues.Widgets;
using Aetherphone.Apps.Wallet.Widgets;
using Aetherphone.Core.Home;

namespace Aetherphone.Windows.Widgets;

internal static class WidgetCatalog
{
    public static WidgetRegistry Build(WidgetServices services, CalendarEvents calendarEvents, DirectMessagesStore messages)
    {
        var phone = services.Phone;
        var calendarFeed = new CalendarWidgetFeed(phone.Configuration, calendarEvents);
        var widgets = new List<IHomeWidget>
        {
            new WeatherWidget(phone.Weather),
            new ClockWidget(phone.Configuration),
            new AlarmWidget(phone.Configuration, phone.AlarmRinger),
            new TimerWidget(phone.Configuration, phone.AlarmRinger),
            new UpNextWidget(calendarFeed),
            new MonthWidget(),
            new HalloweenWidget(),
            new FeaturedPhotoWidget(services.Photos),
            new ResetsWidget(phone.GameTimers),
            new VenturesWidget(phone.GameTimers),
            new ActivityWidget(phone.Activity, phone.Configuration),
            new CoinBalanceWidget(phone.Coins, phone.AethernetSession),
            new WeatherWatchWidget(phone.Weather),
            new HydrationWidget(phone.Health),
            new DailiesWidget(phone.Dailies),
            new OceanFishingWidget(phone.Fishing),
            new CurrenciesWidget(phone.Wallet, phone.Activity),
            new JobWidget(phone.GameData),
            new HuntsLiveWidget(phone.Hunts, phone.HuntMobCatalog, phone.Configuration),
            new HousingLotteryWidget(phone.Housing),
            new TeleportWidget(phone.Maps, phone.Configuration),
            new NowPlayingWidget(phone.Playback, phone.PcMedia, phone.MusicLibrary, phone.Media, phone.Http),
            new NowWatchingWidget(services.Video, phone.RemoteImages, phone.Http),
            new PeopleWidget(messages, messages.Contacts, phone.Configuration,
                phone.AethernetSession, phone.RemoteImages),
            new ChatsWidget(messages, phone.Configuration, phone.AethernetSession, phone.RemoteImages),
            new TellsWidget(phone.ChatInbox, phone.ChatLog, phone.Lodestone),
            new RecentNotificationsWidget(phone.Notifications),
        };

        AddUtility(widgets, services);

        return new WidgetRegistry(widgets, services.Apps);
    }

    private static void AddUtility(List<IHomeWidget> widgets, WidgetServices services)
    {
        var phone = services.Phone;
        widgets.Add(new NoteWidget(phone.Configuration));
        widgets.Add(new RemindersWidget(phone.Configuration));
        widgets.Add(new ShortcutsWidget(phone.Shortcuts, phone.ShortcutRunner));
        widgets.Add(new QuickTogglesWidget(phone.Configuration, phone.Themes, phone.Calls));
        widgets.Add(new MusterWidget(phone.Musters));
        widgets.Add(new VenuesWidget(phone.Venues, phone.Configuration, phone.GameData, phone.RemoteImages,
            phone.Artwork));
        widgets.Add(new MarketWatchWidget(phone.MarketAlerts, phone.MarketWatchlist, phone.MarketIndex, phone.Textures));
        widgets.Add(new DailyGameWidget(phone.GameStats));
        widgets.Add(new DailySpinWidget(phone.CasinoSpin, phone.AethernetSession));
        widgets.Add(new LodestoneWidget(phone.News, phone.GameData, phone.RemoteImages));
    }
}
