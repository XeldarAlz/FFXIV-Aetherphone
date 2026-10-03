using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Notifications;
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

internal readonly struct FaceInk
{
    public readonly Vector4 Strong;
    public readonly Vector4 Muted;

    public FaceInk(Vector4 strong, Vector4 muted)
    {
        Strong = strong;
        Muted = muted;
    }
}

internal readonly struct TransportState
{
    public readonly bool CanPrevious;
    public readonly bool CanNext;
    public readonly bool CanToggle;
    public readonly bool Playing;

    public TransportState(bool canPrevious, bool canNext, bool canToggle, bool playing)
    {
        CanPrevious = canPrevious;
        CanNext = canNext;
        CanToggle = canToggle;
        Playing = playing;
    }
}

internal static partial class MinimizedPhoneRenderer
{
    public const float MusicArtSide = 24f;
    private const float StatusIconSize = 9f;
    private const float StatusScale = 0.5f;
    private const float UnreadHeight = 11f;
    private const float UnreadPadding = 6f;
    private const float IslandWidth = 30f;
    private const float IslandHeight = 12f;
    private const float IslandEqualizerHeight = 6f;
    private const float IslandDot = 2.6f;
    private const float DateScale = 0.6f;
    private const float ArtRadiusFactor = 0.24f;
    private const float ArtTextGap = 6f;
    private const float TitleScale = 0.66f;
    private const float SubtitleScale = 0.56f;
    private const float TransportSmall = 8f;
    private const float TransportLarge = 10f;
    private const float TransportStride = 19f;
    private const float TransportRowHalf = 10f;
    private const float CallNameScale = 0.7f;
    private const float CallStatusScale = 0.6f;
    private const float CallStatusGap = 2f;
    private const float DotGap = 4f;
    private const float CallButtonRadius = 10f;
    private const float CallButtonSpread = 15f;
    private const float CallButtonIcon = 9f;
    private const float BannerTile = 20f;
    private const float BannerPadding = 7f;
    private const float BannerTextGap = 6f;
    private const float BannerTitleScale = 0.62f;
    private const float BannerBodyScale = 0.56f;
    private const float PageDotRadius = 1.3f;
    private const float PageDotActive = 4.5f;
    private const float PageDotGap = 3.2f;
    private const float PageDotInset = 4f;
    private const float IndicatorWidth = 30f;
    private const float IndicatorHeight = 2.5f;
    private const float IndicatorBottom = 6f;
    private const float IndicatorAlpha = 0.6f;
    public static readonly Vector4 CallTone = new(0.20f, 0.78f, 0.35f, 1f);
    private static readonly Vector4 MusicAccent = AppAccents.For("music");
    private static readonly Vector4 BadgeTone = new(0.90f, 0.22f, 0.19f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);

    public static TextStyle DateStyle() => new(Text(DateScale), FontWeight.SemiBold);

