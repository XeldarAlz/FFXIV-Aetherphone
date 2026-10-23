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
    private const int SkeletonRows = 2;
    private const float SkeletonTitleShare = 0.6f;
    private const float SkeletonHandleShare = 0.35f;
    private const float TileMedalUnits = 11f;
    private const float TileShadeShare = 0.4f;

    private static readonly LocString[] ScopeNames = { L.Prism.ScopeNew, L.Prism.ScopePopular, L.Prism.ScopeFollowing };
    private static readonly GlamScope[] ShownScopes = { GlamScope.New, GlamScope.Popular, GlamScope.Following };
    private static readonly LocString[] FilterNames = { L.Prism.FilterAll, L.Prism.FilterModded, L.Prism.FilterVanilla };
    private static readonly Vector4 TileShade = new(0f, 0f, 0f, 0.7f);
    private static readonly Vector4 TileClear = new(0f, 0f, 0f, 0f);
    private static readonly Vector4 TilePressed = new(0f, 0f, 0f, 0.22f);
    private static readonly Vector4 CaptionScrimTop = new(0f, 0f, 0f, 0.5f);
    private static readonly Vector4 CaptionScrimBottom = new(0f, 0f, 0f, 0.78f);

    private readonly PullToRefresh discoverPull = new();
    private GlamScope discoverScope = GlamScope.New;
    private int discoverFilter;
    private bool todayResolved;
    private Action? refreshDiscoverNow;

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
        var top = DrawMasthead(drawList, content, DisplayName, string.Empty, string.Empty, scale, out var activity,
            PhoneIcons.Heart, social.UnseenCount(Id) > 0);
        if (activity)
        {
            OpenActivity();
        }

        top = DrawSearchRow(drawList, left, right, top, scale);
        if (!searching)
        {
            top = DrawScopeRow(drawList, left, right, top, scale);
        }

        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        var lane = glams.Lane(discoverScope);
        using var surface = BeginBody(body);
        if (!searching)
        {
            discoverPull.Draw(body, surface.Pull, surface.Dragging, lane.Loading, PrismArt.Muted,
                refreshDiscoverNow ??= RefreshDiscoverAlways);
        }

        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth() - gutter * 2f;
        if (searching)
        {
            ImGui.Dummy(new Vector2(width, DrawSearchResults(list, origin.X + gutter, origin.Y, width, scale) - origin.Y));
            return;
        }

        var cursor = DrawThemeRow(list, origin.X + gutter, origin.Y, width, scale);
        cursor = DrawDayHero(list, origin.X + gutter, cursor, width, scale);
        cursor = DrawLaneGrid(list, origin.X + gutter, cursor, width, lane,
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
            var active = discoverScope == ShownScopes[index];
            var width = PrismType.Measure(label, PrismFace.Label, labelPixels).X;
            if (DrawToggleLink(drawList, x, top, label, width, active, labelLine, scale) && !active)
            {
                discoverScope = ShownScopes[index];
                pendingScroll = 0f;
                RefreshDiscover(false);
            }

            x += width + Metrics.Space.Lg * scale;
        }

        var fits = Loc.T(L.Prism.FitsMe);
        var fitsWidth = PrismType.Measure(fits, PrismFace.Label, labelPixels).X;
        var modded = Loc.T(FilterNames[1]);
        var moddedWidth = PrismType.Measure(modded, PrismFace.Label, labelPixels).X;
        var vanilla = Loc.T(FilterNames[2]);
        var vanillaWidth = PrismType.Measure(vanilla, PrismFace.Label, labelPixels).X;
        var gap = Metrics.Space.Lg * scale;
        var cursor = right - fitsWidth - moddedWidth - vanillaWidth - gap * 2f;
        if (x > cursor)
        {
            top += labelLine + Metrics.Space.Md * scale;
            cursor = left;
        }

        if (DrawToggleLink(drawList, cursor, top, fits, fitsWidth, glams.FitsBody, labelLine, scale))
        {
            var (race, gender) = glams.FitsBody ? (0, 0) : WornBody();
            glams.SetBody(race, gender);
            pendingScroll = 0f;
            RefreshDiscover(true);
        }

        cursor += fitsWidth + gap;
        if (DrawToggleLink(drawList, cursor, top, modded, moddedWidth, discoverFilter == 1, labelLine, scale))
        {
            SetDiscoverFilter(discoverFilter == 1 ? 0 : 1);
        }

        cursor += moddedWidth + gap;
        if (DrawToggleLink(drawList, cursor, top, vanilla, vanillaWidth, discoverFilter == 2, labelLine, scale))
        {
            SetDiscoverFilter(discoverFilter == 2 ? 0 : 2);
        }

        return top + labelLine + Metrics.Space.Lg * scale;
    }

    private static bool DrawToggleLink(ImDrawListPtr drawList, float x, float top, string label, float width,
        bool active, float labelLine, float scale)
    {
        var tapped = PrismArt.Link(drawList, new Vector2(x, top), label, active ? PrismArt.Fore : PrismArt.Muted, scale,
            labelLine + Metrics.Space.Md * scale);
        if (active)
        {
            PrismArt.Hairline(drawList, x, x + width, top + labelLine, PrismArt.Fore, scale);
        }

        return tapped;
    }

    private void SetDiscoverFilter(int filter)
    {
        discoverFilter = filter;
        glams.SetVanillaOnly(filter switch
        {
            1 => false,
            2 => true,
            _ => null,
        });
        pendingScroll = 0f;
        RefreshDiscover(true);
    }

    private void RefreshDiscoverAlways() => RefreshDiscover(true);

    private float DrawThemeRow(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        if (store.Week is not { Id.Length: > 0 } week || !HasPrimary(week))
        {
            return top;
        }

        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var gap = Metrics.Space.Sm * scale;
        var height = MathF.Max(labelLine + Metrics.Space.Md * scale * 2f, Metrics.Size.TapTarget * scale);
        var row = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        var y = top + (height - labelLine) * 0.5f;
        PrismArt.Hairline(drawList, row.Min.X, row.Max.X, row.Min.Y, PrismArt.Faint, scale);
        PrismArt.Hairline(drawList, row.Min.X, row.Max.X, row.Max.Y, PrismArt.Faint, scale);
        var action = Loc.T(week.Phase != PrismPhases.Entering ? L.Prism.JudgeHeat
            : week.MyEntry is null ? L.Prism.Submit : L.Prism.YourLook);
        var right = row.Max.X - PrismType.DrawRight(drawList, new Vector2(row.Max.X, y), Arrow, PrismArt.Fore,
            PrismFace.Label, labelPixels) - gap;
        right -= PrismType.DrawRight(drawList, new Vector2(right, y), action, PrismArt.Fore, PrismFace.Label,
            labelPixels) + gap * 2f;
        var label = Loc.T(L.Prism.TabWeek);
        var labelWidth = PrismType.Measure(label, PrismFace.Label, labelPixels).X + gap;
        var titleLeft = left;
        if (labelWidth + PrismType.Measure(week.Title, PrismFace.Label, labelPixels).X <= right - left)
        {
            PrismType.Draw(drawList, new Vector2(left, y), label, PrismArt.Muted, PrismFace.Label, labelPixels);
            titleLeft += labelWidth;
        }

        PrismType.Draw(drawList, new Vector2(titleLeft, y), PrismType.FitLabel(week.Title, labelPixels, right - titleLeft),
            PrismArt.Fore, PrismFace.Label, labelPixels);
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            OpenTab(PrismTab.Week);
        }

        return row.Max.Y + Metrics.Space.Lg * scale;
    }

    private float DrawLaneGrid(ImDrawListPtr drawList, float left, float top, float width, FeedLane<GlamDto> lane,
        LocString emptyHint, float scale)
    {
        var items = lane.Items;
        if (items.Length == 0 && lane.Loading)
        {
            return DrawGlamSkeleton(drawList, left, top, width, scale);
        }

        if (items.Length == 0)
        {
            var text = Loc.T(lane.Failed ? L.Common.LoadFailedHint : emptyHint);
            var copy = PrismType.Sentences(text, TextStyles.Subheadline, width);
            top += Typography.DrawWrappedLeft(new Vector2(left, top), copy, PrismArt.Muted, TextStyles.Subheadline,
                PrismType.BalancedWidth(copy, TextStyles.Subheadline, width)) + Metrics.Space.Md * scale;
            if (!lane.Failed)
            {
                return DrawLaneAction(drawList, left, top, width, scale);
            }

            if (PrismArt.Link(drawList, new Vector2(left, top), Loc.T(L.Common.Retry), PrismArt.Fore, scale))
            {
                RetryLane(lane);
            }

            return top + Metrics.Size.TapTarget * scale;
        }

        var bottom = DrawGlamGrid(drawList, left, top, width, items, scale);
        if (lane.LoadingMore)
        {
            var resume = ImGui.GetCursorScreenPos();
            ImGui.SetCursorScreenPos(new Vector2(left, bottom));
            InfiniteScroll.DrawLoadingRow(left + width * 0.5f, PrismArt.Muted);
            bottom = ImGui.GetCursorScreenPos().Y;
            ImGui.SetCursorScreenPos(resume);
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
        var clear = ImGui.GetColorU32(TileClear);
        var scrimTop = ImGui.GetColorU32(CaptionScrimTop);
        var scrimBottom = ImGui.GetColorU32(CaptionScrimBottom);
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
            var captionTop = max.Y - pad - labelLine * 2f;
            var scrimMiddle = captionTop - Metrics.Space.Sm * scale;
            drawList.AddRectFilledMultiColor(new Vector2(x, MathF.Max(min.Y, captionTop - labelLine * 2f)),
                new Vector2(max.X, scrimMiddle), clear, clear, scrimTop, scrimTop);
            drawList.AddRectFilledMultiColor(new Vector2(x, scrimMiddle), max, scrimTop, scrimTop, scrimBottom,
                scrimBottom);
            drawList.PushClipRect(min, max, true);
            if (glam.Placing is >= 1 and <= 3)
            {
                DrawTileMedal(drawList, new Vector2(x + pad, y + pad), glam.Placing, scale);
            }

            PrismType.Draw(drawList, new Vector2(x + pad, captionTop),
                PrismType.FitLabel(glam.Title, labelPixels, column - pad * 2f), PrismArt.PhotoFore, PrismFace.Label,
                labelPixels);
            var likesWidth = 0f;
            if (glam.Likes > 0)
            {
                var bottom = max.Y - pad - labelLine;
                var ink = PrismType.Snap(PrismFace.Label, labelPixels);
                var iconGap = Metrics.Space.Xs * scale;
                likesWidth = PrismType.DrawRight(drawList, new Vector2(max.X - pad, bottom),
                    NumberText.Compact(glam.Likes), PrismArt.PhotoFore, PrismFace.Label, labelPixels);
                PhoneIcon.Draw(drawList, new Vector2(max.X - pad - likesWidth - iconGap - ink * 0.5f, bottom + ink * 0.5f),
                    glam.Liked ? PhoneIcons.HeartFilled : PhoneIcons.Heart, PrismArt.PhotoFore, ink);
                likesWidth += ink + iconGap + gap;
            }

            PrismType.Draw(drawList, new Vector2(x + pad, max.Y - pad - labelLine),
                PrismType.FitLabel(AuthorLine(glam.Author), labelPixels, column - pad * 2f - likesWidth),
                Palette.WithAlpha(PrismArt.PhotoFore, MutedOnPhoto), PrismFace.Label, labelPixels);
            drawList.PopClipRect();
            var hovered = UiInteract.Hover(min, max);
            DrawPressVeil(drawList, min, max, hovered);
            if (UiInteract.Click(min, max, hovered))
            {
                OpenGlam(glam);
            }
        }

        return top + rows * (height + gap);
    }

    private float DrawLaneAction(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        if (!glams.IsSignedIn || screen is not (PrismScreen.Discover or PrismScreen.Me))
        {
            return top + Metrics.Size.TapTarget * scale;
        }

        var following = screen == PrismScreen.Discover && discoverScope == GlamScope.Following;
        var saved = screen == PrismScreen.Me && showSaved;
        var label = following ? L.Prism.ScopePopular : saved ? L.Prism.TabDiscover : L.Prism.PostGlam;
        var strip = new Rect(new Vector2(left, top), new Vector2(left + width, top + PrismArt.StripUnits * scale));
        if (!PrismArt.Strip(drawList, strip, Loc.T(label), Arrow, !following && !saved))
        {
            return strip.Max.Y + PrismArt.BottomMargin(scale);
        }

        if (following)
        {
            discoverScope = GlamScope.Popular;
            RefreshDiscover(false);
        }
        else if (saved)
        {
            OpenTab(PrismTab.Discover);
        }
        else
        {
            StartCompose();
        }

        return strip.Max.Y + PrismArt.BottomMargin(scale);
    }

    private static void DrawPressVeil(ImDrawListPtr drawList, Vector2 min, Vector2 max, bool hovered)
    {
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && !DragScrollHost.AnyDragging)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(TilePressed));
        }
    }

    private static void DrawTileMedal(ImDrawListPtr drawList, Vector2 corner, int placing, float scale)
    {
        var radius = TileMedalUnits * scale;
        var center = corner + new Vector2(radius, radius);
        PrismArt.Medal(drawList, center, radius, placing, 0f, scale);
        var numeral = Plain[placing];
        var size = PrismType.Measure(numeral, PrismFace.Numerals, radius);
        PrismType.Draw(drawList, center - size * 0.5f, numeral, PrismArt.Fore, PrismFace.Numerals, radius);
    }

    private float DrawGlamSkeleton(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var gap = Metrics.Space.Sm * scale;
        var column = (width - gap * (GridColumns - 1)) / GridColumns;
        var height = column / GlamStore.PhotoRatio;
        var labelLine = PrismType.LineHeight(PrismFace.Label, PrismArt.LabelPixels());
        var pad = Metrics.Space.Sm * scale;
        var fill = ImGui.GetColorU32(Palette.WithAlpha(PrismArt.Fore, PickFillAlpha));
        var faint = ImGui.GetColorU32(PrismArt.Faint);
        for (var index = 0; index < SkeletonRows * GridColumns; index++)
        {
            var x = left + index % GridColumns * (column + gap);
            var y = top + index / GridColumns * (height + gap);
            drawList.AddRectFilled(new Vector2(x, y), new Vector2(x + column, y + height), fill);
            DrawSkeletonBars(drawList, x + pad, y + height - pad, column, labelLine, faint);
        }

        return top + SkeletonRows * (height + gap) + PrismArt.BottomMargin(scale);
    }

    private static void DrawSkeletonBars(ImDrawListPtr drawList, float left, float bottom, float width, float labelLine,
        uint color)
    {
        var y = bottom - labelLine * 2f + labelLine * 0.25f;
        drawList.AddRectFilled(new Vector2(left, y), new Vector2(left + width * SkeletonTitleShare, y + labelLine * 0.5f),
            color);
        y += labelLine;
        drawList.AddRectFilled(new Vector2(left, y), new Vector2(left + width * SkeletonHandleShare, y + labelLine * 0.5f),
            color);
    }

    private void RefreshDiscover(bool always)
    {
        if (always || glams.Lane(discoverScope).NeverLoaded)
        {
            glams.Refresh(discoverScope);
        }

        if (always || glams.Lane(GlamScope.Today).NeverLoaded)
        {
            glams.Refresh(GlamScope.Today);
        }
    }

    private float DrawDayHero(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var today = glams.Lane(GlamScope.Today);
        if (today.Items.Length == 0)
        {
            if (!today.Loading)
            {
                todayResolved = true;
            }

            if (todayResolved)
            {
                return top;
            }
        }

        var gap = Metrics.Space.Sm * scale;
        var column = (width - gap * (GridColumns - 1)) / GridColumns;
        var photo = new Rect(new Vector2(left, top), new Vector2(left + column, top + column / GlamStore.PhotoRatio));
        drawList.AddRectFilled(photo.Min, photo.Max, ImGui.GetColorU32(Palette.WithAlpha(PrismArt.Fore, PickFillAlpha)));
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var textLeft = photo.Max.X + Metrics.Space.Md * scale;
        var textWidth = left + width - textLeft;
        PrismType.Draw(drawList, new Vector2(textLeft, top),
            PrismType.FitLabel(Loc.T(L.Prism.GlamOfTheDay), labelPixels, textWidth), PrismArt.Muted, PrismFace.Label,
            labelPixels);
        if (today.Items.Length == 0)
        {
            DrawSkeletonBars(drawList, textLeft, photo.Max.Y, textWidth, labelLine, ImGui.GetColorU32(PrismArt.Faint));
            return photo.Max.Y + Metrics.Space.Xl * scale;
        }

        var glam = today.Items[0];
        DrawLook(drawList, photo, Cover(glam), false);
        var pixels = PrismType.Fit(glam.Title, PrismFace.Headline, textWidth, PrismType.TitleUnits * scale,
            PrismType.HeadingUnits * scale, 3, out var lines);
        var textTop = top + labelLine + Metrics.Space.Xs * scale;
        var y = MathF.Max(textTop, photo.Max.Y - lines.Length * PrismType.LineHeight(PrismFace.Headline, pixels) -
                                   Metrics.Space.Xs * scale - labelLine * (glam.TryOns > 0 ? 2f : 1f));
        drawList.PushClipRect(new Vector2(textLeft, textTop), new Vector2(left + width, photo.Max.Y), true);
        y += PrismType.DrawLines(drawList, new Vector2(textLeft, y), lines, PrismArt.Fore, PrismFace.Headline, pixels) +
             Metrics.Space.Xs * scale;
        PrismType.Draw(drawList, new Vector2(textLeft, y), PrismType.FitLabel(AuthorLine(glam.Author), labelPixels,
            textWidth), PrismArt.Muted, PrismFace.Label, labelPixels);
        if (glam.TryOns > 0)
        {
            PrismType.Draw(drawList, new Vector2(textLeft, y + labelLine),
                PrismType.FitLabel(TriedOnLine(glam.TryOns), labelPixels, textWidth), PrismArt.Muted, PrismFace.Label,
                labelPixels);
        }

        drawList.PopClipRect();

        var hovered = UiInteract.Hover(photo.Min, new Vector2(left + width, photo.Max.Y));
        DrawPressVeil(drawList, photo.Min, photo.Max, hovered);
        if (UiInteract.Click(photo.Min, new Vector2(left + width, photo.Max.Y), hovered))
        {
            OpenGlam(glam);
        }

        return photo.Max.Y + Metrics.Space.Xl * scale;
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
        if (PrismArt.Strip(drawList, StripRect(content, scale), Loc.T(L.Prism.OpenSettings), Arrow, true))
        {
            navigation.Open("settings");
        }
    }
}
