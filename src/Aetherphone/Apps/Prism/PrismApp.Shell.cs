using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Prism;

internal enum PrismTab : byte
{
    Discover,
    Week,
    Wardrobe,
    Me,
}

internal readonly record struct PrismStop(
    PrismScreen Screen,
    GlamDto? Glam,
    GlamAuthorDto? Author,
    bool Saved,
    bool Placings,
    GlamListKind Kind,
    uint ItemId,
    GlamModDto? Mod,
    string Tag,
    string Title,
    string OutfitId,
    WeekGallery Gallery,
    float Scroll);

internal sealed partial class PrismApp
{
    private const float WearingBarUnits = 44f;
    private const float TabBarUnits = 44f;
    private const int PostSlot = 2;
    private const float WearingGapUnits = 8f;
    private const int HistoryDepth = 12;
    private const float KeepScroll = -1f;
    private const float RestoreWaitSeconds = 3f;

    private static readonly PrismScreen[] TabRoots =
    {
        PrismScreen.Discover, PrismScreen.Cover, PrismScreen.Wardrobe, PrismScreen.Me,
    };

    private readonly PrismStop[] history = new PrismStop[HistoryDepth];
    private readonly TabBar tabBar = new() { Height = TabBarUnits, SideInset = 0f };
    private readonly TabItem[] tabItems = new TabItem[TabRoots.Length + 1];
    private readonly AppSkin ui = new(AppPalettes.Neutral(AppAccents.For("prism")));
    private float bottomChrome;
    private int historyDepth;
    private PrismTab tab;
    private float bodyScroll;
    private PrismScreen bodyScreen;
    private PrismScreen drawingScreen;
    private float pendingScroll = KeepScroll;
    private string wearingKey = string.Empty;
    private string wearingTitle = string.Empty;
    private string wearingGlamId = string.Empty;

    private static bool IsTabRoot(PrismScreen value) =>
        value is PrismScreen.Discover or PrismScreen.Cover or PrismScreen.Wardrobe or PrismScreen.Me;

    private static bool IsLibraryScreen(PrismScreen value) =>
        value is PrismScreen.GlamDetail or PrismScreen.Compose or PrismScreen.Profile or PrismScreen.GlamList
            or PrismScreen.WardrobeOutfit or PrismScreen.WardrobeCover or PrismScreen.Rewards or PrismScreen.Activity
            or PrismScreen.Gallery or PrismScreen.EditProfile or PrismScreen.ProfilePhoto or PrismScreen.BannerPick
            or PrismScreen.BannerCrop;

    private static bool ShowsTabBar(PrismScreen value) =>
        IsTabRoot(value) || value is PrismScreen.GlamDetail or PrismScreen.Profile or PrismScreen.GlamList
            or PrismScreen.WardrobeOutfit or PrismScreen.Rewards or PrismScreen.Activity or PrismScreen.Gallery;

    private bool Wearing(string key) => glamour.TryingOn && key.Length > 0 && wearingKey == key;

    private void StartWearing(string key, string title)
    {
        wearingKey = key;
        wearingTitle = title;
        wearingGlamId = string.Empty;
    }

    private void DrawWearingBar(Rect content, float scale)
    {
        var stage = TabBar.ContentArea(content, scale, TabBarUnits);
        var height = WearingBarUnits * scale;
        var min = new Vector2(stage.Min.X, stage.Max.Y - height);
        var max = stage.Max;
        var bar = new Rect(min, max);
        using var layer = ScreenLayer.Begin("prism.wearing", TabBar.GlassLayer(bar, scale), false);
        UiInteract.HoverOverlay(bar);
        var drawList = ImGui.GetWindowDrawList();
        Material.ThemedGlass(drawList, min, max, height * 0.5f, scale, ui.BackdropColor, TabBar.GlassOpacity);
        var left = min.X + height * 0.5f;
        var right = max.X - height * 0.5f;
        var labelPixels = PrismArt.LabelPixels();
        var y = min.Y + (height - PrismType.LineHeight(PrismFace.Label, labelPixels)) * 0.5f;
        var wearing = Loc.T(L.Prism.Wearing);
        var off = Loc.T(L.Prism.TakeOff);
        var offWidth = PrismType.Measure(off, PrismFace.Label, labelPixels).X;
        PrismType.Draw(drawList, new Vector2(left, y), wearing, PrismArt.Muted, PrismFace.Label, labelPixels);
        var titleLeft = left + PrismType.Measure(wearing, PrismFace.Label, labelPixels).X + Metrics.Space.Sm * scale;
        var room = right - offWidth - Metrics.Space.Md * scale - titleLeft;
        if (room > 0f && wearingTitle.Length > 0)
        {
            PrismType.Draw(drawList, new Vector2(titleLeft, y), PrismType.FitLabel(wearingTitle, labelPixels, room),
                PrismArt.Fore, PrismFace.Label, labelPixels);
        }

        if (PrismArt.Link(drawList, new Vector2(right - offWidth, y), off, PrismArt.Fore, scale, overlay: true))
        {
            glamour.Revert();
        }
    }

