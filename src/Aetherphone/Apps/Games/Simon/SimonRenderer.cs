using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Animation;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Simon;

internal sealed class SimonRenderer
{
    private static readonly Vector4[] PadColors =
    {
        new(0.46f, 0.86f, 0.66f, 1f), new(0.95f, 0.45f, 0.50f, 1f), new(0.95f, 0.74f, 0.34f, 1f),
        new(0.40f, 0.68f, 0.98f, 1f),
    };

    public static Vector4 ColorOf(int pad) => PadColors[pad];

    public static Rect PadRect(GameGrid grid, int pad)
    {
        return grid.Cell(pad % 2, pad / 2);
    }

    public void Draw(GameGrid grid, float[] lit, string hubValue, string hubLabel, Vector4 hubColor, PhoneTheme theme,
        float scale, float focusDim, bool inputPulse)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = grid.Pitch * 0.16f;
        for (var pad = 0; pad < SimonBoard.PadCount; pad++)
        {
            DrawPad(drawList, PadRect(grid, pad), PadColors[pad], lit[pad], rounding, scale, focusDim);
        }

        DrawHub(drawList, grid.Center, grid.Pitch * 0.42f, hubValue, hubLabel, hubColor, theme, scale, inputPulse);
    }

    private void DrawPad(ImDrawListPtr drawList, Rect rect, Vector4 color, float lit, float rounding, float scale,
        float focusDim)
    {
        var dim = GamePalette.Darken(color, 0.58f + focusDim * (1f - lit));
        var bright = GamePalette.Lighten(color, 0.12f);
        var fill = Vector4.Lerp(dim, bright, lit);
        var center = rect.Center;
        var half = rect.Size * 0.5f * (1f + 0.04f * lit);
        var min = center - half;
        var max = center + half;
        if (lit > 0.01f)
        {
            ProgressRing.Glow(center, rect.Width * 0.5f, color, 0.6f * lit);
        }

        Squircle.FillVerticalGradient(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(fill, 0.08f)), ImGui.GetColorU32(GamePalette.Darken(fill, 0.14f)));
        if (lit > 0.01f)
        {
            drawList.AddCircleFilled(center, half.X * 0.75f,
                ImGui.GetColorU32(GamePalette.Lighten(color, 0.4f) with { W = 0.22f * lit }));
        }

        Squircle.Fill(drawList, min, new Vector2(max.X, min.Y + half.Y), rounding,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f + 0.18f * lit)));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(color, 0.3f) with { W = 0.35f + 0.5f * lit }), 1.4f * scale);
    }

    private void DrawHub(ImDrawListPtr drawList, Vector2 center, float radius, string value, string label,
        Vector4 color, PhoneTheme theme, float scale, bool inputPulse)
    {
        if (inputPulse)
        {
            ProgressRing.Glow(center, radius * 1.05f, color, 0.35f + 0.35f * Pulse.Wave(Pulse.Breath));
        }

        drawList.AddCircleFilled(center + new Vector2(0f, 3f * scale), radius + 5f * scale,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)), 48);
        drawList.AddCircleFilled(center, radius + 4f * scale, ImGui.GetColorU32(GamePalette.Board), 48);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(GamePalette.Cell), 48);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(color with { W = 0.6f }), 48, 1.6f * scale);
        Typography.DrawCentered(new Vector2(center.X, center.Y - radius * 0.16f), value, theme.TextStrong,
            TextStyles.Title1);
        Typography.DrawCentered(new Vector2(center.X, center.Y + radius * 0.42f), Loc.Culture.TextInfo.ToUpper(label),
            color,
            TextStyles.Caption2);
    }
}
