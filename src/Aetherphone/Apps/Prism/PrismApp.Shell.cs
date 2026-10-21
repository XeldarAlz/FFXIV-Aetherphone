using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

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
    string OutfitId);

internal sealed partial class PrismApp
{
    private const float TabBarUnits = 50f;
    private const float TabMarkGapUnits = 5f;
    private const float WearingBarUnits = 40f;
    private const int HistoryDepth = 12;

    private static readonly LocString[] TabNames =
    {
        L.Prism.TabDiscover, L.Prism.TabWeek, L.Prism.TabWardrobe, L.Prism.TabMe,
    };

    private static readonly PrismScreen[] TabRoots =
    {
        PrismScreen.Discover, PrismScreen.Cover, PrismScreen.Wardrobe, PrismScreen.Me,
    };

    private readonly PrismStop[] history = new PrismStop[HistoryDepth];
    private int historyDepth;
    private PrismTab tab;
    private string? introWeek;
    private string wearingKey = string.Empty;
    private string wearingTitle = string.Empty;
    private string wearingGlamId = string.Empty;

    private static bool IsTabRoot(PrismScreen value) =>
        value is PrismScreen.Discover or PrismScreen.Cover or PrismScreen.Wardrobe or PrismScreen.Me;

    private static bool IsLibraryScreen(PrismScreen value) =>
        value is PrismScreen.GlamDetail or PrismScreen.Compose or PrismScreen.Profile or PrismScreen.GlamList
            or PrismScreen.WardrobeOutfit or PrismScreen.WardrobeCover or PrismScreen.FashionReport;

    private static Rect AboveTabBar(Rect content, bool wearing, float scale) =>
        new(content.Min, new Vector2(content.Max.X,
            content.Max.Y - (TabBarUnits + (wearing ? WearingBarUnits : 0f)) * scale));

    private bool Wearing(string key) => glamour.TryingOn && key.Length > 0 && wearingKey == key;

    private void StartWearing(string key, string title)
    {
        wearingKey = key;
        wearingTitle = title;
        wearingGlamId = string.Empty;
    }

    private void DrawWearingBar(ImDrawListPtr drawList, Rect content, float scale)
    {
        var top = content.Max.Y - (TabBarUnits + WearingBarUnits) * scale;
        PrismArt.Hairline(drawList, content.Min.X, content.Max.X, top, PrismArt.Faint, scale);
        var gutter = PrismArt.Gutter(scale);
        var left = content.Min.X + gutter;
        var right = content.Max.X - gutter;
        var labelPixels = PrismArt.LabelPixels();
        var y = top + (WearingBarUnits * scale - PrismType.LineHeight(PrismFace.Label, labelPixels)) * 0.5f;
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

        if (PrismArt.Link(drawList, new Vector2(right - offWidth, y), off, PrismArt.Fore, scale))
        {
            glamour.Revert();
        }
    }

    private void DrawTabBar(ImDrawListPtr drawList, Rect content, float scale)
    {
        var top = content.Max.Y - TabBarUnits * scale;
        PrismArt.Hairline(drawList, content.Min.X, content.Max.X, top, PrismArt.Faint, scale);
        var cell = content.Width / TabNames.Length;
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var labelTop = top + (TabBarUnits * scale - labelLine) * 0.5f;
        for (var index = 0; index < TabNames.Length; index++)
        {
            var min = new Vector2(content.Min.X + cell * index, top);
            var max = new Vector2(min.X + cell, content.Max.Y);
            var label = PrismType.FitLabel(Loc.T(TabNames[index]), labelPixels, cell - Metrics.Space.Sm * scale * 2f);
            var width = PrismType.Measure(label, PrismFace.Label, labelPixels).X;
            var left = min.X + (cell - width) * 0.5f;
            var active = (int)tab == index;
            var hovered = UiInteract.Hover(min, max);
            PrismType.Draw(drawList, new Vector2(left, labelTop), label, active || hovered ? PrismArt.Fore : PrismArt.Muted,
                PrismFace.Label, labelPixels);
            if (active)
            {
                PrismArt.Hairline(drawList, left, left + width, labelTop + labelLine + TabMarkGapUnits * scale * 0.5f,
                    PrismArt.Fore, scale);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered) && !active)
            {
                OpenTab((PrismTab)index);
            }
        }
    }

    private void OpenTab(PrismTab next)
    {
        var direction = next >= tab ? 1 : -1;
        historyDepth = 0;
        Go(TabRoots[(int)next], direction);
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
            listItemId, listMod, listTag, listTitle, outfitId);

        Go(next);
    }

    private void GoBack()
    {
        if (historyDepth == 0)
        {
            Go(TabRoots[(int)tab], -1);
            return;
        }

        var stop = history[--historyDepth];
        history[historyDepth] = default;
        Go(stop.Screen, -1);
        Restore(stop);
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
