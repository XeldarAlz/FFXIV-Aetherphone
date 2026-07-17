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
        var titleCenterY = artRect.Max.Y + 16f * scale;
        Typography.DrawCentered(dl, new Vector2(rect.Center.X, titleCenterY),
            Typography.FitText(title, textWidth, TextStyles.Headline), Palette.WithAlpha(theme.TextStrong, opacity),
            TextStyles.Headline);
        if (subtitle.Length > 0)
        {
            Typography.DrawCentered(dl, new Vector2(rect.Center.X, titleCenterY + 18f * scale),
                Typography.FitText(subtitle, textWidth, TextStyles.Footnote),
                Palette.WithAlpha(theme.TextMuted, opacity), TextStyles.Footnote);
        }

        DrawTransport(dl, rect.Center.X, rect.Max.Y - pad - 16f * scale, 40f * scale, theme.Accent, theme.TextStrong,
            opacity, active, hasQueue, interactive, scale);
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
        var fittedTitle = Typography.FitText(title, width, TextStyles.Headline);
        if (subtitle.Length > 0)
        {
            var fittedSubtitle = Typography.FitText(subtitle, width, TextStyles.Footnote);
            Typography.Draw(dl, new Vector2(left, centerY - 19f * scale), fittedTitle,
                Palette.WithAlpha(theme.TextStrong, opacity), TextStyles.Headline);
            Typography.Draw(dl, new Vector2(left, centerY + 3f * scale), fittedSubtitle,
                Palette.WithAlpha(theme.TextMuted, opacity), TextStyles.Footnote);
            return;
        }

        Typography.Draw(dl, new Vector2(left, centerY - 10f * scale), fittedTitle,
            Palette.WithAlpha(theme.TextStrong, opacity), TextStyles.Headline);
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
