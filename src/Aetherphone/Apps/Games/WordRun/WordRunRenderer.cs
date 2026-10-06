using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.WordRun;

internal readonly struct KeyboardPress
{
    public readonly char Letter;
    public readonly bool Enter;
    public readonly bool Backspace;

    public KeyboardPress(char letter, bool enter, bool backspace)
    {
        Letter = letter;
        Enter = enter;
        Backspace = backspace;
    }
}

internal static class WordRunRenderer
{
    public const float FlipStagger = 0.08f;
    public const float FlipDuration = 0.24f;
    public const float RevealSeconds = FlipStagger * (WordRunBoard.WordLength - 1) + FlipDuration;
    public const float ShakeSeconds = 0.4f;
    public const float GapFraction = 0.12f;
    public const float SpeedRowHeight = 18f;
    public static readonly Vector4 CorrectColor = new(0.33f, 0.70f, 0.42f, 1f);
    public static readonly Vector4 PresentColor = new(0.80f, 0.65f, 0.26f, 1f);
    public static readonly Vector4 AbsentColor = new(0.30f, 0.31f, 0.36f, 1f);
    private const float KeyGapFraction = 0.06f;
    private const float WideKeyFactor = 1.5f;
    private const float KeyMaxHeight = 46f;
    private const float KeySidePad = 4f;
    private const float KeyRoundingFraction = 0.22f;
    private const float TileRoundingFraction = 0.14f;
    private const float MinOpen = 0.02f;
    private const float MaxOpen = 1.12f;
    private const float LetterShowOpen = 0.6f;
    private const float LetterHideSquash = 0.35f;
    private const float HeatTint = 0.45f;
    private const float HeatStrokeAlpha = 0.6f;
    private const float SpeedBarHeight = 6f;
    private const float SpeedLabelGap = 8f;
    private const float EmptyAlpha = 0.07f;
    private const float StrokeAlpha = 0.55f;
    private const float ShakeFrequency = 40f;
    private const float ShakeAmplitude = 3f;
    private static readonly Vector4 EntryFill = new(1f, 1f, 1f, 0.92f);
    private static readonly Vector4 KeyBase = new(0.98f, 0.97f, 0.94f, 1f);
    private static readonly Vector4 KeyDim = new(0.92f, 0.91f, 0.88f, 1f);
    private static readonly Vector4 Warm = new(1f, 0.72f, 0.30f, 1f);
    private static readonly Vector4 InkLight = new(0.98f, 0.98f, 1f, 1f);
    private static readonly string[] KeyboardRows = { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
    private static readonly string[] LetterLabels = BuildLetterLabels();
    private static readonly TextStyle TileStyle = TextStyles.Title2;
    private static readonly TextStyle KeyStyle = TextStyles.Headline;
    private static readonly TextStyle SpeedStyle = TextStyles.FootnoteEmphasized;

    private static string[] BuildLetterLabels()
    {
        var labels = new string[WordRunBoard.LetterCount];
        for (var index = 0; index < labels.Length; index++)
        {
            labels[index] = ((char)('A' + index)).ToString();
        }

        return labels;
    }

    public static Vector4 TileColor(WordTile tile)
    {
        switch (tile)
        {
            case WordTile.Correct:
                return CorrectColor;
            case WordTile.Present:
                return PresentColor;
            default:
                return AbsentColor;
        }
    }

    public static GameGrid Grid(Rect area, float verticalBias = 0f) =>
        GameGrid.Centered(area, WordRunBoard.WordLength, WordRunBoard.MaxGuesses, GapFraction, verticalBias);

    public static Rect PlateRect(in GameGrid grid, float scale) => BoardPlate.Around(grid.Bounds, scale);

    public static void DrawBoard(ImDrawListPtr drawList, WordRunBoard board, in GameGrid grid, int revealRow,
        float revealSeconds, float shakeRemaining, Vector4 accent, StageInk ink, float entrance, float scale)
    {
        BoardPlate.Draw(drawList, PlateRect(grid, scale), BoardPlate.Radius * scale, scale, accent, ink);
        var rounding = grid.Pitch * TileRoundingFraction;
        var inkDark = ink == StageInk.Dark ? GamePalette.InkDark : GamePalette.InkLight;
        var shakeOffset = 0f;
        if (shakeRemaining > 0f)
        {
            var elapsed = ShakeSeconds - shakeRemaining;
            shakeOffset = MathF.Sin(elapsed * ShakeFrequency) * ShakeAmplitude * scale * (1f - elapsed / ShakeSeconds);
        }

        var cellCount = WordRunBoard.MaxGuesses * WordRunBoard.WordLength;
        for (var row = 0; row < WordRunBoard.MaxGuesses; row++)
        {
            for (var column = 0; column < WordRunBoard.WordLength; column++)
            {
                var lift = StageCell.Lift(GameJuice.Stagger(entrance, row * WordRunBoard.WordLength + column, cellCount)) * scale;
                var cell = grid.Cell(column, row).Translate(new Vector2(0f, -lift));
                if (row < board.RowCount)
                {
                    var flip = row == revealRow ? (revealSeconds - column * FlipStagger) / FlipDuration : 1f;
                    DrawJudgedTile(drawList, cell, board.Letter(row, column), board.Tile(row, column), rounding, inkDark,
                        accent, flip, scale);
                    continue;
                }

                if (row == board.RowCount && board.Outcome == WordOutcome.Playing)
                {
                    var shaken = cell.Translate(new Vector2(shakeOffset, 0f));
                    DrawEntryTile(drawList, shaken, column < board.EntryLength ? board.EntryLetter(column) : '\0',
                        rounding, inkDark, accent, scale);
                    continue;
                }

                StageCell.Draw(drawList, cell, inkDark with { W = EmptyAlpha }, CellDepth.Sunken, rounding, scale);
            }
        }
    }

    private static void DrawEntryTile(ImDrawListPtr drawList, Rect cell, char letter, float rounding, Vector4 ink,
        Vector4 accent, float scale)
    {
        StageCell.Draw(drawList, cell, EntryFill, CellDepth.Sunken, rounding, scale);
        var strokeAlpha = letter == '\0' ? EmptyAlpha * 2f : StrokeAlpha;
        Squircle.Stroke(drawList, cell.Min, cell.Max, rounding, ImGui.GetColorU32(accent with { W = strokeAlpha }),
            1.2f * scale);
        if (letter == '\0')
        {
            return;
        }

        Typography.DrawCentered(drawList, cell.Center, LetterLabels[letter - 'A'], ink, TileStyle.Scale, TileStyle.Weight);
    }

    private static void DrawJudgedTile(ImDrawListPtr drawList, Rect cell, char letter, WordTile tile, float rounding,
        Vector4 ink, Vector4 accent, float flip, float scale)
    {
        var label = LetterLabels[letter - 'A'];
        if (flip < 0.5f)
        {
            var squash = flip < 0f ? 1f : 1f - flip * 2f;
            var closed = SquashX(cell, squash);
            StageCell.Draw(drawList, closed, EntryFill, CellDepth.Sunken, rounding, scale);
            Squircle.Stroke(drawList, closed.Min, closed.Max, rounding, ImGui.GetColorU32(accent with { W = StrokeAlpha }),
                1.2f * scale);
            if (squash > LetterHideSquash)
            {
                Typography.DrawCentered(drawList, cell.Center, label, ink, TileStyle.Scale * squash, TileStyle.Weight);
            }

            return;
        }

        var open = flip >= 1f ? 1f : Math.Clamp(Easing.EaseOutBack((flip - 0.5f) * 2f), MinOpen, MaxOpen);
        var revealed = SquashX(cell, open);
        StageCell.Draw(drawList, revealed, TileColor(tile), CellDepth.Raised, rounding, scale);
        if (open > LetterShowOpen)
        {
            Typography.DrawCentered(drawList, cell.Center, label, InkLight, TileStyle.Scale * MathF.Min(1f, open),
                TileStyle.Weight);
        }
    }

    private static Rect SquashX(Rect cell, float amount)
    {
        var halfWidth = cell.Width * 0.5f * MathF.Max(MinOpen, amount);
        return new Rect(new Vector2(cell.Center.X - halfWidth, cell.Min.Y), new Vector2(cell.Center.X + halfWidth, cell.Max.Y));
    }

    public static void DrawSpeedBar(ImDrawListPtr drawList, Rect row, float fraction, string bonusLabel, Vector4 accent,
        StageInk ink, float scale)
    {
        var inkDark = ink == StageInk.Dark ? GamePalette.InkDark : GamePalette.InkLight;
        var labelWidth = Typography.Measure(bonusLabel, SpeedStyle).X;
        var barHeight = SpeedBarHeight * scale;
        var barRight = row.Max.X - labelWidth - SpeedLabelGap * scale;
        var barTop = row.Center.Y - barHeight * 0.5f;
        var barMin = new Vector2(row.Min.X, barTop);
        var barMax = new Vector2(MathF.Max(row.Min.X + barHeight, barRight), barTop + barHeight);
        drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(inkDark with { W = 0.10f }), barHeight * 0.5f);
        if (fraction > 0f)
        {
            var color = Vector4.Lerp(Warm, accent, fraction);
            var fillMax = new Vector2(barMin.X + (barMax.X - barMin.X) * fraction, barMax.Y);
            drawList.AddRectFilled(barMin, fillMax, ImGui.GetColorU32(color), barHeight * 0.5f);
        }

        Typography.Draw(drawList, new Vector2(row.Max.X - labelWidth, row.Center.Y - Typography.LineHeight(SpeedStyle) * 0.5f),
            bonusLabel, fraction > 0f ? inkDark : inkDark with { W = 0.45f }, SpeedStyle);
    }

