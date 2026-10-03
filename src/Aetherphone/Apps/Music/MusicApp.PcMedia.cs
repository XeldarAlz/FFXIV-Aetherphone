using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.SystemMedia;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float PcCardPadding = 14f;
    private const float PcCardRadius = 22f;
    private const float PcCardArt = 64f;
    private const float PcCaptionGap = 10f;
    private const float PcBodyGap = 12f;
    private const float PcTextGap = 12f;
    private const float PcLineGap = 2f;
    private const float PcProgressThickness = 4f;
    private const float PcTransportRadius = 18f;
    private const float PcPlayRadius = 22f;
    private const float PcTransportStride = 60f;
    private const float PcSheetTransportStride = 76f;
    private const float PcSheetPlayRadius = 30f;
    private const float PcSheetTransportRadius = 24f;
    private const float PcSheetArtFraction = 0.4f;
    private const float PcScrubThickness = 4f;
    private const float PcPlayFillAlpha = 0.10f;
    private const float PcRailAlpha = 0.22f;
    private const float PcDisabledAlpha = 0.35f;
    private const float PcPressDim = 0.06f;
    private const string RemainingSign = "-";
    private static readonly MarqueeId PcCardTitle = new("music.pc.card.", "title");
    private static readonly MarqueeId PcCardArtist = new("music.pc.card.", "artist");
    private static readonly MarqueeId PcSheetTitle = new("music.pc.sheet.", "title");
    private static readonly MarqueeId PcSheetArtist = new("music.pc.sheet.", "artist");

    private readonly PcMediaSource pcMedia;
    private readonly Sheet pcMediaSheet = new();
    private MediaSessionSnapshot pcSheetSnapshot = MediaSessionSnapshot.Empty;
    private bool pcScrubbing;
    private float pcScrubValue;

    private bool PcMediaCapturesPointer => pcMediaSheet.CapturesPointer;

    private void DrawPcMediaCard(float scale)
    {
        if (!configuration.ShowWindowsMedia)
        {
            return;
        }

        ref readonly var snapshot = ref pcMedia.Current;
        if (!snapshot.HasSession)
        {
            return;
        }

        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var padding = PcCardPadding * scale;
        var captionHeight = Typography.LineHeight(TextStyles.Footnote);
        var artSide = PcCardArt * scale;
        var hasProgress = snapshot.Duration > TimeSpan.Zero;
        var progressBlock = hasProgress ? PcProgressThickness * scale + PcBodyGap * scale : 0f;
        var playRadius = PcPlayRadius * scale;
        var height = padding + captionHeight + PcCaptionGap * scale + artSide + PcBodyGap * scale + progressBlock +
                     playRadius * 2f + padding * 0.5f;
        var inset = MusicUi.Inset * scale;
        var min = new Vector2(origin.X + inset, origin.Y);
        var max = new Vector2(origin.X + width - inset, origin.Y + height);
        if (ImGui.IsRectVisible(min, max))
        {
            DrawPcCardBody(ImGui.GetWindowDrawList(), snapshot, min, max, scale, captionHeight, artSide, hasProgress,
                playRadius);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + MusicUi.SectionGap * scale));
    }

    private void DrawPcCardBody(ImDrawListPtr drawList, in MediaSessionSnapshot snapshot, Vector2 min, Vector2 max,
        float scale, float captionHeight, float artSide, bool hasProgress, float playRadius)
    {
        var padding = PcCardPadding * scale;
        var radius = PcCardRadius * scale;
        Material.ThemedGlass(drawList, min, max, radius, scale, ui.BackdropColor);
        var left = min.X + padding;
        var right = max.X - padding;
        var top = min.Y + padding;
        DrawPcSource(drawList, snapshot, left, top, right - left, captionHeight, ui.MutedInk, 1f);
        var artMin = new Vector2(left, top + captionHeight + PcCaptionGap * scale);
        PcMediaView.DrawArt(drawList, artMin, artSide, artSide * ArtworkTile.TileRadiusFraction,
            pcMedia.Artwork(snapshot, artSide), snapshot, ui.Accent, 1f);
        var textLeft = artMin.X + artSide + PcTextGap * scale;
        DrawPcLines(drawList, snapshot, PcCardTitle, PcCardArtist, textLeft, artMin.Y + artSide * 0.5f,
            right - textLeft, TextStyles.Headline, TextStyles.Subheadline, ui.TitleInk, ui.MutedInk, scale);
        var cursorY = artMin.Y + artSide + PcBodyGap * scale;
        if (hasProgress)
        {
            var thickness = PcProgressThickness * scale;
            DrawPcRail(drawList, new Rect(new Vector2(left, cursorY), new Vector2(right, cursorY + thickness)),
                PcMediaView.Progress(snapshot), ui.TitleInk, 1f);
            cursorY += thickness + PcBodyGap * scale;
        }

        var transportCenter = new Vector2((min.X + max.X) * 0.5f, cursorY + playRadius);
        var overTransport = DrawPcTransport(drawList, snapshot, transportCenter, PcTransportStride * scale,
            PcTransportRadius * scale, playRadius, ui.TitleInk, 1f, true);
        var hovered = !overTransport && UiInteract.Hover(min, max);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, PcPressDim)));
        }

        if (UiInteract.Click(min, max, hovered))
        {
            pcScrubbing = false;
            pcSheetSnapshot = snapshot;
            pcMediaSheet.Open();
        }
    }

    private static void DrawPcSource(ImDrawListPtr drawList, in MediaSessionSnapshot snapshot, float left, float top,
        float width, float lineHeight, Vector4 ink, float alpha)
    {
        var color = Palette.WithAlpha(ink, alpha);
        var glyphCenter = new Vector2(left + lineHeight * 0.5f, top + lineHeight * 0.5f);
        ProgressRing.CenterIcon(drawList, glyphCenter, PcMediaGlyph.For(snapshot.AppName), color, lineHeight * 0.8f);
        var textLeft = left + lineHeight + Metrics.Space.Xs * UiScale.Current;
        var text = Typography.FitText(PcMediaView.Source(snapshot), MathF.Max(1f, left + width - textLeft),
            TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, top), text, color, TextStyles.Footnote);
    }

    private static void DrawPcLines(ImDrawListPtr drawList, in MediaSessionSnapshot snapshot, MarqueeId titleId,
        MarqueeId artistId, float left, float centerY, float width, in TextStyle titleStyle, in TextStyle artistStyle,
        Vector4 titleInk, Vector4 artistInk, float scale)
    {
        var titleHeight = Typography.LineHeight(titleStyle);
        var artistHeight = Typography.LineHeight(artistStyle);
        var gap = PcLineGap * scale;
        var top = centerY - (titleHeight + gap + artistHeight) * 0.5f;
        var textWidth = MathF.Max(1f, width);
        Marquee.DrawLeftAuto(drawList, titleId, PcMediaView.Title(snapshot), left, top, textWidth, titleStyle,
            titleInk);
        Marquee.DrawLeftAuto(drawList, artistId, PcMediaView.Subtitle(snapshot), left, top + titleHeight + gap,
            textWidth, artistStyle, artistInk);
    }

    private static void DrawPcRail(ImDrawListPtr drawList, Rect track, float fraction, Vector4 ink, float alpha)
    {
        var rounding = track.Height * 0.5f;
        drawList.AddRectFilled(track.Min, track.Max, ImGui.GetColorU32(Palette.WithAlpha(ink, PcRailAlpha * alpha)),
            rounding);
        if (fraction <= 0f)
        {
            return;
        }

        var fillMax = new Vector2(track.Min.X + track.Width * Math.Clamp(fraction, 0f, 1f), track.Max.Y);
        drawList.AddRectFilled(track.Min, fillMax, ImGui.GetColorU32(Palette.WithAlpha(ink, alpha)), rounding);
    }

    private bool DrawPcTransport(ImDrawListPtr drawList, in MediaSessionSnapshot snapshot, Vector2 center,
        float stride, float sideRadius, float playRadius, Vector4 ink, float alpha, bool interactive)
    {
        var previousCenter = center - new Vector2(stride, 0f);
        var nextCenter = center + new Vector2(stride, 0f);
        var reach = new Vector2(stride + sideRadius, playRadius);
        var overTransport = UiInteract.Hover(center - reach, center + reach);
        var canPrevious = snapshot.CanPrevious;
        if (TransportButton.Draw(previousCenter, sideRadius, TransportAction.Previous, ui.Accent, ink,
                canPrevious ? alpha : alpha * PcDisabledAlpha, interactive && canPrevious, drawList))
        {
            pcMedia.Previous();
        }

        drawList.AddCircleFilled(center, playRadius,
            ImGui.GetColorU32(Palette.WithAlpha(ink, PcPlayFillAlpha * alpha)), 40);
        var canToggle = snapshot.CanPlayPause;
        if (TransportButton.Draw(center, playRadius, snapshot.IsPlaying ? TransportAction.Pause : TransportAction.Play,
                ui.Accent, ink, canToggle ? alpha : alpha * PcDisabledAlpha, interactive && canToggle, drawList))
        {
            pcMedia.TogglePlayPause(snapshot);
        }

        var canNext = snapshot.CanNext;
        if (TransportButton.Draw(nextCenter, sideRadius, TransportAction.Next, ui.Accent, ink,
                canNext ? alpha : alpha * PcDisabledAlpha, interactive && canNext, drawList))
        {
            pcMedia.Next();
        }

        return overTransport;
    }

    private void DrawPcMediaSheet(Rect screen, float scale)
    {
        if (!pcMediaSheet.CapturesPointer)
        {
            return;
        }

        ref readonly var live = ref pcMedia.Current;
        if (live.HasSession)
        {
            pcSheetSnapshot = live;
        }
        else if (pcMediaSheet.IsOpen)
        {
            pcMediaSheet.Close();
        }

        var inset = Metrics.Space.Xl * scale;
        var artSide = MathF.Min(screen.Width - inset * 2f, screen.Height * PcSheetArtFraction);
        var sheetHeight = SheetMetrics.GrabberZone * scale + PcSheetContentHeight(artSide, inset, scale);
        var detent = MathF.Min(sheetHeight, screen.Height * SheetMetrics.LargeFraction);
        using var layer = ScreenLayer.Begin("music.pcMedia", screen, false);
        var frame = pcMediaSheet.Begin(ImGui.GetWindowDrawList(), screen, theme, SheetDetents.Fitted(detent),
            SheetMetrics.AppVeil);
        if (!frame.Visible)
        {
            return;
        }

        DrawPcSheetContent(in frame, pcSheetSnapshot, artSide, inset, scale);
        pcMediaSheet.End(in frame);
    }

    private static float PcSheetContentHeight(float artSide, float inset, float scale)
    {
        return inset * 0.5f + Typography.LineHeight(TextStyles.Footnote) + inset * 0.75f + artSide + inset +
               Typography.LineHeight(TextStyles.Title3) + PcLineGap * scale + Typography.LineHeight(TextStyles.Body) +
               inset + PcScrubThickness * scale + Metrics.Space.Sm * scale +
               Typography.LineHeight(TextStyles.Footnote) + inset + PcSheetPlayRadius * scale * 2f + inset;
    }

    private void DrawPcSheetContent(in SheetFrame frame, in MediaSessionSnapshot snapshot, float artSide, float inset,
        float scale)
    {
        var drawList = frame.DrawList;
        var content = frame.Content;
        var ink = frame.Ink;
        var muted = Palette.WithAlpha(ink, 0.6f);
        var left = content.Min.X + inset;
        var width = content.Width - inset * 2f;
        var top = content.Min.Y + inset * 0.5f;
        var captionHeight = Typography.LineHeight(TextStyles.Footnote);
        DrawPcSource(drawList, snapshot, left, top, width, captionHeight, ink, 0.6f);
        top += captionHeight + inset * 0.75f;
        var artMin = new Vector2(content.Center.X - artSide * 0.5f, top);
        PcMediaView.DrawArt(drawList, artMin, artSide, artSide * ArtworkTile.HeroRadiusFraction,
            pcMedia.Artwork(snapshot, artSide), snapshot, ui.Accent, 1f);
        top += artSide + inset;
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        Marquee.DrawLeftAuto(drawList, PcSheetTitle, PcMediaView.Title(snapshot), left, top, width, TextStyles.Title3,
            ink);
        top += titleHeight + PcLineGap * scale;
        Marquee.DrawLeftAuto(drawList, PcSheetArtist, PcMediaView.Subtitle(snapshot), left, top, width,
            TextStyles.Body, muted);
        top += Typography.LineHeight(TextStyles.Body) + inset;
        var thickness = PcScrubThickness * scale;
        var track = new Rect(new Vector2(left, top), new Vector2(left + width, top + thickness));
        DrawPcScrubber(drawList, snapshot, track, ink, muted, frame.Interactive, frame.Opacity);
        top += thickness + Metrics.Space.Sm * scale + captionHeight + inset;
        var playRadius = PcSheetPlayRadius * scale;
        DrawPcTransport(drawList, snapshot, new Vector2(content.Center.X, top + playRadius),
            PcSheetTransportStride * scale, PcSheetTransportRadius * scale, playRadius, ink, 1f, frame.Interactive);
    }

    private void DrawPcScrubber(ImDrawListPtr drawList, in MediaSessionSnapshot snapshot, Rect track, Vector4 ink,
        Vector4 muted, bool interactive, float alpha)
    {
        var labelTop = track.Max.Y + Metrics.Space.Sm * UiScale.Current;
        if (snapshot.Duration <= TimeSpan.Zero)
        {
            DrawPcRail(drawList, track, 0f, ink, alpha);
            return;
        }

        var durationSeconds = snapshot.Duration.TotalSeconds;
        var fraction = pcScrubbing ? pcScrubValue : PcMediaView.Progress(snapshot);
        if (!snapshot.CanSeek || !interactive)
        {
            pcScrubbing = false;
            DrawPcRail(drawList, track, fraction, ink, alpha);
            DrawPcTimes(drawList, track, labelTop, fraction, durationSeconds, muted);
            if (!snapshot.CanSeek)
            {
                var hint = Typography.FitText(Loc.T(L.Music.PcMedia.SeekUnavailable), track.Width * 0.5f,
                    TextStyles.Footnote);
                var hintWidth = Typography.Measure(hint, TextStyles.Footnote).X;
                Typography.Draw(drawList, new Vector2(track.Center.X - hintWidth * 0.5f, labelTop), hint, muted,
                    TextStyles.Footnote);
            }

            return;
        }

        var updated = Scrubber.Draw(track, fraction, ink, Palette.WithAlpha(ink, PcRailAlpha), alpha);
        if ((pcScrubbing || Scrubber.IsHovered(track)) && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            pcScrubbing = true;
            pcScrubValue = updated;
            pcMediaSheet.ReleasePress();
        }
        else if (pcScrubbing)
        {
            pcScrubbing = false;
            pcMedia.Seek(TimeSpan.FromSeconds(pcScrubValue * durationSeconds));
        }

        DrawPcTimes(drawList, track, labelTop, pcScrubbing ? pcScrubValue : fraction, durationSeconds, muted);
    }

    private static void DrawPcTimes(ImDrawListPtr drawList, Rect track, float top, float fraction,
        double durationSeconds, Vector4 color)
    {
        var elapsedSeconds = (int)(fraction * durationSeconds);
        var remainingSeconds = Math.Max(0, (int)Math.Ceiling(durationSeconds) - elapsedSeconds);
        Typography.Draw(drawList, new Vector2(track.Min.X, top), TimeText.Duration(elapsedSeconds), color,
            TextStyles.Footnote);
        var remaining = TimeText.Duration(remainingSeconds);
        var remainingWidth = Typography.Measure(remaining, TextStyles.Footnote).X;
        var signWidth = Typography.Measure(RemainingSign, TextStyles.Footnote).X;
        var remainingLeft = track.Max.X - remainingWidth;
        Typography.Draw(drawList, new Vector2(remainingLeft - signWidth, top), RemainingSign, color,
            TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(remainingLeft, top), remaining, color, TextStyles.Footnote);
    }
}
