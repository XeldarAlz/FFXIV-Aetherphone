using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const int GridColumns = 2;
    private const float TileShadeShare = 0.4f;
    private const float ThemeTitleUnits = 22f;

    private static readonly LocString[] ScopeNames = { L.Prism.ScopeToday, L.Prism.ScopeNew, L.Prism.ScopeFollowing };
    private static readonly LocString[] FilterNames = { L.Prism.FilterAll, L.Prism.FilterModded, L.Prism.FilterVanilla };
    private static readonly Vector4 TileShade = new(0f, 0f, 0f, 0.7f);
    private static readonly Vector4 TileClear = new(0f, 0f, 0f, 0f);

    private GlamScope discoverScope = GlamScope.New;
    private int discoverFilter;

    private void DrawDiscover(ImDrawListPtr drawList, Rect content, float scale)
    {
        if (!glams.IsSignedIn)
        {
            DrawSignedOut(drawList, content, scale);
            return;
        }

        var gutter = PrismArt.Gutter(scale);
        var left = content.Min.X + gutter;
        var right = content.Max.X - gutter;
        var top = content.Min.Y + HeaderGapUnits * scale;
        PrismType.Draw(drawList, new Vector2(left, top), DisplayName, PrismArt.Fore, PrismFace.Headline,
            MastheadUnits * scale);
        var post = Loc.T(L.Prism.PostGlam);
        if (PrismArt.Button(drawList, new Vector2(right - PrismArt.ButtonWidth(post, scale), top), post, scale, out _))
        {
            StartCompose();
        }

        top += PrismType.LineHeight(PrismFace.Headline, MastheadUnits * scale) + Metrics.Space.Md * scale;
        PrismArt.SpectrumRule(drawList, left, right, top, scale);
        top += Metrics.Space.Lg * scale;
        top = DrawScopeRow(drawList, left, right, top, scale);
        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        var lane = glams.Lane(discoverScope);
        using var surface = AppSurface.BeginEdgeToEdge(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth() - gutter * 2f;
        var cursor = DrawThemeCard(list, origin.X + gutter, origin.Y, width, scale);
        cursor = DrawLaneGrid(list, body, origin.X + gutter, cursor, width, lane,
            discoverScope == GlamScope.Following ? L.Prism.FollowingEmptyHint : L.Prism.DiscoverEmptyHint, scale);
        ImGui.Dummy(new Vector2(width, cursor - origin.Y));
        if (InfiniteScroll.ReachedBottom())
        {
            glams.LoadMore(discoverScope);
        }
    }

    private float DrawScopeRow(ImDrawListPtr drawList, float left, float right, float top, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var x = left;
        for (var index = 0; index < ScopeNames.Length; index++)
        {
            var label = Loc.T(ScopeNames[index]);
            var active = (int)discoverScope == index;
            var width = PrismType.Measure(label, PrismFace.Label, labelPixels).X;
            if (PrismArt.Link(drawList, new Vector2(x, top), label, active ? PrismArt.Fore : PrismArt.Muted, scale) && !active)
            {
                discoverScope = (GlamScope)index;
                if (glams.Lane(discoverScope).NeverLoaded)
                {
                    glams.Refresh(discoverScope);
                }
            }

            if (active)
            {
                PrismArt.Hairline(drawList, x, x + width, top + labelLine, PrismArt.Fore, scale);
            }

            x += width + Metrics.Space.Lg * scale;
        }

        var filter = Loc.T(FilterNames[discoverFilter]);
        var filterWidth = PrismType.Measure(filter, PrismFace.Label, labelPixels).X;
        if (x > right - filterWidth)
        {
            top += labelLine + Metrics.Space.Md * scale;
        }

        if (PrismArt.Link(drawList, new Vector2(right - filterWidth, top), filter, PrismArt.Muted, scale))
        {
            discoverFilter = (discoverFilter + 1) % FilterNames.Length;
            glams.SetVanillaOnly(discoverFilter switch
            {
                1 => false,
                2 => true,
                _ => null,
            });
            glams.Refresh(discoverScope);
        }

        return top + labelLine + Metrics.Space.Lg * scale;
    }

    private float DrawThemeCard(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        if (store.Week is not { Id.Length: > 0 } week || !HasPrimary(week))
        {
            return top;
        }

        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var pad = Metrics.Space.Md * scale;
        var pixels = PrismType.Fit(week.Title, PrismFace.Headline, width - pad * 2f, ThemeTitleUnits * scale,
            ThemeTitleUnits * scale * 0.75f, 2, out var lines);
        var height = pad * 2f + labelLine + lines.Length * PrismType.LineHeight(PrismFace.Headline, pixels) +
                     Metrics.Space.Sm * scale + labelLine;
        var card = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        drawList.AddRect(card.Min, card.Max, ImGui.GetColorU32(PrismArt.Faint), 0f, ImDrawFlags.None, 1f);
        var y = top + pad;
        PrismType.Draw(drawList, new Vector2(left + pad, y), Loc.T(L.Prism.ThemeLabel), PrismArt.Muted, PrismFace.Label,
            labelPixels);
        y += labelLine;
        y += PrismType.DrawLines(drawList, new Vector2(left + pad, y), lines, PrismArt.Fore, PrismFace.Headline, pixels) +
             Metrics.Space.Sm * scale;
        var action = Loc.T(week.Phase != PrismPhases.Entering ? L.Prism.JudgeHeat
            : week.MyEntry is null ? L.Prism.Submit : L.Prism.YourLook);
        if (PrismArt.Link(drawList, new Vector2(left + pad, y), action, PrismArt.Fore, scale))
        {
            OpenTab(PrismTab.Week);
        }

        PrismType.Draw(drawList, new Vector2(left + pad + PrismType.Measure(action, PrismFace.Label, labelPixels).X +
            Metrics.Space.Sm * scale, y), Arrow, PrismArt.Fore, PrismFace.Label, labelPixels);

        return card.Max.Y + Metrics.Space.Lg * scale;
    }

    private float DrawLaneGrid(ImDrawListPtr drawList, Rect body, float left, float top, float width,
        FeedLane<GlamDto> lane, LocString emptyHint, float scale)
    {
        var items = lane.Items;
        if (items.Length == 0)
        {
            var text = Loc.T(lane.Loading ? L.Common.Loading : lane.Failed ? L.Common.LoadFailedHint : emptyHint);
            var copy = PrismType.Sentences(text, TextStyles.Subheadline, width);
            top += Typography.DrawWrappedLeft(new Vector2(left, top), copy, PrismArt.Muted, TextStyles.Subheadline,
                PrismType.BalancedWidth(copy, TextStyles.Subheadline, width)) + Metrics.Space.Md * scale;
            if (lane.Failed && !lane.Loading &&
                PrismArt.Link(drawList, new Vector2(left, top), Loc.T(L.Common.Retry), PrismArt.Fore, scale))
            {
                RetryLane(lane);
            }

            return top + Metrics.Size.TapTarget * scale;
        }

        var bottom = DrawGlamGrid(drawList, left, top, width, items, scale);
        if (lane.LoadingMore)
        {
            ImGui.SetCursorScreenPos(new Vector2(left, bottom));
            InfiniteScroll.DrawLoadingRow(left + width * 0.5f, PrismArt.Muted);
            bottom = ImGui.GetCursorScreenPos().Y;
        }

        return bottom + PrismArt.BottomMargin(scale);
    }

    private float DrawGlamGrid(ImDrawListPtr drawList, float left, float top, float width, GlamDto[] items, float scale)
    {
        var gap = Metrics.Space.Sm * scale;
        var column = (width - gap * (GridColumns - 1)) / GridColumns;
        var height = column / GlamStore.PhotoRatio;
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var pad = Metrics.Space.Sm * scale;
        var rows = (items.Length + GridColumns - 1) / GridColumns;
        for (var index = 0; index < items.Length; index++)
        {
            var x = left + index % GridColumns * (column + gap);
            var y = top + index / GridColumns * (height + gap);
            var min = new Vector2(x, y);
            var max = new Vector2(x + column, y + height);
            if (!ImGui.IsRectVisible(min, max))
            {
                continue;
            }

            var glam = items[index];
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(PrismArt.Fore, PickFillAlpha)));
            DrawLook(drawList, new Rect(min, max), Cover(glam), false);
            var shade = new Vector2(x, max.Y - height * TileShadeShare);
            drawList.AddRectFilledMultiColor(shade, max, ImGui.GetColorU32(TileClear), ImGui.GetColorU32(TileClear),
                ImGui.GetColorU32(TileShade), ImGui.GetColorU32(TileShade));
            drawList.PushClipRect(min, max, true);
            PrismType.Draw(drawList, new Vector2(x + pad, max.Y - pad - labelLine * 2f),
                PrismType.FitLabel(glam.Title, labelPixels, column - pad * 2f), PrismArt.PhotoFore, PrismFace.Label,
                labelPixels);
            PrismType.Draw(drawList, new Vector2(x + pad, max.Y - pad - labelLine),
                PrismType.FitLabel(AuthorLine(glam.Author), labelPixels, column - pad * 2f),
                Palette.WithAlpha(PrismArt.PhotoFore, MutedOnPhoto), PrismFace.Label, labelPixels);
            drawList.PopClipRect();
            var hovered = UiInteract.Hover(min, max);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                OpenGlam(glam);
            }
        }

        return top + rows * (height + gap);
    }

    private void RetryLane(FeedLane<GlamDto> lane)
    {
        lane.ClearFailure();
        if (!ReferenceEquals(lane, glams.List))
        {
            glams.Refresh(discoverScope);
        }
        else if (screen == PrismScreen.GlamList)
        {
            OpenList();
        }
        else if (glams.Profile?.User is { } author)
        {
            glams.OpenProfile(author, showSaved);
        }
    }

    private static string Cover(GlamDto glam) => glam.Photos is { Length: > 0 } photos ? photos[0].Url : string.Empty;

    private void DrawSignedOut(ImDrawListPtr drawList, Rect content, float scale)
    {
        DrawMasthead(drawList, content, string.Empty, scale);
        DrawNotice(drawList, content, Loc.T(L.Prism.SignInTitle), Loc.T(L.Prism.SignInRequired), scale);
        if (PrismArt.Strip(drawList, StripRect(content, scale), Loc.T(L.Prism.OpenSettings), "→", true))
        {
            navigation.Open("settings");
        }
    }
}