    public static KeyboardPress DrawKeyboard(ImDrawListPtr drawList, WordRunBoard board, Rect area, Vector4 accent,
        float heat, bool interactive, float scale)
    {
        var gap = MathF.Max(2f * scale, area.Width * KeyGapFraction * 0.1f);
        var sidePad = KeySidePad * scale;
        var keyWidth = MathF.Floor((area.Width - sidePad * 2f - gap * 9f) / 10f);
        var keyHeight = MathF.Min(KeyMaxHeight * scale, (area.Height - gap * 2f) / 3f);
        var wide = MathF.Floor(keyWidth * WideKeyFactor);
        var letter = '\0';
        var enter = false;
        var backspace = false;
        var canSubmit = interactive && board.EntryLength == WordRunBoard.WordLength;
        var canErase = interactive && board.EntryLength > 0;
        var hotKey = Vector4.Lerp(KeyBase, accent, heat * HeatTint);
        var top = area.Min.Y + (area.Height - keyHeight * 3f - gap * 2f) * 0.5f;
        for (var rowIndex = 0; rowIndex < KeyboardRows.Length; rowIndex++)
        {
            var rowLetters = KeyboardRows[rowIndex];
            var rowWidth = rowLetters.Length * keyWidth + (rowLetters.Length - 1) * gap;
            var extras = rowIndex == KeyboardRows.Length - 1;
            if (extras)
            {
                rowWidth += (wide + gap) * 2f;
            }

            var x = area.Center.X - rowWidth * 0.5f;
            var y = top + rowIndex * (keyHeight + gap);
            if (extras)
            {
                if (DrawIconKey(drawList, new Rect(new Vector2(x, y), new Vector2(x + wide, y + keyHeight)),
                        FontAwesomeIcon.Check, accent, canSubmit, scale))
                {
                    enter = true;
                }

                x += wide + gap;
            }

            for (var index = 0; index < rowLetters.Length; index++)
            {
                var keyRect = new Rect(new Vector2(x, y), new Vector2(x + keyWidth, y + keyHeight));
                var letterIndex = rowLetters[index] - 'A';
                if (DrawLetterKey(drawList, keyRect, letterIndex, board.KeyState(letterIndex), accent, hotKey, heat,
                        interactive, scale))
                {
                    letter = rowLetters[index];
                }

                x += keyWidth + gap;
            }

            if (extras && DrawIconKey(drawList, new Rect(new Vector2(x, y), new Vector2(x + wide, y + keyHeight)),
                    FontAwesomeIcon.Backspace, accent, canErase, scale))
            {
                backspace = true;
            }
        }

        return new KeyboardPress(letter, enter, backspace);
    }

