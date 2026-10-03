using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Playback;
using Aetherphone.Core.SystemMedia;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Core.ControlCenter.Modules;

internal sealed partial class MediaModule : IControlModule
{
    private const string AppId = "music";
    private const float IdleArtAlpha = 0.55f;
    private const float DisabledTransportAlpha = 0.45f;
    private const float TransportSpread = 2.35f;
    private const float SideButtonFraction = 0.88f;
    private const float ArtRadiusFraction = 0.22f;
    private static readonly ControlSpan[] SpanOptions = { ControlSpan.Large, ControlSpan.Bar };
    private static readonly MarqueeId LargeTitle = new("media.large.", "title");
    private static readonly MarqueeId LargeSubtitle = new("media.large.", "subtitle");
    private static readonly MarqueeId BarTitle = new("media.bar.", "title");
    private static readonly MarqueeId BarSubtitle = new("media.bar.", "subtitle");
    private static readonly MarqueeId DetailTitle = new("media.detail.", "title");
    private static readonly MarqueeId DetailSubtitle = new("media.detail.", "subtitle");
    private static readonly string[] GridTransportIds = { "cc.media.previous", "cc.media.play", "cc.media.next" };

    private static readonly string[] DetailTransportIds =
        { "cc.media.detail.previous", "cc.media.detail.play", "cc.media.detail.next" };

    private readonly PlaybackHub playback;

    public MediaModule(PlaybackHub playback, PcMediaSource pcMedia)
    {
        this.playback = playback;
        this.pcMedia = pcMedia;
    }

    public string Id => "media";
    public string GalleryLabel => Loc.T(L.Apps.Music);
    public FontAwesomeIcon GalleryIcon => FontAwesomeIcon.Music;
    public IReadOnlyList<ControlSpan> Sizes => SpanOptions;
    public ControlSpan DefaultSpan => ControlSpan.Large;

    public void Draw(in ControlModuleContext context)
    {
        var drawList = context.DrawList;
        var rect = context.Rect;
        var scale = context.Scale;
        var opacity = context.Opacity;
        var theme = context.Theme;
        ControlTile.Surface(drawList, rect, theme, opacity);
        if (!playback.IsActive && DrawPcMedia(in context))
        {
            return;
        }

        var active = playback.IsActive;
        var title = active ? playback.Title : Loc.T(L.ControlCenter.NotPlaying);
        var subtitle = active ? playback.Subtitle : string.Empty;
        var interactive = context.Interactive && active;
        var padding = 14f * scale;
        if (context.Expanded)
        {
            DrawDetail(drawList, rect, theme, title, subtitle, active, interactive, opacity, scale, padding);
            return;
        }

        if (context.Span == ControlSpan.Large)
        {
            DrawLarge(drawList, rect, theme, title, subtitle, active, interactive, opacity, scale, padding);
            return;
        }

        DrawBar(drawList, rect, theme, title, subtitle, active, interactive, opacity, scale, padding);
    }

    private void DrawLarge(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, string title, string subtitle,
        bool active, bool interactive, float opacity, float scale, float padding)
    {
        var artSize = MathF.Min(rect.Height * 0.28f, 56f * scale);
        var artCenter = new Vector2(rect.Min.X + padding + artSize * 0.5f, rect.Min.Y + padding + artSize * 0.5f);
        DrawArt(drawList, artCenter, artSize, active, opacity);
        var textLeft = artCenter.X + artSize * 0.5f + Metrics.Space.Glass * scale;
        DrawTextBlock(drawList, LargeTitle, LargeSubtitle, textLeft, artCenter.Y, rect.Max.X - padding - textLeft,
            title, subtitle, opacity, scale);
        var buttonRadius = MathF.Min(rect.Width * 0.12f, 19f * scale);
        DrawTransport(drawList, GridTransportIds, rect.Center.X, rect.Max.Y - padding - buttonRadius, buttonRadius,
            buttonRadius * TransportSpread, theme, opacity, active, interactive);
    }

    private void DrawBar(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, string title, string subtitle,
        bool active, bool interactive, float opacity, float scale, float padding)
    {
        var artSize = MathF.Min(rect.Height - 2f * padding, 44f * scale);
        var artCenter = new Vector2(rect.Min.X + padding + artSize * 0.5f, rect.Center.Y);
        DrawArt(drawList, artCenter, artSize, active, opacity);
        var buttonRadius = 15f * scale;
        var spread = buttonRadius * TransportSpread;
        var transportCenterX = rect.Max.X - padding - buttonRadius - spread;
        var textLeft = artCenter.X + artSize * 0.5f + Metrics.Space.Md * scale;
        var textRight = transportCenterX - spread - buttonRadius - Metrics.Space.Glass * scale;
        DrawTextBlock(drawList, BarTitle, BarSubtitle, textLeft, rect.Center.Y, textRight - textLeft, title, subtitle,
            opacity, scale);
        DrawTransport(drawList, GridTransportIds, transportCenterX, rect.Center.Y, buttonRadius, spread, theme, opacity,
            active, interactive);
    }

