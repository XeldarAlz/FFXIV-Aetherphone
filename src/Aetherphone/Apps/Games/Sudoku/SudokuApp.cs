using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Sudoku;

internal sealed class SudokuApp : IMiniGame
{
    private const string GameId = "sudoku";
    private const int MaxMistakes = 3;
    private const int MaxHints = 3;
    private const float PopSpeed = 3.6f;
    private const float WaveSpeed = 0.8f;
    private const float ErrorPulseSeconds = 0.25f;
    private const float ErrorPulseStrength = 0.55f;
    private const float RowGap = 8f;
    private const float CapsulePadX = 10f;
    private const float CapsuleIconSize = 11f;
    private const float CapsuleIconGap = 5f;
    private const float CapsuleHeartGap = 2f;
    private const float CapsuleSectionGap = 8f;
    private static readonly LocString[] Modes = { L.Games.Easy, L.Games.Medium, L.Games.Hard };
    private static readonly string[] ModeStatIds = { "sudoku.easy", "sudoku.medium", "sudoku.hard" };
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Sudoku, GameGenre.Brain, L.Sudoku.Hook,
        Backdrop.Paper, HudStyle.Compact, ScoreKind.Time, Modes, ModeStatIds, keyboard: true);
    private static readonly Vector4 ErrorColor = new(0.92f, 0.28f, 0.32f, 1f);
    private static readonly Vector4 ErrorSpark = new(0.95f, 0.36f, 0.40f, 1f);
    private static readonly Vector4 WinSparkle = new(1f, 0.95f, 0.7f, 1f);
    private static readonly Vector4[] WinPalette =
    {
        Core.Theme.Accent.Mint, Core.Theme.Accent.Amber, Core.Theme.Accent.Pink, Core.Theme.Accent.Blue,
    };
    private static readonly TextStyle CapsuleStyle = TextStyles.FootnoteEmphasized;

    private readonly SudokuBoard board = new();
    private readonly SudokuRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly float[] pop = new float[SudokuBoard.CellCount];
    private readonly float[] shake = new float[SudokuBoard.CellCount];
    private GameGrid grid;
    private int boardMode = -1;
    private ulong boardSeed;
    private int selected = -1;
    private bool notesMode;
    private int mistakes;
    private int hintsUsed;
    private float elapsed;
    private float entrance;
    private float solveWave;
    private bool finished;
    private bool won;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        SyncBoard(start.Mode, start.Seed);
        selected = -1;
        notesMode = false;
        mistakes = 0;
        hintsUsed = 0;
        elapsed = 0f;
        entrance = 0f;
        solveWave = 0f;
        finished = false;
        won = false;
        particles.Clear();
        fx.Clear();
        Array.Clear(pop);
        Array.Clear(shake);
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        SyncBoard(context.Session.Mode, context.Session.Seed);
        var scale = UiScale.Current;
        LayoutRows(context.Safe, scale, out var boardArea, out _, out _);
        grid = GameGrid.Centered(boardArea, SudokuBoard.Size, SudokuBoard.Size, SudokuRenderer.CellGap);
        var view = new SudokuView(-1, -1, 1f, 0f, Vector2.Zero);
        renderer.Draw(ImGui.GetWindowDrawList(), board, grid, view, pop, shake, Accent, context.Backdrop.Ink,
            context.Theme, scale);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var theme = context.Theme;
        var playing = context.Session.State == StageFlow.Playing && !finished;
        if (!finished)
        {
            elapsed += context.DeltaSeconds;
        }

        Advance(context.RawDeltaSeconds);
        LayoutRows(context.Safe, scale, out var boardArea, out var toolsRow, out var padRow);
        grid = GameGrid.Centered(Scaled(boardArea, context.Fx.PlateScale), SudokuBoard.Size, SudokuBoard.Size,
            SudokuRenderer.CellGap);
        var hovered = playing ? ResolveHover() : -1;
        if (playing)
        {
            HandleBoardInput(hovered);
            HandleKeyboard(scale, context);
        }

        var view = new SudokuView(selected, hovered, entrance, solveWave, fx.ShakeOffset(scale));
        renderer.Draw(drawList, board, grid, view, pop, shake, Accent, context.Backdrop.Ink, theme, scale);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        var caption = context.Backdrop.Ink == StageInk.Dark ? GamePalette.InkDark with { W = 0.62f } : theme.TextMuted;
        var tool = SudokuControls.DrawTools(toolsRow, theme, caption, Accent, notesMode, board.CanUndo,
            MaxHints - hintsUsed, scale);
        var digit = SudokuControls.DrawPad(board, padRow, Accent, notesMode, scale);
        if (playing)
        {
            ApplyTool(tool, scale);
            if (digit > 0)
            {
                PlaceDigit((byte)digit, scale, context);
            }

            DetectFinish(scale, context);
        }

        DrawHud(drawList, theme, scale, context);
        context.Session.Report((int)elapsed);
    }

    private void SyncBoard(int mode, ulong seed)
    {
        if (boardMode == mode && boardSeed == seed && !board.CanUndo)
        {
            return;
        }

        boardMode = mode;
        boardSeed = seed;
        board.Reset(DifficultyFor(mode), seed);
        Array.Clear(pop);
        Array.Clear(shake);
    }

    private void Advance(float deltaSeconds)
    {
        entrance = GameJuice.Advance(entrance, deltaSeconds);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        if (won)
        {
            solveWave = GameJuice.Advance(solveWave, deltaSeconds, WaveSpeed);
        }

        for (var index = 0; index < pop.Length; index++)
        {
            if (pop[index] > 0f)
            {
                pop[index] = MathF.Max(0f, pop[index] - deltaSeconds * PopSpeed);
            }

            if (shake[index] > 0f)
            {
                shake[index] = MathF.Max(0f, shake[index] - deltaSeconds);
            }
        }
    }

    private static void LayoutRows(Rect safe, float scale, out Rect boardArea, out Rect toolsRow, out Rect padRow)
    {
        var gap = RowGap * scale;
        padRow = new Rect(new Vector2(safe.Min.X, safe.Max.Y - SudokuControls.PadHeight * scale), safe.Max);
        toolsRow = new Rect(new Vector2(safe.Min.X, padRow.Min.Y - gap - SudokuControls.ToolsHeight * scale),
            new Vector2(safe.Max.X, padRow.Min.Y - gap));
        var inset = BoardPlate.Padding * scale;
        boardArea = new Rect(new Vector2(safe.Min.X + inset, safe.Min.Y + inset),
            new Vector2(safe.Max.X - inset, toolsRow.Min.Y - gap - inset));
    }

    private static Rect Scaled(Rect rect, float factor)
    {
        var half = rect.Size * 0.5f * factor;
        return new Rect(rect.Center - half, rect.Center + half);
    }

    private int ResolveHover()
    {
        if (!UiInteract.Hover(grid.Bounds.Min, grid.Bounds.Max))
        {
            return -1;
        }

        var local = ImGui.GetMousePos() - grid.Origin;
        var column = (int)(local.X / grid.Pitch);
        var row = (int)(local.Y / grid.Pitch);
        if (column < 0 || column >= SudokuBoard.Size || row < 0 || row >= SudokuBoard.Size)
        {
            return -1;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return row * SudokuBoard.Size + column;
    }

    private void HandleBoardInput(int hovered)
    {
        if (hovered < 0 || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        selected = selected == hovered ? -1 : hovered;
    }

    private void HandleKeyboard(float scale, in GameContext context)
    {
        if (!GameInput.Claim())
        {
            return;
        }

        for (var digit = 1; digit <= SudokuBoard.Size; digit++)
        {
            var offset = digit - 1;
            if (ImGui.IsKeyPressed(ImGuiKey.Key1 + offset, false) ||
                ImGui.IsKeyPressed(ImGuiKey.Keypad1 + offset, false))
            {
                PlaceDigit((byte)digit, scale, context);
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
            default:
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
        var center = CellCenter(cell);
        particles.Sparkle(center, 10, Core.Theme.Accent.Mint, 130f * scale, 2.2f, 0.6f);
        fx.Shockwave(center, grid.Pitch * 1.4f, Core.Theme.Accent.Mint with { W = 0.7f }, 0.4f, 2.2f);
    }

    private void PlaceDigit(byte digit, float scale, in GameContext context)
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
            RegisterMistake(scale, context);
            return;
        }

        UiFeedback.Play(UiSound.GameTick);
        CelebratePlacement(scale, context);
    }

    private void RegisterMistake(float scale, in GameContext context)
    {
        UiFeedback.Play(UiSound.GameWrong);
        mistakes++;
        shake[selected] = SudokuRenderer.ShakeSeconds;
        fx.AddTrauma(0.12f);
        context.Fx.Vignette(ErrorColor, ErrorPulseStrength, ErrorPulseSeconds);
        particles.Burst(CellCenter(selected), 10, ErrorSpark, 120f * scale, 2.6f, 0.4f, 220f);
        if (mistakes < MaxMistakes)
        {
            return;
        }

        finished = true;
        won = false;
        context.Session.Finish(new GameOutcome(0, ScoreKind.Time, context.Session.StatId, won: false)
            .WithStat(L.Games.Time, TimeText.MinutesSeconds((int)elapsed))
            .WithStat(L.Games.Mistakes, GameNumber.Label(mistakes)));
    }

    private void CelebratePlacement(float scale, in GameContext context)
    {
        var center = CellCenter(selected);
        particles.Sparkle(center, 6, GamePalette.Lighten(Accent, 0.3f), 90f * scale, 1.8f, 0.4f);
        var units = 0;
        if (board.IsRowComplete(SudokuBoard.RowOf(selected)))
        {
            units++;
        }

        if (board.IsColumnComplete(SudokuBoard.ColumnOf(selected)))
        {
            units++;
        }

        if (board.IsBoxComplete(SudokuBoard.BoxOf(selected)))
        {
            units++;
        }

        if (units == 0)
        {
            return;
        }

        fx.AddTrauma(0.08f * units);
        context.Fx.Punch(0.02f * units);
        fx.Shockwave(center, grid.Pitch * 3.2f, GamePalette.Lighten(Accent, 0.3f) with { W = 0.7f }, 0.5f, 2.6f);
        particles.Burst(center, 14 * units, Accent, 180f * scale, 3f, 0.6f, 240f);
    }

    private void DetectFinish(float scale, in GameContext context)
    {
        if (finished || !board.Solved)
        {
            return;
        }

        UiFeedback.Play(UiSound.GameClear);
        finished = true;
        won = true;
        selected = -1;
        fx.AddTrauma(0.25f);
        context.Fx.Flash(Accent, 0.3f);
        context.Fx.Sweep();
        var center = grid.Center;
        particles.Confetti(new Vector2(center.X, grid.Origin.Y), 76, WinPalette, 260f * scale, 4f, 1.3f);
        particles.Sparkle(center, 16, WinSparkle, 200f * scale, 2.6f, 0.9f);
        fx.Shockwave(center, grid.Width * 0.6f, GamePalette.Lighten(Accent, 0.3f), 0.6f, 3f);
        context.Session.Finish(new GameOutcome(Math.Max(1, (int)elapsed), ScoreKind.Time, context.Session.StatId)
            .WithStat(L.Games.Mistakes, GameNumber.Label(mistakes))
            .WithStat(L.Sudoku.Hints, GameNumber.Label(hintsUsed)));
    }

    private void DrawHud(ImDrawListPtr drawList, PhoneTheme theme, float scale, in GameContext context)
    {
        var timeLabel = TimeText.MinutesSeconds((int)elapsed);
        var textWidth = Typography.Measure(timeLabel, CapsuleStyle).X / scale;
        var width = CapsulePadX * 2f + CapsuleIconSize + CapsuleIconGap + textWidth + CapsuleSectionGap +
                    MaxMistakes * CapsuleIconSize + (MaxMistakes - 1) * CapsuleHeartGap;
        context.Hud.Custom(width);
        var rect = context.Hud.CustomRect(0);
        if (rect.Width <= 0f)
        {
            return;
        }

        StageHud.Capsule(drawList, rect, scale);
        var iconSize = CapsuleIconSize * scale;
        var centerY = rect.Center.Y;
        var left = rect.Min.X + CapsulePadX * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, centerY), FontAwesomeIcon.Clock, Accent,
            iconSize);
        left += iconSize + CapsuleIconGap * scale;
        Typography.Draw(drawList, new Vector2(left, centerY - Typography.LineHeight(CapsuleStyle) * 0.5f), timeLabel,
            theme.TextStrong, CapsuleStyle);
        left += textWidth * scale + CapsuleSectionGap * scale;
        var lastLife = mistakes == MaxMistakes - 1;
        var alive = lastLife ? Vector4.Lerp(Accent, theme.Danger, 0.5f + 0.5f * Pulse.Wave(Pulse.Fast)) : Accent;
        var lost = theme.TextMuted with { W = 0.35f };
        for (var heart = 0; heart < MaxMistakes; heart++)
        {
            var x = left + heart * (CapsuleIconSize + CapsuleHeartGap) * scale + iconSize * 0.5f;
            ProgressRing.CenterIcon(drawList, new Vector2(x, centerY), FontAwesomeIcon.Heart,
                heart < MaxMistakes - mistakes ? alive : lost, iconSize);
        }
    }

    private Vector2 CellCenter(int cell) => grid.CellCenter(SudokuBoard.ColumnOf(cell), SudokuBoard.RowOf(cell));

    private static SudokuDifficulty DifficultyFor(int mode)
    {
        return mode switch
        {
            1 => SudokuDifficulty.Medium,
            2 => SudokuDifficulty.Hard,
            _ => SudokuDifficulty.Easy,
        };
    }
}
