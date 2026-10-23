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
    GlamListKind Kind,
    uint ItemId,
    GlamModDto? Mod,
    string Tag,
    string Title,
    string OutfitId,
    float Scroll);

internal sealed partial class PrismApp
{
    private const float WearingBarUnits = 44f;
    private const float WearingGapUnits = 8f;
    private const int HistoryDepth = 12;
    private const float KeepScroll = -1f;
    private const float RestoreWaitSeconds = 3f;

    private static readonly PrismScreen[] TabRoots =
    {
        PrismScreen.Discover, PrismScreen.Cover, PrismScreen.Wardrobe, PrismScreen.Me,
    };

    private readonly PrismStop[] history = new PrismStop[HistoryDepth];
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[TabRoots.Length];
    private readonly AppSkin ui = new(AppPalettes.Neutral(AppAccents.For("prism")));
    private float bottomChrome;
    private int historyDepth;
    private PrismTab tab;
    private float bodyScroll;
    private PrismScreen bodyScreen;
    private PrismScreen drawingScreen;
    private float pendingScroll = KeepScroll;
    private string? introWeek;
    private string wearingKey = string.Empty;
    private string wearingTitle = string.Empty;
    private string wearingGlamId = string.Empty;

    private static bool IsTabRoot(PrismScreen value) =>
        value is PrismScreen.Discover or PrismScreen.Cover or PrismScreen.Wardrobe or PrismScreen.Me;

    private static bool IsLibraryScreen(PrismScreen value) =>
        value is PrismScreen.GlamDetail or PrismScreen.Compose or PrismScreen.Profile or PrismScreen.GlamList
            or PrismScreen.WardrobeOutfit or PrismScreen.WardrobeCover or PrismScreen.Rewards or PrismScreen.Activity;

    private static bool ShowsTabBar(PrismScreen value) =>
        IsTabRoot(value) || value is PrismScreen.GlamDetail or PrismScreen.Profile or PrismScreen.GlamList
            or PrismScreen.WardrobeOutfit or PrismScreen.Rewards or PrismScreen.Activity;

    private bool Wearing(string key) => glamour.TryingOn && key.Length > 0 && wearingKey == key;

    private void StartWearing(string key, string title)
    {
        wearingKey = key;
        wearingTitle = title;
        wearingGlamId = string.Empty;
    }

    private void DrawWearingBar(Rect content, float scale)
    {
        var stage = TabBar.ContentArea(content, scale);
        var height = WearingBarUnits * scale;
        var side = Metrics.Space.GlassInset * scale;
        var min = new Vector2(stage.Min.X + side, stage.Max.Y - height);
        var max = new Vector2(stage.Max.X - side, stage.Max.Y);
        using var layer = ScreenLayer.Begin("prism.wearing", new Rect(new Vector2(stage.Min.X, min.Y), stage.Max), false);
        UiInteract.HoverOverlay(new Rect(min, max));
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
        ui.Palette = ui.Palette with { Accent = PrismArt.Fore, BackdropTop = PrismArt.Back, BackdropBottom = PrismArt.Back };
    }

    private void DrawTabBar(Rect content)
    {
        tabItems[(int)PrismTab.Discover] =
            new TabItem(Loc.T(L.Prism.TabDiscover), PhoneIcons.Compass, PhoneIcons.CompassFilled);
        tabItems[(int)PrismTab.Week] = new TabItem(Loc.T(L.Prism.TabWeek), PhoneIcons.Crown);
        tabItems[(int)PrismTab.Wardrobe] = new TabItem(Loc.T(L.Prism.TabWardrobe), IconGlyph.Of(FontAwesomeIcon.Tshirt));
        tabItems[(int)PrismTab.Me] = new TabItem(Loc.T(L.Prism.TabMe), PhoneIcons.User, PhoneIcons.UserFilled);
        TabBarAction? post = glams.IsSignedIn ? new TabBarAction(PhoneIcons.Plus, Loc.T(L.Prism.PostGlam)) : null;
        var result = tabBar.Draw(content, ui, tabItems, (int)tab, post);
        if (result.ActionTapped)
        {
            StartCompose();
        }
        else if (result.Tapped == (int)tab && IsTabRoot(screen))
        {
            pendingScroll = 0f;
        }
        else if (result.Tapped >= 0)
        {
            OpenTab((PrismTab)result.Tapped);
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
            var week = store.Week?.Id ?? string.Empty;
            if (week != introWeek)
            {
                introWeek = week;
                introDelay = IntroSeconds;
            }

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

        history[historyDepth++] = new PrismStop(screen, glams.Detail, glams.Profile?.User, showSaved, listKind,
            listItemId, listMod, listTag, listTitle, outfitId, bodyScreen == screen ? bodyScroll : 0f);

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
        var surface = AppSurface.BeginEdgeToEdge(body);
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
                glams.OpenProfile(stop.Author, stop.Saved);
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
