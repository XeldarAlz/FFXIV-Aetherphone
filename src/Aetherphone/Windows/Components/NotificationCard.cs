using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class NotificationCard
{
    public const float Height = 66f;
    private const float Rounding = 16f;
    private const float TileSize = 38f;
    private const float TileLeftPad = 13f;
    private static readonly Vector4 Ink = new(0.99f, 0.99f, 1f, 1f);

    public static void DrawBase(ImDrawListPtr drawList, Rect rect, PhoneNotification notification, PhoneTheme theme,
        float scale, float opacity, float shadowStrength)
    {
        var min = rect.Min;
        var max = rect.Max;
        var rounding = Rounding * scale;
        if (shadowStrength > 0f)
        {
            Elevation.Card(drawList, min, max, rounding, scale, shadowStrength * opacity);
        }

        Squircle.Fill(drawList, min, max, rounding, Color(theme.GroupedCard, opacity));
        Material.EdgeSquircle(drawList, min, max, rounding, scale, opacity);
        var tileSize = TileSize * scale;
        var tileMin = new Vector2(min.X + TileLeftPad * scale, min.Y + (rect.Height - tileSize) * 0.5f);
        var tileMax = tileMin + new Vector2(tileSize, tileSize);
        var tileRounding = tileSize * 0.28f;
        var tint = notification.Accent;
        Squircle.Fill(drawList, tileMin, tileMax, tileRounding, Color(tint, opacity));
        var gloss = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.18f * opacity));
        drawList.AddLine(new Vector2(tileMin.X + tileRounding, tileMin.Y + 1f * scale),
            new Vector2(tileMax.X - tileRounding, tileMin.Y + 1f * scale), gloss, 1f * scale);
        var iconCenter = (tileMin + tileMax) * 0.5f;
        var ink = Palette.WithAlpha(Ink, opacity);
        var hole = Palette.WithAlpha(Palette.Mix(tint, new Vector4(0f, 0f, 0f, 1f), 0.25f), opacity);
        if (!AppIconArt.TryDraw(drawList, notification.AppId, iconCenter, tileSize * 0.5f, ink, hole))
        {
            drawList.AddCircleFilled(iconCenter, 4f * scale, ImGui.GetColorU32(ink), 16);
        }

        var textLeft = tileMax.X + 12f * scale;
        var textRight = max.X - 14f * scale;
        var time = TimeText.Short(notification.ReceivedAt);
        var timeSize = Typography.Measure(time, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(textRight - timeSize.X, min.Y + 13f * scale), time,
            Palette.WithAlpha(theme.TextMuted, opacity), TextStyles.Caption1.Scale, TextStyles.Caption1.Weight);
        var titleMaxWidth = textRight - timeSize.X - 8f * scale - textLeft;
        var titleY = min.Y + 12f * scale;
        Marquee.DrawLeftAuto(drawList, "notificationcard.title." + notification.Id, notification.Title, textLeft,
            titleY, titleMaxWidth, TextStyles.Headline, Palette.WithAlpha(theme.TextStrong, opacity));
        var bodyMaxWidth = textRight - textLeft;
        var bodyY = min.Y + 35f * scale;
        Marquee.DrawLeftAuto(drawList, "notificationcard.body." + notification.Id, notification.Body, textLeft,
            bodyY, bodyMaxWidth, TextStyles.Subheadline, Palette.WithAlpha(theme.TextMuted, opacity));
    }

    public static Vector2 BadgeAnchor(Rect rect, float scale)
    {
        var tileSize = TileSize * scale;
        var tileTop = rect.Min.Y + (rect.Height - tileSize) * 0.5f;
        return new Vector2(rect.Min.X + TileLeftPad * scale + tileSize, tileTop);
    }

    public static void DrawCountBadge(ImDrawListPtr drawList, Vector2 anchor, int count, PhoneTheme theme, float scale,
        float opacity)
    {
        if (count <= 1 || opacity <= 0.01f)
        {
            return;
        }

        var label = count > 99 ? "99+" : count.ToString(Loc.Culture);
        var radius = 9f * scale;
        var textSize = Typography.Measure(label, TextStyles.Caption2);
        var width = MathF.Max(radius * 2f, textSize.X + 9f * scale);
        var min = new Vector2(anchor.X - width * 0.5f, anchor.Y - radius);
        var max = new Vector2(anchor.X + width * 0.5f, anchor.Y + radius);
        Squircle.Fill(drawList, min, max, radius, Color(theme.Danger, opacity));
        Squircle.Stroke(drawList, min, max, radius, Color(theme.AppBackground, opacity), 1.5f * scale);
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label, Palette.WithAlpha(Ink, opacity),
            TextStyles.Caption2.Scale, TextStyles.Caption2.Weight);
    }

    private static uint Color(Vector4 color, float opacity) => ImGui.GetColorU32(color with { W = color.W * opacity });
}
