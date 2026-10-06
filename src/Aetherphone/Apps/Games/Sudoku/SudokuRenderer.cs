using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Sudoku;

internal readonly struct SudokuView
{
    public readonly int Selected;
    public readonly int Hovered;
    public readonly float Entrance;
    public readonly float SolveWave;
    public readonly Vector2 Shake;

    public SudokuView(int selected, int hovered, float entrance, float solveWave, Vector2 shake)
    {
        Selected = selected;
        Hovered = hovered;
        Entrance = entrance;
        SolveWave = solveWave;
        Shake = shake;
    }
}

internal sealed class SudokuRenderer
{
    public const float CellGap = 0.10f;
    public const float ShakeSeconds = 0.35f;
    private const int WaveDiagonals = SudokuBoard.Size * 2 - 1;
    private const float WaveOverlap = 0.45f;
    private const float WaveLift = 6f;
    private const float WaveTint = 0.6f;
    private const float CellShakeAmplitude = 3f;
    private const float CellShakeFrequency = 55f;
    private const float DigitUnit = 26f;
    private const float NoteScaleFactor = 0.44f;
    private const float PopScale = 0.32f;
    private const float RadiusFraction = 0.18f;
    private const float SelectedMix = 0.32f;
    private const float SelectedStroke = 2f;
    private const float PressedInset = 0.04f;
    private const float PeerWashAlpha = 0.10f;
    private const float SameDigitAlpha = 0.28f;
    private const float ConflictAlpha = 0.22f;
    private const float HoverAlpha = 0.06f;
    private const float NoteAlpha = 0.55f;
    private const float BoxLineAlpha = 0.30f;
    private const float BoxLineWidth = 2f;
    private static readonly Vector4 GivenFill = new(0.88f, 0.86f, 0.82f, 1f);
    private static readonly Vector4 EntryFill = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 EmptyFill = new(0.975f, 0.965f, 0.945f, 0.92f);
    private static readonly Vector4 EmptyAltFill = new(0.935f, 0.925f, 0.900f, 0.92f);
    private static readonly Vector4 RevealedInk = new(0.16f, 0.52f, 0.38f, 1f);

    public void Draw(ImDrawListPtr drawList, SudokuBoard board, in GameGrid grid, in SudokuView view, float[] pop,
        float[] shake, Vector4 accent, StageInk ink, PhoneTheme theme, float scale)
    {
        var plate = BoardPlate.Around(grid.Bounds, scale).Translate(view.Shake);
        BoardPlate.Draw(drawList, plate, BoardPlate.Radius * scale, scale, accent, ink);
        var radius = grid.Pitch * RadiusFraction;
        var selectedDigit = view.Selected >= 0 ? board.Value(view.Selected) : (byte)0;
        var digitScale = Math.Clamp(grid.Pitch / (DigitUnit * scale), 0.7f, 1.6f);
        var noteScale = digitScale * NoteScaleFactor;
        var strongInk = ink == StageInk.Dark ? GamePalette.InkDark : GamePalette.InkLight;
        for (var cell = 0; cell < SudokuBoard.CellCount; cell++)
        {
            DrawCell(drawList, board, grid, view, cell, pop[cell], shake[cell], selectedDigit, accent, strongInk, theme,
                radius, digitScale, noteScale, scale);
        }

        DrawBoxLines(drawList, grid, view.Shake, strongInk, scale);
    }

