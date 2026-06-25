using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games;

internal static class GameCommon
{
    public static readonly Vector4 SnakeHead = Styling.AccentMint;
    public static readonly Vector4 SnakeBody = Styling.AccentMintSoft;
    public static readonly Vector4 FoodColor = Styling.AccentPink;
    public static readonly Vector4 GridCell = new(0.10f, 0.12f, 0.15f, 1.00f);
    public static readonly Vector4 GridLine = new(1f, 1f, 1f, 0.04f);
    public static readonly Vector4 CardFaceDown = new(0.13f, 0.15f, 0.19f, 1.00f);
    public static readonly Vector4 CardFaceDownHover = new(0.18f, 0.21f, 0.26f, 1.00f);
    public static readonly Vector4 CardFaceUp = new(0.10f, 0.11f, 0.14f, 1.00f);
    public static readonly Vector4 CardMatched = new(0.08f, 0.10f, 0.12f, 0.70f);
    public static readonly Vector4 OverlayBg = new(0f, 0f, 0f, 0.55f);
    public static readonly Vector4 ButtonRest = new(0.20f, 0.22f, 0.27f, 0.90f);
    public static readonly Vector4 ButtonHover = new(0.28f, 0.31f, 0.37f, 0.95f);
    public static readonly Vector4 ScoreBg = new(0.08f, 0.09f, 0.12f, 0.80f);

    public static readonly string[] MatchSymbols = { "♥", "★", "◆", "●", "▲", "■", "✦", "♪" };

    public static readonly Vector4[] MatchColors =
    {
        Styling.AccentPink,
        Styling.AccentAmber,
        Styling.AccentMint,
        Styling.AccentBlue,
        new(0.75f, 0.50f, 0.95f, 1f),
        Styling.AccentRose,
        new(0.45f, 0.80f, 0.70f, 1f),
        new(0.90f, 0.55f, 0.35f, 1f),
    };

    private static readonly Dictionary<int, string> NumberLabels = new();

    public static string Label(int value)
    {
        if (NumberLabels.TryGetValue(value, out var label))
        {
            return label;
        }

        label = value.ToString();
        NumberLabels[value] = label;
        return label;
    }

    public static void FillRect(Vector2 min, Vector2 max, Vector4 color, float rounding)
    {
        ImGui.GetWindowDrawList().AddRectFilled(min, max, ImGui.GetColorU32(color), rounding);
    }

    public static void DrawRect(Vector2 min, Vector2 max, Vector4 color, float rounding, float thickness = 1f)
    {
        ImGui.GetWindowDrawList().AddRect(min, max, ImGui.GetColorU32(color), rounding, ImDrawFlags.RoundCornersAll, thickness);
    }

    public static bool HitTest(Vector2 min, Vector2 max)
    {
        return ImGui.IsMouseHoveringRect(min, max);
    }

    public static bool WasClicked(Vector2 min, Vector2 max)
    {
        return HitTest(min, max) && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public static Rect LayoutBelowHeader(in Rect content)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var headerHeight = 42f * scale;
        return new Rect(new Vector2(content.Min.X, content.Min.Y + headerHeight), content.Max);
    }

    public static Rect LayoutGameGrid(Rect body, int columns, int rows, float gapFraction = 0.08f)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var pad = 16f * scale;
        var availableWidth = body.Width - pad * 2f;
        var availableHeight = body.Height - pad * 2f;

        var cellFromWidth = availableWidth / columns;
        var cellFromHeight = availableHeight / rows;
        var cellSize = MathF.Min(cellFromWidth, cellFromHeight);

        var gap = cellSize * gapFraction;
        var innerCell = cellSize - gap;
        var gridWidth = columns * cellSize - gap;
        var gridHeight = rows * cellSize - gap;

