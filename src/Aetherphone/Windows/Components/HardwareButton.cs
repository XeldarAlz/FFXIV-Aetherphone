using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal enum RailSide
{
    Left,
    Right,
}

internal static class HardwareButton
{
    private const float PressTravel = 1.5f;

    public static void Draw(ImDrawListPtr drawList, Rect bounds, PhoneTheme theme, RailSide side, bool hovered,
        float press, float active)
    {
        var scale = ImGuiHelpers.GlobalScale;
        Boss(drawList, bounds, theme, scale);

        var travel = press * PressTravel * scale;
        var shift = side == RailSide.Right ? -travel : travel;
        var min = bounds.Min + new Vector2(shift, 0f);
        var max = bounds.Max + new Vector2(shift, 0f);
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

    private static void Boss(ImDrawListPtr drawList, Rect bounds, PhoneTheme theme, float scale)
    {
        var pad = 2.4f * scale;
        var min = new Vector2(bounds.Min.X, bounds.Min.Y - pad);
        var max = new Vector2(bounds.Max.X, bounds.Max.Y + pad);
        var rounding = MathF.Min(max.X - min.X, max.Y - min.Y) * 0.5f;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Palette.Lighten(theme.BezelOuter, 0.06f)));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f)), 1f * scale);
    }

    private static void Face(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, RailSide side,
        Vector4 crown, Vector4 flank)
    {
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

    private static void CrownSpecular(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, RailSide side,
        bool hovered, float press, float scale)
    {
        var alpha = (hovered ? 0.55f : 0.40f) * (1f - press * 0.6f);
        if (alpha <= 0.01f)
        {
            return;
        }

        var color = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));
        var x = side == RailSide.Right ? max.X - 1.6f * scale : min.X + 1.6f * scale;
        drawList.AddLine(new Vector2(x, min.Y + rounding), new Vector2(x, max.Y - rounding), color, 1.3f * scale);
    }

    private static void RecessSeam(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, RailSide side,
        float press, float active, PhoneTheme theme, float scale)
    {
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
}
