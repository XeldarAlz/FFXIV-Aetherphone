using Aetherphone.Core.Analytics;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static class TourRegistry
{
    public const string WelcomeId = "welcome";
    public const string ControlCenterOpenIntent = "chrome.controlcenter.open";
    public const string ControlCenterCloseIntent = "chrome.controlcenter.close";

    private static readonly GuideSequence Welcome = new(WelcomeId, 6, null,
        new[]
        {
            GuideStep.Page(L.Onboarding.HomeTourTitle, L.Onboarding.HomeTourBody, L.Onboarding.Continue),
            GuideStep.Point(L.Onboarding.AppsTourTitle, L.Onboarding.AppsTourBody, "home.app.message"),
            GuideStep.Point(L.Onboarding.WidgetTourTitle, L.Onboarding.WidgetTourBody, "home.widget"),
            GuideStep.Note(L.Onboarding.CustomizeTitle, L.Onboarding.CustomizeBody),
            GuideStep.Tap(L.Onboarding.ControlCenterTitle, L.Onboarding.ControlCenterTapBody, "chrome.controlcenter",
                ControlCenterOpenIntent),
            GuideStep.ControlCenterNote(L.Onboarding.ControlCenterInsideTitle, L.Onboarding.ControlCenterInsideBody,
                ControlCenterCloseIntent),
            GuideStep.Point(L.Onboarding.SignalTourTitle, L.Onboarding.SignalTourBody, "chrome.signal"),
            GuideStep.Point(L.Onboarding.BatteryTourTitle, L.Onboarding.BatteryTourBody, "chrome.battery"),
            GuideStep.Point(L.Onboarding.MinimizeTitle, L.Onboarding.MinimizeBody, "chrome.minimize"),
            GuideStep.Point(L.Onboarding.LockTitle, L.Onboarding.LockBody, "chrome.lock"),
        });

    private static readonly Dictionary<string, GuideSequence> Tours = BuildTours();
    public static GuideSequence GetWelcome() => Welcome;

    public static bool TryGetAppTour(string appId, out GuideSequence sequence) =>
        Tours.TryGetValue(appId, out sequence);

    private static Dictionary<string, GuideSequence> BuildTours()
    {
        var tours = new Dictionary<string, GuideSequence>();
        Add(tours, "messages", 3,
            new[]
            {
                GuideStep.Note(L.Onboarding.MessagesTitle, L.Onboarding.MessagesBody),
                GuideStep.Point(L.Onboarding.MessagesListTitle, L.Onboarding.MessagesListBody, "messages.list"),
                GuideStep.Tap(L.Onboarding.MessagesLinkshellsTitle, L.Onboarding.MessagesLinkshellsBody,
                    "messages.tabs", "messages.tab.linkshells"),
                GuideStep.Tap(L.Apps.Contacts, L.Onboarding.ContactsBody, "messages.tab.contacts",
                    "messages.tab.contacts"),
                GuideStep.Point(L.Onboarding.ContactsListTitle, L.Onboarding.ContactsListBody, "contacts.list"),
                GuideStep.Point(L.Onboarding.ContactsSearchTitle, L.Onboarding.ContactsSearchBody, "contacts.search"),
                GuideStep.Tap(L.Apps.FindPeople, L.Onboarding.FindPeopleBody, "messages.tab.find",
                    "messages.tab.find"),
                GuideStep.Point(L.Onboarding.FindPeopleSearchTitle, L.Onboarding.FindPeopleSearchBody,
                    "findpeople.name"),
                GuideStep.Point(L.Onboarding.FindPeopleKindTitle, L.Onboarding.FindPeopleKindBody, "findpeople.kind"),
            });
        Add(tours, "skywatcher", 2,
            new[]
            {
                GuideStep.Note(L.Onboarding.SkywatcherTitle, L.Onboarding.SkywatcherBody),
                GuideStep.Point(L.Onboarding.SkywatcherCurrentTitle, L.Onboarding.SkywatcherCurrentBody,
                    "skywatcher.current"),
                GuideStep.Point(L.Onboarding.SkywatcherForecastTitle, L.Onboarding.SkywatcherForecastBody,
                    "skywatcher.forecast"),
            });
        Add(tours, "market", 2,
            new[]
            {
                GuideStep.Note(L.Onboarding.MarketTitle, L.Onboarding.MarketBody),
                GuideStep.Point(L.Onboarding.MarketSearchTitle, L.Onboarding.MarketSearchBody, "market.search"),
                GuideStep.Point(L.Onboarding.MarketScopeTitle, L.Onboarding.MarketScopeBody, "market.scope"),
                GuideStep.Note(L.Onboarding.MarketStatsTitle, L.Onboarding.MarketStatsBody),
            });
        Add(tours, "venues", 2,
            new[]
            {
                GuideStep.Note(L.Onboarding.VenuesTitle, L.Onboarding.VenuesBody),
                GuideStep.Point(L.Onboarding.VenuesTimeTitle, L.Onboarding.VenuesTimeBody, "venues.time"),
                GuideStep.Point(L.Onboarding.VenuesFilterTitle, L.Onboarding.VenuesFilterBody, "venues.chips"),
                GuideStep.Point(L.Onboarding.VenuesSearchTitle, L.Onboarding.VenuesSearchBody, "venues.search"),
            });
        Add(tours, "music", 2,
            new[]
            {
                GuideStep.Note(L.Onboarding.MusicTitle, L.Onboarding.MusicBody),
                GuideStep.Point(L.Onboarding.MusicSearchTitle, L.Onboarding.MusicSearchBody, "music.search"),
                GuideStep.Point(L.Onboarding.MusicRadioTitle, L.Onboarding.MusicRadioBody, "music.categories"),
                GuideStep.Note(L.Onboarding.MusicNowPlayingTitle, L.Onboarding.MusicNowPlayingBody),
            });
        Add(tours, "games", 2,
            new[]
            {
                GuideStep.Note(L.Onboarding.GamesTitle, L.Onboarding.GamesBody),
                GuideStep.Point(L.Onboarding.GamesFeaturedTitle, L.Onboarding.GamesFeaturedBody, "games.featured"),
                GuideStep.Point(L.Onboarding.GamesLibraryTitle, L.Onboarding.GamesLibraryBody, "games.library"),
            });
        Add(tours, "camera", 2,
            new[]
            {
                GuideStep.Note(L.Onboarding.CameraTitle, L.Onboarding.CameraBody),
                GuideStep.Point(L.Onboarding.CameraModesTitle, L.Onboarding.CameraModesBody, "camera.modes"),
                GuideStep.Point(L.Onboarding.CameraFlashTitle, L.Onboarding.CameraFlashBody, "camera.flash"),
                GuideStep.Point(L.Onboarding.CameraShutterTitle, L.Onboarding.CameraShutterBody, "camera.shutter"),
            });
        Add(tours, "photos", 2,
            new[]
            {
                GuideStep.Point(L.Onboarding.PhotosTitle, L.Onboarding.PhotosBody, "photos.grid"),
                GuideStep.Note(L.Onboarding.PhotosEmptyTitle, L.Onboarding.PhotosEmptyBody),
            });
        Add(tours, "settings", 2,
            new[]
            {
                GuideStep.Note(L.Onboarding.SettingsTitle, L.Onboarding.SettingsBody),
                GuideStep.Point(L.Onboarding.SettingsAccountTitle, L.Onboarding.SettingsAccountBody,
                    "settings.account"),
                GuideStep.Point(L.Onboarding.SettingsAppearanceTitle, L.Onboarding.SettingsAppearanceBody,
                    "settings.row.appearance"),
                GuideStep.Point(L.Onboarding.SettingsTutorialsTitle, L.Onboarding.SettingsTutorialsBody,
                    "settings.row.tutorials"),
            });
        Add(tours, "character", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Character, L.Onboarding.CharacterBody),
                GuideStep.Point(L.Onboarding.CharacterRingsTitle, L.Onboarding.CharacterRingsBody, "character.rings"),
                GuideStep.Point(L.Onboarding.CharacterSummaryTitle, L.Onboarding.CharacterSummaryBody,
                    "character.summary"),
            });
        Add(tours, "chirper", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Chirper, L.Onboarding.ChirperBody),
                GuideStep.Point(L.Onboarding.ChirperTabsTitle, L.Onboarding.ChirperTabsBody, "chirper.tabs"),
                GuideStep.Point(L.Onboarding.ChirperPostTitle, L.Onboarding.ChirperPostBody, "chirper.compose"),
                GuideStep.Point(L.Onboarding.ChirperSearchTitle, L.Onboarding.ChirperSearchBody, "chirper.search"),
                GuideStep.Point(L.Onboarding.ChirperActivityTitle, L.Onboarding.ChirperActivityBody,
                    "chirper.activity"),
                GuideStep.Note(L.Onboarding.ChirperKindTitle, L.Onboarding.ChirperKindBody),
            });
        Add(tours, "aethergram", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Aethergram, L.Onboarding.AethergramBody),
                GuideStep.Point(L.Onboarding.AethergramShareTitle, L.Onboarding.AethergramShareBody,
                    "aethergram.compose"),
                GuideStep.Tap(L.Onboarding.AethergramSearchTitle, L.Onboarding.AethergramSearchBody,
                    "aethergram.tab.search", "aethergram.tab.search"),
                GuideStep.Point(L.Onboarding.AethergramActivityTitle, L.Onboarding.AethergramActivityBody,
                    "aethergram.tab.activity"),
                GuideStep.Tap(L.Onboarding.AethergramProfileTitle, L.Onboarding.AethergramProfileBody,
                    "aethergram.tab.profile", "aethergram.tab.profile"),
                GuideStep.Note(L.Onboarding.AethergramSafeTitle, L.Onboarding.AethergramSafeBody),
                GuideStep.Note(L.Onboarding.AethergramKindTitle, L.Onboarding.AethergramKindBody),
            });
        Add(tours, "maps", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Maps, L.Onboarding.MapsBody),
                GuideStep.Point(L.Onboarding.MapsLocationTitle, L.Onboarding.MapsLocationBody, "maps.location"),
                GuideStep.Point(L.Onboarding.MapsSearchTitle, L.Onboarding.MapsSearchBody, "maps.search"),
                GuideStep.Note(L.Onboarding.MapsStarTitle, L.Onboarding.MapsStarBody),
            });
        Add(tours, "news", 2,
            new[]
            {
                GuideStep.Note(L.Apps.News, L.Onboarding.NewsBody),
                GuideStep.Point(L.Onboarding.NewsCategoriesTitle, L.Onboarding.NewsCategoriesBody, "news.categories"),
                GuideStep.Point(L.Onboarding.NewsReadTitle, L.Onboarding.NewsReadBody, "news.feed"),
                GuideStep.Point(L.Onboarding.NewsRefreshTitle, L.Onboarding.NewsRefreshBody, "news.refresh"),
            });
        Add(tours, "collections", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Collections, L.Onboarding.CollectionsBody),
                GuideStep.Tap(L.Onboarding.CollectionsCategoryTitle, L.Onboarding.CollectionsCategoryBody,
                    "collections.tile.mounts", "collections.category.mounts"),
                GuideStep.Point(L.Onboarding.CollectionsSearchTitle, L.Onboarding.CollectionsSearchBody,
                    "collections.search"),
                GuideStep.Point(L.Onboarding.CollectionsMissingTitle, L.Onboarding.CollectionsMissingBody,
                    "collections.filters"),
            });
        Add(tours, "wallet", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Wallet, L.Onboarding.WalletBody),
                GuideStep.Point(L.Onboarding.WalletGilTitle, L.Onboarding.WalletGilBody, "wallet.gil"),
                GuideStep.Point(L.Onboarding.WalletCurrenciesTitle, L.Onboarding.WalletCurrenciesBody,
                    "wallet.currencies"),
            });
        Add(tours, "inventory", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Inventory, L.Onboarding.InventoryBody),
                GuideStep.Point(L.Onboarding.InventorySummaryTitle, L.Onboarding.InventorySummaryBody,
                    "inventory.summary"),
                GuideStep.Point(L.Onboarding.InventorySourcesTitle, L.Onboarding.InventorySourcesBody,
                    "inventory.sources"),
                GuideStep.Point(L.Onboarding.InventorySearchTitle, L.Onboarding.InventorySearchBody,
                    "inventory.search"),
            });
        Add(tours, "clock", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Clock, L.Onboarding.ClockIntroBody),
                GuideStep.Tap(L.Onboarding.ClockTabsTitle, L.Onboarding.ClockTabsBody, "clock.tabs",
                    "clock.tab.alarms"),
                GuideStep.Point(L.Onboarding.ClockAddTitle, L.Onboarding.ClockAddBody, "clock.add"),
            });
        Add(tours, "calendar", 2,
            new[]
            {
                GuideStep.Point(L.Calendar.Title, L.Onboarding.CalendarBody, "calendar.grid"),
                GuideStep.Point(L.Onboarding.CalendarAgendaTitle, L.Onboarding.CalendarAgendaBody, "calendar.agenda"),
                GuideStep.Point(L.Calendar.NewEvent, L.Onboarding.CalendarAddBody, "calendar.new"),
            });
        Add(tours, "notes", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Notes, L.Onboarding.NotesBody),
                GuideStep.Point(L.Onboarding.NotesNewTitle, L.Onboarding.NotesNewBody, "notes.new"),
                GuideStep.Tap(L.Notes.TabReminders, L.Onboarding.NotesRemindersBody, "notes.tab.reminders",
                    "notes.tab.reminders"),
                GuideStep.Point(L.Onboarding.NotesReminderTitle, L.Onboarding.NotesReminderBody, "notes.new"),
            });
        Add(tours, "calculator", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Calculator, L.Onboarding.CalculatorBody),
                GuideStep.Point(L.Onboarding.CalculatorTapeTitle, L.Onboarding.CalculatorTapeBody,
                    "calculator.display"),
            });
        Add(tours, "timers", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Timers, L.Onboarding.TimersBody),
                GuideStep.Point(L.Onboarding.TimersResetsTitle, L.Onboarding.TimersResetsBody, "timers.resets"),
                GuideStep.Point(L.Onboarding.TimersRemindersTitle, L.Onboarding.TimersRemindersBody,
                    "timers.reminders"),
            });
        Add(tours, "dailies", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Dailies, L.Onboarding.DailiesBody),
                GuideStep.Tap(L.Onboarding.DailiesCadenceTitle, L.Onboarding.DailiesCadenceBody, "dailies.cadence",
                    "dailies.tab.weekly"),
                GuideStep.Point(L.Onboarding.DailiesNotifyTitle, L.Onboarding.DailiesNotifyBody, "dailies.notify"),
            });
        Add(tours, "fishing", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Fishing, L.Onboarding.FishingBody),
                GuideStep.Point(L.Onboarding.FishingHeroTitle, L.Onboarding.FishingHeroBody, "fishing.hero"),
                GuideStep.Point(L.Onboarding.FishingBlueTitle, L.Onboarding.FishingBlueBody, "fishing.bluefish"),
                GuideStep.Point(L.Onboarding.FishingUpcomingTitle, L.Onboarding.FishingUpcomingBody,
                    "fishing.upcoming"),
            });
        Add(tours, "notifications", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Notifications, L.Onboarding.NotificationsBody),
                GuideStep.Point(L.Onboarding.NotificationsHistoryTitle, L.Onboarding.NotificationsHistoryBody,
                    "notifications.list"),
            });
        Add(tours, "message", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Message, L.Onboarding.MessageBody),
                GuideStep.Tap(L.Onboarding.MessageCallsTitle, L.Onboarding.PhoneBody, "message.tab.calls",
                    "message.tab.calls"),
                GuideStep.Note(L.Onboarding.PhoneGroupTitle, L.Onboarding.PhoneGroupBody),
                GuideStep.Tap(L.Onboarding.MessageContactsTitle, L.Onboarding.MessageContactsBody,
                    "message.tab.contacts", "message.tab.contacts"),
                GuideStep.Point(L.Onboarding.MyNumberTourTitle, L.Onboarding.MessageNumberCopyBody,
                    "message.mynumber"),
                GuideStep.Point(L.Onboarding.MessageAddFriendTitle, L.Onboarding.MessageAddFriendBody,
                    "message.addcontact"),
                GuideStep.Note(L.Onboarding.PhoneVoiceTitle, L.Onboarding.PhoneVoiceBody),
            });
        Add(tours, "velvet", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Velvet, L.Onboarding.VelvetBody),
                GuideStep.Point(L.Onboarding.VelvetComposeTitle, L.Onboarding.VelvetComposeBody, "velvet.compose"),
                GuideStep.Tap(L.Onboarding.VelvetDiscoverTitle, L.Onboarding.VelvetDiscoverBody, "velvet.tab.discover",
                    "velvet.tab.discover"),
                GuideStep.Point(L.Onboarding.VelvetMessagesTitle, L.Onboarding.VelvetMessagesBody, "velvet.messages"),
                GuideStep.Tap(L.Onboarding.VelvetProfileTitle, L.Onboarding.VelvetProfileBody, "velvet.tab.me",
                    "velvet.tab.me"),
                GuideStep.Note(L.Onboarding.VelvetKindTitle, L.Onboarding.VelvetKindBody),
            });
        Add(tours, "feedback", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Feedback, L.Onboarding.FeedbackIntroBody),
                GuideStep.Point(L.Onboarding.FeedbackWriteTitle, L.Onboarding.FeedbackWriteBody, "feedback.input"),
                GuideStep.Point(L.Onboarding.FeedbackSendTitle, L.Onboarding.FeedbackSendBody, "feedback.send"),
                GuideStep.Note(L.Onboarding.FeedbackPrivacyTitle, L.Onboarding.FeedbackPrivacyBody),
            });
        Add(tours, "polls", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Polls, L.Onboarding.PollsBody),
                GuideStep.Point(L.Onboarding.PollsVoteTitle, L.Onboarding.PollsVoteBody, "polls.card"),
                GuideStep.Point(L.Onboarding.PollsResultsTitle, L.Onboarding.PollsResultsBody, "polls.card"),
            });
        return tours;
    }

    private static void Add(Dictionary<string, GuideSequence> tours, string appId, int version, GuideStep[] steps) =>
        tours[appId] = new GuideSequence(appId, version, appId, steps);
}
