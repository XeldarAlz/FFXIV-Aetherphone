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

internal static class HardwareButton
{
    private const float PressTravel = 1.5f;

    public static void Draw(ImDrawListPtr drawList, Rect bounds, PhoneTheme theme, RailSide side, bool hovered,
        float press, float active)
    {
        var scale = UiScale.Current;
        Boss(drawList, bounds, theme, side, scale);

        var travel = press * PressTravel * scale;
        var shift = side switch
        {
            RailSide.Right => new Vector2(-travel, 0f),
            RailSide.Top => new Vector2(0f, travel),
            RailSide.Bottom => new Vector2(0f, -travel),
            _ => new Vector2(travel, 0f),
        };
        var min = bounds.Min + shift;
        var max = bounds.Max + shift;
        var rounding = MathF.Min(max.X - min.X, max.Y - min.Y) * 0.5f;

        var metal = theme.RailMetal;
        var crown = Palette.Lighten(metal, 0.30f - press * 0.20f);
        var flank = Palette.Darken(metal, 0.28f + press * 0.12f);

        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(metal));
        Face(drawList, min, max, rounding, side, crown, flank);
        CrownSpecular(drawList, min, max, rounding, side, hovered, press, scale);
        RecessSeam(drawList, min, max, rounding, side, press, active, theme, scale);
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Palette.Darken(metal, 0.60f)), 1f * scale);
    }

    private static void Boss(ImDrawListPtr drawList, Rect bounds, PhoneTheme theme, RailSide side, float scale)
    {
        var pad = 2.4f * scale;
        var horizontal = side is RailSide.Top or RailSide.Bottom;
        var min = horizontal
            ? new Vector2(bounds.Min.X - pad, bounds.Min.Y)
            : new Vector2(bounds.Min.X, bounds.Min.Y - pad);
        var max = horizontal
            ? new Vector2(bounds.Max.X + pad, bounds.Max.Y)
            : new Vector2(bounds.Max.X, bounds.Max.Y + pad);
        var rounding = MathF.Min(max.X - min.X, max.Y - min.Y) * 0.5f;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Palette.Lighten(theme.Glass, 0.06f)));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f)), 1f * scale);
    }

    private static void Face(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, RailSide side,
        Vector4 crown, Vector4 flank)
    {
        if (side is RailSide.Top or RailSide.Bottom)
        {
            FaceHorizontal(drawList, min, max, rounding, side, crown, flank);
            return;
        }

        var top = min.Y + rounding;
        var bottom = max.Y - rounding;
        if (bottom <= top)
        {
            return;
        }

        var crownTop = ImGui.GetColorU32(Palette.Lighten(crown, 0.10f));
        var crownBottom = ImGui.GetColorU32(Palette.Darken(crown, 0.12f));
        var flankTop = ImGui.GetColorU32(Palette.Lighten(flank, 0.08f));
        var flankBottom = ImGui.GetColorU32(Palette.Darken(flank, 0.10f));
        var faceMin = new Vector2(min.X, top);
        var faceMax = new Vector2(max.X, bottom);
        if (side == RailSide.Right)
        {
            drawList.AddRectFilledMultiColor(faceMin, faceMax, flankTop, crownTop, crownBottom, flankBottom);
            return;
        }

        drawList.AddRectFilledMultiColor(faceMin, faceMax, crownTop, flankTop, flankBottom, crownBottom);
    }

    private static void FaceHorizontal(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, RailSide side,
        Vector4 crown, Vector4 flank)
    {
        var left = min.X + rounding;
        var right = max.X - rounding;
        if (right <= left)
        {
            return;
        }

        var crownTop = ImGui.GetColorU32(Palette.Lighten(crown, 0.10f));
        var crownBottom = ImGui.GetColorU32(Palette.Darken(crown, 0.12f));
        var flankTop = ImGui.GetColorU32(Palette.Lighten(flank, 0.08f));
        var flankBottom = ImGui.GetColorU32(Palette.Darken(flank, 0.10f));
        var faceMin = new Vector2(left, min.Y);
        var faceMax = new Vector2(right, max.Y);
        if (side == RailSide.Top)
        {
            drawList.AddRectFilledMultiColor(faceMin, faceMax, crownTop, crownTop, flankBottom, flankBottom);
            return;
        }

        drawList.AddRectFilledMultiColor(faceMin, faceMax, flankTop, flankTop, crownBottom, crownBottom);
    }

    private static void CrownSpecular(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, RailSide side,
        bool hovered, float press, float scale)
    {
        var alpha = (hovered ? 0.55f : 0.40f) * (1f - press * 0.6f);
        if (alpha <= 0.01f)
        {
            return;
        }

        var color = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));
        if (side is RailSide.Top or RailSide.Bottom)
        {
            var y = side == RailSide.Top ? min.Y + 1.6f * scale : max.Y - 1.6f * scale;
            drawList.AddLine(new Vector2(min.X + rounding, y), new Vector2(max.X - rounding, y), color,
                1.3f * scale);
            return;
        }

        var x = side == RailSide.Right ? max.X - 1.6f * scale : min.X + 1.6f * scale;
        drawList.AddLine(new Vector2(x, min.Y + rounding), new Vector2(x, max.Y - rounding), color, 1.3f * scale);
    }

    private static void RecessSeam(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, RailSide side,
        float press, float active, PhoneTheme theme, float scale)
    {
        if (side is RailSide.Top or RailSide.Bottom)
        {
            RecessSeamHorizontal(drawList, min, max, rounding, side, press, active, theme, scale);
            return;
        }

        var innerX = side == RailSide.Right ? min.X + 1f * scale : max.X - 1f * scale;
        var shadow = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.42f + press * 0.30f));
        drawList.AddLine(new Vector2(innerX, min.Y + rounding), new Vector2(innerX, max.Y - rounding), shadow,
            1.4f * scale);
        if (active <= 0.01f)
        {
            return;
        }

        var accent = ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, active));
        var tintX = side == RailSide.Right ? min.X + 2.6f * scale : max.X - 2.6f * scale;
        drawList.AddLine(new Vector2(tintX, min.Y + rounding), new Vector2(tintX, max.Y - rounding), accent,
            1.6f * scale);
    }

    private static void RecessSeamHorizontal(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding,
        RailSide side, float press, float active, PhoneTheme theme, float scale)
    {
        var innerY = side == RailSide.Top ? max.Y - 1f * scale : min.Y + 1f * scale;
        var shadow = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.42f + press * 0.30f));
        drawList.AddLine(new Vector2(min.X + rounding, innerY), new Vector2(max.X - rounding, innerY), shadow,
            1.4f * scale);
        if (active <= 0.01f)
        {
            return;
        }

        var accent = ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, active));
        var tintY = side == RailSide.Top ? max.Y - 2.6f * scale : min.Y + 2.6f * scale;
        drawList.AddLine(new Vector2(min.X + rounding, tintY), new Vector2(max.X - rounding, tintY), accent,
            1.6f * scale);
    }
}
