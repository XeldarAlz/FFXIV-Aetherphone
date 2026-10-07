using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Sweeper;

internal sealed class SweeperApp : IMiniGame
{
    private const string GameId = "minesweeper";
    private const string SurfaceId = "sweeper.board";
    private const float HoldSeconds = 0.35f;
    private const float FlagPopSpeed = 7f;
    private const float WaveBaseSeconds = 0.10f;
    private const float WaveRingSeconds = 0.045f;
    private const int PunchCells = 10;
    private const ulong IdleSeed = 11;
    private static readonly LocString[] Modes = { L.Games.Easy, L.Games.Medium, L.Games.Hard };
    private static readonly string[] ModeStatIds = { "minesweeper.easy", "minesweeper.medium", "minesweeper.hard" };
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Sweeper, GameGenre.Brain, L.Sweeper.Hook,
        Backdrop.Slate, HudStyle.Standard, ScoreKind.Time, Modes, ModeStatIds);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 Ember = new(0.95f, 0.40f, 0.32f, 1f);
    private static readonly Vector4 Flame = new(1f, 0.70f, 0.40f, 1f);
    private static readonly Vector4 Spark = new(1f, 0.85f, 0.50f, 1f);
    private static readonly Vector4 Glow = new(1f, 0.95f, 0.70f, 1f);
    private readonly SweeperBoard board = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly float[] flagPop = new float[SweeperBoard.MaxCells];
    private float entrance;
    private float elapsed;
    private float waveProgress = 1f;
    private float waveSpeed = 1f;
    private float holdSeconds;
    private int pressIndex = -1;
    private bool pressFlagged;
    private bool finished;
    private bool idleReady;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(ModeDifficulty(start.Mode), start.Random);
        particles.Clear();
        fx.Clear();
        Array.Clear(flagPop, 0, SweeperBoard.MaxCells);
        entrance = 0f;
        elapsed = 0f;
        waveProgress = 1f;
        holdSeconds = 0f;
        pressIndex = -1;
        pressFlagged = false;
        finished = false;
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
        var difficulty = ModeDifficulty(context.Session.Mode);
        if (!idleReady || board.Difficulty != difficulty)
        {
            board.Reset(difficulty, GameRandom.FromSeed(IdleSeed));
            Array.Clear(flagPop, 0, SweeperBoard.MaxCells);
            idleReady = true;
        }

        var grid = GameGrid.Centered(context.Safe, board.Columns, board.Rows, SweeperRenderer.GapFraction);
        var view = new SweeperView(1f, 1f, -1, -1, 0f, flagPop);
        SweeperRenderer.DrawBoard(ImGui.GetWindowDrawList(), board, grid, view, UiScale.Current, Accent,
            context.Backdrop.Ink);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        entrance = GameJuice.Advance(entrance, context.RawDeltaSeconds);
        waveProgress = GameJuice.Advance(waveProgress, context.RawDeltaSeconds, waveSpeed);
        UpdateFlagPop(context.RawDeltaSeconds);
        if (!finished && board.Started)
        {
            elapsed += context.DeltaSeconds;
        }

        var area = StageLayout.Punched(context.Safe, context.Fx.PlateScale).Translate(fx.ShakeOffset(scale));
        var grid = GameGrid.Centered(area, board.Columns, board.Rows, SweeperRenderer.GapFraction);
        var playing = !finished && context.Session.State == StageFlow.Playing;
        var hovered = playing ? HoveredCell(grid) : -1;
        if (!finished)
        {
            Step(grid, hovered, scale, context);
        }

        var holdFraction = pressIndex >= 0 && !pressFlagged ? holdSeconds / HoldSeconds : 0f;
        var view = new SweeperView(entrance, waveProgress, hovered, pressIndex, holdFraction, flagPop);
        SweeperRenderer.DrawBoard(drawList, board, grid, view, scale, Accent, context.Backdrop.Ink);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        DrawHud(drawList, scale, context);
        context.Session.Report((int)elapsed);
    }

    private void Step(in GameGrid grid, int hovered, float scale, in GameContext context)
    {
        HandleInput(grid, hovered, scale, context);
        switch (board.State)
        {
            case SweeperState.Won:
                OnWin(grid, scale, context);
                return;
            case SweeperState.Lost:
                OnLoss(grid, scale, context);
                return;
            default:
                return;
        }
    }

    private void HandleInput(in GameGrid grid, int hovered, float scale, in GameContext context)
    {
        if (context.Session.State != StageFlow.Playing)
        {
            pressIndex = -1;
            holdSeconds = 0f;
            return;
        }

        PressSurface.Claim(SurfaceId, grid.Bounds, out var activated);
        if (hovered >= 0)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && !board.IsRevealed(hovered))
            {
                ToggleFlag(hovered, grid, scale);
            }
        }

        if (activated && hovered >= 0)
        {
            pressIndex = hovered;
            holdSeconds = 0f;
            pressFlagged = false;
        }

        if (pressIndex < 0)
        {
            return;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var target = pressIndex;
            pressIndex = -1;
            holdSeconds = 0f;
            if (!pressFlagged && hovered == target)
            {
                Act(target, grid, scale, context);
            }

            return;
        }

        if (hovered != pressIndex)
        {
            pressIndex = -1;
            holdSeconds = 0f;
            return;
        }

        if (pressFlagged || board.IsRevealed(pressIndex))
        {
            return;
        }

        holdSeconds += context.DeltaSeconds;
        if (holdSeconds < HoldSeconds)
        {
            return;
        }

        pressFlagged = true;
        ToggleFlag(pressIndex, grid, scale);
    }

    private void Act(int index, in GameGrid grid, float scale, in GameContext context)
    {
        var revealed = board.IsRevealed(index) ? board.Chord(index) : board.Reveal(index);
        if (!revealed)
        {
            return;
        }

        waveProgress = 0f;
        waveSpeed = 1f / (WaveBaseSeconds + WaveRingSeconds * board.WaveMaxDistance);
        if (board.State == SweeperState.Lost)
        {
            return;
        }

        UiFeedback.Play(UiSound.GamePiece);
        var center = grid.CellCenter(index % board.Columns, index / board.Columns);
        var reach = grid.Pitch * (1.1f + 0.25f * board.WaveMaxDistance);
        fx.Shockwave(center, reach, GamePalette.Lighten(Accent, 0.3f) with { W = 0.6f }, 0.4f, 2.2f);
        if (board.WaveCellCount >= PunchCells)
        {
            context.Fx.Punch(0.03f);
            fx.AddTrauma(0.05f);
        }
    }

    private void ToggleFlag(int index, in GameGrid grid, float scale)
    {
        board.ToggleFlag(index);
        if (!board.IsFlagged(index))
        {
            UiFeedback.Play(UiSound.Tap);
            return;
        }

        UiFeedback.Play(UiSound.GameTick);
        flagPop[index] = 1f;
        particles.Sparkle(grid.CellCenter(index % board.Columns, index / board.Columns), 4, Spark, 90f * scale, 1.8f,
            0.5f);
    }

    private void OnWin(in GameGrid grid, float scale, in GameContext context)
    {
        finished = true;
        UiFeedback.Play(UiSound.GameClear);
        context.Fx.Sweep();
        particles.Sparkle(grid.Center, 18, Glow, 200f * scale, 2.6f, 0.9f);
        fx.Shockwave(grid.Center, grid.Width * 0.55f, GamePalette.Lighten(Accent, 0.3f), 0.6f, 3f);
        var seconds = Math.Max(1, (int)elapsed);
        context.Session.Finish(new GameOutcome(seconds, ScoreKind.Time, context.Session.StatId)
            .WithStat(L.Games.Mines, GameNumber.Label(board.MineCount))
            .WithStat(L.Sweeper.Flags, GameNumber.Label(board.FlagCount)));
    }

    private void OnLoss(in GameGrid grid, float scale, in GameContext context)
    {
        finished = true;
        UiFeedback.Play(UiSound.GameExplosion);
        fx.AddTrauma(0.95f);
        context.Fx.Flash(Danger, 0.35f);
        context.Fx.Vignette(Danger, 0.45f, 0.8f);
        if (board.ClickedBomb >= 0)
        {
            var center = grid.CellCenter(board.ClickedBomb % board.Columns, board.ClickedBomb / board.Columns);
            particles.Burst(center, 28, Ember, 320f * scale, 4.5f, 0.8f, 420f, shape: ParticleShape.Shard);
            particles.Streaks(center, 14, Flame, 460f * scale, 2.8f, 0.5f);
            fx.Shockwave(center, 130f * scale, Flame, 0.6f, 3.6f);
        }

        context.Session.Finish(new GameOutcome((int)elapsed, ScoreKind.Time, context.Session.StatId, won: false)
            .WithStat(L.Games.Time, TimeText.MinutesSeconds((int)elapsed))
            .WithStat(L.Games.Mines, GameNumber.Label(board.MineCount))
            .WithStat(L.Sweeper.Flags, GameNumber.Label(board.FlagCount)));
    }

    private void DrawHud(ImDrawListPtr drawList, float scale, in GameContext context)
    {
        context.Hud.Clock(elapsed);
        context.Hud.Best(context.Session.Best);
        var minesLabel = GameNumber.Label(board.MinesRemaining);
        context.Hud.Custom(StatCapsule.Width(minesLabel, scale));
        var flagInk = board.MinesRemaining < 0 ? context.Theme.Danger : Accent;
        StatCapsule.Draw(drawList, context.Hud.CustomRect(0), FontAwesomeIcon.Flag, minesLabel, flagInk, scale);
    }

    private int HoveredCell(in GameGrid grid)
    {
        if (!UiInteract.Hover(grid.Bounds.Min, grid.Bounds.Max))
        {
            return -1;
        }

        var local = ImGui.GetMousePos() - grid.Origin;
        var column = (int)(local.X / grid.Pitch);
        var row = (int)(local.Y / grid.Pitch);
        if (column < 0 || column >= board.Columns || row < 0 || row >= board.Rows)
        {
            return -1;
        }

        var cell = grid.Cell(column, row);
        return UiInteract.Hover(cell.Min, cell.Max) ? row * board.Columns + column : -1;
    }

    private void UpdateFlagPop(float deltaSeconds)
    {
        for (var index = 0; index < SweeperBoard.MaxCells; index++)
        {
            if (flagPop[index] > 0f)
            {
                flagPop[index] = MathF.Max(0f, flagPop[index] - deltaSeconds * FlagPopSpeed);
            }
        }
    }

    private static Difficulty ModeDifficulty(int mode) => (Difficulty)Math.Clamp(mode, 0, Modes.Length - 1);
}
