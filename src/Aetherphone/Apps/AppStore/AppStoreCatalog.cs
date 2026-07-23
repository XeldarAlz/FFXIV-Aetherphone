using System.Collections.Frozen;
using Aetherphone.Core.Localization;

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

internal readonly record struct StoreEntry(LocString Subtitle, LocString Body, StoreCategory Category, bool Adult);

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
        new(L.StoreCopy.StoreSub, L.StoreCopy.StoreBody, StoreCategory.Tools, false);

    private static readonly FrozenDictionary<string, StoreEntry> Entries = new Dictionary<string, StoreEntry>
    {
        ["chirper"] = new(L.StoreCopy.ChirperSub, L.StoreCopy.ChirperBody, StoreCategory.Social, false),
        ["aethergram"] = new(L.StoreCopy.AethergramSub, L.StoreCopy.AethergramBody, StoreCategory.Social, false),
        ["velvet"] = new(L.StoreCopy.VelvetSub, L.StoreCopy.VelvetBody, StoreCategory.Social, true),
        ["polls"] = new(L.StoreCopy.PollsSub, L.StoreCopy.PollsBody, StoreCategory.Social, false),
        ["venues"] = new(L.StoreCopy.VenuesSub, L.StoreCopy.VenuesBody, StoreCategory.Social, false),
        ["messages"] = new(L.StoreCopy.LinkpearlSub, L.StoreCopy.LinkpearlBody, StoreCategory.Chat, false),
        ["message"] = new(L.StoreCopy.MessageSub, L.StoreCopy.MessageBody, StoreCategory.Chat, false),
        ["camera"] = new(L.StoreCopy.CameraSub, L.StoreCopy.CameraBody, StoreCategory.Creativity, false),
        ["photos"] = new(L.StoreCopy.PhotosSub, L.StoreCopy.PhotosBody, StoreCategory.Creativity, false),
        ["music"] = new(L.StoreCopy.MusicSub, L.StoreCopy.MusicBody, StoreCategory.Play, false),
        ["games"] = new(L.StoreCopy.GamesSub, L.StoreCopy.GamesBody, StoreCategory.Play, false),
        ["news"] = new(L.StoreCopy.NewsSub, L.StoreCopy.NewsBody, StoreCategory.Play, false),
        ["fishing"] = new(L.StoreCopy.FishingSub, L.StoreCopy.FishingBody, StoreCategory.Adventure, false),
        ["skywatcher"] = new(L.StoreCopy.SkywatcherSub, L.StoreCopy.SkywatcherBody, StoreCategory.Adventure, false),
        ["maps"] = new(L.StoreCopy.MapsSub, L.StoreCopy.MapsBody, StoreCategory.Adventure, false),
        ["collections"] = new(L.StoreCopy.CollectionsSub, L.StoreCopy.CollectionsBody, StoreCategory.Adventure, false),
        ["inventory"] = new(L.StoreCopy.InventorySub, L.StoreCopy.InventoryBody, StoreCategory.Adventure, false),
        ["jobs"] = new(L.StoreCopy.JobsSub, L.StoreCopy.JobsBody, StoreCategory.Adventure, false),
        ["character"] = new(L.StoreCopy.CharacterSub, L.StoreCopy.CharacterBody, StoreCategory.Adventure, false),
        ["wallet"] = new(L.StoreCopy.WalletSub, L.StoreCopy.WalletBody, StoreCategory.Adventure, false),
        ["market"] = new(L.StoreCopy.MarketSub, L.StoreCopy.MarketBody, StoreCategory.Adventure, false),
        ["dailies"] = new(L.StoreCopy.DailiesSub, L.StoreCopy.DailiesBody, StoreCategory.Adventure, false),
        ["notes"] = new(L.StoreCopy.NotesSub, L.StoreCopy.NotesBody, StoreCategory.Work, false),
        ["calendar"] = new(L.StoreCopy.CalendarSub, L.StoreCopy.CalendarBody, StoreCategory.Work, false),
        ["timers"] = new(L.StoreCopy.TimersSub, L.StoreCopy.TimersBody, StoreCategory.Work, false),
        ["clock"] = new(L.StoreCopy.ClockSub, L.StoreCopy.ClockBody, StoreCategory.Work, false),
        ["calculator"] = new(L.StoreCopy.CalculatorSub, L.StoreCopy.CalculatorBody, StoreCategory.Work, false),
        ["settings"] = new(L.StoreCopy.SettingsSub, L.StoreCopy.SettingsBody, StoreCategory.Tools, false),
        ["notifications"] = new(L.StoreCopy.NotificationsSub, L.StoreCopy.NotificationsBody, StoreCategory.Tools, false),
        ["feedback"] = new(L.StoreCopy.FeedbackSub, L.StoreCopy.FeedbackBody, StoreCategory.Tools, false),
        ["dev"] = new(L.StoreCopy.DevSub, L.StoreCopy.DevBody, StoreCategory.Tools, false),
        ["appstore"] = new(L.StoreCopy.StoreSub, L.StoreCopy.StoreBody, StoreCategory.Tools, false),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static StoreEntry For(string appId) => Entries.TryGetValue(appId, out var entry) ? entry : Fallback;

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