    public static void DrawStatusLine(ImDrawListPtr drawList, Rect line, in FaceInk ink, float alpha, float dnd,
        string unread, float unreadPresence, float scale)
    {
        if (dnd > 0.01f)
        {
            var moonCenter = new Vector2(line.Min.X + StatusIconSize * 0.5f * scale, line.Center.Y);
            ProgressRing.CenterIcon(drawList, moonCenter, FontAwesomeIcon.Moon,
                Palette.WithAlpha(StatusBar.DndTone, alpha * dnd), StatusIconSize * scale);
        }

        if (unreadPresence <= 0.01f || unread.Length == 0)
        {
            return;
        }

        var style = new TextStyle(Text(StatusScale), FontWeight.Bold);
        var size = Typography.Measure(unread, style);
        var height = UnreadHeight * scale;
        var width = MathF.Max(height, size.X + UnreadPadding * scale);
        var max = new Vector2(line.Max.X, line.Center.Y + height * 0.5f);
        var min = new Vector2(max.X - width, max.Y - height);
        var fade = alpha * unreadPresence;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(BadgeTone, fade)), height * 0.5f);
        Typography.Draw(drawList, (min + max) * 0.5f - size * 0.5f, unread, Palette.WithAlpha(White, fade), style);
    }

    public static void DrawIsland(ImDrawListPtr drawList, Vector2 center, bool call, bool playing, float clock,
        float alpha, float scale)
    {
        if (alpha <= 0.01f)
        {
            return;
        }

        var half = new Vector2(IslandWidth * 0.5f * scale, IslandHeight * 0.5f * scale);
        drawList.AddRectFilled(center - half, center + half, ImGui.GetColorU32(Palette.WithAlpha(Black, alpha)),
            half.Y);
        var leftCenter = new Vector2(center.X - half.X + half.Y, center.Y);
        var rightCenter = new Vector2(center.X + half.X - half.Y, center.Y);
        if (call)
        {
            drawList.AddCircleFilled(leftCenter, IslandDot * scale,
                ImGui.GetColorU32(Palette.WithAlpha(CallTone, alpha)), 12);
        }

        Equalizer.Draw(drawList, rightCenter, scale * 0.6f, IslandEqualizerHeight * scale, clock, MusicAccent, alpha,
            playing);
    }

    public static void DrawHero(ImDrawListPtr drawList, Rect screen, float top, string date, Vector2 dateSize,
        string time, float clockScale, in FaceInk ink, float alpha)
    {
        var centerX = screen.Center.X;
        Typography.Draw(drawList, new Vector2(centerX - dateSize.X * 0.5f, top), date,
            Palette.WithAlpha(ink.Muted, ink.Muted.W * alpha), DateStyle());
        var clockSize = Typography.Measure(time, clockScale, FontWeight.Bold);
        Typography.Draw(drawList, new Vector2(centerX - clockSize.X * 0.5f, top + dateSize.Y), time,
            Palette.WithAlpha(ink.Strong, alpha), clockScale, FontWeight.Bold);
    }

    public static Rect MusicArtRect(Rect inner, float scale) =>
        new(inner.Min, inner.Min + new Vector2(MusicArtSide * scale, MusicArtSide * scale));

    public static float ArtRadius(float scale) => MusicArtSide * ArtRadiusFactor * scale;

    public static void DrawMusicText(ImDrawListPtr drawList, Rect inner, string id, string title, string subtitle,
        in FaceInk ink, float alpha, float scale)
    {
        var left = inner.Min.X + (MusicArtSide + ArtTextGap) * scale;
        var width = MathF.Max(1f, inner.Max.X - left);
        var titleStyle = new TextStyle(Text(TitleScale), FontWeight.SemiBold);
        var subtitleStyle = new TextStyle(Text(SubtitleScale), FontWeight.Regular);
        var titleHeight = Typography.Measure(title, titleStyle).Y;
        var subtitleHeight = subtitle.Length > 0 ? Typography.Measure(subtitle, subtitleStyle).Y : 0f;
        var top = inner.Min.Y + (MusicArtSide * scale - titleHeight - subtitleHeight) * 0.5f;
        Marquee.DrawLeftAuto(drawList, new MarqueeId(id, ".title"), title, left, top, width, titleStyle,
            Palette.WithAlpha(ink.Strong, alpha));
        if (subtitle.Length == 0)
        {
            return;
        }

        Marquee.DrawLeftAuto(drawList, new MarqueeId(id, ".subtitle"), subtitle, left, top + titleHeight, width,
            subtitleStyle, Palette.WithAlpha(ink.Muted, ink.Muted.W * alpha));
    }

    public static MinimizedControlResult DrawTransport(ImDrawListPtr drawList, Rect inner, in TransportState state,
        in FaceInk ink, float alpha, bool active, float scale)
    {
        var centerY = inner.Max.Y - TransportRowHalf * scale;
        var centerX = inner.Center.X;
        var small = TransportSmall * scale;
        var large = TransportLarge * scale;
        var stride = TransportStride * scale;
        var playCenter = new Vector2(centerX, centerY);
        var previousCenter = new Vector2(centerX - stride, centerY);
        var nextCenter = new Vector2(centerX + stride, centerY);
        var hovered = active && ((state.CanToggle && Hovered(playCenter, large)) ||
                                 (state.CanPrevious && Hovered(previousCenter, small)) ||
                                 (state.CanNext && Hovered(nextCenter, small)));
        var action = MinimizedControl.None;
        if (state.CanPrevious && TransportButton.Draw(previousCenter, small, TransportAction.Previous, MusicAccent,
                ink.Strong, alpha, active, drawList))
        {
            action = MinimizedControl.Previous;
        }

        if (state.CanNext && TransportButton.Draw(nextCenter, small, TransportAction.Next, MusicAccent, ink.Strong,
                alpha, active, drawList))
        {
            action = MinimizedControl.Next;
        }

        if (TransportButton.Draw(playCenter, large, state.Playing ? TransportAction.Pause : TransportAction.Play,
                MusicAccent, ink.Strong, state.CanToggle ? alpha : alpha * 0.4f, active && state.CanToggle, drawList))
        {
            action = MinimizedControl.PlayPause;
        }

        return new MinimizedControlResult(action, hovered);
    }

    public static MinimizedControlResult DrawCallCard(ImDrawListPtr drawList, Rect inner, in CallView view,
        string status, float clock, in FaceInk ink, float alpha, bool active, float scale)
    {
        var centerX = inner.Center.X;
        var nameStyle = new TextStyle(Text(CallNameScale), FontWeight.SemiBold);
        var nameHeight = Typography.Measure(view.PeerLabel, nameStyle).Y;
        Marquee.DrawCenteredAuto(drawList, "minimized.call.name", view.PeerLabel, centerX, inner.Min.Y, inner.Width,
            nameStyle, Palette.WithAlpha(ink.Strong, alpha));
        var statusScale = Text(CallStatusScale);
        var statusSize = Typography.Measure(status, statusScale, FontWeight.Medium);
        var pulse = 0.5f + 0.5f * MathF.Sin(clock * 3f);
        var dotRadius = (2.4f + 0.8f * pulse) * scale;
        var groupWidth = dotRadius * 2f + DotGap * scale + statusSize.X;
        var left = centerX - groupWidth * 0.5f;
        var statusTop = inner.Min.Y + nameHeight + CallStatusGap * scale;
        drawList.AddCircleFilled(new Vector2(left + dotRadius, statusTop + statusSize.Y * 0.5f), dotRadius,
            ImGui.GetColorU32(Palette.WithAlpha(CallTone, alpha)), 16);
        Typography.Draw(drawList, new Vector2(left + dotRadius * 2f + DotGap * scale, statusTop), status,
            Palette.WithAlpha(CallTone, 0.95f * alpha), statusScale, FontWeight.Medium);

        var radius = CallButtonRadius * scale;
        var centerY = inner.Max.Y - radius;
        var muteCenter = new Vector2(centerX - CallButtonSpread * scale, centerY);
        var hangupCenter = new Vector2(centerX + CallButtonSpread * scale, centerY);
        var muteFill = view.Muted ? CallTone : Palette.WithAlpha(ink.Strong, 0.2f);
        var muteHovered = active && Hovered(muteCenter, radius);
        var hangupHovered = active && Hovered(hangupCenter, radius);
        var action = MinimizedControl.None;
        var icon = CallButtonIcon * scale;
        if (RoundButton(drawList, muteCenter, radius,
                view.Muted ? FontAwesomeIcon.MicrophoneSlash : FontAwesomeIcon.Microphone, muteFill, ink.Strong, alpha,
                muteHovered, icon))
        {
            action = MinimizedControl.ToggleMute;
        }

        if (RoundButton(drawList, hangupCenter, radius, FontAwesomeIcon.PhoneSlash, BadgeTone, White, alpha,
                hangupHovered, icon))
        {
            action = MinimizedControl.Hangup;
        }

        return new MinimizedControlResult(action, muteHovered || hangupHovered);
    }

    public static MinimizedControlResult DrawHangup(ImDrawListPtr drawList, Vector2 center, float radius, float alpha,
        bool active, float scale)
    {
        var hovered = active && Hovered(center, radius);
        var clicked = RoundButton(drawList, center, radius, FontAwesomeIcon.PhoneSlash, BadgeTone, White, alpha,
            hovered, radius * 1.1f);
        return new MinimizedControlResult(clicked ? MinimizedControl.Hangup : MinimizedControl.None, hovered);
    }

    public static float BannerHeight(float scale)
    {
        var title = Typography.Measure("0", Text(BannerTitleScale), FontWeight.SemiBold).Y;
        var body = Typography.Measure("0", Text(BannerBodyScale), FontWeight.Regular).Y;
        return MathF.Max(BannerTile * scale, title + body) + BannerPadding * 2f * scale;
    }

    public static void DrawBanner(ImDrawListPtr drawList, Rect banner, PhoneNotification notification,
        in FaceInk ink, float alpha, float scale)
    {
        var tile = BannerTile * scale;
        var tileMin = new Vector2(banner.Min.X + BannerPadding * scale, banner.Center.Y - tile * 0.5f);
        var tileMax = tileMin + new Vector2(tile, tile);
        if (!AppIconTile.TryDraw(drawList, notification.AppId, notification.Accent, tileMin, tileMax,
                tile * Metrics.Radius.TileFactor, alpha, false, scale))
        {
            DrawFallbackTile(drawList, notification, tileMin, tileMax, tile, alpha);
        }

        var left = tileMax.X + BannerTextGap * scale;
        var width = MathF.Max(1f, banner.Max.X - BannerPadding * scale - left);
        var titleStyle = new TextStyle(Text(BannerTitleScale), FontWeight.SemiBold);
        var bodyStyle = new TextStyle(Text(BannerBodyScale), FontWeight.Regular);
        var titleHeight = Typography.Measure(notification.Title, titleStyle).Y;
        var bodyHeight = Typography.Measure("0", bodyStyle).Y;
        var top = banner.Center.Y - (titleHeight + bodyHeight) * 0.5f;
        Marquee.DrawLeftAuto(drawList, "minimized.banner.title", notification.Title, left, top, width, titleStyle,
            Palette.WithAlpha(ink.Strong, alpha));
        EmojiText.DrawLine(drawList, "minimized.banner.body", notification.SingleLineBody,
            new Vector2(left, top + titleHeight), width, ink.Muted, alpha, bodyStyle);
    }

    public static void DrawPageDots(ImDrawListPtr drawList, Rect slot, int count, float position, in FaceInk ink,
        float alpha, float scale)
    {
        if (count < 2 || alpha <= 0.01f)
        {
            return;
        }

        var radius = PageDotRadius * scale;
        var gap = PageDotGap * scale;
        var active = PageDotActive * scale;
        var total = count * radius * 2f + (count - 1) * gap + (active - radius * 2f);
        var x = slot.Max.X - PageDotInset * scale;
        var y = slot.Center.Y - total * 0.5f;
        for (var index = 0; index < count; index++)
        {
            var focus = Math.Clamp(1f - MathF.Abs(position - index), 0f, 1f);
            var height = radius * 2f + (active - radius * 2f) * focus;
            var color = Palette.WithAlpha(ink.Strong, alpha * (0.35f + 0.65f * focus));
            drawList.AddRectFilled(new Vector2(x - radius, y), new Vector2(x + radius, y + height),
                ImGui.GetColorU32(color), radius);
            y += height + gap;
        }
    }

    public static void DrawIndicator(ImDrawListPtr drawList, Rect screen, in FaceInk ink, float alpha, float scale)
    {
        var half = new Vector2(IndicatorWidth * 0.5f * scale, IndicatorHeight * 0.5f * scale);
        var center = new Vector2(screen.Center.X, screen.Max.Y - IndicatorBottom * scale);
        drawList.AddRectFilled(center - half, center + half,
            ImGui.GetColorU32(Palette.WithAlpha(ink.Strong, IndicatorAlpha * alpha)), half.Y);
    }

    public static void DrawPulse(ImDrawListPtr drawList, in ChassisGeometry geometry, Vector4 accent, float strength,
        float scale)
    {
        var glass = geometry.Glass;
        var inner = 1f * scale;
        var outer = 4f * scale;
        Squircle.Stroke(drawList, glass.Min - new Vector2(inner, inner), glass.Max + new Vector2(inner, inner),
            geometry.GlassRadius + inner, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.75f * strength)), 2f * scale);
        Squircle.Stroke(drawList, glass.Min - new Vector2(outer, outer), glass.Max + new Vector2(outer, outer),
            geometry.GlassRadius + outer, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.28f * strength)), 3f * scale);
    }

    private static void DrawFallbackTile(ImDrawListPtr drawList, PhoneNotification notification, Vector2 tileMin,
        Vector2 tileMax, float tile, float alpha)
    {
        var surface = IconTile.Surface(notification.Accent);
        var tileCenter = (tileMin + tileMax) * 0.5f;
        Squircle.Fill(drawList, tileMin, tileMax, tile * Metrics.Radius.TileFactor,
            ImGui.GetColorU32(Palette.WithAlpha(surface, alpha)));
        var ink = Palette.WithAlpha(AccentRing.Ink, alpha);
        if (AppIconArt.TryDraw(drawList, notification.AppId, tileCenter, tile, ink, Palette.WithAlpha(surface, alpha)))
        {
            return;
        }

        var initial = notification.Title.Length > 0 ? notification.Title.Substring(0, 1) : "?";
        Typography.DrawCentered(drawList, tileCenter, initial, ink, Text(0.8f), FontWeight.SemiBold);
    }

    private static bool RoundButton(ImDrawListPtr drawList, Vector2 center, float radius, FontAwesomeIcon icon,
        Vector4 fill, Vector4 ink, float alpha, bool hovered, float iconSize)
    {
        var color = hovered ? Palette.Mix(fill, White, 0.14f) : fill;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(color, alpha * color.W)), 28);
        ProgressRing.CenterIcon(drawList, center, icon, Palette.WithAlpha(ink, alpha), iconSize);
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
