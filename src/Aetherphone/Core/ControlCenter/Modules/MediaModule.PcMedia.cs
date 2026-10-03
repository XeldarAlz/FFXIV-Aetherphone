using Aetherphone.Core.Apps;
using Aetherphone.Core.SystemMedia;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.ControlCenter.Modules;

internal sealed partial class MediaModule
{
    private const float PcArtRadiusFraction = 0.2f;
    private static readonly MarqueeId PcLargeTitle = new("media.pc.large.", "title");
    private static readonly MarqueeId PcLargeSubtitle = new("media.pc.large.", "subtitle");
    private static readonly MarqueeId PcBarTitle = new("media.pc.bar.", "title");
    private static readonly MarqueeId PcBarSubtitle = new("media.pc.bar.", "subtitle");
    private static readonly MarqueeId PcDetailTitle = new("media.pc.detail.", "title");
    private static readonly MarqueeId PcDetailSubtitle = new("media.pc.detail.", "subtitle");
    private static readonly MarqueeId PcDetailSource = new("media.pc.detail.", "source");
    private static readonly string[] PcGridTransportIds = { "cc.pc.previous", "cc.pc.play", "cc.pc.next" };

    private static readonly string[] PcDetailTransportIds =
        { "cc.pc.detail.previous", "cc.pc.detail.play", "cc.pc.detail.next" };

    private readonly PcMediaSource pcMedia;

    private bool DrawPcMedia(in ControlModuleContext context)
    {
        ref readonly var snapshot = ref pcMedia.Current;
        if (!PcMediaSource.IsLive(snapshot))
        {
            return false;
        }

        var drawList = context.DrawList;
        var rect = context.Rect;
        var scale = context.Scale;
        var opacity = context.Opacity;
        var padding = 14f * scale;
        var title = PcMediaView.Title(snapshot);
        var subtitle = PcMediaView.Subtitle(snapshot);
        if (context.Expanded)
        {
            DrawPcDetail(drawList, rect, context.Theme, snapshot, title, subtitle, context.Interactive, opacity,
                scale, padding);
            return true;
        }

        if (context.Span == ControlSpan.Large)
        {
            var artSize = MathF.Min(rect.Height * 0.28f, 56f * scale);
            var artMin = new Vector2(rect.Min.X + padding, rect.Min.Y + padding);
            DrawPcArt(drawList, artMin, artSize, snapshot, opacity);
            var textLeft = artMin.X + artSize + Metrics.Space.Glass * scale;
            DrawTextBlock(drawList, PcLargeTitle, PcLargeSubtitle, textLeft, artMin.Y + artSize * 0.5f,
                rect.Max.X - padding - textLeft, title, subtitle, opacity, scale);
            var buttonRadius = MathF.Min(rect.Width * 0.12f, 19f * scale);
            DrawPcTransport(drawList, PcGridTransportIds, rect.Center.X, rect.Max.Y - padding - buttonRadius,
                buttonRadius, buttonRadius * TransportSpread, context.Theme, snapshot, opacity, context.Interactive);
            return true;
        }

        var barArt = MathF.Min(rect.Height - 2f * padding, 44f * scale);
        var barArtMin = new Vector2(rect.Min.X + padding, rect.Center.Y - barArt * 0.5f);
        DrawPcArt(drawList, barArtMin, barArt, snapshot, opacity);
        var radius = 15f * scale;
        var spread = radius * TransportSpread;
        var transportCenterX = rect.Max.X - padding - radius - spread;
        var barTextLeft = barArtMin.X + barArt + Metrics.Space.Md * scale;
        var textRight = transportCenterX - spread - radius - Metrics.Space.Glass * scale;
        DrawTextBlock(drawList, PcBarTitle, PcBarSubtitle, barTextLeft, rect.Center.Y, textRight - barTextLeft, title,
            subtitle, opacity, scale);
        DrawPcTransport(drawList, PcGridTransportIds, transportCenterX, rect.Center.Y, radius, spread, context.Theme,
            snapshot, opacity, context.Interactive);
        return true;
    }

