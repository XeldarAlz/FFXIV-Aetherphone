using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Sudoku;

internal sealed class SudokuApp : IMiniGame
{
    private const string GameId = "sudoku";
    private const int MaxMistakes = 3;
    private const int MaxHints = 3;
    private const float PopSpeed = 3.6f;
    private const float DifficultyRowY = 22f;
    private const float StatsRowY = 64f;
    private const float BoardTop = 92f;

    private readonly SudokuBoard board = new();
    private readonly SudokuRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly float[] pop = new float[SudokuBoard.CellCount];
    private readonly string[] difficultyLabels = new string[3];
    private SudokuLayout layout;
    private int difficulty;
    private int selected = -1;
    private bool notesMode;
    private int mistakes;
    private int hintsUsed;
    private float elapsed;
    private bool finished;
    private bool won;
    private bool pendingSubmit;
    private bool newBestTime;
    private int loadedBestTime = -1;
    private float resultAppear;
    private string resultTimeText = "0:00";
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Sudoku);
    public string Genre => Loc.T(L.Games.GenreLogic);

    public void Open()
    {
        StartNewGame(difficulty);
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    private void StartNewGame(int target)
    {
        difficulty = target;
        board.Reset(DifficultyFor(target));
        particles.Clear();
        fx.Clear();
        Array.Clear(pop, 0, pop.Length);
        selected = -1;
        notesMode = false;
        mistakes = 0;
        hintsUsed = 0;
        elapsed = 0f;
        finished = false;
        won = false;
        pendingSubmit = false;
        newBestTime = false;
        loadedBestTime = -1;
        resultAppear = 0f;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var body = context.Body;
        if (loadedBestTime < 0)
        {
            loadedBestTime = context.Stats.Get(StatId(difficulty)).BestTimeSeconds;
        }

        if (!finished)
        {
            elapsed += deltaSeconds;
        }

        if (pendingSubmit)
        {
            newBestTime = context.Stats.SubmitTime(StatId(difficulty), (int)elapsed);
            pendingSubmit = false;
        }

        UpdatePop(deltaSeconds);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        GameScene.Ambient(ImGui.GetWindowDrawList(), body, Accent);
        DrawDifficultyRow(body, theme, scale);
        DrawStatsRow(body, theme, scale);
        var padRow = new Rect(new Vector2(body.Min.X + 6f * scale, body.Max.Y - (SudokuControls.PadHeight + 8f) * scale),
            new Vector2(body.Max.X - 6f * scale, body.Max.Y - 8f * scale));
        var toolsRow = new Rect(new Vector2(body.Min.X + 6f * scale, padRow.Min.Y - (SudokuControls.ToolsHeight + 6f) * scale),
            new Vector2(body.Max.X - 6f * scale, padRow.Min.Y - 6f * scale));
        var boardArea = new Rect(new Vector2(body.Min.X + 6f * scale, body.Min.Y + BoardTop * scale),
            new Vector2(body.Max.X - 6f * scale, toolsRow.Min.Y - 6f * scale));
        layout = SudokuRenderer.Layout(boardArea, scale);
        var hovered = ResolveHover();
        if (!finished)
        {
            HandleBoardInput(hovered);
            HandleKeyboard(scale);
        }

        renderer.Draw(board, layout, selected, hovered, pop, theme, Accent, scale);
        var drawList = ImGui.GetWindowDrawList();
        fx.DrawFlash(drawList, body, 0f);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        var tool = SudokuControls.DrawTools(toolsRow, theme, Accent, notesMode, board.CanUndo, MaxHints - hintsUsed,
            scale);
        var pressedDigit = SudokuControls.DrawPad(board, padRow, theme, Accent, notesMode, scale);
        if (!finished)
        {
            ApplyTool(tool, scale);
            if (pressedDigit > 0)
            {
                PlaceDigit((byte)pressedDigit, scale);
            }
        }

        DetectFinish(scale);
        if (finished)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private void DrawDifficultyRow(Rect body, PhoneTheme theme, float scale)
    {
        difficultyLabels[0] = Loc.T(L.Games.Easy);
        difficultyLabels[1] = Loc.T(L.Games.Medium);
        difficultyLabels[2] = Loc.T(L.Games.Hard);
        var rowY = body.Min.Y + DifficultyRowY * scale;
        var segmentRow = new Rect(new Vector2(body.Min.X + 4f * scale, rowY - 13f * scale),
            new Vector2(body.Max.X - 44f * scale, rowY + 13f * scale));
        var selection = SegmentStrip.Draw("sudoku.difficulty", segmentRow, difficultyLabels, difficulty, theme);
        if (selection != difficulty)
        {
            StartNewGame(selection);
            return;
        }

        if (GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 15f * scale, theme))
        {
            StartNewGame(difficulty);
        }
    }

    private void DrawStatsRow(Rect body, PhoneTheme theme, float scale)
    {
        var rowY = body.Min.Y + StatsRowY * scale;
        GameHud.Pill(new Vector2(body.Center.X - 56f * scale, rowY), Loc.T(L.Games.Time),
            TimeText.MinutesSeconds((int)elapsed), Accent, theme);
        GameHud.Pill(new Vector2(body.Center.X + 56f * scale, rowY), Loc.T(L.Games.Mistakes),
            $"{GameNumber.Label(mistakes)}/{GameNumber.Label(MaxMistakes)}", Accent, theme, mistakes > 0);
    }

    private int ResolveHover()
    {
        var mouse = ImGui.GetMousePos();
        if (!layout.Bounds.Contains(mouse))
        {
            return -1;
        }

        var cell = layout.HitTest(mouse);
        if (cell >= 0)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return cell;
    }

    private void HandleBoardInput(int hovered)
    {
        if (hovered < 0 || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        selected = selected == hovered ? -1 : hovered;
    }

    private void HandleKeyboard(float scale)
    {
        if (!ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
        {
            return;
        }

        ImGui.SetNextFrameWantCaptureKeyboard(true);
        for (var digit = 1; digit <= SudokuBoard.Size; digit++)
        {
            var offset = digit - 1;
            if (ImGui.IsKeyPressed(ImGuiKey.Key1 + offset, false) ||
                ImGui.IsKeyPressed(ImGuiKey.Keypad1 + offset, false))
            {
                PlaceDigit((byte)digit, scale);
                return;
            }
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Backspace, false) || ImGui.IsKeyPressed(ImGuiKey.Delete, false))
        {
            EraseSelected();
            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Space, false))
        {
            notesMode = !notesMode;
            return;
        }

        MoveSelection();
    }

    private void MoveSelection()
    {
        var column = selected < 0 ? 0 : SudokuBoard.ColumnOf(selected);
        var row = selected < 0 ? 0 : SudokuBoard.RowOf(selected);
        var moved = false;
        if (ImGui.IsKeyPressed(ImGuiKey.LeftArrow))
        {
            column = column > 0 ? column - 1 : SudokuBoard.Size - 1;
            moved = true;
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.RightArrow))
        {
            column = column < SudokuBoard.Size - 1 ? column + 1 : 0;
            moved = true;
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
        {
            row = row > 0 ? row - 1 : SudokuBoard.Size - 1;
            moved = true;
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
        {
            row = row < SudokuBoard.Size - 1 ? row + 1 : 0;
            moved = true;
        }

        if (moved)
        {
            selected = row * SudokuBoard.Size + column;
        }
    }

    private void ApplyTool(SudokuTool tool, float scale)
    {
        switch (tool)
        {
            case SudokuTool.Undo:
                if (board.Undo())
                {
                    fx.AddTrauma(0.05f);
                }

                break;
            case SudokuTool.Erase:
                EraseSelected();
                break;
            case SudokuTool.Notes:
                notesMode = !notesMode;
                break;
            case SudokuTool.Hint:
                UseHint(scale);
                break;
        }
    }

    private void EraseSelected()
    {
        if (selected < 0)
        {
            return;
        }

        board.ClearCell(selected);
    }

    private void UseHint(float scale)
    {
        if (hintsUsed >= MaxHints)
        {
            return;
        }

        var cell = board.PickHintCell(selected);
        if (cell < 0 || !board.Reveal(cell))
        {
            return;
        }

        hintsUsed++;
        selected = cell;
        pop[cell] = 1f;
        var center = layout.CellCenter(cell);
        particles.Sparkle(center, 10, Core.Theme.Accent.Mint, 130f * scale, 2.2f, 0.6f);
        fx.Shockwave(center, layout.CellSize * 1.4f, Core.Theme.Accent.Mint with { W = 0.7f }, 0.4f, 2.2f);
    }

    private void PlaceDigit(byte digit, float scale)
    {
        if (selected < 0 || board.IsGiven(selected))
        {
            return;
        }

        if (notesMode)
        {
            board.ToggleNote(selected, digit);
            return;
        }

        if (!board.SetEntry(selected, digit))
        {
            return;
        }

        pop[selected] = 1f;
        if (board.IsWrong(selected))
        {
            RegisterMistake(scale);
            return;
        }

        CelebratePlacement(scale);
    }

    private void RegisterMistake(float scale)
    {
        mistakes++;
        fx.AddTrauma(0.28f);
        fx.Flash(new Vector4(0.92f, 0.28f, 0.32f, 1f), 0.22f);
        particles.Burst(layout.CellCenter(selected), 10, new Vector4(0.95f, 0.36f, 0.40f, 1f), 120f * scale, 2.6f,
            0.4f, 220f);
        if (mistakes < MaxMistakes)
        {
            return;
        }

        finished = true;
        won = false;
        resultAppear = 0f;
        resultTimeText = TimeText.MinutesSeconds((int)elapsed);
    }

    private void CelebratePlacement(float scale)
    {
        var center = layout.CellCenter(selected);
        particles.Sparkle(center, 6, GamePalette.Lighten(Accent, 0.3f), 90f * scale, 1.8f, 0.4f);
        var row = SudokuBoard.RowOf(selected);
        var column = SudokuBoard.ColumnOf(selected);
        var box = SudokuBoard.BoxOf(selected);
        var units = 0;
        if (board.IsRowComplete(row))
        {
            units++;
        }

        if (board.IsColumnComplete(column))
        {
            units++;
        }

        if (board.IsBoxComplete(box))
        {
            units++;
        }

        if (units == 0)
        {
            return;
        }

        fx.AddTrauma(0.10f * units);
        fx.Shockwave(center, layout.CellSize * 3.2f, GamePalette.Lighten(Accent, 0.3f) with { W = 0.7f }, 0.5f, 2.6f);
        particles.Burst(center, 14 * units, Accent, 180f * scale, 3f, 0.6f, 240f);
    }

    private void DetectFinish(float scale)
    {
        if (finished || !board.Solved)
        {
            return;
        }

        finished = true;
        won = true;
        pendingSubmit = true;
        resultAppear = 0f;
        resultTimeText = TimeText.MinutesSeconds((int)elapsed);
        fx.AddTrauma(0.35f);
        fx.Flash(Accent, 0.4f);
        ReadOnlySpan<Vector4> palette = new[]
        {
            Accent, Core.Theme.Accent.Mint, Core.Theme.Accent.Amber, Core.Theme.Accent.Pink,
        };
        var center = layout.Center;
        particles.Confetti(new Vector2(center.X, layout.Origin.Y), 76, palette, 260f * scale, 4f, 1.3f);
        particles.Sparkle(center, 16, new Vector4(1f, 0.95f, 0.7f, 1f), 200f * scale, 2.6f, 0.9f);
        fx.Shockwave(center, layout.BoardSize * 0.6f, GamePalette.Lighten(Accent, 0.3f), 0.6f, 3f);
    }

    private void UpdatePop(float deltaSeconds)
    {
        for (var index = 0; index < pop.Length; index++)
        {
            if (pop[index] > 0f)
            {
                pop[index] = MathF.Max(0f, pop[index] - deltaSeconds * PopSpeed);
            }
        }
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        var title = won ? Loc.T(L.Games.YouWin) : Loc.T(L.Games.GameOver);
        var titleColor = won ? Accent : theme.Danger;
        string? secondary = null;
        if (won && loadedBestTime > 0)
        {
            secondary = $"{Loc.T(L.Games.Best)} {TimeText.MinutesSeconds(loadedBestTime)}";
        }
        else if (!won)
        {
            secondary = $"{Loc.T(L.Games.Mistakes)} {GameNumber.Label(mistakes)}";
        }

        var result = new GameResult(title, titleColor, Loc.T(L.Games.Time), resultTimeText, secondary,
            won && newBestTime);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartNewGame(difficulty);
        }
    }

    private static SudokuDifficulty DifficultyFor(int difficulty)
    {
        return difficulty switch
        {
            1 => SudokuDifficulty.Medium,
            2 => SudokuDifficulty.Hard,
            _ => SudokuDifficulty.Easy,
        };
    }

    private static string StatId(int difficulty)
    {
        return difficulty switch
        {
            1 => "sudoku.medium",
            2 => "sudoku.hard",
            _ => "sudoku.easy",
        };
    }
}
