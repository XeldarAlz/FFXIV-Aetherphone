using Aetherphone.Apps.Music.Components;
using Aetherphone.Apps.Music.Library;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const int ReplayTopCount = 10;
    private const int ReplayPeriodCount = 4;
    private const int SecondsPerMinute = 60;
    private const float ReplaySegmentRowHeight = 40f;
    private const float ReplayCardRadius = 22f;
    private const float ReplayCardPad = 18f;
    private const float ReplayChartHeight = 120f;
    private const float ReplayBarFill = 0.62f;
    private const float ReplayBarMinimum = 2f;
    private const float ReplayIdleBarAlpha = 0.5f;
    private const float ReplayEmptyBarAlpha = 0.14f;
    private const float ReplayGridAlpha = 0.12f;
    private const float ReplayCardTopAlpha = 0.34f;
    private const float ReplayCardBottomAlpha = 0.1f;
    private const float ReplayCardStrokeAlpha = 0.16f;
    private const float ReplayButtonHeight = 44f;
    private const float ReplayRankColumn = 28f;
    private const float ReplayArtistSide = 104f;
    private const float ReplayHomeCardHeight = 76f;
    private const float ReplayHomeTile = 48f;
    private const float ReplayHomeGlyphScale = 1.05f;
    private const float ReplayChevronScale = 0.65f;
    private const float ReplayMenuGlyphScale = 0.8f;
    private const float ReplayArtistRadiusFraction = 0.5f;
    private const float ReplayHomeTileRadiusFraction = 0.24f;
    private const float ReplayCardPressedAlpha = 0.24f;
    private const int ReplayHighlightedRanks = 3;
    private const string ReplayContextPrefix = "replay.";
    private const int ReplayMonthLabelStride = 3;
    private const int ReplayWeekLabelStride = 7;
    private static readonly Vector4 ReplayHomeGlyphInk = new(1f, 1f, 1f, 1f);

    private readonly string[] replayPeriodLabels = new string[ReplayPeriodCount];
    private ReplayPeriod replayPeriod = ReplayPeriod.Week;
    private ListeningSummary replaySummary = ListeningSummary.Empty;
    private Song[] replaySongs = Array.Empty<Song>();
    private string[] replaySongPlays = Array.Empty<string>();
    private string[] replaySongRanks = Array.Empty<string>();
    private string[] replayArtistPlays = Array.Empty<string>();
    private string[] replayBarLabels = Array.Empty<string>();
    private string[] replayAxisLabels = Array.Empty<string>();
    private string replayTotalText = string.Empty;
    private string replayPlaysText = string.Empty;
    private string replayAxisTop = string.Empty;
    private string replayContextId = string.Empty;
    private string replayLanguage = string.Empty;
    private int replayVersion = -1;
    private int replayDay = -1;
    private int replayTodayBar = -1;
    private long replayAxisMinutes;
    private ReplayPeriod replayBuiltPeriod = ReplayPeriod.AllTime;
    private readonly ShelfRail replayArtistRail = new();
    private string homeReplayText = string.Empty;
    private string homeReplayLanguage = string.Empty;
    private int homeReplayVersion = -1;
    private int homeReplayDay = -1;

    private void DrawReplay(in PhoneContext context)
    {
        var scale = UiScale.Current;
        EnsureReplay();
        var frame = BeginPage(context);
        using (AppSurface.BeginEdgeToEdge(frame.Body))
        {
            DrawReplayPeriods(scale);
            if (replaySummary.IsEmpty)
            {
                DrawReplayEmpty(scale);
            }
            else
            {
                DrawReplayHero(scale);
                DrawReplayPlayButton(scale);
                DrawReplayTopSongs(scale);
                DrawReplayTopArtists();
            }

            LibraryKit.Gap(LibraryBottomGap);
        }

        EndPage(in frame, context, Loc.T(L.Music.Replay.Title));
    }

    private static int ReplayToday() => DateOnly.FromDateTime(DateTime.Now).DayNumber;

    private void EnsureReplay()
    {
        var today = ReplayToday();
        var language = Loc.Current.Code;
        if (replayVersion == library.Version && replayDay == today && replayBuiltPeriod == replayPeriod &&
            string.Equals(replayLanguage, language, StringComparison.Ordinal))
        {
            return;
        }

        replayVersion = library.Version;
        replayDay = today;
        replayBuiltPeriod = replayPeriod;
        replayLanguage = language;
        var todayDate = DateOnly.FromDayNumber(today);
        replaySummary = library.BuildListening(replayPeriod, todayDate, ReplayTopCount);
        replayContextId = ReplayContextPrefix + (int)replayPeriod;
        var culture = Loc.Culture;
        replayTotalText = (replaySummary.TotalSeconds / SecondsPerMinute).ToString("N0", culture);
        replayPlaysText = PlaysText(replaySummary.TotalPlays);
        var topSongs = replaySummary.TopSongs;
        replaySongs = new Song[topSongs.Length];
        replaySongPlays = new string[topSongs.Length];
        replaySongRanks = new string[topSongs.Length];
        for (var index = 0; index < topSongs.Length; index++)
        {
            replaySongs[index] = topSongs[index].Song;
            replaySongPlays[index] = PlaysText(topSongs[index].Plays);
            replaySongRanks[index] = (index + 1).ToString(culture);
        }

        var topArtists = replaySummary.TopArtists;
        replayArtistPlays = new string[topArtists.Length];
        for (var index = 0; index < topArtists.Length; index++)
        {
            replayArtistPlays[index] = PlaysText(topArtists[index].Plays);
        }

        BuildReplayChartLabels(todayDate);
    }

    private void BuildReplayChartLabels(DateOnly today)
    {
        var bars = replaySummary.Bars;
        var culture = Loc.Culture;
        var peakMinutes = (replaySummary.PeakBarSeconds + SecondsPerMinute - 1) / SecondsPerMinute;
        replayAxisMinutes = NiceCeiling(peakMinutes);
        replayAxisTop = replayAxisMinutes.ToString("N0", culture);
        replayBarLabels = new string[bars.Length];
        replayAxisLabels = new string[bars.Length];
        replayTodayBar = -1;
        var minutesFormat = Loc.T(L.Music.Replay.MinutesShort);
        var dayNames = culture.DateTimeFormat.AbbreviatedDayNames;
        var monthNames = culture.DateTimeFormat.AbbreviatedMonthNames;
        for (var index = 0; index < bars.Length; index++)
        {
            var bar = bars[index];
            replayBarLabels[index] = string.Format(culture, minutesFormat, bar.Seconds / SecondsPerMinute);
            replayAxisLabels[index] = ReplayAxisLabel(bar.Start, index, bars.Length, dayNames, monthNames, culture);
            var isToday = replaySummary.Unit == ReplayBarUnit.Day
                ? bar.Start == today
                : bar.Start.Year == today.Year && bar.Start.Month == today.Month;
            if (isToday)
            {
                replayTodayBar = index;
            }
        }
    }

    private static string ReplayAxisLabel(DateOnly start, int index, int count, string[] dayNames,
        string[] monthNames, IFormatProvider culture)
    {
        if (count == ReplayWeekLabelStride)
        {
            return dayNames[(int)start.DayOfWeek];
        }

        if (count == ListeningStats.MonthsInYear)
        {
            return index % ReplayMonthLabelStride == 0 ? monthNames[start.Month - 1] : string.Empty;
        }

        return index % ReplayWeekLabelStride == 0 ? start.Day.ToString(culture) : string.Empty;
    }

    private static long NiceCeiling(long value)
    {
        if (value <= 0)
        {
            return 0;
        }

        var magnitude = 1L;
        while (magnitude * 10 <= value)
        {
            magnitude *= 10;
        }

        var leading = (value + magnitude - 1) / magnitude;
        var nice = leading <= 1 ? 1 : leading <= 2 ? 2 : leading <= 5 ? 5 : 10;
        return nice * magnitude;
    }

    private static string PlaysText(int plays)
    {
        return plays == 1
            ? Loc.T(L.Music.Replay.PlayOne)
            : string.Format(Loc.Culture, Loc.T(L.Music.Replay.PlaysMany), plays);
    }

    private void DrawReplayPeriods(float scale)
    {
        replayPeriodLabels[0] = Loc.T(L.Music.Replay.ThisWeek);
        replayPeriodLabels[1] = Loc.T(L.Music.Replay.ThisMonth);
        replayPeriodLabels[2] = Loc.T(L.Music.Replay.ThisYear);
        replayPeriodLabels[3] = Loc.T(L.Music.Replay.AllTime);
        LibraryKit.Gap(Metrics.Space.Sm);
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = MusicUi.Inset * scale;
        var height = ReplaySegmentRowHeight * scale;
        var row = new Rect(new Vector2(origin.X + inset, origin.Y),
            new Vector2(origin.X + width - inset, origin.Y + height));
        var picked = SegmentStrip.Draw("music.replay.period", row, replayPeriodLabels, (int)replayPeriod,
            ui.Palette);
        if (picked != (int)replayPeriod)
        {
            replayPeriod = (ReplayPeriod)picked;
            replayArtistRail.Reset();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawReplayEmpty(float scale)
    {
        LibraryKit.Gap(Metrics.Space.Xxl);
        var width = ScrollLayout.StableContentWidth();
        var textWidth = MathF.Max(1f, width - MusicUi.Inset * 2f * scale);
        LibraryKit.CenteredText(Loc.T(L.Music.Replay.EmptyTitle), TextStyles.Title3, ui.TitleInk, textWidth);
        LibraryKit.Gap(Metrics.Space.Xs);
        LibraryKit.CenteredText(Loc.T(L.Music.Replay.EmptySub), TextStyles.Subheadline, ui.MutedInk, textWidth);
    }

    private void DrawReplayHero(float scale)
    {
        LibraryKit.Gap(Metrics.Space.Lg);
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = MusicUi.Inset * scale;
        var pad = ReplayCardPad * scale;
        var numberHeight = Typography.LineHeight(TextStyles.LargeTitle);
        var captionHeight = Typography.LineHeight(TextStyles.Subheadline);
        var legendHeight = Typography.LineHeight(TextStyles.Footnote);
        var axisHeight = Typography.LineHeight(TextStyles.Footnote);
        var chartHeight = ReplayChartHeight * scale;
        var gap = Metrics.Space.Md * scale;
        var cardHeight = pad + numberHeight + captionHeight + gap + legendHeight + Metrics.Space.Sm * scale +
                         chartHeight + Metrics.Space.Xs * scale + axisHeight + pad;
        var min = new Vector2(origin.X + inset, origin.Y);
        var max = new Vector2(origin.X + width - inset, origin.Y + cardHeight);
        var drawList = ImGui.GetWindowDrawList();
        var radius = ReplayCardRadius * scale;
        var accent = ui.Accent;
        Squircle.FillVerticalGradient(drawList, min, max, radius,
            ImGui.GetColorU32(accent with { W = ReplayCardTopAlpha }),
            ImGui.GetColorU32(accent with { W = ReplayCardBottomAlpha }));
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = ReplayCardStrokeAlpha }), 1f);
        var left = min.X + pad;
        var right = max.X - pad;
        var y = min.Y + pad;
        var numberWidth = Typography.Measure(replayTotalText, TextStyles.LargeTitle).X;
        Typography.Draw(drawList, new Vector2(left, y), replayTotalText, ui.TitleInk, TextStyles.LargeTitle);
        var playsWidth = Typography.Measure(replayPlaysText, TextStyles.SubheadlineEmphasized).X;
        var playsLeft = MathF.Max(left + numberWidth + gap, right - playsWidth);
        var playsHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(playsLeft, y + numberHeight - playsHeight),
            Typography.FitText(replayPlaysText, MathF.Max(1f, right - playsLeft), TextStyles.SubheadlineEmphasized),
            accent, TextStyles.SubheadlineEmphasized);
        y += numberHeight;
        Typography.Draw(drawList, new Vector2(left, y),
            Typography.FitText(Loc.T(L.Music.Replay.MinutesListened), right - left, TextStyles.Subheadline),
            ui.MutedInk, TextStyles.Subheadline);
        y += captionHeight + gap;
        var legend = Loc.T(replaySummary.Unit == ReplayBarUnit.Day
            ? L.Music.Replay.MinutesPerDay
            : L.Music.Replay.MinutesPerMonth);
        Typography.Draw(drawList, new Vector2(left, y), Typography.FitText(legend, right - left, TextStyles.Footnote),
            ui.MutedInk, TextStyles.Footnote);
        y += legendHeight + Metrics.Space.Sm * scale;
        DrawReplayChart(drawList, new Rect(new Vector2(left, y), new Vector2(right, y + chartHeight)), scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight));
    }

    private void DrawReplayChart(ImDrawListPtr drawList, Rect area, float scale)
    {
        var bars = replaySummary.Bars;
        if (bars.Length == 0)
        {
            return;
        }

        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        var gutter = MathF.Max(Typography.Measure(replayAxisTop, TextStyles.Footnote).X,
            Typography.Measure("0", TextStyles.Footnote).X) + Metrics.Space.Xs * scale;
        var plotMin = new Vector2(area.Min.X + gutter, area.Min.Y + labelHeight * 0.5f);
        var plotMax = new Vector2(area.Max.X, area.Max.Y);
        var plotHeight = MathF.Max(1f, plotMax.Y - plotMin.Y);
        var grid = ImGui.GetColorU32(ui.TitleInk with { W = ReplayGridAlpha });
        drawList.AddLine(new Vector2(plotMin.X, plotMax.Y), plotMax, grid, scale);
        drawList.AddLine(plotMin, new Vector2(plotMax.X, plotMin.Y), grid, scale);
        drawList.AddLine(new Vector2(plotMin.X, (plotMin.Y + plotMax.Y) * 0.5f),
            new Vector2(plotMax.X, (plotMin.Y + plotMax.Y) * 0.5f), grid, scale);
        Typography.Draw(drawList, new Vector2(area.Min.X, plotMin.Y - labelHeight * 0.5f), replayAxisTop,
            ui.MutedInk, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(area.Min.X, plotMax.Y - labelHeight * 0.5f), "0", ui.MutedInk,
            TextStyles.Footnote);
        var slot = (plotMax.X - plotMin.X) / bars.Length;
        var barWidth = MathF.Max(ReplayBarMinimum * scale, slot * ReplayBarFill);
        var axisMaxSeconds = replayAxisMinutes * (float)SecondsPerMinute;
        var hoveredBar = -1;
        for (var index = 0; index < bars.Length; index++)
        {
            var centerX = plotMin.X + slot * (index + 0.5f);
            var barMinX = centerX - barWidth * 0.5f;
            var barMaxX = centerX + barWidth * 0.5f;
            var seconds = bars[index].Seconds;
            var fraction = axisMaxSeconds > 0f ? MathF.Min(1f, seconds / axisMaxSeconds) : 0f;
            var barHeight = MathF.Max(ReplayBarMinimum * scale, fraction * plotHeight);
            var color = seconds <= 0
                ? ui.TitleInk with { W = ReplayEmptyBarAlpha }
                : index == replayTodayBar ? ui.Accent : ui.Accent with { W = ReplayIdleBarAlpha };
            drawList.AddRectFilled(new Vector2(barMinX, plotMax.Y - barHeight), new Vector2(barMaxX, plotMax.Y),
                ImGui.GetColorU32(color), MathF.Min(barWidth * 0.5f, Metrics.Radius.Sm * scale),
                ImDrawFlags.RoundCornersTop);
            if (UiInteract.Hover(new Vector2(plotMin.X + slot * index, plotMin.Y),
                    new Vector2(plotMin.X + slot * (index + 1), plotMax.Y)))
            {
                hoveredBar = index;
            }

            var axisLabel = replayAxisLabels[index];
            if (axisLabel.Length == 0)
            {
                continue;
            }

            var labelWidth = Typography.Measure(axisLabel, TextStyles.Footnote).X;
            var labelLeft = Math.Clamp(centerX - labelWidth * 0.5f, plotMin.X, plotMax.X - labelWidth);
            Typography.Draw(drawList, new Vector2(labelLeft, plotMax.Y + Metrics.Space.Xs * scale), axisLabel,
                index == replayTodayBar ? ui.TitleInk : ui.MutedInk, TextStyles.Footnote);
        }

        if (hoveredBar < 0 || bars[hoveredBar].Seconds <= 0)
        {
            return;
        }

        var tip = replayBarLabels[hoveredBar];
        var tipSize = Typography.Measure(tip, TextStyles.FootnoteEmphasized);
        var tipCenterX = plotMin.X + slot * (hoveredBar + 0.5f);
        var tipLeft = Math.Clamp(tipCenterX - tipSize.X * 0.5f, plotMin.X, plotMax.X - tipSize.X);
        Typography.Draw(drawList, new Vector2(tipLeft, plotMin.Y - labelHeight * 0.5f), tip, ui.TitleInk,
            TextStyles.FootnoteEmphasized);
    }

    private void DrawReplayPlayButton(float scale)
    {
        if (replaySongs.Length == 0)
        {
            return;
        }

        LibraryKit.Gap(Metrics.Space.Md);
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = MusicUi.Inset * scale;
        var height = ReplayButtonHeight * scale;
        var rect = new Rect(new Vector2(origin.X + inset, origin.Y),
            new Vector2(origin.X + width - inset, origin.Y + height));
        if (LibraryKit.ActionButton(rect, IconGlyph.Of(FontAwesomeIcon.Play), Loc.T(L.Music.Replay.PlayTopSongs), ui))
        {
            playback.PlaySongs(replaySongs, 0, replayContextId, Loc.T(L.Music.Replay.Title));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawReplayTopSongs(float scale)
    {
        if (replaySongs.Length == 0)
        {
            return;
        }

        SectionHeader.Draw(ui, Loc.T(L.Music.Replay.TopSongs), false);
        for (var index = 0; index < replaySongs.Length; index++)
        {
            var action = DrawReplaySongRow(index, scale);
            if (action == SongRowAction.Play)
            {
                PlayFrom(replaySongs, index, replayContextId, Loc.T(L.Music.Replay.Title));
            }
            else if (action == SongRowAction.Menu)
            {
                songMenu.Open(replaySongs[index]);
            }
        }
    }

    private SongRowAction DrawReplaySongRow(int index, float scale)
    {
        var height = SongRow.Height * scale;
        var width = ScrollLayout.StableContentWidth();
        if (!ImGui.IsRectVisible(new Vector2(width, height)))
        {
            ImGui.Dummy(new Vector2(width, height));
            return SongRowAction.None;
        }

        var song = replaySongs[index];
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, height, ui.HoverWash);
        var min = cell.Bounds.Min;
        var max = cell.Bounds.Max;
        var inset = MusicUi.Inset * scale;
        var centerY = min.Y + height * 0.5f;
        var current = kit.IsCurrent(song);
        var rank = replaySongRanks[index];
        var rankSize = Typography.Measure(rank, TextStyles.Headline);
        var rankColumn = ReplayRankColumn * scale;
        var rankLeft = min.X + inset + (rankColumn - rankSize.X) * 0.5f;
        Typography.Draw(drawList, new Vector2(rankLeft, centerY - rankSize.Y * 0.5f), rank,
            index < ReplayHighlightedRanks ? ui.Accent : ui.MutedInk, TextStyles.Headline);
        var side = ArtworkTile.Side(ArtworkTile.RowArt);
        var artMin = new Vector2(min.X + inset + rankColumn + Metrics.Space.Xs * scale,
            min.Y + (height - side) * 0.5f);
        ArtworkTile.Draw(drawList, images, artMin, side, song.ThumbnailUrl, song.Title);
        var textLeft = artMin.X + side + Metrics.Space.Md * scale;
        var menuRadius = Metrics.Space.Lg * scale;
        var menuCenter = new Vector2(max.X - inset - menuRadius * 0.5f, centerY);
        var plays = replaySongPlays[index];
        var playsWidth = Typography.Measure(plays, TextStyles.Footnote).X;
        var playsRight = menuCenter.X - menuRadius - Metrics.Space.Xs * scale;
        var textWidth = MathF.Max(1f, playsRight - playsWidth - Metrics.Space.Sm * scale - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Body);
        var subtitleHeight = Typography.LineHeight(TextStyles.Subheadline);
        var titleTop = min.Y + (height - titleHeight - subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, titleTop), Typography.FitText(song.Title, textWidth,
            TextStyles.Body), current ? ui.Accent : ui.TitleInk, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(textLeft, titleTop + titleHeight),
            Typography.FitText(song.Author ?? string.Empty, textWidth, TextStyles.Subheadline), ui.MutedInk,
            TextStyles.Subheadline);
        var playsHeight = Typography.LineHeight(TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(playsRight - playsWidth, centerY - playsHeight * 0.5f), plays,
            ui.MutedInk, TextStyles.Footnote);
        var menuTapped = ui.IconButton(menuCenter, menuRadius, IconGlyph.Of(FontAwesomeIcon.EllipsisH), ui.MutedInk,
            AppSkin.Transparent, ReplayMenuGlyphScale, Loc.T(L.Music.MoreOptions));
        var rightClicked = cell.Hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Right);
        FeedCell.End(drawList, cell, ui.Hairline, false);
        FeedCell.Hairline(drawList, textLeft, max.X, max.Y, ui.Hairline);
        if (menuTapped || rightClicked)
        {
            return SongRowAction.Menu;
        }

        return cell.Tapped ? SongRowAction.Play : SongRowAction.None;
    }

    private void DrawReplayTopArtists()
    {
        var artists = replaySummary.TopArtists;
        if (artists.Length == 0)
        {
            return;
        }

        var side = ArtworkTile.Side(ReplayArtistSide);
        replayArtistRail.Begin(ui, Loc.T(L.Music.Replay.TopArtists), false, artists.Length, side,
            ArtworkTile.CardHeight(side));
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < artists.Length; index++)
        {
            if (!replayArtistRail.Tile(index, out var tile))
            {
                continue;
            }

            var artist = artists[index];
            var hovered = replayArtistRail.Hover(tile);
            ArtworkTile.Draw(drawList, images, tile.Min, side, artist.ThumbnailUrl, artist.Name,
                ReplayArtistRadiusFraction);
            ArtworkTile.DrawPressed(drawList, tile.Min, side, hovered, ReplayArtistRadiusFraction);
            ArtworkTile.DrawCaption(drawList, ui, tile.Min, side, artist.Name, replayArtistPlays[index]);
            if (replayArtistRail.Tapped(tile, hovered))
            {
                Push(MusicRoute.Artist(artist.ChannelId, artist.Name));
            }
        }

        replayArtistRail.End();
    }

    private void DrawHomeReplayCard(float scale)
    {
        EnsureHomeReplay();
        LibraryKit.Gap(MusicUi.SectionGap);
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = MusicUi.Inset * scale;
        var height = ReplayHomeCardHeight * scale;
        var min = new Vector2(origin.X + inset, origin.Y);
        var max = new Vector2(origin.X + width - inset, origin.Y + height);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(min, max);
        var radius = ReplayCardRadius * scale;
        var accent = ui.Accent;
        var topAlpha = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left)
            ? ReplayCardPressedAlpha
            : ReplayCardTopAlpha;
        Squircle.FillVerticalGradient(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = topAlpha }),
            ImGui.GetColorU32(accent with { W = ReplayCardBottomAlpha }));
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = ReplayCardStrokeAlpha }), 1f);
        var pad = Metrics.Space.Lg * scale;
        var tile = ReplayHomeTile * scale;
        var tileMin = new Vector2(min.X + pad, min.Y + (height - tile) * 0.5f);
        var tileMax = tileMin + new Vector2(tile, tile);
        Squircle.Fill(drawList, tileMin, tileMax, tile * ReplayHomeTileRadiusFraction, ImGui.GetColorU32(accent));
        AppSkin.Icon(drawList, (tileMin + tileMax) * 0.5f, IconGlyph.Of(FontAwesomeIcon.ChartBar), ReplayHomeGlyphInk,
            ReplayHomeGlyphScale);
        var chevronCenter = new Vector2(max.X - pad, min.Y + height * 0.5f);
        AppSkin.Icon(drawList, chevronCenter, IconGlyph.Of(FontAwesomeIcon.ChevronRight), ui.MutedInk,
            ReplayChevronScale);
        var textLeft = tileMax.X + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, chevronCenter.X - Metrics.Space.Lg * scale - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var subtitleHeight = Typography.LineHeight(TextStyles.Subheadline);
        var top = min.Y + (height - titleHeight - subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(Loc.T(L.Music.Replay.Title), textWidth, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight),
            Typography.FitText(homeReplayText, textWidth, TextStyles.Subheadline), ui.MutedInk, TextStyles.Subheadline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        if (UiInteract.Click(min, max, hovered))
        {
            Push(MusicRoute.Of(MusicScreen.Replay));
        }
    }

    private void EnsureHomeReplay()
    {
        var today = ReplayToday();
        var language = Loc.Current.Code;
        if (homeReplayVersion == library.Version && homeReplayDay == today &&
            string.Equals(homeReplayLanguage, language, StringComparison.Ordinal))
        {
            return;
        }

        homeReplayVersion = library.Version;
        homeReplayDay = today;
        homeReplayLanguage = language;
        var week = library.BuildListening(ReplayPeriod.Week, DateOnly.FromDayNumber(today), 0);
        var minutes = week.TotalSeconds / SecondsPerMinute;
        homeReplayText = minutes > 0
            ? string.Format(Loc.Culture, Loc.T(L.Music.Replay.HomeWeekMinutes), minutes.ToString("N0", Loc.Culture))
            : Loc.T(L.Music.Replay.HomeTeaser);
    }
}
