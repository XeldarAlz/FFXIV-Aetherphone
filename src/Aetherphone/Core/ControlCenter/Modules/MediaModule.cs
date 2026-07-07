using System.Numerics;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Core.ControlCenter.Modules;

internal sealed class MediaModule : IControlModule
{
    private static readonly ControlSpan[] SpanOptions = { ControlSpan.Wide, ControlSpan.Large };

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
        var pad = 16f * scale;
        var active = playback.IsActive;
        var artSize = MathF.Min(rect.Height - 2f * pad, 46f * scale);
        var artRect = new Rect(new Vector2(rect.Min.X + pad, rect.Min.Y + pad),
            new Vector2(rect.Min.X + pad + artSize, rect.Min.Y + pad + artSize));
        Squircle.Fill(dl, artRect.Min, artRect.Max, artSize * 0.28f,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, (active ? 0.9f : 0.35f) * opacity)));
        ProgressRing.CenterIcon(dl, artRect.Center, FontAwesomeIcon.Music, new Vector4(1f, 1f, 1f, opacity),
            artSize * 0.42f);
        var textLeft = artRect.Max.X + 12f * scale;
        var title = active ? Truncate(playback.Title, context.Span == ControlSpan.Large ? 24 : 18)
            : Loc.T(L.ControlCenter.NotPlaying);
        var subtitle = active ? Truncate(playback.Subtitle, context.Span == ControlSpan.Large ? 26 : 20) : string.Empty;
        Typography.Draw(dl, new Vector2(textLeft, artRect.Min.Y + 1f * scale), title,
            Palette.WithAlpha(theme.TextStrong, opacity), 0.9f, FontWeight.SemiBold);
        if (subtitle.Length > 0)
        {
            Typography.Draw(dl, new Vector2(textLeft, artRect.Min.Y + 22f * scale), subtitle,
                Palette.WithAlpha(theme.TextMuted, opacity), 0.76f);
        }

        var controlY = rect.Max.Y - 24f * scale;
        var accent = theme.Accent;
        var ink = theme.TextStrong;
        var interactive = context.Interactive && active;
        if (playback.HasQueue && TransportButton.Draw(new Vector2(rect.Max.X - 94f * scale, controlY), 15f * scale,
                TransportAction.Previous, accent, ink, opacity, interactive, dl))
        {
            playback.Previous();
        }

        if (TransportButton.Draw(new Vector2(rect.Max.X - 54f * scale, controlY), 16f * scale, TransportAction.Stop,
                accent, ink, opacity * (active ? 1f : 0.4f), interactive, dl) && active)
        {
            playback.Stop();
        }

        if (playback.HasQueue && TransportButton.Draw(new Vector2(rect.Max.X - 18f * scale, controlY), 15f * scale,
                TransportAction.Next, accent, ink, opacity, interactive, dl))
        {
            playback.Next();
        }
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value ?? string.Empty;
        }

        return value.Substring(0, max - 1) + "…";
    }
}
