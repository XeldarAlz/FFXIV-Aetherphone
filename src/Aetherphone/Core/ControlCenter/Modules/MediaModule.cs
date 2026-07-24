using Aetherphone.Core.Localization;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Core.ControlCenter.Modules;

internal sealed class MediaModule : IControlModule
{
    private static readonly ControlSpan[] SpanOptions = { ControlSpan.Large, ControlSpan.Bar };

    private readonly PlaybackHub playback;

    public MediaModule(PlaybackHub playback)
    {
        this.playback = playback;
    }

    public string Id => "media";
    public string GalleryLabel => Loc.T(L.Apps.Music);
    public FontAwesomeIcon GalleryIcon => FontAwesomeIcon.Music;
    public IReadOnlyList<ControlSpan> Sizes => SpanOptions;
    public ControlSpan DefaultSpan => ControlSpan.Large;

    public void Draw(in ControlModuleContext context)
    {
        var dl = context.DrawList;
        var rect = context.Rect;
        var scale = context.Scale;
        var opacity = context.Opacity;
        var theme = context.Theme;
        var radius = 20f * scale;
        Squircle.Fill(dl, rect.Min, rect.Max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f * opacity)));
        Material.EdgeSquircle(dl, rect.Min, rect.Max, radius, scale, opacity);

        var pad = 14f * scale;
        var active = playback.IsActive;
        var accent = theme.Accent;
        var ink = theme.TextStrong;
        var hasQueue = playback.HasQueue;
        var interactive = context.Interactive && active;
        var title = active ? playback.Title : Loc.T(L.ControlCenter.NotPlaying);
        var subtitle = active ? playback.Subtitle : string.Empty;
        if (context.Span == ControlSpan.Large)
        {
            DrawLarge(dl, rect, theme, title, subtitle, active, hasQueue, interactive, opacity, scale, pad);
            return;
        }

        var artSize = MathF.Min(rect.Height - 2f * pad, 44f * scale);
        var artRect = new Rect(new Vector2(rect.Min.X + pad, rect.Center.Y - artSize * 0.5f),
            new Vector2(rect.Min.X + pad + artSize, rect.Center.Y + artSize * 0.5f));
        DrawArt(dl, artRect, theme, active, opacity);
        var band = hasQueue ? 100f * scale : 44f * scale;
        var textLeft = artRect.Max.X + 12f * scale;
        DrawText(dl, textLeft, rect.Center.Y, rect.Max.X - band - textLeft, title, subtitle, theme, opacity, scale);
        DrawTransport(dl, rect.Max.X - band * 0.5f, rect.Center.Y, 34f * scale, accent, ink, opacity, active, hasQueue,
            interactive, scale);
    }

    private void DrawLarge(ImDrawListPtr dl, Rect rect, PhoneTheme theme, string title, string subtitle, bool active,
        bool hasQueue, bool interactive, float opacity, float scale, float pad)
    {
        var artSize = MathF.Min(rect.Height * 0.30f, 46f * scale);
        var artTop = rect.Min.Y + pad + 4f * scale;
        var artRect = new Rect(new Vector2(rect.Center.X - artSize * 0.5f, artTop),
            new Vector2(rect.Center.X + artSize * 0.5f, artTop + artSize));
        DrawArt(dl, artRect, theme, active, opacity);
        var textWidth = rect.Width - 2f * pad;
        var contentBottom = active ? rect.Max.Y - pad - 32f * scale - 6f * scale : rect.Max.Y - pad;
        var titleCenterY = MathF.Min(artRect.Max.Y + 16f * scale, contentBottom);
        var titleSize = Typography.Measure(title, TextStyles.Headline);
        var titleTopY = titleCenterY - titleSize.Y * 0.5f;
        var titleHovering = ImGui.IsMouseHoveringRect(new Vector2(rect.Center.X - textWidth * 0.5f, titleTopY),
            new Vector2(rect.Center.X + textWidth * 0.5f, titleTopY + titleSize.Y));
        Marquee.DrawCentered(dl, "media.large.title", title, rect.Center.X, titleTopY, textWidth, TextStyles.Headline,
            Palette.WithAlpha(theme.TextStrong, opacity), titleHovering);
        if (subtitle.Length > 0 && titleCenterY + 18f * scale <= contentBottom)
        {
            Typography.DrawCentered(dl, new Vector2(rect.Center.X, titleCenterY + 18f * scale),
                Typography.FitText(subtitle, textWidth, TextStyles.Footnote),
                Palette.WithAlpha(theme.TextMuted, opacity), TextStyles.Footnote);
        }

        if (active)
        {
            DrawTransport(dl, rect.Center.X, rect.Max.Y - pad - 16f * scale, 28f * scale, theme.Accent,
                theme.TextStrong, opacity, true, hasQueue, interactive, scale);
        }
    }

    private static void DrawArt(ImDrawListPtr dl, Rect artRect, PhoneTheme theme, bool active, float opacity)
    {
        Squircle.Fill(dl, artRect.Min, artRect.Max, artRect.Width * 0.28f,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, (active ? 0.9f : 0.35f) * opacity)));
        ProgressRing.CenterIcon(dl, artRect.Center, FontAwesomeIcon.Music, new Vector4(1f, 1f, 1f, opacity),
            artRect.Width * 0.42f);
    }

    private static void DrawText(ImDrawListPtr dl, float left, float centerY, float width, string title,
        string subtitle, PhoneTheme theme, float opacity, float scale)
    {
        if (subtitle.Length > 0)
        {
            var titleY = centerY - 19f * scale;
            var titleSize = Typography.Measure(title, TextStyles.Headline);
            var titleHovering = ImGui.IsMouseHoveringRect(new Vector2(left, titleY),
                new Vector2(left + width, titleY + titleSize.Y));
            Marquee.DrawLeft(dl, "media.bar.title", title, left, titleY, width, TextStyles.Headline,
                Palette.WithAlpha(theme.TextStrong, opacity), titleHovering);
            var subtitleY = centerY + 3f * scale;
            var subtitleSize = Typography.Measure(subtitle, TextStyles.Footnote);
            var subtitleHovering = ImGui.IsMouseHoveringRect(new Vector2(left, subtitleY),
                new Vector2(left + width, subtitleY + subtitleSize.Y));
            Marquee.DrawLeft(dl, "media.bar.subtitle", subtitle, left, subtitleY, width, TextStyles.Footnote,
                Palette.WithAlpha(theme.TextMuted, opacity), subtitleHovering);
            return;
        }

        var soloY = centerY - 10f * scale;
        var soloSize = Typography.Measure(title, TextStyles.Headline);
        var soloHovering = ImGui.IsMouseHoveringRect(new Vector2(left, soloY),
            new Vector2(left + width, soloY + soloSize.Y));
        Marquee.DrawLeft(dl, "media.bar.title", title, left, soloY, width, TextStyles.Headline,
            Palette.WithAlpha(theme.TextStrong, opacity), soloHovering);
    }

    private void DrawTransport(ImDrawListPtr dl, float centerX, float centerY, float spread, Vector4 accent,
        Vector4 ink, float opacity, bool active, bool hasQueue, bool interactive, float scale)
    {
        if (hasQueue && TransportButton.Draw(new Vector2(centerX - spread, centerY), 15f * scale,
                TransportAction.Previous, accent, ink, opacity, interactive, dl))
        {
            playback.Previous();
        }

        if (TransportButton.Draw(new Vector2(centerX, centerY), 16f * scale, TransportAction.Stop, accent, ink,
                opacity * (active ? 1f : 0.4f), interactive, dl) && active)
        {
            playback.Stop();
        }

        if (hasQueue && TransportButton.Draw(new Vector2(centerX + spread, centerY), 15f * scale, TransportAction.Next,
                accent, ink, opacity, interactive, dl))
        {
            playback.Next();
        }
    }
}
