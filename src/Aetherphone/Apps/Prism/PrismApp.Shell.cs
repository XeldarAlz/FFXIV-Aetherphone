using Aetherphone.Core;
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

internal sealed partial class PrismApp
{
    private const float TabBarUnits = 50f;
    private const float TabMarkGapUnits = 5f;
    private const int HistoryDepth = 12;

    private static readonly LocString[] TabNames =
    {
        L.Prism.TabDiscover, L.Prism.TabWeek, L.Prism.TabWardrobe, L.Prism.TabMe,
    };

    private static readonly PrismScreen[] TabRoots =
    {
        PrismScreen.Discover, PrismScreen.Cover, PrismScreen.Wardrobe, PrismScreen.Me,
    };

    private readonly PrismScreen[] history = new PrismScreen[HistoryDepth];
    private int historyDepth;
    private PrismTab tab;

    private static bool IsTabRoot(PrismScreen value) =>
        value is PrismScreen.Discover or PrismScreen.Cover or PrismScreen.Wardrobe or PrismScreen.Me;

    private static bool IsLibraryScreen(PrismScreen value) =>
        value is PrismScreen.GlamDetail or PrismScreen.Compose or PrismScreen.Profile or PrismScreen.GlamList
            or PrismScreen.WardrobeOutfit;

    private static Rect AboveTabBar(Rect content, float scale) =>
        new(content.Min, new Vector2(content.Max.X, content.Max.Y - TabBarUnits * scale));

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
            var label = Loc.T(TabNames[index]);
            var width = PrismType.Measure(label, PrismFace.Label, labelPixels).X;
            var left = min.X + (cell - width) * 0.5f;
            var active = (int)tab == index;
            var hovered = UiInteract.Hover(min, max);
            PrismType.Draw(drawList, new Vector2(left, labelTop), label, active || hovered ? PrismArt.Fore : PrismArt.Muted,
                PrismFace.Label, labelPixels);
            if (active)
            {
                PrismArt.SpectrumRule(drawList, left, left + width, labelTop + labelLine + TabMarkGapUnits * scale * 0.5f,
                    scale);
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
        if (next == PrismTab.Discover && glams.Lane(discoverScope).NeverLoaded)
        {
            glams.Refresh(discoverScope);
        }
        else if (next == PrismTab.Me)
        {
            OpenOwnProfile();
        }
        else if (next == PrismTab.Week)
        {
            introDelay = IntroSeconds;
            store.Refresh();
        }
    }

    private void Push(PrismScreen next)
    {
        if (historyDepth < HistoryDepth)
        {
            history[historyDepth++] = screen;
        }

        Go(next);
    }

    private void GoBack()
    {
        Go(historyDepth > 0 ? history[--historyDepth] : TabRoots[(int)tab], -1);
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