        var gridMin = new Vector2(body.Center.X - gridWidth * 0.5f, body.Center.Y - gridHeight * 0.5f - 8f * scale);
        return new Rect(gridMin, new Vector2(gridMin.X + gridWidth, gridMin.Y + gridHeight));
    }

    public static (Vector2 min, Vector2 max) CellBounds(Rect grid, int column, int row, int columns, int rows, float gapFraction = 0.08f)
    {
        var cellSize = grid.Width / columns;
        var gap = cellSize * gapFraction;
        var halfGap = gap * 0.5f;
        var min = new Vector2(grid.Min.X + column * cellSize + halfGap, grid.Min.Y + row * cellSize + halfGap);
        var max = new Vector2(min.X + cellSize - gap, min.Y + cellSize - gap);
        return (min, max);
    }

    public static bool DrawActionButton(Vector2 center, Vector2 size, string label, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var half = size * 0.5f;
        var min = center - half;
        var max = center + half;
        var rounding = 10f * scale;

        var hovered = HitTest(min, max);
        var bg = hovered ? ButtonHover : ButtonRest;

        FillRect(min, max, bg, rounding);
        DrawRect(min, max, Styling.BorderDim, rounding, 1f);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.DrawCentered(center, label, theme.TextStrong, 1f);

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public static void DrawScorePill(Vector2 center, string label, int value, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var labelSize = Typography.Measure(label, 0.75f);
        var valueText = Label(value);
        var valueSize = Typography.Measure(valueText, 1.3f);

        var pillWidth = MathF.Max(valueSize.X, labelSize.X) + 28f * scale;
        var pillHeight = 48f * scale;

        var min = new Vector2(center.X - pillWidth * 0.5f, center.Y - pillHeight * 0.5f);
        var max = new Vector2(center.X + pillWidth * 0.5f, center.Y + pillHeight * 0.5f);

        FillRect(min, max, ScoreBg, 12f * scale);
        DrawRect(min, max, Styling.BorderDim, 12f * scale, 1f);

        Typography.DrawCentered(new Vector2(center.X, center.Y - 6f * scale), valueText, theme.Accent, 1.3f, FontWeight.Bold);
        Typography.DrawCentered(new Vector2(center.X, center.Y + 12f * scale), label, theme.TextMuted, 0.75f);
    }

    public static bool DrawGameOverOverlay(Rect area, PhoneTheme theme, int score, string scoreLabel)
    {
        FillRect(area.Min, area.Max, OverlayBg, 0f);

        var scale = ImGuiHelpers.GlobalScale;
        var center = area.Center;

        Typography.DrawCentered(new Vector2(center.X, center.Y - 60f * scale), "Game Over", theme.TextStrong, 1.6f, FontWeight.Bold);
        Typography.DrawCentered(new Vector2(center.X, center.Y - 30f * scale), $"{scoreLabel}: {score}", theme.TextMuted, 1.1f);

        var buttonSize = new Vector2(100f * scale, 36f * scale);
        return DrawActionButton(new Vector2(center.X, center.Y + 20f * scale), buttonSize, "Play Again", theme);
    }

    public static bool DrawWinOverlay(Rect area, PhoneTheme theme, int attempts, int elapsedSeconds)
    {
        FillRect(area.Min, area.Max, OverlayBg, 0f);

        var scale = ImGuiHelpers.GlobalScale;
        var center = area.Center;

        Typography.DrawCentered(new Vector2(center.X, center.Y - 70f * scale), "You Win!", theme.Accent, 1.6f, FontWeight.Bold);

        var minutes = elapsedSeconds / 60;
        var seconds = elapsedSeconds % 60;
        var timeText = $"{minutes}:{seconds:D2}";
        Typography.DrawCentered(new Vector2(center.X, center.Y - 38f * scale), $"{attempts} attempts  ·  {timeText}", theme.TextMuted, 1f);

        var buttonSize = new Vector2(100f * scale, 36f * scale);
        return DrawActionButton(new Vector2(center.X, center.Y + 8f * scale), buttonSize, "Play Again", theme);
    }
}
