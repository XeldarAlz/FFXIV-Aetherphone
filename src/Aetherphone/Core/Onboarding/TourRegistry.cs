using Aetherphone.Core.Analytics;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static class TourRegistry
{
    public const string WelcomeId = "welcome";

    private static readonly GuideSequence Welcome = new(WelcomeId, 5, null,
        new[]
        {
            GuideStep.Page(L.Onboarding.WelcomeTitle, L.Onboarding.WelcomeBody, L.Onboarding.Continue),
            GuideStep.Page(L.Onboarding.FeedbackTitle, L.Onboarding.FeedbackBody, L.Onboarding.GetStarted),
            GuideStep.Point(L.Onboarding.WidgetTourTitle, L.Onboarding.WidgetTourBody, "home.widget"),
            GuideStep.Tap(L.Onboarding.FriendsTourTitle, L.Onboarding.FriendsTourBody, "home.app.friends",
                static nav => nav.Open("friends", AppOpenSource.Onboarding)),
            GuideStep.Point(L.Onboarding.MyNumberTourTitle, L.Onboarding.MyNumberTourBody, "friends.mynumber"),
            GuideStep.Tap(L.Onboarding.ReturnHomeTitle, L.Onboarding.ReturnHomeBody, "chrome.home",
                static nav => nav.GoHome()),
            GuideStep.Note(L.Onboarding.CustomizeTitle, L.Onboarding.CustomizeBody),
            GuideStep.Point(L.Onboarding.ControlCenterTitle, L.Onboarding.ControlCenterBody, "chrome.controlcenter"),
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
        Add(tours, "messages", 1, new[] { GuideStep.Note(L.Onboarding.MessagesTitle, L.Onboarding.MessagesBody), });
        Add(tours, "skywatcher", 1,
            new[]
            {
                GuideStep.Note(L.Onboarding.SkywatcherTitle, L.Onboarding.SkywatcherBody),
                GuideStep.Point(L.Onboarding.SkywatcherForecastTitle, L.Onboarding.SkywatcherForecastBody,
                    "skywatcher.forecast"),
            });
        Add(tours, "market", 1,
            new[]
            {
                GuideStep.Note(L.Onboarding.MarketTitle, L.Onboarding.MarketBody),
                GuideStep.Note(L.Onboarding.MarketStatsTitle, L.Onboarding.MarketStatsBody),
            });
        Add(tours, "venues", 1, new[] { GuideStep.Note(L.Onboarding.VenuesTitle, L.Onboarding.VenuesBody), });
        Add(tours, "music", 1,
            new[]
            {
                GuideStep.Note(L.Onboarding.MusicTitle, L.Onboarding.MusicBody),
                GuideStep.Note(L.Onboarding.MusicNowPlayingTitle, L.Onboarding.MusicNowPlayingBody),
            });
        Add(tours, "games", 1, new[] { GuideStep.Note(L.Onboarding.GamesTitle, L.Onboarding.GamesBody), });
        Add(tours, "camera", 1, new[] { GuideStep.Note(L.Onboarding.CameraTitle, L.Onboarding.CameraBody), });
        Add(tours, "photos", 1, new[] { GuideStep.Note(L.Onboarding.PhotosTitle, L.Onboarding.PhotosBody), });
        Add(tours, "settings", 1, new[] { GuideStep.Note(L.Onboarding.SettingsTitle, L.Onboarding.SettingsBody), });
        Add(tours, "contacts", 1, new[] { GuideStep.Note(L.Apps.Contacts, L.Onboarding.ContactsBody), });
        Add(tours, "character", 1, new[] { GuideStep.Note(L.Apps.Character, L.Onboarding.CharacterBody), });
        Add(tours, "chirper", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Chirper, L.Onboarding.ChirperBody),
                GuideStep.Note(L.Onboarding.ChirperPostTitle, L.Onboarding.ChirperPostBody),
                GuideStep.Note(L.Onboarding.ChirperKindTitle, L.Onboarding.ChirperKindBody),
            });
        Add(tours, "aethergram", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Aethergram, L.Onboarding.AethergramBody),
                GuideStep.Note(L.Onboarding.AethergramShareTitle, L.Onboarding.AethergramShareBody),
                GuideStep.Note(L.Onboarding.AethergramSafeTitle, L.Onboarding.AethergramSafeBody),
                GuideStep.Note(L.Onboarding.AethergramKindTitle, L.Onboarding.AethergramKindBody),
            });
        Add(tours, "maps", 1, new[] { GuideStep.Note(L.Apps.Maps, L.Onboarding.MapsBody), });
        Add(tours, "findpeople", 1, new[] { GuideStep.Note(L.Apps.FindPeople, L.Onboarding.FindPeopleBody), });
        Add(tours, "news", 1, new[] { GuideStep.Note(L.Apps.News, L.Onboarding.NewsBody), });
        Add(tours, "collections", 1, new[] { GuideStep.Note(L.Apps.Collections, L.Onboarding.CollectionsBody), });
        Add(tours, "wallet", 1, new[] { GuideStep.Note(L.Apps.Wallet, L.Onboarding.WalletBody), });
        Add(tours, "inventory", 1, new[] { GuideStep.Note(L.Apps.Inventory, L.Onboarding.InventoryBody), });
        Add(tours, "clock", 1, new[] { GuideStep.Note(L.Apps.Clock, L.Onboarding.ClockBody), });
        Add(tours, "calendar", 1,
            new[]
            {
                GuideStep.Note(L.Calendar.Title, L.Onboarding.CalendarBody),
                GuideStep.Note(L.Calendar.NewEvent, L.Onboarding.CalendarAddBody),
            });
        Add(tours, "notes", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Notes, L.Onboarding.NotesBody),
                GuideStep.Note(L.Notes.TabReminders, L.Onboarding.NotesRemindersBody),
            });
        Add(tours, "calculator", 1, new[] { GuideStep.Note(L.Apps.Calculator, L.Onboarding.CalculatorBody), });
        Add(tours, "timers", 1, new[] { GuideStep.Note(L.Apps.Timers, L.Onboarding.TimersBody), });
        Add(tours, "dailies", 1, new[] { GuideStep.Note(L.Apps.Dailies, L.Onboarding.DailiesBody), });
        Add(tours, "fishing", 1, new[] { GuideStep.Note(L.Apps.Fishing, L.Onboarding.FishingBody), });
        Add(tours, "notifications", 1, new[] { GuideStep.Note(L.Apps.Notifications, L.Onboarding.NotificationsBody), });
        Add(tours, "phone", 1,
            new[]
            {
                GuideStep.Note(L.Phone.Title, L.Onboarding.PhoneBody),
                GuideStep.Note(L.Onboarding.PhoneGroupTitle, L.Onboarding.PhoneGroupBody),
                GuideStep.Note(L.Onboarding.PhoneVoiceTitle, L.Onboarding.PhoneVoiceBody),
            });
        Add(tours, "velvet", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Velvet, L.Onboarding.VelvetBody),
                GuideStep.Note(L.Onboarding.VelvetDiscoverTitle, L.Onboarding.VelvetDiscoverBody),
                GuideStep.Note(L.Onboarding.VelvetMessagesTitle, L.Onboarding.VelvetMessagesBody),
                GuideStep.Note(L.Onboarding.VelvetProfileTitle, L.Onboarding.VelvetProfileBody),
                GuideStep.Note(L.Onboarding.VelvetKindTitle, L.Onboarding.VelvetKindBody),
            });
        Add(tours, "feedback", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Feedback, L.Onboarding.FeedbackIntroBody),
                GuideStep.Note(L.Onboarding.FeedbackWriteTitle, L.Onboarding.FeedbackWriteBody),
                GuideStep.Note(L.Onboarding.FeedbackPrivacyTitle, L.Onboarding.FeedbackPrivacyBody),
            });
        Add(tours, "polls", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Polls, L.Onboarding.PollsBody),
                GuideStep.Note(L.Onboarding.PollsResultsTitle, L.Onboarding.PollsResultsBody),
            });
        return tours;
    }

    private static void Add(Dictionary<string, GuideSequence> tours, string appId, int version, GuideStep[] steps) =>
        tours[appId] = new GuideSequence(appId, version, appId, steps);
}
