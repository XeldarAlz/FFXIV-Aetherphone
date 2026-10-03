using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal enum RailSide
{
    Left,
    Right,
    Top,
    Bottom,
}

internal enum HardwareKey : byte
{
    Action,
    LockPosition,
    Side,
    CameraControl,
}

internal static class HardwareButton
{
    private const float ProudShare = 0.45f;
    private const float SapphireProudShare = 0.30f;
    private const float PressTravelShare = 0.45f;
    private const float CrownPosition = 0.62f;
    private const float Bury = 1f;
    private const float InwardHit = 8f;
    private const float OutwardHit = 2f;
    private const float AlongHit = 6f;
    private const float SpecularMinProud = 2f;

    public static Rect HitRect(Rect slot, RailSide side)
    {
        var scale = UiScale.Current;
        var inward = InwardHit * scale;
        var outward = OutwardHit * scale;
        var along = AlongHit * scale;
        return side switch
        {
            RailSide.Top => new Rect(new Vector2(slot.Min.X - along, slot.Min.Y - outward),
                new Vector2(slot.Max.X + along, slot.Max.Y + inward)),
            RailSide.Bottom => new Rect(new Vector2(slot.Min.X - along, slot.Min.Y - inward),
                new Vector2(slot.Max.X + along, slot.Max.Y + outward)),
            RailSide.Left => new Rect(new Vector2(slot.Min.X - outward, slot.Min.Y - along),
                new Vector2(slot.Max.X + inward, slot.Max.Y + along)),
            _ => new Rect(new Vector2(slot.Min.X - inward, slot.Min.Y - along),
                new Vector2(slot.Max.X + outward, slot.Max.Y + along)),
        };
    }

    public static void Draw(ImDrawListPtr drawList, Rect slot, PhoneTheme theme, RailSide side, HardwareKey key,
        bool hovered, float press)
    {
        var scale = UiScale.Current;
        var sapphire = key == HardwareKey.CameraControl;
        var gutter = Protrusion(slot, side);
        var proud = gutter * (sapphire ? SapphireProudShare : ProudShare) * (1f - press * PressTravelShare);
        if (proud < 0.5f)
        {
            return;
        }

        var cap = Proud(slot, side, proud, Bury * scale);
        var rounding = MathF.Min(proud, Length(cap, side) * 0.5f);
        var metal = theme.FrameMetal with { W = 1f };
        var body = sapphire ? Palette.Lighten(theme.Glass, 0.12f) : metal;
        FillCap(drawList, cap, rounding, side, ImGui.GetColorU32(body));
        var crown = Palette.Lighten(body, (sapphire ? 0.22f : 0.32f) - press * 0.18f);
        var flank = Palette.Darken(body, 0.34f + press * 0.10f);
        var edge = Palette.Darken(body, 0.14f);
        Face(drawList, cap, rounding, side, flank, crown, edge);
        if (proud >= SpecularMinProud * scale)
        {
            Specular(drawList, cap, rounding, side, hovered, press, scale);
        }

        var outline = sapphire ? Palette.WithAlpha(metal, 0.9f) : new Vector4(0f, 0f, 0f, 0.30f);
        StrokeOutside(drawList, cap, slot, rounding, side, ImGui.GetColorU32(outline), scale);
    }

    private static float Protrusion(Rect slot, RailSide side) =>
        side is RailSide.Top or RailSide.Bottom ? slot.Height : slot.Width;

    private static float Length(Rect rect, RailSide side) =>
        side is RailSide.Top or RailSide.Bottom ? rect.Width : rect.Height;

    private static Rect Proud(Rect slot, RailSide side, float proud, float bury) => side switch
    {
        RailSide.Right => new Rect(new Vector2(slot.Min.X - bury, slot.Min.Y),
            new Vector2(slot.Min.X + proud, slot.Max.Y)),
        RailSide.Left => new Rect(new Vector2(slot.Max.X - proud, slot.Min.Y),
            new Vector2(slot.Max.X + bury, slot.Max.Y)),
        RailSide.Top => new Rect(new Vector2(slot.Min.X, slot.Max.Y - proud),
            new Vector2(slot.Max.X, slot.Max.Y + bury)),
        _ => new Rect(new Vector2(slot.Min.X, slot.Min.Y - bury), new Vector2(slot.Max.X, slot.Min.Y + proud)),
    };

    private static void FillCap(ImDrawListPtr drawList, Rect rect, float rounding, RailSide side, uint color)
    {
        switch (side)
        {
            case RailSide.Right:
                Squircle.FillSideCap(drawList, rect.Min, rect.Max, rounding, color, false);
                return;
            case RailSide.Top:
                Squircle.FillCap(drawList, rect.Min, rect.Max, rounding, color, true);
                return;
            case RailSide.Bottom:
                Squircle.FillCap(drawList, rect.Min, rect.Max, rounding, color, false);
                return;
            default:
                Squircle.FillSideCap(drawList, rect.Min, rect.Max, rounding, color, true);
                return;
        }
    }

