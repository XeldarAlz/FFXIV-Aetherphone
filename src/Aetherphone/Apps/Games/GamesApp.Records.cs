using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp
{
    private const string RecordsNavId = "games.records.nav";
    private const float SummaryHeight = 118f;
    private const float SummaryRingRadius = 22f;
    private const float SummaryRingStroke = 5f;
    private const float SummaryIconSize = 22f;
    private const float SummaryValueTop = 62f;
    private const float SummaryLabelGap = 2f;
    private const float DailyRowHeight = 72f;
    private const float DailyIconSize = 44f;
    private const float DailyPillHeight = Button.SmallHeight;
    private const float RecordRowHeight = 62f;
    private const float RecordIconSize = 40f;
    private const float RecordIconGap = 12f;
    private const float RecordValueReserve = 0.38f;
    private const float EmptyBlockHeight = 300f;
    private const float RankChevronSize = 13f;
    private const float JoinRanksGlyphScale = 0.5f;
    private const int SummaryColumns = 3;
    private const string RankRowPrefix = "games.records.rank.";
    private const string RankSeparator = " · ";

    private CachedText recordsText;
    private CachedText streakText;
    private CachedText recordCountText;
    private GameScoreRankDto[] labeledRanks = Array.Empty<GameScoreRankDto>();
    private LanguageInfo? rankLabelLanguage;
    private int[] rankGameIndexes = Array.Empty<int>();
    private string[] rankStatIds = Array.Empty<string>();
    private string[] rankTitles = Array.Empty<string>();
    private string[] rankLines = Array.Empty<string>();
    private string[] rankRowIds = Array.Empty<string>();
    private int rankRowCount;

    private void DrawRecords(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var y = DrawSummary(origin.X, origin.Y, width, scale);
            y = DrawDailyRow(origin.X, y + Metrics.Space.Md * scale, width, scale);
            y = DrawYourRanks(origin, y, width, scale);
            y += GamesHubArt.SectionGap * scale;
            var records = library.Records;
            var drawList = ImGui.GetWindowDrawList();
            if (records.Length == 0)
            {
                var empty = new Rect(new Vector2(origin.X, y), new Vector2(origin.X + width, y + EmptyBlockHeight * scale));
                if (GamesHubArt.StateScreen(drawList, ui, empty, FontAwesomeIcon.Trophy, Loc.T(L.GamesHub.RecordsEmptyTitle),
                        Loc.T(L.GamesHub.RecordsEmptyHint), Loc.T(L.GamesHub.PlayToday), "games.records.playToday"))
                {
                    OpenGame(games[featuredIndex]);
                }

                y = empty.Max.Y;
            }
            else
            {
                GamesHubArt.Section(drawList, ui, origin.X, y, width, Loc.T(L.GamesHub.PersonalBests), string.Empty,
                    string.Empty);
                y += GamesHubArt.SectionHeight * scale;
                y = DrawRecordRows(records, origin, y, width, scale);
            }

            FinishPage(origin, width, y, scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, RecordsNavId, Loc.T(L.GamesHub.TabRecords), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private float DrawSummary(float left, float top, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var height = SummaryHeight * scale;
        var min = new Vector2(left, top);
        var max = new Vector2(left + width, top + height);
        var rounding = Metrics.Radius.Widget * scale;
        ui.Card(drawList, min, max, rounding);
        GamesHubArt.ReportAnchor("games.records.summary", new Rect(min, max));
        var column = width / SummaryColumns;
        var played = library.PlayedCount;
        var total = library.Entries.Length;
        var accent = ui.Accent;
        var visualTop = top + Metrics.Space.Lg * scale;
        var ringCenter = new Vector2(left + column * 0.5f, visualTop + SummaryRingRadius * scale);
        var stroke = SummaryRingStroke * scale;
        ProgressRing.Track(drawList, ringCenter, SummaryRingRadius * scale, stroke, Palette.WithAlpha(accent, 0.22f));
        ProgressRing.Fill(drawList, ringCenter, SummaryRingRadius * scale, stroke,
            total > 0 ? played / (float)total : 0f, accent);
        ProgressRing.CenterIcon(drawList, ringCenter, FontAwesomeIcon.Gamepad, accent, SummaryIconSize * scale * 0.8f);
        var playedKey = ((long)played << 20) | (uint)total;
        var playedLabel = recordsText.IsCurrent(playedKey)
            ? recordsText.Value
            : recordsText.Store(playedKey, Loc.T(L.GamesHub.PlayedOf, GameNumber.Label(played), GameNumber.Label(total)));
        DrawSummaryColumn(drawList, left, column, top, playedLabel, Loc.T(L.GamesHub.StatPlayed), scale);

        var streak = stats.DailyStreak;
        var flameCenter = new Vector2(left + column * 1.5f, ringCenter.Y);
        ProgressRing.CenterIcon(drawList, flameCenter, stats.DailyDone ? FontAwesomeIcon.Check : FontAwesomeIcon.Fire,
            streak > 0 ? StreakEmber : ui.MutedInk, SummaryIconSize * scale);
        var streakLabel = streakText.IsCurrent(streak) ? streakText.Value : streakText.Store(streak, GameNumber.Label(streak));
        DrawSummaryColumn(drawList, left + column, column, top, streakLabel, Loc.T(L.GamesHub.StatStreak), scale);

        var recordCount = library.Records.Length;
        var trophyCenter = new Vector2(left + column * 2.5f, ringCenter.Y);
        ProgressRing.CenterIcon(drawList, trophyCenter, FontAwesomeIcon.Trophy,
            recordCount > 0 ? accent : ui.MutedInk, SummaryIconSize * scale);
        var recordLabel = recordCountText.IsCurrent(recordCount)
            ? recordCountText.Value
            : recordCountText.Store(recordCount, GameNumber.Label(recordCount));
        DrawSummaryColumn(drawList, left + column * 2f, column, top, recordLabel, Loc.T(L.GamesHub.StatRecords), scale);
        for (var index = 1; index < SummaryColumns; index++)
        {
            var x = left + column * index;
            drawList.AddLine(new Vector2(x, top + Metrics.Space.Lg * scale), new Vector2(x, max.Y - Metrics.Space.Lg * scale),
                ImGui.GetColorU32(ui.Hairline), Metrics.Stroke.Hairline);
        }

        return max.Y;
    }

    private void DrawSummaryColumn(ImDrawListPtr drawList, float left, float width, float top, string value,
        string label, float scale)
    {
        var centerX = left + width * 0.5f;
        var textWidth = MathF.Max(1f, width - Metrics.Space.Sm * 2f * scale);
        var valueY = top + SummaryValueTop * scale;
        var fittedValue = Typography.FitText(value, textWidth, TextStyles.Title3);
        var valueSize = Typography.Measure(fittedValue, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(centerX - valueSize.X * 0.5f, valueY), fittedValue, ui.TitleInk,
            TextStyles.Title3);
        var fittedLabel = Typography.FitText(label, textWidth, TextStyles.Footnote);
        var labelSize = Typography.Measure(fittedLabel, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(centerX - labelSize.X * 0.5f, valueY + valueSize.Y + SummaryLabelGap * scale),
            fittedLabel, ui.MutedInk, TextStyles.Footnote);
    }

    private float DrawDailyRow(float left, float top, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var game = games[featuredIndex];
        var height = DailyRowHeight * scale;
        var min = new Vector2(left, top);
        var max = new Vector2(left + width, top + height);
        var rounding = Metrics.Radius.Widget * scale;
        var done = stats.DailyDone;
        var pillHeight = DailyPillHeight * scale;
        var pillLabel = Loc.T(L.Games.Play);
        var pillWidth = GamesHubArt.ButtonWidth(pillLabel, pillHeight);
        var pad = Metrics.Space.Lg * scale;
        var pillRect = new Rect(new Vector2(max.X - pad - pillWidth, top + (height - pillHeight) * 0.5f),
            new Vector2(max.X - pad, top + (height + pillHeight) * 0.5f));
        ui.Card(drawList, min, max, rounding);
        var iconSize = DailyIconSize * scale;
        var iconCenter = new Vector2(min.X + pad + iconSize * 0.5f, top + height * 0.5f);
        DrawGameIcon(drawList, game.Id, game.Accent, iconCenter, iconSize, scale);
        var textLeft = iconCenter.X + iconSize * 0.5f + RecordIconGap * scale;
        var trailing = done ? Metrics.Size.TapTarget * scale : pillWidth + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, max.X - pad - trailing - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var textTop = top + (height - titleHeight - subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(Loc.T(L.Games.Daily), textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        var subtitle = done ? Loc.T(L.WidgetsUtility.PlayedToday) : game.Title;
        Typography.Draw(drawList, new Vector2(textLeft, textTop + titleHeight),
            Typography.FitText(subtitle, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        if (done)
        {
            ProgressRing.CenterIcon(drawList, new Vector2(max.X - pad - SummaryIconSize * scale * 0.5f, iconCenter.Y),
                FontAwesomeIcon.CheckCircle, ui.Accent, SummaryIconSize * scale);
            return max.Y;
        }

        if (Button.Draw(drawList, pillRect, pillLabel, ui.Ink, id: "games.records.daily"))
        {
            OpenGame(game);
        }

        return max.Y;
    }

    private float DrawRecordRows(ReadOnlySpan<int> records, Vector2 origin, float top, float width, float scale)
    {
        ImGui.SetCursorScreenPos(new Vector2(origin.X, top));
        var card = GroupCard.Begin(ui, records.Length, RecordRowHeight);
        card.SeparatorInset = RecordIconSize + RecordIconGap;
        var drawList = ImGui.GetWindowDrawList();
        var clipMin = drawList.GetClipRectMin();
        var clipMax = drawList.GetClipRectMax();
        var activate = -1;
        for (var index = 0; index < records.Length; index++)
        {
            var row = card.NextRow();
            if (row.Max.Y < clipMin.Y || row.Min.Y > clipMax.Y)
            {
                continue;
            }

            if (DrawRecordRow(drawList, row, records[index], scale))
            {
                activate = records[index];
            }
        }

        card.End();
        if (activate >= 0)
        {
            Activate(activate);
        }

        return card.Bounds.Max.Y;
    }

    private bool DrawRecordRow(ImDrawListPtr drawList, Rect row, int entryIndex, float scale)
    {
        var padding = Metrics.Space.Lg * scale;
        var hit = new Rect(new Vector2(row.Min.X - padding, row.Min.Y), new Vector2(row.Max.X + padding, row.Max.Y));
        var hovered = UiInteract.Hover(hit.Min, hit.Max);
        if (hovered)
        {
            drawList.AddRectFilled(hit.Min, hit.Max, ImGui.GetColorU32(ui.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        ref readonly var entry = ref library.Entries[entryIndex];
        var iconSize = RecordIconSize * scale;
        var iconCenter = new Vector2(row.Min.X + iconSize * 0.5f, row.Center.Y);
        DrawEntryIcon(drawList, entry, library.Accent(entryIndex), iconCenter, iconSize, scale);
        var textLeft = iconCenter.X + iconSize * 0.5f + RecordIconGap * scale;
        var valueWidth = row.Width * RecordValueReserve;
        var textWidth = MathF.Max(1f, row.Max.X - valueWidth - Metrics.Space.Sm * scale - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var textTop = row.Center.Y - (titleHeight + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(library.Title(entryIndex), textWidth, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + titleHeight),
            Typography.FitText(Loc.T(GameGenres.Label(entry.Genre)), textWidth, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        var value = Typography.FitText(library.BestValue(entryIndex), valueWidth, TextStyles.Headline);
        var valueSize = Typography.Measure(value, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(row.Max.X - valueSize.X, textTop), value, ui.TitleInk,
            TextStyles.Headline);
        var kind = Typography.FitText(Loc.T(KindLabel(library.BestKind(entryIndex))), valueWidth, TextStyles.Footnote);
        var kindSize = Typography.Measure(kind, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(row.Max.X - kindSize.X, textTop + titleHeight), kind, ui.MutedInk,
            TextStyles.Footnote);
        return UiInteract.Click(hit.Min, hit.Max, hovered);
    }

    private float DrawYourRanks(Vector2 origin, float top, float width, float scale)
    {
        if (leaderboard.OptedOut)
        {
            return DrawJoinRanks(origin, top, width, scale);
        }

        var ranks = leaderboard.MyRanks;
        if (!leaderboard.IsSignedIn || ranks.Length == 0)
        {
            return top;
        }

        RefreshRankLabels(ranks);
        if (rankRowCount == 0)
        {
            return top;
        }

        var drawList = ImGui.GetWindowDrawList();
        var y = top + GamesHubArt.SectionGap * scale;
        GamesHubArt.Section(drawList, ui, origin.X, y, width, Loc.T(L.Stage.YourRanks), string.Empty, string.Empty);
        y += GamesHubArt.SectionHeight * scale;
        ImGui.SetCursorScreenPos(new Vector2(origin.X, y));
        var card = GroupCard.Begin(ui, rankRowCount, RecordRowHeight);
        card.SeparatorInset = RecordIconSize + RecordIconGap;
        var clipMin = drawList.GetClipRectMin();
        var clipMax = drawList.GetClipRectMax();
        var activate = -1;
        for (var rowIndex = 0; rowIndex < rankRowCount; rowIndex++)
        {
            var row = card.NextRow();
            if (row.Max.Y < clipMin.Y || row.Min.Y > clipMax.Y)
            {
                continue;
            }

            if (DrawRankRow(drawList, row, rowIndex, scale))
            {
                activate = rowIndex;
            }
        }

        card.End();
        if (activate >= 0)
        {
            OpenLeaderboard(games[rankGameIndexes[activate]], rankStatIds[activate], TabTitle(GamesTab.Records));
        }

        return card.Bounds.Max.Y;
    }

    private float DrawJoinRanks(Vector2 origin, float top, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var y = top + GamesHubArt.SectionGap * scale;
        GamesHubArt.Section(drawList, ui, origin.X, y, width, Loc.T(L.Stage.YourRanks), string.Empty, string.Empty);
        y += GamesHubArt.SectionHeight * scale;
        var label = Loc.T(L.Leaderboard.JoinToSeeRanks);
        var iconSize = RecordIconSize * scale;
        var chevron = RankChevronSize * scale;
        var rowInset = Metrics.Space.Lg * scale;
        var textWidth = MathF.Max(1f,
            width - rowInset * 2f - iconSize - RecordIconGap * scale - chevron - Metrics.Space.Sm * scale);
        var textHeight = Typography.MeasureWrappedBlock(label, TextStyles.Headline, textWidth).Y;
        var rowHeight = MathF.Max(RecordRowHeight, textHeight / scale + Metrics.Space.Md * 2f);
        ImGui.SetCursorScreenPos(new Vector2(origin.X, y));
        var card = GroupCard.Begin(ui, 1, rowHeight);
        var row = card.NextRow();
        var padding = Metrics.Space.Lg * scale;
        var hit = new Rect(new Vector2(row.Min.X - padding, row.Min.Y), new Vector2(row.Max.X + padding, row.Max.Y));
        var hovered = UiInteract.Hover(hit.Min, hit.Max);
        if (hovered)
        {
            drawList.AddRectFilled(hit.Min, hit.Max, ImGui.GetColorU32(ui.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var iconMin = new Vector2(row.Min.X, row.Center.Y - iconSize * 0.5f);
        var iconMax = iconMin + new Vector2(iconSize, iconSize);
        IconTile.FillShaded(drawList, iconMin, iconMax, iconSize * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, (iconMin + iconMax) * 0.5f, FontAwesomeIcon.Trophy, AccentRing.Ink,
            iconSize * JoinRanksGlyphScale);
        PhoneIcon.Draw(drawList, new Vector2(row.Max.X - chevron * 0.5f, row.Center.Y), PhoneIcons.ChevronRight,
            ui.MutedInk, chevron);
        Typography.DrawWrappedLeft(new Vector2(iconMax.X + RecordIconGap * scale, row.Center.Y - textHeight * 0.5f),
            label, ui.TitleInk, TextStyles.Headline, textWidth);
        card.End();
        if (UiInteract.Click(hit.Min, hit.Max, hovered))
        {
            RequestConsent();
        }

        return card.Bounds.Max.Y;
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
            rankGameIndexes = new int[ranks.Length];
            rankStatIds = new string[ranks.Length];
            rankTitles = new string[ranks.Length];
            rankLines = new string[ranks.Length];
            rankRowIds = new string[ranks.Length];
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
            rankStatIds[slot] = rank.GameId;
            rankRowIds[slot] = RankRowPrefix + rank.GameId;
            var title = games[gameIndex].Title;
            rankTitles[slot] = ScoreStatIds.SuffixOf(rank.GameId).Length == 0
                ? title
                : string.Concat(title, RankSeparator, Loc.T(LeaderboardModeName(rank.GameId, ScoreKind.Score, false)));
            var line = Loc.T(L.Stage.RankOf, GameNumber.Label(rank.Rank), CountText.Exact(rank.Total));
            rankLines[slot] = rank.WeekRank > 0
                ? string.Concat(line, RankSeparator, Loc.T(L.Leaderboard.WeekRank, GameNumber.Label(rank.WeekRank)))
                : line;
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

    private bool DrawRankRow(ImDrawListPtr drawList, Rect row, int rowIndex, float scale)
    {
        var padding = Metrics.Space.Lg * scale;
        var hit = new Rect(new Vector2(row.Min.X - padding, row.Min.Y), new Vector2(row.Max.X + padding, row.Max.Y));
        var hovered = UiInteract.Hover(hit.Min, hit.Max);
        if (hovered)
        {
            drawList.AddRectFilled(hit.Min, hit.Max, ImGui.GetColorU32(ui.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var game = games[rankGameIndexes[rowIndex]];
        var iconSize = RecordIconSize * scale;
        var iconCenter = new Vector2(row.Min.X + iconSize * 0.5f, row.Center.Y);
        DrawGameIcon(drawList, game.Id, game.Accent, iconCenter, iconSize, scale);
        var chevron = RankChevronSize * scale;
        PhoneIcon.Draw(drawList, new Vector2(row.Max.X - chevron * 0.5f, row.Center.Y), PhoneIcons.ChevronRight,
            ui.MutedInk, chevron);
        var textLeft = iconCenter.X + iconSize * 0.5f + RecordIconGap * scale;
        var textWidth = MathF.Max(1f, row.Max.X - chevron - Metrics.Space.Sm * scale - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var textTop = row.Center.Y - (titleHeight + subtitleHeight) * 0.5f;
        Marquee.DrawLeft(drawList, rankRowIds[rowIndex], rankTitles[rowIndex], textLeft, textTop, textWidth,
            TextStyles.Headline, ui.TitleInk, hovered);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + titleHeight),
            Typography.FitText(rankLines[rowIndex], textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        return UiInteract.Click(hit.Min, hit.Max, hovered);
    }

    private static LocString KindLabel(RecordKind kind) => kind switch
    {
        RecordKind.Time => L.GamesHub.KindTime,
        RecordKind.Level => L.GamesHub.KindLevel,
        RecordKind.Streak => L.GamesHub.KindStreak,
        _ => L.GamesHub.KindScore,
    };
}
