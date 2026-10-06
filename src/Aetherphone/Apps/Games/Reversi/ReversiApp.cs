using System.Threading.Tasks;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Reversi;

internal sealed class ReversiApp : IMiniGame
{
    private const string GameId = "reversi";
    private const int EasyDepth = 4;
    private const int HardDepth = 6;
    private const int HardMode = 1;
    private const float FlipSeconds = 0.42f;
    private const float FlipSecondsPerRing = 0.05f;
    private const float PlaceSeconds = 0.28f;
    private const float AiDelay = 0.45f;
    private const int PunchFlips = 6;
    private static readonly LocString[] Modes = { L.Games.Easy, L.Games.Hard };
    private static readonly string[] ModeStatIds = { "reversi.easy", "reversi.hard" };
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Reversi, GameGenre.Tabletop, L.Reversi.Hook,
        Backdrop.Felt, HudStyle.Standard, ScoreKind.Streak, Modes, ModeStatIds);
    private static readonly Vector4 AccentColor = AppAccents.For(GameId);
    private static readonly Vector4 Gold = new(1f, 0.9f, 0.55f, 1f);
    private static readonly Vector4 Cream = new(1f, 0.95f, 0.7f, 1f);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);

    private readonly ReversiBoard board = new();
    private readonly ReversiBoard searchBoard = new();
    private readonly ReversiRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly sbyte[] flipDistance = new sbyte[ReversiBoard.CellCount];
    private readonly sbyte[] flipFrom = new sbyte[ReversiBoard.CellCount];
    private readonly int[] flipped = new int[ReversiBoard.MaxFlips];
    private readonly Vector4[] winPalette;
    private readonly Func<int> searchWork;
    private Task<int>? searchTask;
    private int searchToken;
    private int taskToken;
    private int searchDepth = EasyDepth;
    private int mode;
    private int current = ReversiBoard.Dark;
    private int placedCell = -1;
    private int flipSpan = 1;
    private int pendingMove;
    private float flipWave = 1f;
    private float flipSpeed = 1f;
    private float placeProgress = 1f;
    private float entrance = 1f;
    private float aiThinkTimer;
    private float dotPhase;
    private ulong legalMask;
    private bool hasPendingMove;
    private bool over;
    private bool finished;

    public ReversiApp()
    {
        winPalette = new[] { Accent, Core.Theme.Accent.Amber, Core.Theme.Accent.Blue, Core.Theme.Accent.Pink };
        searchWork = () => searchBoard.BestMove(ReversiBoard.Light, searchDepth);
        ResetTable();
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AccentColor;

    public void Start(in GameStart start)
    {
        mode = start.Mode;
        searchToken++;
        ResetTable();
        entrance = 0f;
    }

    public void Close()
    {
        searchToken++;
        ResetTable();
    }

    public void Dispose()
    {
        searchToken++;
    }

    public void DrawIdle(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var grid = BuildGrid(context.Safe, 1f, Vector2.Zero, scale);
        DrawTable(drawList, grid, new ReversiRenderState(flipDistance, flipFrom, 1f, 1, -1, 1f, 0UL, 1f),
            context, scale);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var simDelta = context.DeltaSeconds;
        var rawDelta = context.RawDeltaSeconds;
        var playing = context.Session.State == StageFlow.Playing;
        entrance = GameJuice.Advance(entrance, rawDelta);
        dotPhase += rawDelta;
        flipWave = GameJuice.Advance(flipWave, simDelta, flipSpeed);
        placeProgress = GameJuice.Advance(placeProgress, simDelta, 1f / PlaceSeconds);
        particles.Update(rawDelta);
        fx.Update(rawDelta);
        var grid = BuildGrid(context.Safe, context.Fx.PlateScale, fx.ShakeOffset(scale), scale);
        var animating = Animating;
        if (!over && playing)
        {
            if (current == ReversiBoard.Dark)
            {
                if (!animating)
                {
                    HandlePlayer(grid, context, scale);
                }
            }
            else
            {
                UpdateAi(simDelta, grid, context, scale);
            }
        }

        if (over && !finished && !animating)
        {
            FinishGame(context, grid, scale);
        }

        var showHints = !over && !Animating && current == ReversiBoard.Dark;
        var state = new ReversiRenderState(flipDistance, flipFrom, flipWave, flipSpan, placedCell, placeProgress,
            showHints ? legalMask : 0UL, entrance);
        DrawTable(drawList, grid, state, context, scale);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        board.Counts(out var dark, out var light);
        context.Hud.Custom(ReversiRenderer.CountsWidth(scale));
        var countsRect = context.Hud.CustomRect(0);
        if (countsRect.Width > 0f)
        {
            renderer.DrawCounts(drawList, countsRect, dark, light, over ? 0 : current,
                !over && current == ReversiBoard.Light, dotPhase, Accent, context.Theme, scale);
        }

        context.Hud.Best(context.Session.Best);
        context.Session.Report(dark);
    }

    private bool Animating => flipWave < 1f || placeProgress < 1f;

    private void ResetTable()
    {
        board.Reset();
        Array.Fill(flipDistance, (sbyte)-1);
        particles.Clear();
        fx.Clear();
        current = ReversiBoard.Dark;
        placedCell = -1;
        flipWave = 1f;
        placeProgress = 1f;
        aiThinkTimer = 0f;
        hasPendingMove = false;
        over = false;
        finished = false;
        legalMask = board.LegalMask(ReversiBoard.Dark);
    }

    private static GameGrid BuildGrid(Rect safe, float plateScale, Vector2 shake, float scale)
    {
        var area = Grow(safe, plateScale).Inset(BoardPlate.Padding * scale).Translate(shake);
        return GameGrid.Centered(area, ReversiBoard.Size, ReversiBoard.Size, 0f);
    }

    private static Rect Grow(Rect rect, float factor)
    {
        var half = rect.Size * 0.5f * factor;
        return new Rect(rect.Center - half, rect.Center + half);
    }

    private void DrawTable(ImDrawListPtr drawList, GameGrid grid, in ReversiRenderState state,
        in GameContext context, float scale)
    {
        var plate = BoardPlate.Around(grid.Bounds, scale);
        BoardPlate.Draw(drawList, plate, BoardPlate.Radius * scale, scale, Accent, context.Backdrop.Ink);
        renderer.Draw(drawList, board, grid, state, Accent, scale);
    }

    private void HandlePlayer(GameGrid grid, in GameContext context, float scale)
    {
        var hovered = HitTest(grid);
        if (hovered < 0 || (legalMask & (1UL << hovered)) == 0)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var cell = grid.Cell(ReversiBoard.ColumnOf(hovered), ReversiBoard.RowOf(hovered));
        if (!UiInteract.Click(cell.Min, cell.Max, true, false))
        {
            return;
        }

        ApplyAnimated(hovered, ReversiBoard.Dark, grid, context, scale);
        AdvanceTurn(ReversiBoard.Dark, grid);
    }

    private void UpdateAi(float simDelta, GameGrid grid, in GameContext context, float scale)
    {
        aiThinkTimer = MathF.Max(0f, aiThinkTimer - simDelta);
        if (searchTask is null && !hasPendingMove)
        {
            StartSearch();
        }

        CollectSearchResult();
        if (!hasPendingMove || Animating || aiThinkTimer > 0f)
        {
            return;
        }

        hasPendingMove = false;
        if (pendingMove >= 0)
        {
            ApplyAnimated(pendingMove, ReversiBoard.Light, grid, context, scale);
        }

        AdvanceTurn(ReversiBoard.Light, grid);
    }

    private void StartSearch()
    {
        searchBoard.CopyFrom(board);
        searchDepth = mode == HardMode ? HardDepth : EasyDepth;
        taskToken = searchToken;
        searchTask = Task.Run(searchWork);
    }

    private void CollectSearchResult()
    {
        if (searchTask is null || !searchTask.IsCompleted)
        {
            return;
        }

        var completed = searchTask;
        searchTask = null;
        if (taskToken != searchToken)
        {
            return;
        }

        pendingMove = completed.IsCompletedSuccessfully ? completed.Result : board.BestMove(ReversiBoard.Light, 1);
        hasPendingMove = true;
    }

    private void ApplyAnimated(int cell, int player, GameGrid grid, in GameContext context, float scale)
    {
        var count = board.ApplyMove(cell, player, flipped);
        if (count == 0)
        {
            return;
        }

        UiFeedback.Play(UiSound.GamePiece);
        Array.Fill(flipDistance, (sbyte)-1);
        placedCell = cell;
        placeProgress = 0f;
        var placedRow = ReversiBoard.RowOf(cell);
        var placedColumn = ReversiBoard.ColumnOf(cell);
        var opponent = (sbyte)ReversiBoard.Opponent(player);
        var maxDistance = 0;
        for (var index = 0; index < count; index++)
        {
            var flippedCell = flipped[index];
            var distance = Math.Max(Math.Abs(ReversiBoard.RowOf(flippedCell) - placedRow),
                Math.Abs(ReversiBoard.ColumnOf(flippedCell) - placedColumn));
            flipFrom[flippedCell] = opponent;
            flipDistance[flippedCell] = (sbyte)distance;
            maxDistance = Math.Max(maxDistance, distance);
        }

        flipSpan = maxDistance + 1;
        flipWave = 0f;
        flipSpeed = 1f / (FlipSeconds + maxDistance * FlipSecondsPerRing);
        legalMask = 0UL;
        var center = grid.CellCenter(placedColumn, placedRow);
        particles.Burst(center, 8, Accent, 130f * scale, 2.8f, 0.45f, 220f);
        fx.Shockwave(center, grid.Pitch * 0.9f, GamePalette.Lighten(Accent, 0.3f) with { W = 0.7f }, 0.4f, 2.4f);
        if (count >= 4)
        {
            fx.AddTrauma(MathF.Min(0.3f, 0.05f * count));
        }

        if (count >= PunchFlips)
        {
            context.Fx.Punch(0.04f);
        }

        if (!ReversiBoard.IsCorner(cell))
        {
            return;
        }

        particles.Burst(center, 18, Core.Theme.Accent.Amber, 220f * scale, 3.6f, 0.7f, 300f);
        particles.Sparkle(center, 8, Gold, 140f * scale, 2.4f, 0.7f);
        fx.AddTrauma(0.2f);
    }

    private void AdvanceTurn(int mover, GameGrid grid)
    {
        var opponent = ReversiBoard.Opponent(mover);
        if (board.HasAnyMove(opponent))
        {
            SetTurn(opponent);
            return;
        }

        if (board.HasAnyMove(mover))
        {
            SetTurn(mover);
            fx.AddText(Loc.T(L.Games.Pass), grid.Center, Accent, 1.4f);
            return;
        }

        over = true;
        legalMask = 0UL;
    }

    private void SetTurn(int player)
    {
        current = player;
        hasPendingMove = false;
        if (player == ReversiBoard.Dark)
        {
            legalMask = board.LegalMask(ReversiBoard.Dark);
            return;
        }

        legalMask = 0UL;
        aiThinkTimer = AiDelay;
    }

    private void FinishGame(in GameContext context, GameGrid grid, float scale)
    {
        finished = true;
        board.Counts(out var dark, out var light);
        var won = dark > light;
        if (won)
        {
            fx.AddTrauma(0.4f);
            fx.Flash(Accent, 0.35f);
            particles.Confetti(new Vector2(grid.Center.X, grid.Bounds.Min.Y), 80, winPalette, 260f * scale, 4f, 1.4f);
            particles.Sparkle(grid.Center, 16, Cream, 200f * scale, 2.6f, 0.9f);
            fx.Shockwave(grid.Center, grid.Width * 0.55f, GamePalette.Lighten(Accent, 0.3f), 0.6f, 3f);
            context.Fx.Punch(0.05f);
            context.Fx.Sweep();
        }
        else
        {
            context.Fx.Vignette(Danger, 0.3f, 0.9f);
        }

        var streakAfter = won ? context.Session.Best + 1 : 0;
        context.Session.Finish(new GameOutcome(streakAfter, ScoreKind.Streak, StageSpec.StatIdFor(mode), won)
            .WithStat(L.Games.You, GameNumber.Label(dark))
            .WithStat(L.Games.Cpu, GameNumber.Label(light)));
    }

    private static int HitTest(GameGrid grid)
    {
        if (!UiInteract.Hover(grid.Bounds.Min, grid.Bounds.Max))
        {
            return -1;
        }

        var local = ImGui.GetMousePos() - grid.Origin;
        var column = (int)(local.X / grid.Pitch);
        var row = (int)(local.Y / grid.Pitch);
        if (column < 0 || column >= ReversiBoard.Size || row < 0 || row >= ReversiBoard.Size)
        {
            return -1;
        }

        return row * ReversiBoard.Size + column;
    }
}
