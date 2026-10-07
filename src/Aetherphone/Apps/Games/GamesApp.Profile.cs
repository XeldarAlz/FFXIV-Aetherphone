using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Hub;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp
{
    private const string ProfileNavId = "games.profile.nav";
    private const string BestsNavId = "games.bests.nav";
    private const string ProfileIdentityId = "games.profile.identity";
    private const string ProfileRanksRailId = "##games.profile.ranks";
    private const string ProfileRanksScope = "games.profile.ranks";
    private const string ProfileBestsScope = "games.profile.bests";
    private const string BestsGridScope = "games.bests.grid";
    private const string ProfileSeeAllId = "games.profile.seeAllBests";
    private const string ProfileJoinId = "games.profile.join";
    private const string ProfileSignInId = "games.profile.signIn";
    private const string RankCardPrefix = "games.profile.rank.";
    private const float IdentityAvatarRadius = 32f;
    private const float IdentityTileSize = 64f;
    private const float IdentityGlyphSize = 30f;
    private const float StatTileHeight = 96f;
    private const float StatTileGap = 10f;
    private const float StatGlyphSize = 22f;
    private const float StatRingStroke = 3f;
    private const float StatTrackAlpha = 0.22f;
    private const float RankCardWidth = 132f;
    private const float RankCardMinHeight = 116f;
    private const float RankCardGap = 12f;
    private const float RankIconSize = 36f;
    private const float BestCardMinHeight = 112f;
    private const float BestCardGap = 10f;
    private const float BestIconSize = 40f;
    private const float BestStarSize = 9f;
    private const float EmptyStarAlpha = 0.35f;
    private const float CardPadding = Metrics.Space.Md;
    private const float CardTextGap = Metrics.Space.Sm;
    private const float RailClipPad = Metrics.Space.Xxs;
    private const float ProfileEmptyHeight = 280f;
    private const int ProfileBestLimit = 8;
    private const int StatColumns = 3;
    private const int BestColumns = 2;
    private const int SkeletonRankCards = 3;
    private const int IdentitySegments = 48;

    private readonly StreakCalendar streakCalendar = new();
    private readonly TileRail profileRanksRail = new();
    private CachedText playedStatText;
    private GameScoreRankDto[] labeledRanks = Array.Empty<GameScoreRankDto>();
    private LanguageInfo? rankLabelLanguage;
    private int[] rankOrder = Array.Empty<int>();
    private int[] rankGameIndexes = Array.Empty<int>();
    private int[] rankValues = Array.Empty<int>();
    private string[] rankStatIds = Array.Empty<string>();
    private string[] rankTitles = Array.Empty<string>();
    private string[] rankChips = Array.Empty<string>();
    private string[] rankTotals = Array.Empty<string>();
    private string[] rankWeeks = Array.Empty<string>();
    private string[] rankCardIds = Array.Empty<string>();
    private int rankRowCount;
    private string[] bestKindLines = Array.Empty<string>();
    private int bestKindVersion = -1;
    private LanguageInfo? bestKindLanguage;

    private void ResetProfile()
    {
        profileRanksRail.Reset();
    }

    private void DrawProfile(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var left = origin.X;
            SyncProfileRanks();
            var y = DrawIdentity(drawList, left, origin.Y, width, scale) + Metrics.Space.Lg * scale;
            y = DrawStatTiles(drawList, left, y, width, scale) + Metrics.Space.Md * scale;
            y = DrawCalendar(drawList, left, y, width, scale);
            y = DrawProfileRanks(drawList, left, y, width, scale);
            y = DrawProfileBests(drawList, left, y + HubMetrics.SectionGap * scale, width, scale);
            FinishPage(origin, width, y, scale);
            if (profileRanksRail.Swiping)
            {
                surface.CancelDrag();
            }
        }

        AppHeader.EndLargeTitle(in navBar, context, ProfileNavId, Loc.T(L.GamesHub.TabProfile), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private void DrawBests(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context);
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            SyncBestKindLines();
            var y = DrawBestGrid(drawList, library.Records, BestsGridScope, origin.X, origin.Y, width, false,
                scale);
            FinishPage(origin, width, y, scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, BestsNavId, Loc.T(L.GamesHub.PersonalBests), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, TabTitle(GamesTab.Profile), back);
    }

    private void SyncProfileRanks()
    {
        if (!leaderboard.IsSignedIn || leaderboard.OptedOut)
        {
            return;
        }

        leaderboard.EnsureMyRanksFresh();
        RefreshRankLabels(leaderboard.MyRanks);
    }

    private float DrawIdentity(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var size = IdentityTileSize * scale;
        var center = new Vector2(left + size * 0.5f, top + size * 0.5f);
        var textLeft = left + size + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, left + width - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        if (leaderboard.IsSignedIn && leaderboard.CurrentUser is { } user)
        {
            var name = SocialIdentity.Name(user.DisplayName, user.Handle);
            AvatarView.DrawRemote(drawList, center, IdentityAvatarRadius * scale, ui.Theme, name, string.Empty,
                user.AvatarUrl, images, lodestone, 1f, IdentitySegments);
            var textTop = center.Y - (titleHeight + Typography.LineHeight(TextStyles.Footnote)) * 0.5f;
            UserName.DrawAuto(drawList, ProfileIdentityId, name, user.Badges, textLeft, textTop, textWidth,
                TextStyles.Title2, ui.TitleInk, ui.Theme);
            Typography.Draw(drawList, new Vector2(textLeft, textTop + titleHeight),
                Typography.FitText(UserHandle(user), textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
            return top + size;
        }

        var min = new Vector2(left, top);
        var radius = GameIconArt.Radius(size);
        Elevation.IconRest(drawList, min, min + new Vector2(size, size), radius, scale);
        IconTile.FillShaded(drawList, min, min + new Vector2(size, size), radius, IconTile.Surface(ui.Accent));
        PhoneIcon.Draw(drawList, center, PhoneIcons.Gamepad, AccentRing.Ink, IdentityGlyphSize * scale);
        Typography.Draw(drawList, new Vector2(textLeft, center.Y - titleHeight * 0.5f),
            Typography.FitText(Loc.T(L.GamesHub.YourGames), textWidth, TextStyles.Title2), ui.TitleInk,
            TextStyles.Title2);
        return top + size;
    }

    private float DrawStatTiles(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var gap = StatTileGap * scale;
        var tileWidth = (width - gap * (StatColumns - 1)) / StatColumns;
        var height = StatTileHeight * scale;
        var played = library.PlayedCount;
        var total = library.Entries.Length;
        var playedKey = ((long)played << 20) | (uint)total;
        var playedLabel = playedStatText.IsCurrent(playedKey)
            ? playedStatText.Value
            : playedStatText.Store(playedKey,
                Loc.T(L.GamesHub.PlayedOf, GameNumber.Label(played), GameNumber.Label(total)));
        var tile = new Rect(new Vector2(left, top), new Vector2(left + tileWidth, top + height));
        var glyphCenter = StatGlyphCenter(tile, scale);
        DrawStatTile(drawList, tile, playedLabel, Loc.T(L.GamesHub.StatPlayed), scale);
        var ringRadius = (StatGlyphSize - StatRingStroke) * 0.5f * scale;
        var stroke = StatRingStroke * scale;
        ProgressRing.Track(drawList, glyphCenter, ringRadius, stroke, ui.Accent with { W = StatTrackAlpha });
        ProgressRing.Fill(drawList, glyphCenter, ringRadius, stroke, total > 0 ? played / (float)total : 0f, ui.Accent);

        var streak = stats.DailyStreak;
        tile = Offset(tile, tileWidth + gap);
        DrawStatTile(drawList, tile, GameNumber.Label(streak), Loc.T(L.GamesHub.StatStreak), scale);
        PhoneIcon.Draw(drawList, StatGlyphCenter(tile, scale), streak > 0 ? PhoneIcons.FlameFilled : PhoneIcons.Flame,
            streak > 0 ? HubMetrics.Ember : ui.MutedInk, StatGlyphSize * scale);

        tile = Offset(tile, tileWidth + gap);
        var ranked = leaderboard.IsSignedIn && !leaderboard.OptedOut;
        var third = ranked ? rankRowCount : library.TotalStars;
        DrawStatTile(drawList, tile, GameNumber.Label(third),
            Loc.T(ranked ? L.GamesHub.StatRanked : L.GamesHub.KindStars), scale);
        PhoneIcon.Draw(drawList, StatGlyphCenter(tile, scale), ranked ? PhoneIcons.Crown : PhoneIcons.StarFilled,
            third > 0 ? GamePalette.Star : ui.MutedInk, StatGlyphSize * scale);
        return top + height;
    }

    private static Rect Offset(Rect rect, float dx) =>
        new(new Vector2(rect.Min.X + dx, rect.Min.Y), new Vector2(rect.Max.X + dx, rect.Max.Y));

    private static Vector2 StatGlyphCenter(Rect tile, float scale)
    {
        var half = StatGlyphSize * 0.5f * scale;
        var pad = CardPadding * scale;
        return new Vector2(tile.Min.X + pad + half, tile.Min.Y + pad + half);
    }

    private void DrawStatTile(ImDrawListPtr drawList, Rect tile, string value, string label, float scale)
    {
        ui.Card(drawList, tile.Min, tile.Max, HubMetrics.SmallCardRadius * scale);
        var pad = CardPadding * scale;
        var textWidth = MathF.Max(1f, tile.Width - pad * 2f);
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        var labelTop = tile.Max.Y - pad - labelHeight;
        var valueTop = labelTop - Typography.LineHeight(TextStyles.Title2);
        Typography.Draw(drawList, new Vector2(tile.Min.X + pad, valueTop),
            Typography.FitText(value, textWidth, TextStyles.Title2), ui.TitleInk, TextStyles.Title2);
        Typography.Draw(drawList, new Vector2(tile.Min.X + pad, labelTop),
            Typography.FitText(label, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
    }

    private float DrawCalendar(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var bottom = top + StreakCalendar.Height(scale);
        if (!Visible(drawList, top, bottom))
        {
            return bottom;
        }

        var daily = games[featuredIndex];
        if (streakCalendar.Draw(drawList, ui, stats, daily, new Vector2(left, top), width, scale))
        {
            OpenGame(daily);
        }

        return bottom;
    }

    private float DrawProfileRanks(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var signedIn = leaderboard.IsSignedIn;
        var optedOut = leaderboard.OptedOut;
        var loading = signedIn && !optedOut && !leaderboard.MyRanksLoaded && leaderboard.LoadingMyRanks;
        if (signedIn && !optedOut && !loading && rankRowCount == 0)
        {
            return top;
        }

        var y = top + HubMetrics.SectionGap * scale;
        GamesHubArt.Section(drawList, ui, left, y, width, Loc.T(L.Stage.YourRanks), string.Empty, string.Empty);
        y += (GamesHubArt.SectionHeight + HubMetrics.HeaderGap) * scale;
        if (!signedIn)
        {
            var settings = navigation.IsAvailable(SettingsAppId) ? Loc.T(L.GamesHub.OpenSettings) : string.Empty;
            var bottom = DrawCompactCard(drawList, left, y, width, scale, PhoneIcons.UserCircle,
                Loc.T(L.Stage.SignInToRank), ui.TitleInk, settings, ProfileSignInId, true, out var openSettings);
            if (openSettings)
            {
                navigation.Open(SettingsAppId);
            }

            return bottom;
        }

        if (optedOut)
        {
            return DrawConsentCompact(left, y, width, scale, ProfileJoinId);
        }

        return loading ? DrawRankSkeleton(drawList, left, y, width, scale) : DrawRankRail(drawList, left, y, width,
            scale);
    }

    private float RankCardHeight(float scale) =>
        MathF.Max(RankCardMinHeight * scale,
            (CardPadding * 2f + RankIconSize + CardTextGap) * scale
            + Typography.LineHeight(TextStyles.FootnoteEmphasized) * 2f + Typography.LineHeight(TextStyles.Footnote));

    private float DrawRankSkeleton(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var cardWidth = RankCardWidth * scale;
        var height = RankCardHeight(scale);
        var gap = RankCardGap * scale;
        drawList.PushClipRect(new Vector2(left, top - RailClipPad * scale),
            new Vector2(left + width, top + height + RailClipPad * scale), true);
        for (var index = 0; index < SkeletonRankCards; index++)
        {
            var min = new Vector2(left + index * (cardWidth + gap), top);
            Skeleton.Bar(drawList, min, min + new Vector2(cardWidth, height), HubMetrics.SmallCardRadius * scale);
        }

        drawList.PopClipRect();
        return top + height;
    }

    private float DrawRankRail(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var cardWidth = RankCardWidth * scale;
        var cardHeight = RankCardHeight(scale);
        var gap = RankCardGap * scale;
        var bleed = HubMetrics.RailBleed * scale;
        var pad = RailClipPad * scale;
        var bottom = top + cardHeight;
        if (!Visible(drawList, top, bottom))
        {
            return bottom;
        }

        var row = new Rect(new Vector2(left - bleed, top - pad), new Vector2(left + width + bleed, bottom + pad));
        var claim = new Rect(new Vector2(left, row.Min.Y), new Vector2(left + width, row.Max.Y));
        var contentWidth = bleed * 2f + rankRowCount * (cardWidth + gap) - gap;
        profileRanksRail.Begin(drawList, ProfileRanksRailId, row, claim, contentWidth);
        var interactive = profileRanksRail.TapAllowed;
        var x = left - profileRanksRail.Offset;
        var activate = -1;
        ImGui.PushID(ProfileRanksScope);
        for (var position = 0; position < rankRowCount; position++)
        {
            var slot = rankOrder[position];
            var card = new Rect(new Vector2(x, top), new Vector2(x + cardWidth, bottom));
            if (card.Max.X >= row.Min.X && card.Min.X <= row.Max.X && DrawRankCard(drawList, card, slot, interactive,
                    scale))
            {
                activate = slot;
            }

            x += cardWidth + gap;
        }

        ImGui.PopID();
        profileRanksRail.End(drawList, row, contentWidth, ui);
        if (activate >= 0)
        {
            OpenLeaderboard(games[rankGameIndexes[activate]], rankStatIds[activate], TabTitle(GamesTab.Profile));
        }

        return bottom;
    }

    private bool DrawRankCard(ImDrawListPtr drawList, Rect card, int slot, bool interactive, float scale)
    {
        var id = rankCardIds[slot];
        var hovered = interactive && UiInteract.Hover(card.Min, card.Max);
        var firstVertex = drawList.VtxBuffer.Size;
        var pose = CardPose(id, hovered);
        var game = games[rankGameIndexes[slot]];
        ui.Card(drawList, card.Min, card.Max, HubMetrics.SmallCardRadius * scale);
        var pad = CardPadding * scale;
        var icon = RankIconSize * scale;
        var iconMin = new Vector2(card.Min.X + pad, card.Min.Y + pad);
        GameIconArt.Draw(drawList, game.Id, game.Accent, iconMin, iconMin + new Vector2(icon, icon), null, true);
        var chipLeft = iconMin.X + icon + CardTextGap * scale;
        var chipWidth = MathF.Max(1f, card.Max.X - pad - chipLeft);
        var chipHeight = Typography.LineHeight(TextStyles.Title2);
        Typography.Draw(drawList, new Vector2(chipLeft, iconMin.Y + (icon - chipHeight) * 0.5f),
            Typography.FitText(rankChips[slot], chipWidth, TextStyles.Title2), ui.TitleInk, TextStyles.Title2);
        var textLeft = card.Min.X + pad;
        var textWidth = MathF.Max(1f, card.Width - pad * 2f);
        var y = iconMin.Y + icon + CardTextGap * scale;
        Marquee.DrawLeft(drawList, id, rankTitles[slot], textLeft, y, textWidth, TextStyles.FootnoteEmphasized,
            ui.TitleInk, hovered);
        y += Typography.LineHeight(TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, y),
            Typography.FitText(rankTotals[slot], textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        y += Typography.LineHeight(TextStyles.Footnote);
        if (rankWeeks[slot].Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, y),
                Typography.FitText(rankWeeks[slot], textWidth, TextStyles.FootnoteEmphasized),
                ui.Ink.WithAccent(game.Accent).AccentInk, TextStyles.FootnoteEmphasized);
        }

        VertexWarp.Scale(drawList, firstVertex, card.Center, pose);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(card.Min, card.Max, hovered);
    }

    private static float CardPose(string id, bool hovered)
    {
        var hover = HoverFx.Amount(id, hovered);
        var press = PressFx.Scale(id, hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left), Motion.PressScaleCard);
        return press * (1f + Motion.HoverLiftCard * hover);
    }

    private float DrawProfileBests(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var records = library.Records;
        if (records.Length == 0)
        {
            var body = new Rect(new Vector2(left, top), new Vector2(left + width, top + ProfileEmptyHeight * scale));
            if (Visible(drawList, body.Min.Y, body.Max.Y) && EmptyState.Draw(body, ui, FontAwesomeIcon.Trophy,
                    Loc.T(L.GamesHub.RecordsEmptyTitle), Loc.T(L.GamesHub.RecordsEmptyHint),
                    Loc.T(L.GamesHub.PlayToday)))
            {
                OpenGame(games[featuredIndex]);
            }

            return body.Max.Y;
        }

        SyncBestKindLines();
        var seeAll = records.Length > ProfileBestLimit ? Loc.T(L.GamesHub.SeeAll) : string.Empty;
        if (GamesHubArt.Section(drawList, ui, left, top, width, Loc.T(L.GamesHub.PersonalBests), seeAll,
                ProfileSeeAllId))
        {
            router.Push(GamesRoute.Bests);
        }

        var y = top + (GamesHubArt.SectionHeight + HubMetrics.HeaderGap) * scale;
        var shown = records.Length > ProfileBestLimit ? records[..ProfileBestLimit] : records;
        return DrawBestGrid(drawList, shown, ProfileBestsScope, left, y, width, true, scale);
    }

    private float BestCardHeight(float scale) =>
        MathF.Max(BestCardMinHeight * scale,
            (CardPadding * 2f + BestIconSize + CardTextGap) * scale + Typography.LineHeight(TextStyles.Title3)
            + Typography.LineHeight(TextStyles.Footnote));

    private float DrawBestGrid(ImDrawListPtr drawList, ReadOnlySpan<int> records, string scope, float left, float top,
        float width, bool morph, float scale)
    {
        if (records.Length == 0)
        {
            return top;
        }

        var gap = BestCardGap * scale;
        var cardWidth = (width - gap * (BestColumns - 1)) / BestColumns;
        var cardHeight = BestCardHeight(scale);
        var activate = -1;
        var source = default(Rect);
        ImGui.PushID(scope);
        for (var index = 0; index < records.Length; index++)
        {
            var column = index % BestColumns;
            var row = index / BestColumns;
            var min = new Vector2(left + column * (cardWidth + gap), top + row * (cardHeight + gap));
            if (!Visible(drawList, min.Y, min.Y + cardHeight))
            {
                continue;
            }

            if (DrawBestCard(drawList, new Rect(min, min + new Vector2(cardWidth, cardHeight)), records[index], scale,
                    out var icon))
            {
                activate = records[index];
                source = icon;
            }
        }

        ImGui.PopID();
        if (activate >= 0)
        {
            Activate(activate, morph ? source : default);
        }

        var rows = (records.Length + BestColumns - 1) / BestColumns;
        return top + rows * (cardHeight + gap) - gap;
    }

    private bool DrawBestCard(ImDrawListPtr drawList, Rect card, int entryIndex, float scale, out Rect iconRect)
    {
        var id = library.TileIds[entryIndex];
        var hovered = UiInteract.Hover(card.Min, card.Max);
        var firstVertex = drawList.VtxBuffer.Size;
        var pose = CardPose(id, hovered);
        ui.Card(drawList, card.Min, card.Max, HubMetrics.SmallCardRadius * scale);
        var pad = CardPadding * scale;
        var icon = BestIconSize * scale;
        var iconMin = new Vector2(card.Min.X + pad, card.Min.Y + pad);
        iconRect = new Rect(iconMin, iconMin + new Vector2(icon, icon));
        GameIconArt.Draw(drawList, library.IconIds[entryIndex], library.Accent(entryIndex), iconRect.Min, iconRect.Max,
            null, true);
        var sideLeft = iconRect.Max.X + CardTextGap * scale;
        var sideWidth = MathF.Max(1f, card.Max.X - pad - sideLeft);
        var titleHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var starMax = library.StarMax(entryIndex);
        var sideHeight = titleHeight + (starMax > 0 ? BestStarSize * scale + CardTextGap * scale * 0.5f : 0f);
        var titleTop = iconMin.Y + (icon - sideHeight) * 0.5f;
        Marquee.DrawLeft(drawList, id, library.Title(entryIndex), sideLeft, titleTop, sideWidth,
            TextStyles.FootnoteEmphasized, ui.TitleInk, hovered);
        if (starMax > 0)
        {
            var starSize = BestStarSize * scale;
            var starCenter = new Vector2(sideLeft + StarRow.Width(starSize) * 0.5f,
                titleTop + titleHeight + CardTextGap * scale * 0.5f + starSize * 0.5f);
            StarRow.Draw(drawList, starCenter, starSize, library.StarTier(entryIndex),
                ui.MutedInk with { W = EmptyStarAlpha }, 1f);
        }

        var textWidth = MathF.Max(1f, card.Width - pad * 2f);
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        var labelTop = card.Max.Y - pad - labelHeight;
        var valueTop = labelTop - Typography.LineHeight(TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, valueTop),
            Typography.FitText(library.BestValue(entryIndex), textWidth, TextStyles.Title3), ui.TitleInk,
            TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, labelTop),
            Typography.FitText(bestKindLines[entryIndex], textWidth, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        VertexWarp.Scale(drawList, firstVertex, card.Center, pose);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(card.Min, card.Max, hovered);
    }

    private void SyncBestKindLines()
    {
        if (bestKindVersion == library.Version && ReferenceEquals(bestKindLanguage, Loc.Current))
        {
            return;
        }

        bestKindVersion = library.Version;
        bestKindLanguage = Loc.Current;
        if (bestKindLines.Length != library.Entries.Length)
        {
            bestKindLines = new string[library.Entries.Length];
        }

        for (var index = 0; index < bestKindLines.Length; index++)
        {
            if (library.BestKind(index) == RecordKind.None)
            {
                bestKindLines[index] = string.Empty;
                continue;
            }

            var kind = Loc.T(library.KindLabel(index));
            var tier = library.BestTier(index);
            bestKindLines[index] = tier.Length > 0 ? string.Concat(kind, MetaSeparator, tier) : kind;
        }
    }

    private void RefreshRankLabels(GameScoreRankDto[] ranks)
    {
        if (ReferenceEquals(ranks, labeledRanks) && ReferenceEquals(rankLabelLanguage, Loc.Current))
        {
            return;
        }

        labeledRanks = ranks;
        rankLabelLanguage = Loc.Current;
        if (rankGameIndexes.Length < ranks.Length)
        {
            rankOrder = new int[ranks.Length];
            rankGameIndexes = new int[ranks.Length];
            rankValues = new int[ranks.Length];
            rankStatIds = new string[ranks.Length];
            rankTitles = new string[ranks.Length];
            rankChips = new string[ranks.Length];
            rankTotals = new string[ranks.Length];
            rankWeeks = new string[ranks.Length];
            rankCardIds = new string[ranks.Length];
        }

        rankRowCount = 0;
        for (var index = 0; index < ranks.Length; index++)
        {
            var rank = ranks[index];
            var gameIndex = rank.Rank > 0 ? GameIndexFor(rank.GameId) : -1;
            if (gameIndex < 0)
            {
                continue;
            }

            var slot = rankRowCount++;
            rankGameIndexes[slot] = gameIndex;
            rankValues[slot] = rank.Rank;
            rankStatIds[slot] = rank.GameId;
            rankCardIds[slot] = RankCardPrefix + rank.GameId;
            var title = games[gameIndex].Title;
            rankTitles[slot] = ScoreStatIds.SuffixOf(rank.GameId).Length == 0
                ? title
                : string.Concat(title, MetaSeparator, Loc.T(LeaderboardModeName(rank.GameId, ScoreKind.Score, false)));
            rankChips[slot] = Loc.T(L.Leaderboard.RankChip, GameNumber.Label(rank.Rank));
            rankTotals[slot] = Loc.T(L.GamesHub.OfTotal, CountText.Exact(rank.Total));
            rankWeeks[slot] = rank.WeekRank > 0
                ? Loc.T(L.Leaderboard.WeekRank, GameNumber.Label(rank.WeekRank))
                : string.Empty;
        }

        SortByRank(rankOrder, rankValues, rankRowCount);
    }

    internal static void SortByRank(int[] order, int[] values, int count)
    {
        for (var position = 0; position < count; position++)
        {
            var candidate = position;
            var slot = position - 1;
            while (slot >= 0 && values[order[slot]] > values[candidate])
            {
                order[slot + 1] = order[slot];
                slot--;
            }

            order[slot + 1] = candidate;
        }
    }

    private int GameIndexFor(string statId)
    {
        for (var index = 0; index < games.Length; index++)
        {
            if (ScoreStatIds.BelongsTo(statId, games[index].Id))
            {
                return index;
            }
        }

        return -1;
    }
}