    private void DrawDetail(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, string title, string subtitle,
        bool active, bool interactive, float opacity, float scale, float padding)
    {
        var artSize = MathF.Min(rect.Width * 0.42f, rect.Height * 0.38f);
        var artCenter = new Vector2(rect.Center.X, rect.Min.Y + padding + Metrics.Space.Sm * scale + artSize * 0.5f);
        DrawArt(drawList, artCenter, artSize, active, opacity);
        var textWidth = rect.Width - 2f * padding;
        var titleTop = artCenter.Y + artSize * 0.5f + padding;
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var titleLeft = rect.Center.X - textWidth * 0.5f;
        var titleHovering = UiInteract.Hover(new Vector2(titleLeft, titleTop),
            new Vector2(titleLeft + textWidth, titleTop + titleHeight));
        Marquee.DrawCentered(drawList, DetailTitle, title, rect.Center.X, titleTop, textWidth, TextStyles.Headline,
            ControlTile.Glyph(true, opacity), titleHovering);
        if (subtitle.Length > 0)
        {
            var subtitleTop = titleTop + titleHeight + Metrics.Space.Xxs * scale;
            var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
            var subtitleHovering = UiInteract.Hover(new Vector2(titleLeft, subtitleTop),
                new Vector2(titleLeft + textWidth, subtitleTop + subtitleHeight));
            Marquee.DrawCentered(drawList, DetailSubtitle, subtitle, rect.Center.X, subtitleTop, textWidth,
                TextStyles.Footnote, ControlTile.Glyph(false, opacity), subtitleHovering);
        }

        var buttonRadius = MathF.Min(rect.Width * 0.10f, 26f * scale);
        DrawTransport(drawList, DetailTransportIds, rect.Center.X,
            rect.Max.Y - padding - Metrics.Space.Xs * scale - buttonRadius, buttonRadius, buttonRadius * 2.5f, theme,
            opacity, active, interactive);
    }

    private void DrawArt(ImDrawListPtr drawList, Vector2 center, float size, bool active, float opacity)
    {
        var half = size * 0.5f;
        if (active && NowPlayingArt.TryDrawSquircle(drawList, center - new Vector2(half, half), size,
                size * ArtRadiusFraction, playback.ArtworkUrl, opacity))
        {
            return;
        }

        var surface = IconTile.Surface(AppAccents.For(AppId));
        IconTile.DrawApp(drawList, AppId, center, size, surface, (active ? 1f : IdleArtAlpha) * opacity);
    }

    private static void DrawTextBlock(ImDrawListPtr drawList, MarqueeId titleId, MarqueeId subtitleId, float left,
        float centerY, float width, string title, string subtitle, float opacity, float scale)
    {
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        if (subtitle.Length == 0)
        {
            DrawLine(drawList, titleId, title, left, centerY - titleHeight * 0.5f, width, TextStyles.Headline,
                ControlTile.Glyph(true, opacity));
            return;
        }

        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var gap = 2f * scale;
        var titleTop = centerY - (titleHeight + gap + subtitleHeight) * 0.5f;
        DrawLine(drawList, titleId, title, left, titleTop, width, TextStyles.Headline,
            ControlTile.Glyph(true, opacity));
        DrawLine(drawList, subtitleId, subtitle, left, titleTop + titleHeight + gap, width, TextStyles.Footnote,
            ControlTile.Glyph(false, opacity));
    }

    private static void DrawLine(ImDrawListPtr drawList, MarqueeId id, string text, float left, float top, float width,
        in TextStyle style, Vector4 color)
    {
        if (width <= 1f)
        {
            return;
        }

        var hovering = UiInteract.Hover(new Vector2(left, top),
            new Vector2(left + width, top + Typography.LineHeight(style)));
        Marquee.DrawLeft(drawList, id, text, left, top, width, style, color, hovering);
    }

    private void DrawTransport(ImDrawListPtr drawList, string[] ids, float centerX, float centerY, float radius,
        float spread, PhoneTheme theme, float opacity, bool active, bool interactive)
    {
        var hasQueue = active && playback.HasQueue;
        var sideOpacity = opacity * (hasQueue ? 1f : DisabledTransportAlpha);
        var sideRadius = radius * SideButtonFraction;
        if (ControlTile.Transport(drawList, ids[0], new Vector2(centerX - spread, centerY), sideRadius,
                TransportAction.Previous, theme, sideOpacity, interactive && hasQueue))
        {
            playback.Previous();
        }

        var centerAction = active && playback.IsPlaying ? TransportAction.Pause : TransportAction.Play;
        if (ControlTile.Transport(drawList, ids[1], new Vector2(centerX, centerY), radius, centerAction, theme,
                opacity * (active ? 1f : DisabledTransportAlpha), interactive))
        {
            playback.TogglePlayPause();
        }

        if (ControlTile.Transport(drawList, ids[2], new Vector2(centerX + spread, centerY), sideRadius,
                TransportAction.Next, theme, sideOpacity, interactive && hasQueue))
        {
            playback.Next();
        }
    }
}