    private void DrawPcDetail(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, in MediaSessionSnapshot snapshot,
        string title, string subtitle, bool interactive, float opacity, float scale, float padding)
    {
        var artSize = MathF.Min(rect.Width * 0.42f, rect.Height * 0.38f);
        var artMin = new Vector2(rect.Center.X - artSize * 0.5f, rect.Min.Y + padding + Metrics.Space.Sm * scale);
        DrawPcArt(drawList, artMin, artSize, snapshot, opacity);
        var textWidth = rect.Width - 2f * padding;
        var titleLeft = rect.Center.X - textWidth * 0.5f;
        var top = artMin.Y + artSize + padding;
        top = DrawPcCenteredLine(drawList, PcDetailTitle, title, rect.Center.X, titleLeft, top, textWidth,
            TextStyles.Headline, ControlTile.Glyph(true, opacity));
        top = DrawPcCenteredLine(drawList, PcDetailSubtitle, subtitle, rect.Center.X, titleLeft,
            top + Metrics.Space.Xxs * scale, textWidth, TextStyles.Footnote, ControlTile.Glyph(false, opacity));
        DrawPcCenteredLine(drawList, PcDetailSource, PcMediaView.Source(snapshot), rect.Center.X, titleLeft,
            top + Metrics.Space.Xxs * scale, textWidth, TextStyles.Footnote, ControlTile.Glyph(false, opacity));
        var buttonRadius = MathF.Min(rect.Width * 0.10f, 26f * scale);
        DrawPcTransport(drawList, PcDetailTransportIds, rect.Center.X,
            rect.Max.Y - padding - Metrics.Space.Xs * scale - buttonRadius, buttonRadius, buttonRadius * 2.5f, theme,
            snapshot, opacity, interactive);
    }

    private static float DrawPcCenteredLine(ImDrawListPtr drawList, MarqueeId id, string text, float centerX,
        float left, float top, float width, in TextStyle style, Vector4 color)
    {
        var height = Typography.LineHeight(style);
        var hovering = UiInteract.Hover(new Vector2(left, top), new Vector2(left + width, top + height));
        Marquee.DrawCentered(drawList, id, text, centerX, top, width, style, color, hovering);
        return top + height;
    }

    private void DrawPcArt(ImDrawListPtr drawList, Vector2 min, float side, in MediaSessionSnapshot snapshot,
        float opacity)
    {
        PcMediaView.DrawArt(drawList, min, side, side * PcArtRadiusFraction, pcMedia.Artwork(snapshot, side),
            snapshot, AppAccents.For(AppId), opacity);
    }

    private void DrawPcTransport(ImDrawListPtr drawList, string[] ids, float centerX, float centerY, float radius,
        float spread, PhoneTheme theme, in MediaSessionSnapshot snapshot, float opacity, bool interactive)
    {
        var sideRadius = radius * SideButtonFraction;
        var canPrevious = snapshot.CanPrevious;
        if (ControlTile.Transport(drawList, ids[0], new Vector2(centerX - spread, centerY), sideRadius,
                TransportAction.Previous, theme, opacity * (canPrevious ? 1f : DisabledTransportAlpha),
                interactive && canPrevious))
        {
            pcMedia.Previous();
        }

        var canToggle = snapshot.CanPlayPause;
        if (ControlTile.Transport(drawList, ids[1], new Vector2(centerX, centerY), radius,
                snapshot.IsPlaying ? TransportAction.Pause : TransportAction.Play, theme,
                opacity * (canToggle ? 1f : DisabledTransportAlpha), interactive && canToggle))
        {
            pcMedia.TogglePlayPause(snapshot);
        }

        var canNext = snapshot.CanNext;
        if (ControlTile.Transport(drawList, ids[2], new Vector2(centerX + spread, centerY), sideRadius,
                TransportAction.Next, theme, opacity * (canNext ? 1f : DisabledTransportAlpha),
                interactive && canNext))
        {
            pcMedia.Next();
        }
    }
}
