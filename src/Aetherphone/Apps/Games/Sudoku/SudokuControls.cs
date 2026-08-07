using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Sudoku;

internal enum SudokuTool : byte
{
    None,
    Undo,
    Erase,
    Notes,
    Hint,
}

internal static class SudokuControls
{
    public const float ToolsHeight = 54f;
    public const float PadHeight = 52f;
    private const float KeyGap = 4f;

    public static SudokuTool DrawTools(Rect row, PhoneTheme theme, Vector4 accent, bool notesMode, bool canUndo,
        int hintsLeft, float scale)
    {
        var slotWidth = row.Width / 4f;
        var result = SudokuTool.None;
        if (ToolButton(row, 0, slotWidth, FontAwesomeIcon.Undo, Loc.T(L.Games.Undo), null, canUndo, false, theme,
                accent, scale))
        {
            result = SudokuTool.Undo;
        }

        if (ToolButton(row, 1, slotWidth, FontAwesomeIcon.Eraser, Loc.T(L.Games.Erase), null, true, false, theme,
                accent, scale))
        {
            result = SudokuTool.Erase;
        }

        if (ToolButton(row, 2, slotWidth, FontAwesomeIcon.PencilAlt, Loc.T(L.Games.Notes), null, true, notesMode,
                theme, accent, scale))
        {
            result = SudokuTool.Notes;
        }

        if (ToolButton(row, 3, slotWidth, FontAwesomeIcon.Lightbulb, Loc.T(L.Games.Hint),
                GameNumber.Label(hintsLeft), hintsLeft > 0, false, theme, accent, scale))
        {
            result = SudokuTool.Hint;
        }

        return result;
    }

    public static int DrawPad(SudokuBoard board, Rect row, PhoneTheme theme, Vector4 accent, bool notesMode,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var gap = KeyGap * scale;
        var keyWidth = (row.Width - gap * (SudokuBoard.Size - 1)) / SudokuBoard.Size;
        var rounding = Metrics.Radius.Sm * scale;
        var digitScale = MathF.Max(0.85f, MathF.Min(1.5f, keyWidth / (22f * scale)));
        var pressed = 0;
        for (var digit = 1; digit <= SudokuBoard.Size; digit++)
        {
            var min = new Vector2(row.Min.X + (digit - 1) * (keyWidth + gap), row.Min.Y);
            var max = new Vector2(min.X + keyWidth, row.Max.Y);
            var remaining = SudokuBoard.Size - board.PlacedCount(digit);
            var exhausted = remaining <= 0 && !notesMode;
            var hovered = !exhausted && UiInteract.Hover(min, max);
            var fill = exhausted
                ? GamePalette.Cell with { W = 0.45f }
                : hovered
                    ? GamePalette.Lighten(accent, 0.10f) with { W = 0.36f }
                    : GamePalette.CellHover;
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(fill));
            Squircle.Stroke(drawList, min, max, rounding,
                ImGui.GetColorU32(accent with { W = notesMode ? 0.45f : 0.22f }), 1f * scale);
            var digitColor = exhausted
                ? theme.TextMuted with { W = 0.45f }
                : notesMode
                    ? GamePalette.Lighten(accent, 0.42f)
                    : theme.TextStrong;
            var center = (min + max) * 0.5f;
            Typography.DrawCentered(new Vector2(center.X, center.Y - 5f * scale), GameNumber.Label(digit), digitColor,
                digitScale, FontWeight.SemiBold);
            if (!exhausted && remaining > 0)
            {
                Typography.DrawCentered(new Vector2(center.X, center.Y + 13f * scale), GameNumber.Label(remaining),
                    theme.TextMuted, TextStyles.Caption2);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                pressed = digit;
            }
        }

        return pressed;
    }

    private static bool ToolButton(Rect row, int slot, float slotWidth, FontAwesomeIcon icon, string label,
        string? badge, bool enabled, bool active, PhoneTheme theme, Vector4 accent, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var centerX = row.Min.X + (slot + 0.5f) * slotWidth;
        var radius = 17f * scale;
        var center = new Vector2(centerX, row.Min.Y + radius + 2f * scale);
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = enabled && UiInteract.Hover(min, max);
        Material.Frosted(drawList, min, max, radius, scale, enabled ? 1f : 0.6f);
        if (active)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(accent with { W = 0.38f }));
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.85f }), 1.5f * scale);
        }
        else if (hovered)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(accent with { W = 0.16f }));
        }

        var ink = !enabled ? theme.TextMuted with { W = 0.45f } :
            active ? GamePalette.Lighten(accent, 0.45f) : theme.TextStrong;
        ProgressRing.CenterIcon(drawList, center, icon, ink, radius * 0.82f);
        if (!string.IsNullOrEmpty(badge))
        {
            DrawBadge(drawList, new Vector2(max.X - 2f * scale, min.Y + 2f * scale), badge!, accent, enabled, scale);
        }

        var caption = Typography.FitText(label, slotWidth - 4f * scale, TextStyles.Caption2);
        Typography.DrawCentered(new Vector2(centerX, max.Y + 9f * scale), caption,
            enabled ? theme.TextMuted : theme.TextMuted with { W = 0.45f }, TextStyles.Caption2);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private static void DrawBadge(ImDrawListPtr drawList, Vector2 topRight, string text, Vector4 accent, bool enabled,
        float scale)
    {
        var radius = 7f * scale;
        var center = new Vector2(topRight.X, topRight.Y);
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(enabled ? accent : accent with { W = 0.35f }));
        Typography.DrawCentered(center, text, GamePalette.InkOn(accent), TextStyles.Caption2);
    }
}
