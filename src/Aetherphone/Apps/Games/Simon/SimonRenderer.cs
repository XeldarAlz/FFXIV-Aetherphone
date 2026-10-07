using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Simon;

internal static class SimonRenderer
{
    public const float GapFraction = 0.08f;
    public const float HubRadiusFraction = 0.42f;
    private const float PadRadiusFraction = 0.16f;
    private const float BloomRadiusFraction = 0.38f;
    private const int PadColumns = 2;
    private static readonly Vector4[] PadColors =
    {
        new(0.46f, 0.86f, 0.66f, 1f), new(0.95f, 0.45f, 0.50f, 1f), new(0.95f, 0.74f, 0.34f, 1f),
        new(0.40f, 0.68f, 0.98f, 1f),
    };
    private static readonly Vector4 HubShadow = new(0f, 0f, 0f, 0.35f);

    public static Vector4 ColorOf(int pad) => PadColors[pad];

    public static Rect PadRect(in GameGrid grid, int pad) => grid.Cell(pad % PadColumns, pad / PadColumns);

    public static void DrawBoard(ImDrawListPtr drawList, in GameGrid grid, float[] lit, int pressed, float entrance,
        float focusDim, Vector4 accent, StageInk ink, float scale)
    {
        BoardPlate.Draw(drawList, BoardPlate.Around(grid.Bounds, scale), BoardPlate.Radius * scale, scale, accent, ink);
        var radius = grid.Pitch * PadRadiusFraction;
        for (var pad = 0; pad < SimonBoard.PadCount; pad++)
        {
            var lift = StageCell.Lift(GameJuice.Stagger(entrance, pad, SimonBoard.PadCount)) * scale;
            var rect = PadRect(grid, pad).Translate(new Vector2(0f, -lift));
            DrawPad(drawList, rect, PadColors[pad], lit[pad], pad == pressed, radius, scale, focusDim);
        }
    }

    public static void DrawHub(ImDrawListPtr drawList, Vector2 center, float radius, string value, string label,
        Vector4 color, PhoneTheme theme, float scale, bool inputPulse)
    {
        if (inputPulse)
        {
            ProgressRing.Glow(center, radius * 1.05f, color, 0.35f + 0.35f * Pulse.Wave(Pulse.Breath));
        }

        drawList.AddCircleFilled(center + new Vector2(0f, 3f * scale), radius + 5f * scale,
            ImGui.GetColorU32(HubShadow), 48);
        drawList.AddCircleFilled(center, radius + 4f * scale, ImGui.GetColorU32(GamePalette.Board), 48);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(GamePalette.Cell), 48);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(color with { W = 0.6f }), 48, 1.6f * scale);
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y - radius * 0.16f), value, StageInks.Strong,
            TextStyles.Title1.Scale, TextStyles.Title1.Weight);
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + radius * 0.42f), label, color,
            TextStyles.Caption2.Scale, TextStyles.Caption2.Weight);
    }

    private static void DrawPad(ImDrawListPtr drawList, Rect rect, Vector4 color, float lit, bool pressed,
        float radius, float scale, float focusDim)
    {
        var dim = GamePalette.Darken(color, 0.58f + focusDim * (1f - lit));
        var bright = GamePalette.Lighten(color, 0.12f);
        var fill = Vector4.Lerp(dim, bright, lit);
        if (lit > 0.01f)
        {
            ProgressRing.Glow(rect.Center, rect.Width * 0.5f, color, 0.7f * lit);
        }

        StageCell.Draw(drawList, rect, fill, pressed ? CellDepth.Pressed : CellDepth.Raised, radius, scale);
        if (lit > 0.01f)
        {
            drawList.AddCircleFilled(rect.Center, rect.Width * BloomRadiusFraction,
                ImGui.GetColorU32(GamePalette.Lighten(color, 0.4f) with { W = 0.22f * lit }), 40);
        }

        Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(GamePalette.Lighten(color, 0.3f) with { W = 0.35f + 0.5f * lit }), 1.4f * scale);
    }
}
