using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Reversi;

internal sealed class ReversiApp : IMiniGame
{
    private const string GameId = "reversi";
    private const float FlipDuration = 0.26f;
    private const float PlaceDuration = 0.28f;
    private const float StaggerStep = 0.04f;
    private const float AiDelay = 0.45f;
    private readonly ReversiBoard board = new();
    private readonly ReversiRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly float[] flipTimer = new float[ReversiBoard.CellCount];
    private readonly int[] flipFrom = new int[ReversiBoard.CellCount];
    private readonly float[] placeTimer = new float[ReversiBoard.CellCount];
    private readonly List<int> flipped = new(24);
    private int current = ReversiBoard.Dark;
    private float aiThinkTimer;
    private bool over;
    private int outcome;
    private bool pendingResult;
    private bool newBest;
    private int streak;
    private bool statsLoaded;
    private float resultAppear;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Reversi);
    public string Genre => Loc.T(L.Games.GenreStrategy);
    public void Open()
    {
        statsLoaded = false;
        StartNewGame();
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    private void StartNewGame()
    {
        board.Reset();
        Array.Clear(flipTimer, 0, flipTimer.Length);
        Array.Clear(placeTimer, 0, placeTimer.Length);
        particles.Clear();
        fx.Clear();
        current = ReversiBoard.Dark;
        aiThinkTimer = 0f;
        over = false;
        outcome = 0;
        pendingResult = false;
        newBest = false;
        resultAppear = 0f;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var body = context.Body;
        if (!statsLoaded)
        {
            streak = context.Stats.Get(GameId).Streak;
            statsLoaded = true;
        }

        UpdateTimers(deltaSeconds);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        GameScene.Ambient(ImGui.GetWindowDrawList(), body, Accent);
        board.Counts(out var dark, out var light);
        DrawHud(body, theme, scale, dark, light);
        var area = new Rect(new Vector2(body.Min.X + 4f * scale, body.Min.Y + 62f * scale),
            new Vector2(body.Max.X - 4f * scale, body.Max.Y - 8f * scale));
        var grid = GameGrid.Centered(area, ReversiBoard.Size, ReversiBoard.Size, 0f);
        if (pendingResult)
        {
            ResolveResult(context, grid, scale);
        }

        var animating = AnyAnimating();
        if (!over && !animating)
        {
            if (current == ReversiBoard.Dark)
            {
                HandlePlayer(grid, scale);
            }
            else
            {
                UpdateAi(deltaSeconds, grid, scale);
            }
        }

        var showHints = !over && !animating && current == ReversiBoard.Dark;
        renderer.Draw(board, grid, flipTimer, flipFrom, placeTimer, FlipDuration, PlaceDuration, showHints,
            ReversiBoard.Dark, Accent, scale);
        var drawList = ImGui.GetWindowDrawList();
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        if (over)
        {
            DrawResult(theme, body, deltaSeconds, dark, light);
        }
    }

    private void DrawHud(Rect body, PhoneTheme theme, float scale, int dark, int light)
    {
        var rowY = body.Min.Y + 30f * scale;
        GameHud.Pill(new Vector2(body.Center.X - 52f * scale, rowY), Loc.T(L.Games.You), GameNumber.Label(dark), Accent,
            theme, current == ReversiBoard.Dark && !over);
        GameHud.Pill(new Vector2(body.Center.X + 52f * scale, rowY), Loc.T(L.Games.Cpu), GameNumber.Label(light),
            Accent, theme, current == ReversiBoard.Light && !over);
        if (GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 16f * scale, theme))
        {
            StartNewGame();
        }
    }

    private void HandlePlayer(GameGrid grid, float scale)
    {
        var hovered = HitTest(grid);
        if (hovered < 0 || !board.IsLegal(hovered, ReversiBoard.Dark))
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        ApplyAnimated(hovered, ReversiBoard.Dark, grid, scale);
        AdvanceTurn(ReversiBoard.Dark, grid);
    }

    private void UpdateAi(float deltaSeconds, GameGrid grid, float scale)
    {
        aiThinkTimer -= deltaSeconds;
        if (aiThinkTimer > 0f)
        {
            return;
        }

        var move = board.BestMove(ReversiBoard.Light);
        if (move >= 0)
        {
            ApplyAnimated(move, ReversiBoard.Light, grid, scale);
        }

        AdvanceTurn(ReversiBoard.Light, grid);
    }

    private void ApplyAnimated(int cell, int player, GameGrid grid, float scale)
    {
        if (!board.ApplyMove(cell, player, flipped))
        {
            return;
        }

        placeTimer[cell] = PlaceDuration;
        var placedRow = cell / ReversiBoard.Size;
        var placedColumn = cell % ReversiBoard.Size;
        var opponent = ReversiBoard.Opponent(player);
        for (var index = 0; index < flipped.Count; index++)
        {
            var flippedCell = flipped[index];
            flipFrom[flippedCell] = opponent;
            var row = flippedCell / ReversiBoard.Size;
            var column = flippedCell % ReversiBoard.Size;
            var distance = Math.Max(Math.Abs(row - placedRow), Math.Abs(column - placedColumn));
            flipTimer[flippedCell] = FlipDuration + distance * StaggerStep;
        }

        var center = grid.CellCenter(placedColumn, placedRow);
        particles.Burst(center, 8, Accent, 130f * scale, 2.8f, 0.45f, 220f);
        fx.Shockwave(center, grid.Pitch * 0.9f, GamePalette.Lighten(Accent, 0.3f) with { W = 0.7f }, 0.4f, 2.4f);
        if (flipped.Count >= 4)
        {
            fx.AddTrauma(MathF.Min(0.3f, 0.05f * flipped.Count));
        }

        if (cell == 0 || cell == 7 || cell == 56 || cell == 63)
        {
            particles.Burst(center, 18, Core.Theme.Accent.Amber, 220f * scale, 3.6f, 0.7f, 300f);
            particles.Sparkle(center, 8, new Vector4(1f, 0.9f, 0.55f, 1f), 140f * scale, 2.4f, 0.7f);
            fx.AddTrauma(0.2f);
        }
    }

    private void AdvanceTurn(int mover, GameGrid grid)
    {
        var opponent = ReversiBoard.Opponent(mover);
        if (board.HasAnyMove(opponent))
        {
            current = opponent;
            if (current == ReversiBoard.Light)
            {
                aiThinkTimer = AiDelay;
            }

            return;
        }

        if (board.HasAnyMove(mover))
        {
            current = mover;
            if (current == ReversiBoard.Light)
            {
                aiThinkTimer = AiDelay;
            }

            fx.AddText(Loc.T(L.Games.Pass), grid.Center, Accent, 1.4f);
            return;
        }

        over = true;
        pendingResult = true;
        resultAppear = 0f;
    }

    private void ResolveResult(in GameContext context, GameGrid grid, float scale)
    {
        board.Counts(out var dark, out var light);
        outcome = dark > light ? 1 :
            light > dark ? 2 : 0;
        if (outcome == 1)
        {
            var previous = streak;
            streak = context.Stats.RecordWin(GameId);
            newBest = streak > previous;
            fx.AddTrauma(0.4f);
            fx.Flash(Accent, 0.35f);
            ReadOnlySpan<Vector4> palette = new[]
            {
                Accent, Core.Theme.Accent.Amber, Core.Theme.Accent.Blue, Core.Theme.Accent.Pink,
            };
            particles.Confetti(new Vector2(grid.Center.X, grid.Bounds.Min.Y), 80, palette, 260f * scale, 4f, 1.4f);
            particles.Sparkle(grid.Center, 16, new Vector4(1f, 0.95f, 0.7f, 1f), 200f * scale, 2.6f, 0.9f);
            fx.Shockwave(grid.Center, grid.Width * 0.55f, GamePalette.Lighten(Accent, 0.3f), 0.6f, 3f);
        }
        else
        {
            context.Stats.ResetStreak(GameId);
            streak = 0;
            newBest = false;
        }

        pendingResult = false;
    }

    private int HitTest(GameGrid grid)
    {
        var mouse = ImGui.GetMousePos();
        if (!grid.Bounds.Contains(mouse))
        {
            return -1;
        }

        var local = mouse - grid.Origin;
        var column = (int)(local.X / grid.Pitch);
        var row = (int)(local.Y / grid.Pitch);
        if (column < 0 || column >= ReversiBoard.Size || row < 0 || row >= ReversiBoard.Size)
        {
            return -1;
        }

        return row * ReversiBoard.Size + column;
    }

    private void UpdateTimers(float deltaSeconds)
    {
        for (var index = 0; index < ReversiBoard.CellCount; index++)
        {
            if (flipTimer[index] > 0f)
            {
                flipTimer[index] = MathF.Max(0f, flipTimer[index] - deltaSeconds);
            }

            if (placeTimer[index] > 0f)
            {
                placeTimer[index] = MathF.Max(0f, placeTimer[index] - deltaSeconds);
            }
        }
    }

    private bool AnyAnimating()
    {
        for (var index = 0; index < ReversiBoard.CellCount; index++)
        {
            if (flipTimer[index] > 0f || placeTimer[index] > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds, int dark, int light)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        var title = outcome == 1 ? Loc.T(L.Games.YouWin) :
            outcome == 2 ? Loc.T(L.Games.Lose) : Loc.T(L.Games.Draw);
        var titleColor = outcome == 1 ? Accent :
            outcome == 2 ? theme.Danger : theme.TextStrong;
        var secondary = streak > 1 ? $"{Loc.T(L.Games.Streak)} {GameNumber.Label(streak)}" : null;
        var result = new GameResult(title, titleColor, $"{Loc.T(L.Games.You)} · {Loc.T(L.Games.Cpu)}",
            $"{dark} : {light}", secondary, newBest);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartNewGame();
        }
    }
}