    private void FollowSkin(PhoneTheme theme)
    {
        ui.Theme = theme;
        ui.Palette = ui.Palette with
        {
            Accent = PrismArt.Fore,
            TitleInk = PrismArt.Fore,
            BodyInk = PrismArt.Fore,
            MutedInk = PrismArt.Muted,
            Hairline = PrismArt.Faint,
            BackdropTop = PrismArt.Back,
            BackdropBottom = PrismArt.Back,
            FieldSurface = Surfaces.Fill(PrismArt.Fore, FillLevel.Tertiary),
        };
    }

    private void DrawTabBar(Rect content)
    {
        var post = glams.IsSignedIn;
        var shift = post ? 1 : 0;
        tabItems[(int)PrismTab.Discover] =
            new TabItem(Loc.T(L.Prism.TabDiscover), PhoneIcons.Compass, PhoneIcons.CompassFilled);
        tabItems[(int)PrismTab.Week] = new TabItem(Loc.T(L.Prism.TabWeek), PhoneIcons.Crown);
        if (post)
        {
            tabItems[PostSlot] = new TabItem(Loc.T(L.Prism.PostGlam), PhoneIcons.Plus);
        }

        tabItems[(int)PrismTab.Wardrobe + shift] =
            new TabItem(Loc.T(L.Prism.TabWardrobe), PhoneIcons.Shirt, PhoneIcons.ShirtFilled);
        tabItems[(int)PrismTab.Me + shift] = new TabItem(Loc.T(L.Prism.TabMe), PhoneIcons.User, PhoneIcons.UserFilled);
        var active = (int)tab + (tab >= PrismTab.Wardrobe ? shift : 0);
        var tapped = tabBar.Draw(content, ui, tabItems.AsSpan(0, TabRoots.Length + shift), active).Tapped;
        if (post && tapped == PostSlot)
        {
            StartCompose();
            return;
        }

        if (tapped < 0)
        {
            return;
        }

        var next = (PrismTab)(tapped - (post && tapped > PostSlot ? 1 : 0));
        if (next == tab && IsTabRoot(screen))
        {
            pendingScroll = 0f;
        }
        else
        {
            OpenTab(next);
        }
    }

    private void OpenTab(PrismTab next)
    {
        var direction = next < tab || next == tab && !IsTabRoot(screen) ? -1 : 1;
        historyDepth = 0;
        Go(TabRoots[(int)next], direction);
        pendingScroll = KeepScroll;
        if (next == PrismTab.Discover)
        {
            RefreshDiscover(false);
        }
        else if (next == PrismTab.Me)
        {
            showSaved = false;
            OpenOwnProfile();
            glams.RefreshSeason();
        }
        else if (next == PrismTab.Week)
        {
            store.Refresh();
        }
    }

    private void Push(PrismScreen next)
    {
        if (historyDepth == HistoryDepth)
        {
            Array.Copy(history, 1, history, 0, HistoryDepth - 1);
            historyDepth--;
        }

        history[historyDepth++] = new PrismStop(screen, glams.Detail, glams.Profile?.User, showSaved, showPlacings,
            listKind, listItemId, listMod, listTag, listTitle, outfitId, gallery,
            bodyScreen == screen ? bodyScroll : 0f);

        Go(next);
    }

    private void GoBack()
    {
        if (historyDepth == 0)
        {
            Go(TabRoots[(int)tab], -1);
            pendingScroll = KeepScroll;
            return;
        }

        var stop = history[--historyDepth];
        history[historyDepth] = default;
        Go(stop.Screen, -1);
        pendingScroll = stop.Scroll;
        Restore(stop);
    }

    private AppSurface.SurfaceScope BeginBody(Rect body)
    {
        var surface = AppSurface.BeginEdgeToEdge(body, smoothWheel: true);
        if (drawingScreen != screen)
        {
            return surface;
        }

        if (pendingScroll == 0f)
        {
            surface.JumpToTop();
            pendingScroll = KeepScroll;
        }
        else if (pendingScroll > 0f)
        {
            var reach = ImGui.GetScrollMaxY();
            if (reach >= pendingScroll || screenAge >= RestoreWaitSeconds)
            {
                surface.JumpTo(MathF.Min(pendingScroll, reach));
                pendingScroll = KeepScroll;
            }
        }

        bodyScroll = ImGui.GetScrollY();
        bodyScreen = screen;
        return surface;
    }

    private void Restore(in PrismStop stop)
    {
        switch (stop.Screen)
        {
            case PrismScreen.GlamDetail when stop.Glam is not null:
                ShowGlam(stop.Glam);
                break;
            case PrismScreen.Me or PrismScreen.Profile when stop.Author is not null:
                showSaved = stop.Saved;
                showPlacings = stop.Placings;
                glams.OpenProfile(stop.Screen == PrismScreen.Me ? glams.Me ?? stop.Author : stop.Author, stop.Saved);
                break;
            case PrismScreen.GlamList:
                listKind = stop.Kind;
                listItemId = stop.ItemId;
                listMod = stop.Mod;
                listTag = stop.Tag;
                listTitle = stop.Title;
                OpenList();
                break;
            case PrismScreen.WardrobeOutfit:
                outfitId = stop.OutfitId;
                break;
            case PrismScreen.Gallery:
                gallery = stop.Gallery;
                glams.OpenWeek(gallery.WeekId, gallery.Track);
                break;
        }
    }

    private void SyncTab(PrismScreen next)
    {
        for (var index = 0; index < TabRoots.Length; index++)
        {
            if (TabRoots[index] == next)
            {
                tab = (PrismTab)index;
                return;
            }
        }
    }
}
