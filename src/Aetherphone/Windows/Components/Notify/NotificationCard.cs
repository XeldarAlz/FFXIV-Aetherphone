using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class NotificationCard
{
    public const float Height = 80f;
    public const float Rounding = 22f;
    public const float IconSize = 36f;
    public const float IconInset = 14f;
    private const float TextGap = 12f;
    private const float RightPad = 14f;
    private const float TopPad = 12f;
    private const float BottomPad = 12f;
    private const float TitleBodyGap = 2f;
    private const float TimeGap = 8f;
    private const int BodyLines = 2;
    private const float MutedAlpha = 0.72f;
    private const float ArtFraction = 0.98f;
    private const float DotFraction = 0.13f;
    private const float HoleMix = 0.28f;
    private const int DotSegments = 16;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static Vector4 Ink(GlassTone tone, PhoneTheme theme) =>
        tone == GlassTone.Dark ? White : theme.TextStrong;

    public static Vector4 MutedInk(GlassTone tone, PhoneTheme theme) =>
        tone == GlassTone.Dark ? White with { W = MutedAlpha } : theme.TextMuted;

    public static void DrawGlass(ImDrawListPtr drawList, Rect rect, float scale, float opacity, GlassTone tone) =>
        Material.LiquidGlass(drawList, rect.Min, rect.Max, Rounding * scale, scale, tone, 0f, opacity);

    public static void DrawContent(ImDrawListPtr drawList, Rect rect, PhoneNotification notification,
        PhoneTheme theme, float scale, float opacity, GlassTone tone, bool singleLineBody, string titleMarquee,
        string bodyMarquee) =>
        DrawContent(drawList, rect, notification, Ink(tone, theme), MutedInk(tone, theme), scale, opacity,
            singleLineBody, titleMarquee, bodyMarquee);

    public static void DrawContent(ImDrawListPtr drawList, Rect rect, PhoneNotification notification,
        Vector4 strongInk, Vector4 muted, float scale, float opacity, bool singleLineBody, string titleMarquee,
        string bodyMarquee)
    {
        var ink = Palette.WithAlpha(strongInk, opacity);
        var iconSize = IconSize * scale;
        var iconMin = new Vector2(rect.Min.X + IconInset * scale, rect.Center.Y - iconSize * 0.5f);
        DrawAppIcon(drawList, notification, iconMin, iconSize, opacity);
        var textLeft = iconMin.X + iconSize + TextGap * scale;
        var textRight = rect.Max.X - RightPad * scale;
        if (textRight - textLeft <= 1f)
        {
            return;
        }

        var titleLine = CompactLineHeight(TextStyles.Headline);
        var bodyLine = CompactLineHeight(TextStyles.Subheadline);
        var gap = TitleBodyGap * scale;
        float titleTop;
        float bodyMaxHeight;
        if (singleLineBody)
        {
            titleTop = rect.Center.Y - (titleLine + gap + bodyLine) * 0.5f;
            bodyMaxHeight = bodyLine;
        }
        else
        {
            titleTop = rect.Min.Y + TopPad * scale;
            var available = rect.Max.Y - BottomPad * scale - (titleTop + titleLine + gap);
            bodyMaxHeight = MathF.Min(available, bodyLine * BodyLines + 1f);
        }

        var time = TimeText.Clock(notification.ReceivedAt);
        var timeSize = Typography.Measure(time, TextStyles.Caption2);
        Typography.Draw(drawList, new Vector2(textRight - timeSize.X, titleTop + (titleLine - timeSize.Y) * 0.5f), time,
            Palette.WithAlpha(muted, muted.W * opacity), TextStyles.Caption2);
        var titleMaxWidth = MathF.Max(1f, textRight - timeSize.X - TimeGap * scale - textLeft);
        Marquee.DrawLeftAuto(drawList, new MarqueeId(titleMarquee, notification.Id), notification.Title, textLeft,
            titleTop, titleMaxWidth, TextStyles.Headline, ink);
        var bodyTop = new Vector2(textLeft, titleTop + titleLine + gap);
        var bodyWidth = textRight - textLeft;
        if (singleLineBody)
        {
            EmojiText.DrawLine(drawList, new MarqueeId(bodyMarquee, notification.Id), notification.SingleLineBody,
                bodyTop, bodyWidth, muted, opacity, TextStyles.Subheadline);
            return;
        }

        EmojiText.DrawBlock(drawList, bodyTop, notification.SingleLineBody, muted, opacity, TextStyles.Subheadline,
            bodyWidth, bodyMaxHeight);
    }

    public static void DrawAppIcon(ImDrawListPtr drawList, PhoneNotification notification, Vector2 min, float size,
        float opacity)
    {
        var max = min + new Vector2(size, size);
        if (AppIconTile.TryDraw(drawList, notification.AppId, notification.Accent, min, max,
                size * Metrics.Radius.TileFactor, opacity, false))
        {
            return;
        }

        var surface = IconTile.Surface(notification.Accent);
        Squircle.Fill(drawList, min, max, size * Metrics.Radius.TileFactor, Color(surface, opacity));
        var center = (min + max) * 0.5f;
        var ink = Palette.WithAlpha(AccentRing.Ink, opacity);
        var hole = Palette.WithAlpha(Palette.Mix(surface, AccentRing.Ink, HoleMix), opacity);
        if (AppIconArt.TryDraw(drawList, notification.AppId, center, size * ArtFraction, ink, hole))
        {
            return;
        }

        drawList.AddCircleFilled(center, size * DotFraction, ImGui.GetColorU32(ink), DotSegments);
    }

    private static float CompactLineHeight(in TextStyle style)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            return ImGui.GetTextLineHeight();
        }
    }

    private static uint Color(Vector4 color, float opacity) => ImGui.GetColorU32(color with { W = color.W * opacity });
}
