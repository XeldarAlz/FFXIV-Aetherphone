using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Nonogram;

internal sealed class NonogramApp : IMiniGame
{
    private const string GameId = "nonogram";
    private const string SurfaceId = "nonogram.board";
    private const float CellPopSpeed = 6.5f;
    private const float MistakeFlashSpeed = 2.2f;
    private const float SolvedSpeed = 0.9f;
    private const float SolvedHoldSeconds = 0.9f;
    private const ulong IdleSeed = 23;

    private enum PaintMode : byte
    {
        None,
        Fill,
        Erase,
        Mark,
        Unmark,
    }

    private enum PaintAxis : byte
    {
        None,
        Row,
        Column,
    }

    private static readonly LocString[] Modes = { L.Games.Easy, L.Games.Medium, L.Games.Hard };
    private static readonly string[] ModeStatIds = { "nonogram.easy", "nonogram.medium", "nonogram.hard" };
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Nonogram, GameGenre.Brain, L.Nonogram.Hook,
        Backdrop.Paper, HudStyle.Standard, ScoreKind.Time, Modes, ModeStatIds);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 Glow = new(1f, 0.95f, 0.70f, 1f);
    private readonly NonogramBoard board = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly float[] cellPop = new float[NonogramBoard.MaxCells];
    private PaintMode painting;
    private PaintAxis paintAxis;
    private int paintOrigin = -1;
    private int paintLast = -1;
    private int pressedCell = -1;
    private int mistakeCell = -1;
    private float mistakeFlash;
    private float entrance;
    private float elapsed;
    private float solvedProgress;
    private float finishDelay;
    private bool finished;
    private bool reported;
    private bool idleReady;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(NonogramBoard.SizeFor(start.Mode), start.Random);
        particles.Clear();
        fx.Clear();
        Array.Clear(cellPop, 0, NonogramBoard.MaxCells);
        EndPaint();
        mistakeCell = -1;
        mistakeFlash = 0f;
        entrance = 0f;
        elapsed = 0f;
        solvedProgress = 0f;
        finishDelay = 0f;
        finished = false;
        reported = false;
        idleReady = false;
    }

    public void Close()
    {
        particles.Clear();
        fx.Clear();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        var size = NonogramBoard.SizeFor(context.Session.Mode);
        if (!idleReady || board.Size != size)
        {
            board.Reset(size, GameRandom.FromSeed(IdleSeed));
            Array.Clear(cellPop, 0, NonogramBoard.MaxCells);
            idleReady = true;
        }

        var layout = NonogramRenderer.Layout(context.Safe, board, UiScale.Current);
        var view = new NonogramView(1f, -1, -1, -1, 0f, 0f, cellPop);
        NonogramRenderer.DrawBoard(ImGui.GetWindowDrawList(), board, layout, view, UiScale.Current, Accent,
            context.Backdrop.Ink);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        entrance = GameJuice.Advance(entrance, context.RawDeltaSeconds);
        mistakeFlash = MathF.Max(0f, mistakeFlash - context.RawDeltaSeconds * MistakeFlashSpeed);
        UpdateCellPop(context.RawDeltaSeconds);
        if (board.Solved)
        {
            solvedProgress = GameJuice.Advance(solvedProgress, context.RawDeltaSeconds, SolvedSpeed);
        }

        if (!finished && board.Started)
        {
            elapsed += context.DeltaSeconds;
        }

        var area = StageLayout.Punched(context.Safe, context.Fx.PlateScale).Translate(fx.ShakeOffset(scale));
        var layout = NonogramRenderer.Layout(area, board, scale);
        var playing = !finished && context.Session.State == StageFlow.Playing;
        var hovered = playing ? HoveredCell(layout) : -1;
        if (!finished)
        {
            Step(layout, hovered, scale, context);
        }
        else if (!reported)
        {
            CountDownToResult(context);
        }

        var view = new NonogramView(entrance, hovered, pressedCell, mistakeCell, mistakeFlash, solvedProgress, cellPop);
        NonogramRenderer.DrawBoard(drawList, board, layout, view, scale, Accent, context.Backdrop.Ink);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        DrawHud(drawList, scale, context);
        context.Session.Report((int)elapsed);
    }

    private void Step(in NonogramLayout layout, int hovered, float scale, in GameContext context)
    {
        HandleInput(layout, hovered, context);
        if (!board.Solved)
        {
            return;
        }

        OnSolved(layout, scale, context);
    }

    private void HandleInput(in NonogramLayout layout, int hovered, in GameContext context)
    {
        if (context.Session.State != StageFlow.Playing)
        {
            EndPaint();
            return;
        }

        PressSurface.Claim(SurfaceId, layout.GridRect, out var activated);
        if (hovered >= 0)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (activated && hovered >= 0)
        {
            BeginPaint(hovered, board.MarkAt(hovered) switch
            {
                CellMark.Filled => PaintMode.Erase,
                CellMark.Empty => PaintMode.Fill,
                _ => PaintMode.None,
            });
        }
        else if (hovered >= 0 && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            BeginPaint(hovered, board.MarkAt(hovered) switch
            {
                CellMark.Marked => PaintMode.Unmark,
                CellMark.Empty => PaintMode.Mark,
                _ => PaintMode.None,
            });
        }

        if (painting == PaintMode.None)
        {
            return;
        }

        var button = painting is PaintMode.Fill or PaintMode.Erase ? ImGuiMouseButton.Left : ImGuiMouseButton.Right;
        if (!ImGui.IsMouseDown(button))
        {
            EndPaint();
            return;
        }

        var target = layout.ClampedHit(ImGui.GetMousePos());
        pressedCell = target;
        if (target == paintLast)
        {
            return;
        }

        ResolveAxis(target);
        PaintPath(Project(target));
    }

    private void BeginPaint(int cell, PaintMode mode)
    {
        painting = mode;
        paintAxis = PaintAxis.None;
        paintOrigin = cell;
        paintLast = cell;
        pressedCell = cell;
        if (mode != PaintMode.None)
        {
            Paint(cell);
        }
    }

    private void EndPaint()
    {
        painting = PaintMode.None;
        paintAxis = PaintAxis.None;
        paintOrigin = -1;
        paintLast = -1;
        pressedCell = -1;
    }

    private void ResolveAxis(int target)
    {
        if (paintAxis != PaintAxis.None || target == paintOrigin)
        {
            return;
        }

        var columnDelta = Math.Abs(target % board.Size - paintOrigin % board.Size);
        var rowDelta = Math.Abs(target / board.Size - paintOrigin / board.Size);
        paintAxis = columnDelta >= rowDelta ? PaintAxis.Row : PaintAxis.Column;
    }

    private int Project(int target)
    {
        return paintAxis switch
        {
            PaintAxis.Row => paintOrigin / board.Size * board.Size + target % board.Size,
            PaintAxis.Column => target / board.Size * board.Size + paintOrigin % board.Size,
            _ => paintOrigin,
        };
    }

    private void PaintPath(int target)
    {
        var step = paintAxis == PaintAxis.Row ? 1 : board.Size;
        var current = paintLast;
        while (current != target && painting != PaintMode.None)
        {
            current += target > current ? step : -step;
            Paint(current);
        }

        paintLast = target;
    }

    private void Paint(int cell)
    {
        var current = board.MarkAt(cell);
        var result = painting switch
        {
            PaintMode.Fill when current == CellMark.Empty => board.SetMark(cell, CellMark.Filled),
            PaintMode.Erase when current == CellMark.Filled => board.SetMark(cell, CellMark.Empty),
            PaintMode.Mark when current == CellMark.Empty => board.SetMark(cell, CellMark.Marked),
            PaintMode.Unmark when current == CellMark.Marked => board.SetMark(cell, CellMark.Empty),
            _ => MarkResult.None,
        };
        switch (result)
        {
            case MarkResult.Mistake:
                OnMistake(cell);
                return;
            case MarkResult.Changed:
                OnPainted(cell);
                return;
            default:
                return;
        }
    }

    private void OnPainted(int cell)
    {
        UiFeedback.Play(painting == PaintMode.Fill ? UiSound.GameTick : UiSound.Tap);
        PopChangedCells();
        var row = cell / board.Size;
        var column = cell % board.Size;
        if (board.ChangedCount > 1 && (board.RowSatisfied(row) || board.ColumnSatisfied(column)))
        {
            UiFeedback.Play(UiSound.GamePiece);
        }
    }

    private void OnMistake(int cell)
    {
        UiFeedback.Play(UiSound.GameWrong);
        mistakeCell = cell;
        mistakeFlash = 1f;
        fx.AddTrauma(0.35f);
        PopChangedCells();
        EndPaint();
    }

    private void PopChangedCells()
    {
        for (var index = 0; index < board.ChangedCount; index++)
        {
            cellPop[board.ChangedCell(index)] = 1f;
        }
    }

    private void OnSolved(in NonogramLayout layout, float scale, in GameContext context)
    {
        finished = true;
        finishDelay = SolvedHoldSeconds;
        EndPaint();
        UiFeedback.Play(UiSound.GameClear);
        context.Fx.Sweep();
        context.Fx.Punch(0.04f);
        var gridRect = layout.GridRect;
        particles.Sparkle(gridRect.Center, 18, Glow, 200f * scale, 2.6f, 0.9f);
        fx.Shockwave(gridRect.Center, gridRect.Width * 0.6f, GamePalette.Lighten(Accent, 0.3f), 0.6f, 3f);
    }

    private void CountDownToResult(in GameContext context)
    {
        finishDelay -= context.DeltaSeconds;
        if (finishDelay > 0f)
        {
            return;
        }

        reported = true;
        var seconds = Math.Max(1, (int)elapsed);
        context.Session.Finish(new GameOutcome(seconds, ScoreKind.Time, context.Session.StatId)
            .WithStat(L.Games.Mistakes, GameNumber.Label(board.Mistakes))
            .WithStat(L.Games.Filled, GameNumber.Label(board.FilledTarget)));
    }

    private void DrawHud(ImDrawListPtr drawList, float scale, in GameContext context)
    {
        context.Hud.Clock(elapsed);
        context.Hud.Best(context.Session.Best);
        var mistakesLabel = GameNumber.Label(board.Mistakes);
        context.Hud.Custom(StatCapsule.Width(mistakesLabel, scale));
        var mistakeInk = board.Mistakes > 0 ? Danger : Accent;
        StatCapsule.Draw(drawList, context.Hud.CustomRect(0), FontAwesomeIcon.Times, mistakesLabel, mistakeInk, scale);
    }

    private int HoveredCell(in NonogramLayout layout)
    {
        var gridRect = layout.GridRect;
        if (!UiInteract.Hover(gridRect.Min, gridRect.Max))
        {
            return -1;
        }

        return layout.HitTest(ImGui.GetMousePos());
    }

    private void UpdateCellPop(float deltaSeconds)
    {
        for (var index = 0; index < NonogramBoard.MaxCells; index++)
        {
            if (cellPop[index] > 0f)
            {
                cellPop[index] = MathF.Max(0f, cellPop[index] - deltaSeconds * CellPopSpeed);
            }
        }
    }
}