    private static void DrawCell(ImDrawListPtr drawList, SudokuBoard board, in GameGrid grid, in SudokuView view,
        int cell, float pop, float shake, byte selectedDigit, Vector4 accent, Vector4 strongInk, PhoneTheme theme,
        float radius, float digitScale, float noteScale, float scale)
    {
        var column = SudokuBoard.ColumnOf(cell);
        var row = SudokuBoard.RowOf(cell);
        var diagonal = row + column;
        var entrance = GameJuice.Stagger(view.Entrance, diagonal, WaveDiagonals);
        if (entrance <= 0f)
        {
            return;
        }

        var wave = view.SolveWave > 0f
            ? MathF.Sin(GameJuice.Stagger(view.SolveWave, diagonal, WaveDiagonals, WaveOverlap) * MathF.PI)
            : 0f;
        var lift = StageCell.Lift(entrance) * scale + wave * WaveLift * scale;
        var jitter = shake > 0f
            ? MathF.Sin(shake * CellShakeFrequency) * CellShakeAmplitude * scale * (shake / ShakeSeconds)
            : 0f;
        var rect = grid.Cell(column, row).Translate(view.Shake + new Vector2(jitter, -lift));
        var value = board.Value(cell);
        var given = board.IsGiven(cell);
        var selected = cell == view.Selected;
        var fill = given ? GivenFill : value != 0 ? EntryFill : SudokuBoard.BoxOf(cell) % 2 == 0 ? EmptyFill : EmptyAltFill;
        if (selected)
        {
            fill = Vector4.Lerp(fill, accent, SelectedMix);
        }

        if (wave > 0f)
        {
            fill = Vector4.Lerp(fill, accent, wave * WaveTint);
        }

        fill = fill with { W = fill.W * entrance };
        var depth = selected ? CellDepth.Pressed : given ? CellDepth.Sunken : value != 0 ? CellDepth.Raised : CellDepth.Flat;
        StageCell.Draw(drawList, rect, fill, depth, radius, scale);
        if (!selected && view.Selected >= 0 && SudokuBoard.ArePeers(cell, view.Selected))
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(accent with { W = PeerWashAlpha }));
        }

        if (!selected && selectedDigit != 0 && value == selectedDigit)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(accent with { W = SameDigitAlpha }));
        }

        if (board.IsConflicting(cell) || board.IsWrong(cell))
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, radius,
                ImGui.GetColorU32(theme.Danger with { W = ConflictAlpha }));
        }

        if (cell == view.Hovered && !selected)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(strongInk with { W = HoverAlpha }));
        }

        if (selected)
        {
            var inset = rect.Size * PressedInset * 0.5f;
            Squircle.Stroke(drawList, rect.Min + inset, rect.Max - inset, radius,
                ImGui.GetColorU32(accent with { W = 0.9f }), SelectedStroke * scale);
        }

        var center = rect.Center;
        if (value == 0)
        {
            DrawNotes(drawList, board.Notes(cell), center, strongInk with { W = NoteAlpha * entrance }, noteScale,
                grid.Pitch);
            return;
        }

        var color = DigitColor(board, cell, strongInk, accent, theme) with { W = entrance };
        var scalePop = 1f + PopScale * Easing.EaseOutCubic(pop);
        Typography.DrawCentered(drawList, center, GameNumber.Label(value), color, digitScale * scalePop,
            given ? FontWeight.Bold : FontWeight.SemiBold);
    }

    private static Vector4 DigitColor(SudokuBoard board, int cell, Vector4 strongInk, Vector4 accent, PhoneTheme theme)
    {
        if (board.IsGiven(cell))
        {
            return strongInk;
        }

        if (board.IsWrong(cell))
        {
            return theme.Danger;
        }

        if (board.IsRevealed(cell))
        {
            return RevealedInk;
        }

        return GamePalette.Darken(accent, 0.15f);
    }

    private static void DrawNotes(ImDrawListPtr drawList, ushort notes, Vector2 center, Vector4 ink, float noteScale,
        float pitch)
    {
        if (notes == 0)
        {
            return;
        }

        var step = pitch / SudokuBoard.BoxSize;
        var corner = center - new Vector2(step, step);
        for (var digit = 1; digit <= SudokuBoard.Size; digit++)
        {
            if ((notes & (1 << digit)) == 0)
            {
                continue;
            }

            var slot = digit - 1;
            var noteCenter = corner + new Vector2(slot % SudokuBoard.BoxSize * step + step * 0.5f,
                slot / SudokuBoard.BoxSize * step + step * 0.5f);
            Typography.DrawCentered(drawList, noteCenter, GameNumber.Label(digit), ink, noteScale, FontWeight.Medium);
        }
    }

    private static void DrawBoxLines(ImDrawListPtr drawList, in GameGrid grid, Vector2 shake, Vector4 strongInk,
        float scale)
    {
        var color = ImGui.GetColorU32(strongInk with { W = BoxLineAlpha });
        var thickness = BoxLineWidth * scale;
        var min = grid.Origin + shake;
        var max = min + new Vector2(grid.Width, grid.Height);
        for (var line = SudokuBoard.BoxSize; line < SudokuBoard.Size; line += SudokuBoard.BoxSize)
        {
            var x = min.X + line * grid.Pitch;
            drawList.AddLine(new Vector2(x, min.Y), new Vector2(x, max.Y), color, thickness);
            var y = min.Y + line * grid.Pitch;
            drawList.AddLine(new Vector2(min.X, y), new Vector2(max.X, y), color, thickness);
        }
    }
}
