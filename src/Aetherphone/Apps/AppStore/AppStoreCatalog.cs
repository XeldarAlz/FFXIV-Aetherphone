using System.Collections.Frozen;
using Aetherphone.Core.Localization;
using Dalamud.Interface;

namespace Aetherphone.Apps.AppStore;

internal enum StoreCategory
{
    Social,
    Chat,
    Creativity,
    Play,
    Adventure,
    Work,
    Tools,
}

internal readonly record struct StoreEntry(LocString Subtitle, LocString Body, StoreCategory Category);

internal static class AppStoreCatalog
{
    public static readonly StoreCategory[] Order =
    {
        StoreCategory.Social,
        StoreCategory.Chat,
        StoreCategory.Creativity,
        StoreCategory.Play,
        StoreCategory.Adventure,
        StoreCategory.Work,
        StoreCategory.Tools,
    };

    private static readonly StoreEntry Fallback =
        new(L.StoreCopy.StoreSub, L.StoreCopy.StoreBody, StoreCategory.Tools);

    private static readonly FrozenDictionary<string, StoreEntry> Entries = new Dictionary<string, StoreEntry>
    {
        ["chirper"] = new(L.StoreCopy.ChirperSub, L.StoreCopy.ChirperBody, StoreCategory.Social),
        ["aethergram"] = new(L.StoreCopy.AethergramSub, L.StoreCopy.AethergramBody, StoreCategory.Social),
        ["velvet"] = new(L.StoreCopy.VelvetSub, L.StoreCopy.VelvetBody, StoreCategory.Social),
        ["polls"] = new(L.StoreCopy.PollsSub, L.StoreCopy.PollsBody, StoreCategory.Social),
        ["venues"] = new(L.StoreCopy.VenuesSub, L.StoreCopy.VenuesBody, StoreCategory.Social),
        ["muster"] = new(L.StoreCopy.MusterSub, L.StoreCopy.MusterBody, StoreCategory.Social),
        ["messages"] = new(L.StoreCopy.LinkpearlSub, L.StoreCopy.LinkpearlBody, StoreCategory.Chat),
        ["message"] = new(L.StoreCopy.MessageSub, L.StoreCopy.MessageBody, StoreCategory.Chat),
        ["camera"] = new(L.StoreCopy.CameraSub, L.StoreCopy.CameraBody, StoreCategory.Creativity),
        ["photos"] = new(L.StoreCopy.PhotosSub, L.StoreCopy.PhotosBody, StoreCategory.Creativity),
        ["music"] = new(L.StoreCopy.MusicSub, L.StoreCopy.MusicBody, StoreCategory.Play),
        ["games"] = new(L.StoreCopy.GamesSub, L.StoreCopy.GamesBody, StoreCategory.Play),
        ["news"] = new(L.StoreCopy.NewsSub, L.StoreCopy.NewsBody, StoreCategory.Play),
        ["fishing"] = new(L.StoreCopy.FishingSub, L.StoreCopy.FishingBody, StoreCategory.Adventure),
        ["skywatcher"] = new(L.StoreCopy.SkywatcherSub, L.StoreCopy.SkywatcherBody, StoreCategory.Adventure),
        ["maps"] = new(L.StoreCopy.MapsSub, L.StoreCopy.MapsBody, StoreCategory.Adventure),
        ["collections"] = new(L.StoreCopy.CollectionsSub, L.StoreCopy.CollectionsBody, StoreCategory.Adventure),
        ["inventory"] = new(L.StoreCopy.InventorySub, L.StoreCopy.InventoryBody, StoreCategory.Adventure),
        ["jobs"] = new(L.StoreCopy.JobsSub, L.StoreCopy.JobsBody, StoreCategory.Adventure),
        ["character"] = new(L.StoreCopy.CharacterSub, L.StoreCopy.CharacterBody, StoreCategory.Adventure),
        ["wallet"] = new(L.StoreCopy.WalletSub, L.StoreCopy.WalletBody, StoreCategory.Adventure),
        ["market"] = new(L.StoreCopy.MarketSub, L.StoreCopy.MarketBody, StoreCategory.Adventure),
        ["dailies"] = new(L.StoreCopy.DailiesSub, L.StoreCopy.DailiesBody, StoreCategory.Adventure),
        ["notes"] = new(L.StoreCopy.NotesSub, L.StoreCopy.NotesBody, StoreCategory.Work),
        ["calendar"] = new(L.StoreCopy.CalendarSub, L.StoreCopy.CalendarBody, StoreCategory.Work),
        ["timers"] = new(L.StoreCopy.TimersSub, L.StoreCopy.TimersBody, StoreCategory.Work),
        ["clock"] = new(L.StoreCopy.ClockSub, L.StoreCopy.ClockBody, StoreCategory.Work),
        ["calculator"] = new(L.StoreCopy.CalculatorSub, L.StoreCopy.CalculatorBody, StoreCategory.Work),
        ["settings"] = new(L.StoreCopy.SettingsSub, L.StoreCopy.SettingsBody, StoreCategory.Tools),
        ["notifications"] = new(L.StoreCopy.NotificationsSub, L.StoreCopy.NotificationsBody, StoreCategory.Tools),
        ["feedback"] = new(L.StoreCopy.FeedbackSub, L.StoreCopy.FeedbackBody, StoreCategory.Tools),
        ["dev"] = new(L.StoreCopy.DevSub, L.StoreCopy.DevBody, StoreCategory.Tools),
        ["appstore"] = new(L.StoreCopy.StoreSub, L.StoreCopy.StoreBody, StoreCategory.Tools),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static StoreEntry For(string appId) => Entries.TryGetValue(appId, out var entry) ? entry : Fallback;

    public static Vector4 Tint(StoreCategory category) => category switch
    {
        StoreCategory.Social => new(0.95f, 0.35f, 0.50f, 1f),
        StoreCategory.Chat => new(0.24f, 0.55f, 0.95f, 1f),
        StoreCategory.Creativity => new(0.97f, 0.57f, 0.22f, 1f),
        StoreCategory.Play => new(0.62f, 0.40f, 0.94f, 1f),
        StoreCategory.Adventure => new(0.26f, 0.72f, 0.45f, 1f),
        StoreCategory.Work => new(0.14f, 0.66f, 0.74f, 1f),
        _ => new(0.44f, 0.52f, 0.66f, 1f),
    };

    public static FontAwesomeIcon Icon(StoreCategory category) => category switch
    {
        StoreCategory.Social => FontAwesomeIcon.Users,
        StoreCategory.Chat => FontAwesomeIcon.Comments,
        StoreCategory.Creativity => FontAwesomeIcon.Camera,
        StoreCategory.Play => FontAwesomeIcon.Film,
        StoreCategory.Adventure => FontAwesomeIcon.Compass,
        StoreCategory.Work => FontAwesomeIcon.CheckSquare,
        _ => FontAwesomeIcon.Wrench,
    };

    public static LocString Name(StoreCategory category) => category switch
    {
        StoreCategory.Social => L.Store.CategorySocial,
        StoreCategory.Chat => L.Store.CategoryChat,
        StoreCategory.Creativity => L.Store.CategoryCreativity,
        StoreCategory.Play => L.Store.CategoryPlay,
        StoreCategory.Adventure => L.Store.CategoryAdventure,
        StoreCategory.Work => L.Store.CategoryWork,
        _ => L.Store.CategoryTools,
    };
}
