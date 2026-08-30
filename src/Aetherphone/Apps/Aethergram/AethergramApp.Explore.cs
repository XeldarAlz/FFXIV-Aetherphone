using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float ExploreSearchHeight = 36f;
    private const float ExploreSearchInset = 14f;
    private const float ExploreSearchRounding = 12f;
    private const float ExploreSearchGlyph = 16f;
    private const float ExploreClearRadius = 9f;
    private const float ExploreDebounceSeconds = 0.35f;
    private const int ExploreSearchMaxLength = 64;
    private const int ExploreSkeletonRows = 4;
    private const float TagRowHeight = 60f;
    private const float TagGlyphSize = 38f;
    private const float TagGlyphRounding = 12f;
    private const float TagHeroSize = 56f;
    private const float TagHeroRounding = 16f;
    private const float TagHeroPadY = 14f;

    private static readonly TextStyle TagNameStyle = TextStyles.Headline;
    private static readonly TextStyle TagCountStyle = TextStyles.Subheadline;
    private static readonly TextStyle TagGlyphStyle = new(1.13f, FontWeight.Bold);
    private static readonly TextStyle TagHeroGlyphStyle = new(1.6f, FontWeight.Bold);
    private static readonly TextStyle TagHeroTitleStyle = TextStyles.Title3;
    private static readonly TextStyle SectionLabelStyle = new(0.87f, FontWeight.Bold);

    private readonly PullToRefresh explorePull = new();
    private string exploreDraft = string.Empty;
    private double exploreDirtyAt = -1d;
    private Action? refreshExplore;
    private Action? loadMoreExplore;

    private void OpenSaved()
    {
        store.RefreshSaved();
        router.Push(AethergramRoute.Saved);
    }

    private void OpenHashtag(string tag)
    {
        store.OpenHashtagPosts(tag);
        router.Push(AethergramRoute.Hashtag(tag));
    }

    private string HashtagTitle(string tag)
    {
        if (!string.Equals(hashtagTitleTag, tag, StringComparison.Ordinal))
        {
            hashtagTitleTag = tag;
            hashtagTitle = "#" + tag;
        }

        return hashtagTitle;
    }

    private void ResetExplore()
    {
        exploreDraft = string.Empty;
        exploreDirtyAt = -1d;
        store.ClearDiscover();
    }

    private void DrawSearchTab(Rect area)
    {
        var scale = UiScale.Current;
        DrawExploreSearchField(area);
        StepExploreQuery();
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        if (string.IsNullOrWhiteSpace(exploreDraft))
        {
            DrawExploreGrid(listRect);
            return;
        }

        DrawExploreResults(listRect);
    }

    private void DrawExploreSearchField(Rect area)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var fieldHeight = ExploreSearchHeight * scale;
        var fieldMin = new Vector2(area.Min.X + ExploreSearchInset * scale, rowCenterY - fieldHeight * 0.5f);
        var fieldMax = new Vector2(area.Max.X - ExploreSearchInset * scale, rowCenterY + fieldHeight * 0.5f);
        var rounding = ExploreSearchRounding * scale;
        Squircle.Fill(drawList, fieldMin, fieldMax, rounding, ImGui.GetColorU32(Ink.FieldFill));
        PhoneIcon.Draw(drawList, new Vector2(fieldMin.X + 19f * scale, rowCenterY), PhoneIcons.Search, Ink.MutedInk,
            ExploreSearchGlyph * scale);
        var hasText = exploreDraft.Length > 0;
        var clearRadius = ExploreClearRadius * scale;
        var clearCenter = new Vector2(fieldMax.X - 16f * scale, rowCenterY);
        var inputRight = hasText ? clearCenter.X - clearRadius - 6f * scale : fieldMax.X - 8f * scale;
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + 34f * scale, rowCenterY - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(MathF.Max(1f, inputRight - fieldMin.X - 34f * scale));
        var hint = Loc.T(L.Aethergram.Search);
        Plugin.Fonts.NoticeText(hint);
        Plugin.Fonts.NoticeText(exploreDraft);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, Ink.TitleInk))
        {
            if (ImGui.InputTextWithHint("##aethergramExplore", hint, ref exploreDraft, ExploreSearchMaxLength))
            {
                exploreDirtyAt = ImGui.GetTime();
            }
        }

        if (!hasText)
        {
            return;
        }

        var clearExtent = new Vector2(clearRadius, clearRadius);
        var clearHovered = UiInteract.Hover(clearCenter - clearExtent, clearCenter + clearExtent);
        drawList.AddCircleFilled(clearCenter, clearRadius,
            ImGui.GetColorU32(clearHovered ? Ink.ButtonHover : Ink.ButtonFill), 24);
        PhoneIcon.Draw(drawList, clearCenter, PhoneIcons.X, Ink.TitleInk, 11f * scale);
        if (clearHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(clearCenter - clearExtent, clearCenter + clearExtent, clearHovered))
        {
            ResetExplore();
        }
    }

    private void StepExploreQuery()
    {
        if (exploreDirtyAt < 0d || ImGui.GetTime() - exploreDirtyAt < ExploreDebounceSeconds)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(exploreDraft))
        {
            exploreDirtyAt = -1d;
            store.ClearDiscover();
            return;
        }

        if (store.TagsLoading || store.Searching)
        {
            exploreDirtyAt = ImGui.GetTime();
            return;
        }

        exploreDirtyAt = -1d;
        store.SearchTags(exploreDraft);
        if (!exploreDraft.TrimStart().StartsWith('#'))
        {
            store.Search(exploreDraft);
        }
    }

    private void DrawExploreGrid(Rect listRect)
    {
        var scale = UiScale.Current;
        const SocialFeedScope scope = SocialFeedScope.ForYou;
        profile.EnsureLoaded(scope);
        refreshExplore ??= RefreshExploreFeed;
        loadMoreExplore ??= LoadMoreExploreFeed;
        var posts = store.Feed(scope);
        using (var surface = AppSurface.BeginEdgeToEdge(listRect))
        {
            explorePull.Draw(listRect, surface.Pull, surface.Dragging, store.IsLoading(scope), Ink.MutedInk,
                refreshExplore);
            if (posts.Length > 0)
            {
                ImGui.Dummy(new Vector2(0f, 2f * scale));
                DrawPostGrid(posts, L.Aethergram.ExploreEmpty, store.HasMoreFeed(scope), store.LoadingMore(scope),
                    loadMoreExplore, ExploreGrid, PostSource.Explore);
                return;
            }

            if (store.IsLoading(scope))
            {
                DrawExploreSkeleton(listRect);
                return;
            }

            if (store.FeedFailed(scope))
            {
                feedFailure.Set(store.FeedFailure(scope));
                DrawEmptyState(listRect, feedFailure.Text(), Loc.T(L.Failure.PullToRetry));
                return;
            }

            DrawEmptyState(listRect, Loc.T(L.Aethergram.ExploreEmpty), string.Empty);
        }
    }

    private void RefreshExploreFeed() => RefreshFeed(SocialFeedScope.ForYou);

    private void LoadMoreExploreFeed() => store.LoadMoreFeed(SocialFeedScope.ForYou);

    private static void DrawExploreSkeleton(Rect listRect)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var gap = GridGap * scale;
        var cellWidth = (width - gap * (GridColumns - 1)) / GridColumns;
        var cellHeight = cellWidth * ExploreGrid.Aspect;
        var origin = ImGui.GetCursorScreenPos();
        var fill = ImGui.GetColorU32(Ink.ThumbFill);
        for (var row = 0; row < ExploreSkeletonRows; row++)
        {
            for (var column = 0; column < GridColumns; column++)
            {
                var min = new Vector2(origin.X + column * (cellWidth + gap), origin.Y + row * (cellHeight + gap));
                drawList.AddRectFilled(min, min + new Vector2(cellWidth, cellHeight), fill);
            }
        }

        ImGui.Dummy(new Vector2(width, ExploreSkeletonRows * (cellHeight + gap)));
    }

    private void DrawExploreResults(Rect listRect)
    {
        var scale = UiScale.Current;
        var tagsOnly = exploreDraft.TrimStart().StartsWith('#');
        var people = tagsOnly ? Array.Empty<UserDto>() : store.DiscoverResults;
        var tags = store.DiscoverTags;
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            if (people.Length == 0 && tags.Length == 0)
            {
                var pending = exploreDirtyAt >= 0d || store.Searching || store.TagsLoading;
                DrawEmptyState(listRect,
                    pending ? Loc.T(L.Common.Searching) : Loc.T(L.Aethergram.NoResults),
                    pending ? string.Empty : Loc.T(L.Aethergram.SearchByName));
                return;
            }

            if (people.Length > 0)
            {
                SocialChrome.DrawSectionLabel(Loc.T(L.Aethergram.PeopleSection), Ink, SectionLabelStyle);
                for (var index = 0; index < people.Length; index++)
                {
                    DrawUserRowWithFollow(people[index]);
                }
            }

            if (tags.Length > 0)
            {
                SocialChrome.DrawSectionLabel(Loc.T(L.Aethergram.TagsSection), Ink, SectionLabelStyle);
                for (var index = 0; index < tags.Length; index++)
                {
                    DrawTagRow(tags[index]);
                }
            }

            ImGui.Dummy(new Vector2(0f, 40f * scale));
        }
    }

    private void DrawTagRow(TagSummaryDto summary)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, TagRowHeight * scale, Ink.HoverTint);
        var origin = cell.Bounds.Min;
        var padX = CellPadX * scale;
        var glyphSize = TagGlyphSize * scale;
        var glyphMin = new Vector2(origin.X + padX, origin.Y + (cell.Bounds.Height - glyphSize) * 0.5f);
        var glyphMax = glyphMin + new Vector2(glyphSize, glyphSize);
        Squircle.Fill(drawList, glyphMin, glyphMax, TagGlyphRounding * scale, ImGui.GetColorU32(Ink.AccentWash));
        Typography.DrawCentered(drawList, (glyphMin + glyphMax) * 0.5f, "#", Ink.AccentLink, TagGlyphStyle);
        var textLeft = glyphMax.X + 12f * scale;
        var chevronLeft = cell.Bounds.Max.X - padX - 14f * scale;
        var textWidth = MathF.Max(1f, chevronLeft - textLeft - 8f * scale);
        var nameHeight = Typography.LineHeight(TagNameStyle);
        var countHeight = Typography.LineHeight(TagCountStyle);
        var textTop = origin.Y + (cell.Bounds.Height - nameHeight - countHeight - 2f * scale) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText("#" + summary.Tag, textWidth, TagNameStyle), Ink.TitleInk, TagNameStyle);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight + 2f * scale),
            Typography.FitText(Loc.Plural(L.Aethergram.Posts, summary.Posts), textWidth, TagCountStyle),
            Ink.MutedInk, TagCountStyle);
        PhoneIcon.Draw(drawList, new Vector2(chevronLeft, cell.Bounds.Center.Y), PhoneIcons.ChevronRight,
            Ink.FaintInk, 14f * scale);
        if (cell.Tapped)
        {
            OpenHashtag(summary.Tag);
        }

        FeedCell.End(drawList, cell, Ink.Hairline, false);
    }

    private void DrawHashtag(Rect area, string tag)
    {
        store.EnsureHashtagPosts(tag);
        var scale = UiScale.Current;
        DrawScreenHeader(area, HashtagTitle(tag));
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var posts = store.HashtagPosts;
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            DrawHashtagHero(tag, posts.Length);
            if (posts.Length > 0)
            {
                DrawPostGrid(posts, L.Social.HashtagEmpty, store.HasMoreHashtagPosts, store.HashtagLoadingMore,
                    store.LoadMoreHashtagPosts, SquareGrid, PostSource.Hashtag);
                return;
            }

            var body = new Rect(ImGui.GetCursorScreenPos(), listRect.Max);
            DrawEmptyState(body,
                store.HashtagLoading ? Loc.T(L.Common.Loading) : Loc.T(L.Social.HashtagEmpty), string.Empty);
        }
    }

    private void DrawHashtagHero(string tag, int loadedCount)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var padX = CellPadX * scale;
        var heroSize = TagHeroSize * scale;
        var height = heroSize + TagHeroPadY * 2f * scale;
        var glyphMin = new Vector2(origin.X + padX, origin.Y + TagHeroPadY * scale);
        var glyphMax = glyphMin + new Vector2(heroSize, heroSize);
        Squircle.Fill(drawList, glyphMin, glyphMax, TagHeroRounding * scale, ImGui.GetColorU32(Ink.AccentWash));
        Squircle.Stroke(drawList, glyphMin, glyphMax, TagHeroRounding * scale, ImGui.GetColorU32(Ink.ChipStroke), 1f);
        Typography.DrawCentered(drawList, (glyphMin + glyphMax) * 0.5f, "#", Ink.AccentLink, TagHeroGlyphStyle);
        var textLeft = glyphMax.X + 14f * scale;
        var textWidth = MathF.Max(1f, origin.X + width - padX - textLeft);
        var titleHeight = Typography.LineHeight(TagHeroTitleStyle);
        var showCount = loadedCount > 0 && !store.HasMoreHashtagPosts;
        var countHeight = showCount ? Typography.LineHeight(TagCountStyle) + 2f * scale : 0f;
        var textTop = origin.Y + (height - titleHeight - countHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(HashtagTitle(tag), textWidth, TagHeroTitleStyle), Ink.TitleInk, TagHeroTitleStyle);
        if (showCount)
        {
            Typography.Draw(drawList, new Vector2(textLeft, textTop + titleHeight + 2f * scale),
                Typography.FitText(Loc.Plural(L.Aethergram.Posts, loadedCount), textWidth, TagCountStyle),
                Ink.MutedInk, TagCountStyle);
        }

        DrawHairline(drawList, origin.X, origin.X + width, origin.Y + height);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + GridGap * scale));
    }

    private void DrawSaved(Rect area)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Aethergram.SavedTitle));
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var posts = store.SavedPosts;
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            if (posts.Length > 0)
            {
                ImGui.Dummy(new Vector2(0f, 2f * scale));
                DrawPostGrid(posts, L.Aethergram.SavedEmpty, store.HasMoreSaved, store.SavedLoadingMore,
                    store.LoadMoreSaved, SquareGrid, PostSource.Saved);
                return;
            }

            if (store.SavedLoading)
            {
                DrawEmptyState(listRect, Loc.T(L.Common.Loading), string.Empty);
                return;
            }

            DrawEmptyState(listRect, Loc.T(L.Aethergram.SavedEmpty), Loc.T(L.Aethergram.SavedEmptyHint));
        }
    }
}
