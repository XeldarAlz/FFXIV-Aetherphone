using Aetherphone.Apps.Music.NowPlaying;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lyrics;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float LyricsTopPadding = 16f;
    private const float LyricsLineGap = 16f;
    private const float LyricsEdgeFade = 36f;
    private const float LyricsLeadSeconds = 0.15f;
    private const float LyricsFollowSmoothTime = 0.28f;
    private const float LyricsGlowSmoothTime = 0.16f;
    private const float LyricsPastAlpha = 0.42f;
    private const float LyricsUpcomingAlpha = 0.28f;
    private const float LyricsPlainAlpha = 0.85f;
    private const float LyricsUnsungAlpha = 0.45f;
    private const float LyricsHoverAlpha = 0.06f;
    private const float LyricsTrailingSeconds = 5f;
    private const float ShimmerBarHeight = 22f;
    private const float ShimmerBarGap = 14f;
    private const float ShimmerSpeed = 2.4f;
    private const float ShimmerStagger = 0.7f;
    private const float ShimmerBase = 0.08f;
    private const float ShimmerSwing = 0.07f;
    private const float LyricsIconScale = 1.6f;
    private const float RetryPillHeight = 34f;
    private const float RetryPillPad = 18f;

    private static readonly TextStyle LyricStyle = TextStyles.Title2;
    private static readonly float[] ShimmerWidths = [0.78f, 0.92f, 0.64f, 0.84f];

    private readonly PaneScroll lyricsScroll = new();
    private LrcDocument? lyricsDocument;
    private float lyricsWidth;
    private float lyricsLineHeight;
    private float[] lyricLineTops = [];
    private float[] lyricLineHeights = [];
    private string[][] lyricSegments = [];
    private int[]?[] lyricWordSegment = [];
    private float[]?[] lyricWordStartX = [];
    private float[]?[] lyricWordEndX = [];
    private float lyricsContentHeight;
    private float lyricsPausedUntil;
    private Spring lyricsFollow;
    private Spring lyricsGlow = new(1f);
    private int lyricsActiveLine = -1;
    private int lyricsPreviousLine = -1;
    private string lyricsVideoId = string.Empty;

    private void DrawLyricsPane(ImDrawListPtr drawList, Rect area, bool interactive, float scale, float delta)
    {
        if (!playback.SongActive)
        {
            DrawLyricsMessage(drawList, area, FontAwesomeIcon.QuoteRight,
                Loc.T(L.Music.NowPlaying.LyricsUnavailable), scale);
            return;
        }

        var song = playback.CurrentSong;
        if (!string.Equals(song.VideoId, lyricsVideoId, StringComparison.Ordinal))
        {
            lyricsVideoId = song.VideoId;
            lyricsScroll.Reset();
            lyricsFollow.SnapTo(0f);
            lyricsPausedUntil = 0f;
            lyricsActiveLine = -1;
            lyricsPreviousLine = -1;
        }

        var state = lyrics.Get(song);
        switch (state.Status)
        {
            case LyricsStatus.Loading:
                DrawLyricsShimmer(drawList, area, scale);
                return;
            case LyricsStatus.Instrumental:
                DrawLyricsMessage(drawList, area, FontAwesomeIcon.Music, Loc.T(L.Music.NowPlaying.Instrumental),
                    scale);
                return;
            case LyricsStatus.NotFound:
                DrawLyricsMessage(drawList, area, FontAwesomeIcon.QuoteRight,
                    Loc.T(L.Music.NowPlaying.LyricsUnavailable), scale);
                return;
            case LyricsStatus.Failed:
                var retryTop = DrawLyricsMessage(drawList, area, FontAwesomeIcon.ExclamationCircle,
                    Loc.T(L.Music.NowPlaying.LyricsFailed), scale);
                if (DrawRetryPill(drawList, area.Center.X, retryTop + Metrics.Space.Lg * scale, interactive, scale))
                {
                    lyrics.Retry(song);
                }

                return;
            default:
                DrawLyricLines(drawList, area, state.Document, interactive, scale, delta);
                return;
        }
    }

    private void DrawLyricsShimmer(ImDrawListPtr drawList, Rect area, float scale)
    {
        var left = area.Min.X + NowPlayingSide * scale;
        var width = area.Width - NowPlayingSide * 2f * scale;
        var height = ShimmerBarHeight * scale;
        var top = area.Min.Y + LyricsTopPadding * scale;
        for (var barIndex = 0; barIndex < ShimmerWidths.Length; barIndex++)
        {
            var wave = 0.5f + 0.5f * MathF.Sin(nowPlayingClock * ShimmerSpeed - barIndex * ShimmerStagger);
            var alpha = ShimmerBase + ShimmerSwing * wave;
            var min = new Vector2(left, top + barIndex * (height + ShimmerBarGap * scale));
            drawList.AddRectFilled(min, min + new Vector2(width * ShimmerWidths[barIndex], height),
                ImGui.GetColorU32(NowPlayingInk with { W = alpha }), height * 0.5f);
        }
    }

    private static float DrawLyricsMessage(ImDrawListPtr drawList, Rect area, FontAwesomeIcon icon, string text,
        float scale)
    {
        var width = area.Width - NowPlayingSide * 2f * scale;
        var iconTop = area.Min.Y + area.Height * 0.32f;
        AppSkin.Icon(drawList, new Vector2(area.Center.X, iconTop), IconGlyph.Of(icon), NowPlayingMuted,
            LyricsIconScale);
        var textTop = iconTop + Metrics.Space.Xl * scale;
        return Typography.DrawWrappedCentered(drawList, text, TextStyles.Headline, NowPlayingMuted,
            new Vector2(area.Center.X, textTop), width);
    }

    private static bool DrawRetryPill(ImDrawListPtr drawList, float centerX, float top, bool interactive,
        float scale)
    {
        var label = Loc.T(L.Common.Retry);
        var size = Typography.Measure(label, TextStyles.SubheadlineEmphasized);
        var height = RetryPillHeight * scale;
        var width = size.X + RetryPillPad * 2f * scale;
        var min = new Vector2(centerX - width * 0.5f, top);
        var max = min + new Vector2(width, height);
        var hovered = interactive && UiInteract.Hover(min, max);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(hovered ? NowPlayingRail : NowPlayingWash), height * 0.5f);
        Typography.Draw(drawList, (min + max) * 0.5f - size * 0.5f, label, NowPlayingInk,
            TextStyles.SubheadlineEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return interactive && UiInteract.Click(min, max, hovered);
    }

    private void DrawLyricLines(ImDrawListPtr drawList, Rect area, LrcDocument document, bool interactive,
        float scale, float delta)
    {
        var left = area.Min.X + NowPlayingSide * scale;
        var width = MathF.Max(1f, area.Width - NowPlayingSide * 2f * scale);
        EnsureLyricsLayout(document, width, scale);
        var synced = document.IsSynced;
        var position = (double)playback.Position;
        var active = synced ? document.LineAt(position + LyricsLeadSeconds) : -1;
        if (active != lyricsActiveLine)
        {
            lyricsPreviousLine = lyricsActiveLine;
            lyricsActiveLine = active;
            lyricsGlow.SnapTo(0f);
        }

        var glow = Math.Clamp(lyricsGlow.Step(1f, LyricsGlowSmoothTime, delta), 0f, 1f);
        var maximum = synced
            ? LyricsScroll.MaxOffset(area.Height, lyricsContentHeight)
            : lyricsContentHeight - area.Height;
        if (lyricsScroll.Update(area, lyricsContentHeight, maximum, interactive, false, delta))
        {
            lyricsPausedUntil = nowPlayingClock + LyricsScroll.FollowPauseSeconds;
            lyricsFollow.SnapTo(lyricsScroll.Offset);
        }
        else if (synced && active >= 0 && LyricsScroll.Following(nowPlayingClock, lyricsPausedUntil))
        {
            var target = LyricsScroll.Target(lyricLineTops[active], lyricLineHeights[active], area.Height,
                lyricsContentHeight);
            lyricsScroll.Sync(lyricsFollow.Step(target, LyricsFollowSmoothTime, delta));
        }
        else
        {
            lyricsFollow.SnapTo(lyricsScroll.Offset);
        }

        var offset = lyricsScroll.Offset;
        var edge = LyricsEdgeFade * scale;
        for (var lineIndex = 0; lineIndex < lyricLineTops.Length; lineIndex++)
        {
            var top = area.Min.Y + lyricLineTops[lineIndex] - offset;
            var height = lyricLineHeights[lineIndex];
            if (top > area.Max.Y || top + height < area.Min.Y)
            {
                continue;
            }

            var fade = Math.Clamp((top + height - area.Min.Y) / edge, 0f, 1f) *
                       Math.Clamp((area.Max.Y - top) / edge, 0f, 1f);
            var lineMin = new Vector2(left - Metrics.Space.Sm * scale, top - Metrics.Space.Xs * scale);
            var lineMax = new Vector2(left + width + Metrics.Space.Sm * scale, top + height + Metrics.Space.Xs * scale);
            var hovered = interactive && synced && UiInteract.Hover(lineMin, lineMax);
            if (hovered)
            {
                Squircle.Fill(drawList, lineMin, lineMax, QueueRowRounding * scale,
                    ImGui.GetColorU32(NowPlayingInk with { W = LyricsHoverAlpha * fade }));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            DrawLyricLine(drawList, document, lineIndex, left, top, synced, glow, fade, position);
            if (hovered && UiInteract.Click(lineMin, lineMax, hovered))
            {
                playback.Seek((float)document.StartOf(lineIndex));
                lyricsPausedUntil = 0f;
            }
        }
    }

    private void DrawLyricLine(ImDrawListPtr drawList, LrcDocument document, int lineIndex, float left, float top,
        bool synced, float glow, float fade, double position)
    {
        var segments = lyricSegments[lineIndex];
        var wordSegments = lyricWordSegment[lineIndex];
        var wordTimed = synced && lineIndex == lyricsActiveLine && wordSegments is not null;
        var alpha = LyricAlpha(lineIndex, synced, glow, wordTimed) * fade;
        var ink = NowPlayingInk with { W = alpha };
        for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            Typography.Draw(drawList, new Vector2(left, top + segmentIndex * lyricsLineHeight), segments[segmentIndex],
                ink, LyricStyle);
        }

        if (!wordTimed)
        {
            return;
        }

        var words = document.WordsOf(lineIndex);
        var lineEnd = lineIndex + 1 < document.Count
            ? document.StartOf(lineIndex + 1)
            : position + LyricsTrailingSeconds;
        var wordIndex = LyricsScroll.WordAt(words, position + LyricsLeadSeconds, lineEnd, out var fraction);
        if (wordIndex < 0)
        {
            return;
        }

        var currentSegment = wordSegments![wordIndex];
        var startX = lyricWordStartX[lineIndex]![wordIndex];
        var endX = lyricWordEndX[lineIndex]![wordIndex];
        var bright = NowPlayingInk with { W = fade };
        for (var segmentIndex = 0; segmentIndex <= currentSegment && segmentIndex < segments.Length; segmentIndex++)
        {
            var segmentTop = top + segmentIndex * lyricsLineHeight;
            var reach = segmentIndex < currentSegment ? float.MaxValue / 4f : startX + (endX - startX) * fraction;
            drawList.PushClipRect(new Vector2(left, segmentTop),
                new Vector2(left + reach, segmentTop + lyricsLineHeight), true);
            Typography.Draw(drawList, new Vector2(left, segmentTop), segments[segmentIndex], bright, LyricStyle);
            drawList.PopClipRect();
        }
    }

    private float LyricAlpha(int lineIndex, bool synced, float glow, bool wordTimed)
    {
        if (!synced)
        {
            return LyricsPlainAlpha;
        }

        var activeAlpha = wordTimed ? LyricsUnsungAlpha : 1f;
        if (lineIndex == lyricsActiveLine)
        {
            return LyricsUpcomingAlpha + (activeAlpha - LyricsUpcomingAlpha) * glow;
        }

        var resting = lyricsActiveLine >= 0 && lineIndex < lyricsActiveLine ? LyricsPastAlpha : LyricsUpcomingAlpha;
        if (lineIndex == lyricsPreviousLine)
        {
            return 1f + (resting - 1f) * glow;
        }

        return resting;
    }

    private void EnsureLyricsLayout(LrcDocument document, float width, float scale)
    {
        var lineHeight = Typography.LineHeight(LyricStyle);
        if (ReferenceEquals(document, lyricsDocument) && width == lyricsWidth && lineHeight == lyricsLineHeight)
        {
            return;
        }

        lyricsDocument = document;
        lyricsWidth = width;
        lyricsLineHeight = lineHeight;
        var count = document.Count;
        lyricLineTops = new float[count];
        lyricLineHeights = new float[count];
        lyricSegments = new string[count][];
        lyricWordSegment = new int[]?[count];
        lyricWordStartX = new float[]?[count];
        lyricWordEndX = new float[]?[count];
        var y = LyricsTopPadding * scale;
        for (var lineIndex = 0; lineIndex < count; lineIndex++)
        {
            var text = document.TextOf(lineIndex);
            var segments = text.Length == 0 ? [string.Empty] : Typography.WrapText(text, LyricStyle, width);
            lyricSegments[lineIndex] = segments;
            lyricLineTops[lineIndex] = y;
            lyricLineHeights[lineIndex] = segments.Length * lineHeight;
            y += lyricLineHeights[lineIndex] + LyricsLineGap * scale;
            if (document.IsSynced && document.HasWordTiming)
            {
                BuildWordGeometry(document, lineIndex, text, segments);
            }
        }

        lyricsContentHeight = y;
    }

    private void BuildWordGeometry(LrcDocument document, int lineIndex, string text, string[] segments)
    {
        var words = document.WordsOf(lineIndex);
        if (words.Length == 0 || segments.Length == 0)
        {
            return;
        }

        var segmentStarts = new int[segments.Length];
        var cursor = 0;
        for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            var found = segments[segmentIndex].Length == 0
                ? -1
                : text.IndexOf(segments[segmentIndex], cursor, StringComparison.Ordinal);
            segmentStarts[segmentIndex] = found < 0 ? cursor : found;
            cursor = segmentStarts[segmentIndex] + segments[segmentIndex].Length;
        }

        var wordSegment = new int[words.Length];
        var startX = new float[words.Length];
        var endX = new float[words.Length];
        for (var wordIndex = 0; wordIndex < words.Length; wordIndex++)
        {
            var word = words[wordIndex];
            var segmentIndex = 0;
            while (segmentIndex + 1 < segments.Length && segmentStarts[segmentIndex + 1] <= word.CharStart)
            {
                segmentIndex++;
            }

            var segment = segments[segmentIndex];
            var localStart = Math.Clamp(word.CharStart - segmentStarts[segmentIndex], 0, segment.Length);
            var localEnd = Math.Clamp(word.CharStart + word.CharLength - segmentStarts[segmentIndex], localStart,
                segment.Length);
            wordSegment[wordIndex] = segmentIndex;
            startX[wordIndex] = Typography.Measure(segment[..localStart], LyricStyle).X;
            endX[wordIndex] = Typography.Measure(segment[..localEnd], LyricStyle).X;
        }

        lyricWordSegment[lineIndex] = wordSegment;
        lyricWordStartX[lineIndex] = startX;
        lyricWordEndX[lineIndex] = endX;
    }
}
