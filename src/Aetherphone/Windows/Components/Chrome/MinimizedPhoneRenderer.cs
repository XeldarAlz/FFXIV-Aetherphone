using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal enum MinimizedControl : byte
{
    None,
    Previous,
    PlayPause,
    Next,
    ToggleMute,
    Hangup,
}

internal readonly struct MinimizedControlResult
{
    public readonly MinimizedControl Action;
    public readonly bool Hovered;

    public MinimizedControlResult(MinimizedControl action, bool hovered)
    {
        Action = action;
        Hovered = hovered;
    }
}

internal static class MinimizedPhoneRenderer
{
    private const float MeridiemScale = 0.6f;
    private const float MeridiemGap = 4f;
    private const float MoonHeight = 11f;
    private const float MoonGap = 5f;
    private const float DiscRadius = 15f;
    private const float TitleGap = 6f;
    private const float EqualizerGap = 4f;
    private const float EqualizerHeight = 12f;
    private const float StatusGap = 3f;
    private const float DotGap = 5f;
    private const float TransportSmall = 9f;
    private const float TransportLarge = 11f;
    private const float TransportStride = 21f;
    private const float CallButtonRadius = 13f;
    private const float CallButtonSpread = 17f;
    private const float CallButtonIcon = 11f;
    private const float CardTile = 26f;
    private const float CardTitleGap = 5f;
    private const float CardBodyGap = 2f;
    private const float BadgeTile = 22f;
    private const float BadgePillHeight = 14f;
    private static readonly Vector4 MusicAccent = AppAccents.For("music");
    private static readonly Vector4 CallAccent = new(0.20f, 0.78f, 0.35f, 1f);
    private static readonly Vector4 BadgeTone = new(0.90f, 0.22f, 0.19f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static float InlineMeridiemWidth(string meridiem, float scale) =>
        Typography.Measure(meridiem, Text(MeridiemScale), FontWeight.Medium).X + MeridiemGap * scale;

    public static void DrawTime(ImDrawListPtr dl, Rect screen, float top, string time, string inlineMeridiem,
        float clockScale, Vector2 clockSize, PhoneTheme theme, float alpha, float scale)
    {
        var centerX = screen.Center.X;
        var meridiemWidth = inlineMeridiem.Length > 0 ? InlineMeridiemWidth(inlineMeridiem, scale) : 0f;
        var left = centerX - (clockSize.X + meridiemWidth) * 0.5f;
        Typography.Draw(dl, new Vector2(left, top), time, Palette.WithAlpha(theme.TextStrong, alpha), clockScale,
            FontWeight.Bold);
        if (inlineMeridiem.Length == 0)
        {
            return;
        }

        var meridiemSize = Typography.Measure(inlineMeridiem, Text(MeridiemScale), FontWeight.Medium);
        Typography.Draw(dl, new Vector2(left + clockSize.X + MeridiemGap * scale, top + meridiemSize.Y * 0.35f),
            inlineMeridiem, Palette.WithAlpha(theme.Accent, alpha), Text(MeridiemScale), FontWeight.Medium);
    }

    public static void DrawDateRow(ImDrawListPtr dl, Rect screen, float top, string meridiem, string date,
        Vector2 dateSize, PhoneTheme theme, float alpha, float dnd, float scale)
    {
        var centerX = screen.Center.X;
        var meridiemSize = Vector2.Zero;
        var meridiemWidth = 0f;
        if (meridiem.Length > 0)
        {
            meridiemSize = Typography.Measure(meridiem, Text(MeridiemScale), FontWeight.Medium);
            meridiemWidth = meridiemSize.X + MeridiemGap * scale;
        }

        var moonWidth = (MoonGap + MoonHeight) * scale * dnd;
        var x = centerX - (meridiemWidth + dateSize.X + moonWidth) * 0.5f;
        if (meridiem.Length > 0)
        {
            Typography.Draw(dl, new Vector2(x, top + (dateSize.Y - meridiemSize.Y) * 0.5f), meridiem,
                Palette.WithAlpha(theme.Accent, alpha), Text(MeridiemScale), FontWeight.Medium);
            x += meridiemWidth;
        }

        Typography.Draw(dl, new Vector2(x, top), date, Palette.WithAlpha(theme.TextMuted, alpha), Text(0.72f),
            FontWeight.Regular);
        x += dateSize.X;
        if (dnd > 0.01f)
        {
            var moonCenter = new Vector2(x + MoonGap * scale * dnd + MoonHeight * scale * 0.5f,
                top + dateSize.Y * 0.5f);
            ProgressRing.CenterIcon(dl, moonCenter, FontAwesomeIcon.Moon,
                Palette.WithAlpha(StatusBar.DndTone, alpha * dnd), MoonHeight * scale);
        }
    }

    public static void DrawMusicSection(ImDrawListPtr dl, Rect rect, PlaybackHub playback, float clock, float alpha,
        float scale, PhoneTheme theme)
    {
        var centerX = rect.Center.X;
        var radius = DiscRadius * scale;
        var discCenter = new Vector2(centerX, rect.Min.Y + radius);
        ArtGradient.DrawDisc(dl, discCenter, radius, ArtGradient.FromName(playback.Title), alpha);
        var style = new TextStyle(Text(0.78f), FontWeight.SemiBold);
        var titleTop = discCenter.Y + radius + TitleGap * scale;
        var titleHeight = Typography.Measure(playback.Title, style).Y;
        Marquee.DrawCenteredAuto(dl, "minimized.music.title", playback.Title, centerX, titleTop, rect.Width, style,
            Palette.WithAlpha(theme.TextStrong, alpha));
        var equalizerCenter = new Vector2(centerX,
            titleTop + titleHeight + EqualizerGap * scale + EqualizerHeight * scale * 0.5f);
        Equalizer.Draw(dl, equalizerCenter, scale, EqualizerHeight * scale, clock, MusicAccent, alpha,
            playback.IsPlaying);
    }

    public static MinimizedControlResult DrawMusicTransport(ImDrawListPtr dl, Rect row, PlaybackHub playback,
        PhoneTheme theme, float alpha, bool active, float scale)
    {
        var centerY = row.Center.Y;
        var centerX = row.Center.X;
        var small = TransportSmall * scale;
        var large = TransportLarge * scale;
        var stride = TransportStride * scale;
        var hasQueue = playback.HasQueue;
        var playCenter = new Vector2(centerX, centerY);
        var prevCenter = new Vector2(centerX - stride, centerY);
        var nextCenter = new Vector2(centerX + stride, centerY);
        var hovered = active && (Hovered(playCenter, large) || hasQueue && (Hovered(prevCenter, small) ||
                                                                              Hovered(nextCenter, small)));
        var action = MinimizedControl.None;
        var ink = theme.TextStrong;
        if (hasQueue)
        {
            if (TransportButton.Draw(prevCenter, small, TransportAction.Previous, MusicAccent, ink, alpha, active, dl))
            {
                action = MinimizedControl.Previous;
            }

            if (TransportButton.Draw(nextCenter, small, TransportAction.Next, MusicAccent, ink, alpha, active, dl))
            {
                action = MinimizedControl.Next;
            }
        }

        if (TransportButton.Draw(playCenter, large, playback.IsPlaying ? TransportAction.Pause : TransportAction.Play,
                MusicAccent, ink, alpha, active, dl))
        {
            action = MinimizedControl.PlayPause;
        }

        return new MinimizedControlResult(action, hovered);
    }

    public static void DrawCallSection(ImDrawListPtr dl, Rect rect, in CallView view, string status, float clock,
        float alpha, float scale, PhoneTheme theme)
    {
        var centerX = rect.Center.X;
        var nameScale = Text(0.8f);
        var name = Typography.FitText(view.PeerLabel, rect.Width, nameScale, FontWeight.SemiBold);
        var nameSize = Typography.Measure(name, nameScale, FontWeight.SemiBold);
        Typography.Draw(dl, new Vector2(centerX - nameSize.X * 0.5f, rect.Min.Y), name,
            Palette.WithAlpha(theme.TextStrong, alpha), nameScale, FontWeight.SemiBold);
        var statusScale = Text(0.72f);
        var statusSize = Typography.Measure(status, statusScale, FontWeight.Medium);
        var pulse = 0.5f + 0.5f * MathF.Sin(clock * 3f);
        var dotRadius = (3f + 1f * pulse) * scale;
        var groupWidth = dotRadius * 2f + DotGap * scale + statusSize.X;
        var left = centerX - groupWidth * 0.5f;
        var statusTop = rect.Min.Y + nameSize.Y + StatusGap * scale;
        var rowCenterY = statusTop + statusSize.Y * 0.5f;
        dl.AddCircleFilled(new Vector2(left + dotRadius, rowCenterY), dotRadius,
            ImGui.GetColorU32(Palette.WithAlpha(CallAccent, alpha)), 16);
        Typography.Draw(dl, new Vector2(left + dotRadius * 2f + DotGap * scale, statusTop), status,
            Palette.WithAlpha(CallAccent, 0.95f * alpha), statusScale, FontWeight.Medium);
    }

    public static MinimizedControlResult DrawCallControls(ImDrawListPtr dl, Rect row, in CallView view,
        PhoneTheme theme, float alpha, bool active, float scale)
    {
        var centerY = row.Center.Y;
        var centerX = row.Center.X;
        var radius = CallButtonRadius * scale;
        var muteCenter = new Vector2(centerX - CallButtonSpread * scale, centerY);
        var hangupCenter = new Vector2(centerX + CallButtonSpread * scale, centerY);
        var muteFill = view.Muted ? CallAccent : Palette.WithAlpha(theme.TextStrong, 0.18f);
        var action = MinimizedControl.None;
        var muteHovered = active && Hovered(muteCenter, radius);
        var hangupHovered = active && Hovered(hangupCenter, radius);
        if (RoundButton(dl, muteCenter, radius, view.Muted ? FontAwesomeIcon.MicrophoneSlash : FontAwesomeIcon.Microphone,
                muteFill, theme.TextStrong, alpha, muteHovered, scale))
        {
            action = MinimizedControl.ToggleMute;
        }

        if (RoundButton(dl, hangupCenter, radius, FontAwesomeIcon.PhoneSlash, theme.Danger, White, alpha,
                hangupHovered, scale))
        {
            action = MinimizedControl.Hangup;
        }

        return new MinimizedControlResult(action, muteHovered || hangupHovered);
    }

    public static void DrawCardSection(ImDrawListPtr dl, Rect rect, PhoneNotification notification, PhoneTheme theme,
        float alpha, float scale)
    {
        var centerX = rect.Center.X;
        var tile = CardTile * scale;
        var tileMin = new Vector2(centerX - tile * 0.5f, rect.Min.Y);
        var tileMax = new Vector2(centerX + tile * 0.5f, rect.Min.Y + tile);
        var tileCenter = (tileMin + tileMax) * 0.5f;
        var surface = IconTile.Surface(notification.Accent);
        Squircle.Fill(dl, tileMin, tileMax, tile * Metrics.Radius.TileFactor,
            ImGui.GetColorU32(Palette.WithAlpha(surface, alpha)));
        var ink = Palette.WithAlpha(AccentRing.Ink, alpha);
        if (!AppIconArt.TryDraw(dl, notification.AppId, tileCenter, tile, ink, Palette.WithAlpha(surface, alpha)))
        {
            var initial = notification.Title.Length > 0 ? notification.Title.Substring(0, 1) : "?";
            Typography.DrawCentered(dl, tileCenter, initial, ink, Text(0.95f), FontWeight.SemiBold);
        }

        var titleStyle = new TextStyle(Text(0.75f), FontWeight.SemiBold);
        var bodyStyle = new TextStyle(Text(0.7f), FontWeight.Regular);
        var titleTop = tileMax.Y + CardTitleGap * scale;
        var titleHeight = Typography.Measure(notification.Title, titleStyle).Y;
        Marquee.DrawCenteredAuto(dl, "minimized.card.title", notification.Title, centerX, titleTop, rect.Width,
            titleStyle, Palette.WithAlpha(theme.TextStrong, alpha));
        EmojiText.DrawLineCentered(dl, "minimized.card.body", notification.SingleLineBody,
            new Vector2(centerX, titleTop + titleHeight + CardBodyGap * scale), rect.Width, theme.TextMuted, alpha,
            bodyStyle);
    }

    public static void DrawCardStroke(ImDrawListPtr dl, in ChassisGeometry geometry, Vector4 accent, float alpha,
        bool hovered, float scale)
    {
        Squircle.Stroke(dl, geometry.Glass.Min, geometry.Glass.Max, geometry.GlassRadius,
            ImGui.GetColorU32(Palette.WithAlpha(accent, (hovered ? 0.7f : 0.45f) * alpha)), 1.5f * scale);
    }

    public static void DrawBadge(ImDrawListPtr dl, Vector2 center, string appId, Vector4 accent, string count,
        PhoneTheme theme, float alpha, float scale)
    {
        var tile = BadgeTile * scale;
        var tileMin = center - new Vector2(tile * 0.5f, tile * 0.5f);
        var tileMax = center + new Vector2(tile * 0.5f, tile * 0.5f);
        var surface = IconTile.Surface(accent);
        Squircle.Fill(dl, tileMin, tileMax, tile * Metrics.Radius.TileFactor,
            ImGui.GetColorU32(Palette.WithAlpha(surface, alpha)));
        var ink = Palette.WithAlpha(AccentRing.Ink, alpha);
        if (!AppIconArt.TryDraw(dl, appId, center, tile, ink, Palette.WithAlpha(surface, alpha)))
        {
            ProgressRing.CenterIcon(dl, center, FontAwesomeIcon.Bell, ink, tile * 0.5f);
        }

        var countScale = Text(0.6f);
        var countSize = Typography.Measure(count, countScale, FontWeight.Bold);
        var pillHeight = BadgePillHeight * scale;
        var pillWidth = MathF.Max(pillHeight, countSize.X + 8f * scale);
        var pillCenter = new Vector2(tileMax.X - 2f * scale, tileMin.Y + 2f * scale);
        var pillMin = new Vector2(pillCenter.X - pillWidth * 0.5f, pillCenter.Y - pillHeight * 0.5f);
        var pillMax = new Vector2(pillCenter.X + pillWidth * 0.5f, pillCenter.Y + pillHeight * 0.5f);
        var outline = 1.5f * scale;
        dl.AddRectFilled(pillMin - new Vector2(outline, outline), pillMax + new Vector2(outline, outline),
            ImGui.GetColorU32(Palette.WithAlpha(theme.ScreenBase, alpha)), pillHeight * 0.5f + outline);
        dl.AddRectFilled(pillMin, pillMax, ImGui.GetColorU32(Palette.WithAlpha(BadgeTone, alpha)), pillHeight * 0.5f);
        Typography.Draw(dl, pillCenter - countSize * 0.5f, count, Palette.WithAlpha(White, alpha), countScale,
            FontWeight.Bold);
    }

    public static void DrawPulse(ImDrawListPtr dl, in ChassisGeometry geometry, Vector4 accent, float strength,
        float scale)
    {
        var glass = geometry.Glass;
        var inner = 1f * scale;
        var outer = 4f * scale;
        Squircle.Stroke(dl, glass.Min - new Vector2(inner, inner), glass.Max + new Vector2(inner, inner),
            geometry.GlassRadius + inner, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.75f * strength)), 2f * scale);
        Squircle.Stroke(dl, glass.Min - new Vector2(outer, outer), glass.Max + new Vector2(outer, outer),
            geometry.GlassRadius + outer, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.28f * strength)), 3f * scale);
    }

    private static bool RoundButton(ImDrawListPtr dl, Vector2 center, float radius, FontAwesomeIcon icon, Vector4 fill,
        Vector4 ink, float alpha, bool hovered, float scale)
    {
        var color = hovered ? Palette.Mix(fill, White, 0.14f) : fill;
        dl.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(color, alpha * color.W)), 28);
        ProgressRing.CenterIcon(dl, center, icon, Palette.WithAlpha(ink, alpha), CallButtonIcon * scale);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static bool Hovered(Vector2 center, float radius) =>
        UiInteract.Hover(center - new Vector2(radius, radius), center + new Vector2(radius, radius));

    private static float Text(float scale) => UiScale.MinimizedText(scale);
}
