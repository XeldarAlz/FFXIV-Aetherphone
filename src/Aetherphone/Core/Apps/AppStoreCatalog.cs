using System.Collections.Frozen;
using System.Collections.Immutable;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Interface;

namespace Aetherphone.Core.Apps;

internal enum StoreCategory : byte
{
    Social,
    Community,
    PhotoVideo,
    Entertainment,
    World,
    Character,
    News,
    Productivity,
    Utilities,
}

internal readonly record struct StoreEntry(LocString Name, LocString Subtitle, LocString Body, StoreCategory Category);

internal static class AppStoreCatalog
{
    public static readonly StoreCategory[] Order =
    {
        StoreCategory.Social,
        StoreCategory.Community,
        StoreCategory.PhotoVideo,
        StoreCategory.Entertainment,
        StoreCategory.World,
        StoreCategory.Character,
        StoreCategory.News,
        StoreCategory.Productivity,
        StoreCategory.Utilities,
    };

    private static readonly StoreEntry Fallback =
        new(L.Apps.AppStore, L.StoreCopy.StoreSub, L.StoreCopy.StoreBody, StoreCategory.Utilities);

    private static readonly FrozenDictionary<string, StoreEntry> Entries = new Dictionary<string, StoreEntry>
    {
        ["chirper"] = new(L.Apps.Chirper, L.StoreCopy.ChirperSub, L.StoreCopy.ChirperBody, StoreCategory.Social),
        ["aethergram"] = new(L.Apps.Aethergram, L.StoreCopy.AethergramSub, L.StoreCopy.AethergramBody,
            StoreCategory.Social),
        ["velvet"] = new(L.Apps.Velvet, L.StoreCopy.VelvetSub, L.StoreCopy.VelvetBody, StoreCategory.Social),
        ["message"] = new(L.Apps.Message, L.StoreCopy.MessageSub, L.StoreCopy.MessageBody, StoreCategory.Social),
        ["messages"] = new(L.Apps.Linkpearl, L.StoreCopy.LinkpearlSub, L.StoreCopy.LinkpearlBody,
            StoreCategory.Social),
        ["venues"] = new(L.Apps.Venues, L.StoreCopy.VenuesSub, L.StoreCopy.VenuesBody, StoreCategory.Community),
        ["muster"] = new(L.Apps.Muster, L.StoreCopy.MusterSub, L.StoreCopy.MusterBody, StoreCategory.Community),
        ["yellowpages"] = new(L.Apps.YellowPages, L.StoreCopy.YellowPagesSub, L.StoreCopy.YellowPagesBody,
            StoreCategory.Community),
        ["polls"] = new(L.Apps.Polls, L.StoreCopy.PollsSub, L.StoreCopy.PollsBody, StoreCategory.Community),
        ["camera"] = new(L.Apps.Camera, L.StoreCopy.CameraSub, L.StoreCopy.CameraBody, StoreCategory.PhotoVideo),
        ["photos"] = new(L.Apps.Photos, L.StoreCopy.PhotosSub, L.StoreCopy.PhotosBody, StoreCategory.PhotoVideo),
        ["prism"] = new(L.Apps.Prism, L.StoreCopy.PrismSub, L.StoreCopy.PrismBody, StoreCategory.PhotoVideo),
        ["aetherstream"] = new(L.Apps.AetherStream, L.StoreCopy.AetherStreamSub, L.StoreCopy.AetherStreamBody,
            StoreCategory.PhotoVideo),
        ["music"] = new(L.Apps.Music, L.StoreCopy.MusicSub, L.StoreCopy.MusicBody, StoreCategory.Entertainment),
        ["games"] = new(L.Apps.Games, L.StoreCopy.GamesSub, L.StoreCopy.GamesBody, StoreCategory.Entertainment),
        ["casino"] = new(L.Apps.Casino, L.StoreCopy.CasinoSub, L.StoreCopy.CasinoBody, StoreCategory.Entertainment),
        ["coin"] = new(L.Apps.Coin, L.StoreCopy.CoinSub, L.StoreCopy.CoinBody, StoreCategory.Entertainment),
        ["maps"] = new(L.Apps.Maps, L.StoreCopy.MapsSub, L.StoreCopy.MapsBody, StoreCategory.World),
        ["skywatcher"] = new(L.Apps.Skywatcher, L.StoreCopy.SkywatcherSub, L.StoreCopy.SkywatcherBody,
            StoreCategory.World),
        ["fishing"] = new(L.Apps.Fishing, L.StoreCopy.FishingSub, L.StoreCopy.FishingBody, StoreCategory.World),
        ["hunts"] = new(L.Apps.Hunts, L.StoreCopy.HuntsSub, L.StoreCopy.HuntsBody, StoreCategory.World),
        ["strats"] = new(L.Apps.Strats, L.StoreCopy.StratsSub, L.StoreCopy.StratsBody, StoreCategory.World),
        ["housing"] = new(L.Apps.Housing, L.StoreCopy.HousingSub, L.StoreCopy.HousingBody, StoreCategory.World),
        ["market"] = new(L.Apps.Market, L.StoreCopy.MarketSub, L.StoreCopy.MarketBody, StoreCategory.World),
        ["character"] = new(L.Character.Activity, L.StoreCopy.CharacterSub, L.StoreCopy.CharacterBody,
            StoreCategory.Character),
        ["health"] = new(L.Health.Title, L.StoreCopy.HealthSub, L.StoreCopy.HealthBody, StoreCategory.Character),
        ["jobs"] = new(L.Apps.Jobs, L.StoreCopy.JobsSub, L.StoreCopy.JobsBody, StoreCategory.Character),
        ["collections"] = new(L.Apps.Collections, L.StoreCopy.CollectionsSub, L.StoreCopy.CollectionsBody,
            StoreCategory.Character),
        ["inventory"] = new(L.Apps.Inventory, L.StoreCopy.InventorySub, L.StoreCopy.InventoryBody,
            StoreCategory.Character),
        ["wallet"] = new(L.Apps.Wallet, L.StoreCopy.WalletSub, L.StoreCopy.WalletBody, StoreCategory.Character),
        ["dailies"] = new(L.Apps.Dailies, L.StoreCopy.DailiesSub, L.StoreCopy.DailiesBody, StoreCategory.Character),
        ["news"] = new(L.Apps.News, L.StoreCopy.NewsSub, L.StoreCopy.NewsBody, StoreCategory.News),
        ["announcements"] = new(L.Apps.Announcements, L.StoreCopy.AnnouncementsSub, L.StoreCopy.AnnouncementsBody,
            StoreCategory.News),
        ["notifications"] = new(L.Apps.Notifications, L.StoreCopy.NotificationsSub, L.StoreCopy.NotificationsBody,
            StoreCategory.News),
        ["notes"] = new(L.Apps.Notes, L.StoreCopy.NotesSub, L.StoreCopy.NotesBody, StoreCategory.Productivity),
        ["calendar"] = new(L.Apps.Calendar, L.StoreCopy.CalendarSub, L.StoreCopy.CalendarBody,
            StoreCategory.Productivity),
        ["clock"] = new(L.Apps.Clock, L.StoreCopy.ClockSub, L.StoreCopy.ClockBody, StoreCategory.Productivity),
        ["timers"] = new(L.Apps.Timers, L.StoreCopy.TimersSub, L.StoreCopy.TimersBody, StoreCategory.Productivity),
        ["calculator"] = new(L.Apps.Calculator, L.StoreCopy.CalculatorSub, L.StoreCopy.CalculatorBody,
            StoreCategory.Productivity),
        ["settings"] = new(L.Apps.Settings, L.StoreCopy.SettingsSub, L.StoreCopy.SettingsBody,
            StoreCategory.Utilities),
        ["appstore"] = new(L.Apps.AppStore, L.StoreCopy.StoreSub, L.StoreCopy.StoreBody, StoreCategory.Utilities),
        ["shortcuts"] = new(L.Apps.Shortcuts, L.StoreCopy.ShortcutsSub, L.StoreCopy.ShortcutsBody,
            StoreCategory.Utilities),
        ["feedback"] = new(L.Apps.Feedback, L.StoreCopy.FeedbackSub, L.StoreCopy.FeedbackBody,
            StoreCategory.Utilities),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static ImmutableArray<string> AppIds => Entries.Keys;

    public static StoreEntry For(string appId) => Entries.TryGetValue(appId, out var entry) ? entry : Fallback;

    public static bool TryFor(string appId, out StoreEntry entry) => Entries.TryGetValue(appId, out entry);

    public static Vector4 Tint(StoreCategory category) => category switch
    {
        StoreCategory.Social => AccentRing.Rose,
        StoreCategory.Community => AccentRing.Orange,
        StoreCategory.PhotoVideo => AccentRing.Gold,
        StoreCategory.Entertainment => AccentRing.Violet,
        StoreCategory.World => AccentRing.Emerald,
        StoreCategory.Character => AccentRing.Azure,
        StoreCategory.News => AccentRing.Red,
        StoreCategory.Productivity => AccentRing.Cyan,
        _ => AccentRing.Slate,
    };

    public static FontAwesomeIcon Icon(StoreCategory category) => category switch
    {
        StoreCategory.Social => FontAwesomeIcon.Comments,
        StoreCategory.Community => FontAwesomeIcon.GlassCheers,
        StoreCategory.PhotoVideo => FontAwesomeIcon.Camera,
        StoreCategory.Entertainment => FontAwesomeIcon.Gamepad,
        StoreCategory.World => FontAwesomeIcon.Compass,
        StoreCategory.Character => FontAwesomeIcon.UserCircle,
        StoreCategory.News => FontAwesomeIcon.Newspaper,
        StoreCategory.Productivity => FontAwesomeIcon.CheckSquare,
        _ => FontAwesomeIcon.Wrench,
    };

    public static LocString Name(StoreCategory category) => category switch
    {
        StoreCategory.Social => L.Store.CategorySocial,
        StoreCategory.Community => L.Store.CategoryCommunity,
        StoreCategory.PhotoVideo => L.Store.CategoryCreativity,
        StoreCategory.Entertainment => L.Store.CategoryPlay,
        StoreCategory.World => L.Store.CategoryWorld,
        StoreCategory.Character => L.Store.CategoryCharacter,
        StoreCategory.News => L.Store.CategoryNews,
        StoreCategory.Productivity => L.Store.CategoryWork,
        _ => L.Store.CategoryTools,
    };

    public static bool Matches(string appId, string displayName, string query)
    {
        if (query.Length == 0)
        {
            return false;
        }

        if (displayName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            appId.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var entry = For(appId);
        return Loc.T(entry.Subtitle).Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               Loc.T(entry.Body).Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               Loc.T(Name(entry.Category)).Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }
}