    private static bool DrawLetterKey(ImDrawListPtr drawList, Rect key, int letterIndex, byte state, Vector4 accent,
        Vector4 hotKey, float heat, bool interactive, float scale)
    {
        var hovered = interactive && UiInteract.Hover(key.Min, key.Max);
        var held = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var radius = key.Height * KeyRoundingFraction;
        var depth = held ? CellDepth.Pressed : CellDepth.Raised;
        Vector4 ink;
        switch (state)
        {
            case WordRunBoard.KeyCorrect:
                StageCell.Draw(drawList, key, CorrectColor, depth, radius, scale);
                ink = InkLight;
                break;
            case WordRunBoard.KeyPresent:
                StageCell.Draw(drawList, key, PresentColor, depth, radius, scale);
                ink = InkLight;
                break;
            case WordRunBoard.KeyAbsent:
                StageCell.Draw(drawList, key, KeyDim, CellDepth.Flat, radius, scale);
                ink = GamePalette.InkDark with { W = 0.35f };
                break;
            default:
                StageCell.Draw(drawList, key, hotKey, depth, radius, scale);
                if (heat > 0f)
                {
                    Squircle.Stroke(drawList, key.Min, key.Max, radius,
                        ImGui.GetColorU32(accent with { W = heat * HeatStrokeAlpha }), 1f * scale);
                }

                ink = GamePalette.InkOn(hotKey);
                break;
        }

        if (hovered)
        {
            Squircle.Stroke(drawList, key.Min, key.Max, radius, ImGui.GetColorU32(accent with { W = 0.9f }), 1.5f * scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.DrawCentered(drawList, key.Center, LetterLabels[letterIndex], ink, KeyStyle.Scale, KeyStyle.Weight);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static bool DrawIconKey(ImDrawListPtr drawList, Rect key, FontAwesomeIcon icon, Vector4 accent, bool armed,
        float scale)
    {
        var hovered = armed && UiInteract.Hover(key.Min, key.Max);
        var held = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var radius = key.Height * KeyRoundingFraction;
        if (armed)
        {
            StageCell.Draw(drawList, key, GamePalette.Lighten(accent, 0.1f), held ? CellDepth.Pressed : CellDepth.Raised,
                radius, scale);
        }
        else
        {
            StageCell.Draw(drawList, key, KeyDim, CellDepth.Flat, radius, scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var ink = armed ? GamePalette.InkOn(accent) : GamePalette.InkDark with { W = 0.35f };
        ProgressRing.CenterIcon(drawList, key.Center, icon, ink, key.Height * 0.42f);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