    private static void Face(ImDrawListPtr drawList, Rect cap, float rounding, RailSide side, Vector4 flank,
        Vector4 crown, Vector4 edge)
    {
        var flankColor = ImGui.GetColorU32(flank);
        var crownColor = ImGui.GetColorU32(crown);
        var edgeColor = ImGui.GetColorU32(edge);
        if (side is RailSide.Top or RailSide.Bottom)
        {
            var left = cap.Min.X + rounding;
            var right = cap.Max.X - rounding;
            if (right <= left)
            {
                return;
            }

            var innerY = side == RailSide.Top ? cap.Max.Y : cap.Min.Y;
            var outerY = side == RailSide.Top ? cap.Min.Y : cap.Max.Y;
            var crownY = float.Lerp(innerY, outerY, CrownPosition);
            Band(drawList, new Vector2(left, innerY), new Vector2(right, crownY), flankColor, crownColor, false);
            Band(drawList, new Vector2(left, crownY), new Vector2(right, outerY), crownColor, edgeColor, false);
            return;
        }

        var top = cap.Min.Y + rounding;
        var bottom = cap.Max.Y - rounding;
        if (bottom <= top)
        {
            return;
        }

        var innerX = side == RailSide.Right ? cap.Min.X : cap.Max.X;
        var outerX = side == RailSide.Right ? cap.Max.X : cap.Min.X;
        var crownX = float.Lerp(innerX, outerX, CrownPosition);
        Band(drawList, new Vector2(innerX, top), new Vector2(crownX, bottom), flankColor, crownColor, true);
        Band(drawList, new Vector2(crownX, top), new Vector2(outerX, bottom), crownColor, edgeColor, true);
    }

    private static void Band(ImDrawListPtr drawList, Vector2 from, Vector2 to, uint fromColor, uint toColor,
        bool horizontalGradient)
    {
        var min = Vector2.Min(from, to);
        var max = Vector2.Max(from, to);
        if (max.X - min.X <= 0f || max.Y - min.Y <= 0f)
        {
            return;
        }

        if (horizontalGradient)
        {
            var leftColor = from.X <= to.X ? fromColor : toColor;
            var rightColor = from.X <= to.X ? toColor : fromColor;
            drawList.AddRectFilledMultiColor(min, max, leftColor, rightColor, rightColor, leftColor);
            return;
        }

        var topColor = from.Y <= to.Y ? fromColor : toColor;
        var bottomColor = from.Y <= to.Y ? toColor : fromColor;
        drawList.AddRectFilledMultiColor(min, max, topColor, topColor, bottomColor, bottomColor);
    }

    private static void Specular(ImDrawListPtr drawList, Rect cap, float rounding, RailSide side, bool hovered,
        float press, float scale)
    {
        var alpha = (hovered ? 0.55f : 0.36f) * (1f - press * 0.6f);
        var color = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));
        var thickness = 1f * scale;
        if (side is RailSide.Top or RailSide.Bottom)
        {
            var innerY = side == RailSide.Top ? cap.Max.Y : cap.Min.Y;
            var outerY = side == RailSide.Top ? cap.Min.Y : cap.Max.Y;
            var y = float.Lerp(innerY, outerY, CrownPosition);
            drawList.AddLine(new Vector2(cap.Min.X + rounding, y), new Vector2(cap.Max.X - rounding, y), color,
                thickness);
            return;
        }

        var innerX = side == RailSide.Right ? cap.Min.X : cap.Max.X;
        var outerX = side == RailSide.Right ? cap.Max.X : cap.Min.X;
        var x = float.Lerp(innerX, outerX, CrownPosition);
        drawList.AddLine(new Vector2(x, cap.Min.Y + rounding), new Vector2(x, cap.Max.Y - rounding), color,
            thickness);
    }

    private static void StrokeOutside(ImDrawListPtr drawList, Rect cap, Rect slot, float rounding, RailSide side,
        uint color, float scale)
    {
        var bleed = 2f * scale;
        var clip = side switch
        {
            RailSide.Right => new Rect(new Vector2(slot.Min.X, slot.Min.Y - bleed),
                new Vector2(slot.Max.X + bleed, slot.Max.Y + bleed)),
            RailSide.Left => new Rect(new Vector2(slot.Min.X - bleed, slot.Min.Y - bleed),
                new Vector2(slot.Max.X, slot.Max.Y + bleed)),
            RailSide.Top => new Rect(new Vector2(slot.Min.X - bleed, slot.Min.Y - bleed),
                new Vector2(slot.Max.X + bleed, slot.Max.Y)),
            _ => new Rect(new Vector2(slot.Min.X - bleed, slot.Min.Y),
                new Vector2(slot.Max.X + bleed, slot.Max.Y + bleed)),
        };
        drawList.PushClipRect(clip.Min, clip.Max, true);
        Squircle.Stroke(drawList, cap.Min, cap.Max, rounding, color, 1f * scale);
        drawList.PopClipRect();
    }
}
