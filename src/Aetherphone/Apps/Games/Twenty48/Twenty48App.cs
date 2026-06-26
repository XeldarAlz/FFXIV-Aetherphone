using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Twenty48;

internal sealed class Twenty48App : IMiniGame
{
    private const string GameId = "2048";

    private const float SlideDuration = 0.10f;

    private const float ResolveDuration = 0.16f;

    private const float MilestoneFloor = 256f;

    private enum Phase
    {
        Idle,
        Sliding,
        Resolving,
    }

    private readonly Twenty48Board board = new();

    private readonly Twenty48Renderer renderer = new();

    private readonly ParticleSystem particles = new();

    private readonly FeedbackFx fx = new();

    private Phase phase;

    private float slideTimer;

    private float resolveTimer;

    private bool finished;

    private bool finishedAsWin;

    private float resultAppear;

    private bool newBest;

    private bool pendingSubmit;

    private int loadedBest;

    private int displayBest;

    private bool swipeActive;

    private Vector2 swipeStart;

    public string Id => GameId;

    public string Title => "2048";

    public string Genre => Loc.T(L.Games.GenrePuzzle);

    public Vector4 Accent => new(0.96f, 0.58f, 0.39f, 1f);

    public void Open()
    {
        loadedBest = 0;
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
        particles.Clear();
        fx.Clear();
        phase = Phase.Idle;
        slideTimer = 0f;
        resolveTimer = 0f;
        finished = false;
        finishedAsWin = false;
        resultAppear = 0f;
        newBest = false;
        pendingSubmit = false;
        displayBest = loadedBest;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var body = context.Body;

        if (loadedBest == 0)
        {
            loadedBest = context.Stats.Get(GameId).BestScore;
            displayBest = loadedBest;
        }

        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);

        if (pendingSubmit)
        {
            context.Stats.SubmitScore(GameId, board.Score);
            pendingSubmit = false;
        }

        var rowY = body.Min.Y + 32f * scale;
        var shake = fx.ShakeOffset(scale);
        var gridArea = new Rect(
            new Vector2(body.Min.X + shake.X, body.Min.Y + 70f * scale + shake.Y),
            new Vector2(body.Max.X + shake.X, body.Max.Y - 8f * scale + shake.Y));
        var grid = GameGrid.Centered(gridArea, Twenty48Board.Size, Twenty48Board.Size, 0.06f);

        AdvanceAnimation(deltaSeconds, grid);

        if (!finished && phase == Phase.Idle)
        {
            HandleSwipe(grid);
        }

        if (board.Score > displayBest)
        {
            displayBest = board.Score;
        }

        GameHud.Pill(new Vector2(body.Center.X - 68f * scale, rowY), Loc.T(L.Games.Score), GameNumber.Label(board.Score), Accent, theme);
        GameHud.Pill(new Vector2(body.Center.X + 20f * scale, rowY), Loc.T(L.Games.Best), GameNumber.Label(displayBest), Accent, theme, displayBest > loadedBest);
        if (GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 16f * scale, theme))
        {
            StartNewGame();
            return;
        }

        var anim = BuildAnim();
        renderer.Draw(board, grid, anim, scale);

        var drawList = ImGui.GetWindowDrawList();
        particles.Draw(drawList, scale);
        fx.DrawText();

        if (finished)
        {
            DrawResult(context, theme, body);
        }
    }

    private void AdvanceAnimation(float deltaSeconds, GameGrid grid)
    {
        if (phase == Phase.Sliding)
        {
            slideTimer += deltaSeconds;
            if (slideTimer >= SlideDuration)
            {
                phase = Phase.Resolving;
                resolveTimer = 0f;
                OnSlideResolved(grid);
            }
        }
        else if (phase == Phase.Resolving)
        {
            resolveTimer += deltaSeconds;
            if (resolveTimer >= ResolveDuration)
            {
                phase = Phase.Idle;
                CheckEndState();
            }
        }

        if (finished)
        {
            resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        }
    }

    private void OnSlideResolved(GameGrid grid)
    {
        for (var index = 0; index < Twenty48Board.CellCount; index++)
        {
            if (!board.Merged(index))
            {
                continue;
            }

            var center = grid.CellCenter(index % Twenty48Board.Size, index / Twenty48Board.Size);
            var color = Twenty48Renderer.ColorFor(board.Value(index));
            particles.Burst(center, 9, color, 150f * ImGuiHelpers.GlobalScale, 3f, 0.5f, 280f);
        }

        if (board.LastMergeMax >= MilestoneFloor)
        {
            CelebrateMilestone(grid, board.LastMergeMax);
        }
    }

    private void CelebrateMilestone(GameGrid grid, int value)
    {
        var color = Twenty48Renderer.ColorFor(value);
        var center = grid.Center;
        fx.AddTrauma(value >= Twenty48Board.WinValue ? 0.7f : 0.4f);
        fx.AddText(GameNumber.Label(value) + "!", center, color, 1.6f);
        particles.Burst(center, value >= Twenty48Board.WinValue ? 60 : 34, color, 320f * ImGuiHelpers.GlobalScale, 4f, 0.9f, 360f);
    }

    private void CheckEndState()
    {
        if (board.Won && !finished)
        {
            Finish(true);
            return;
        }

        if (!board.CanMove())
        {
            Finish(false);
        }
    }

    private void Finish(bool asWin)
    {
        finished = true;
        finishedAsWin = asWin;
        resultAppear = 0f;
        newBest = board.Score > loadedBest;
        pendingSubmit = true;
    }

    private void HandleSwipe(GameGrid grid)
    {
        var mouse = ImGui.GetMousePos();
        var bounds = grid.Bounds;

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && bounds.Contains(mouse) && !swipeActive)
        {
            swipeActive = true;
            swipeStart = mouse;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (swipeActive && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                ResolveSwipe(mouse);
            }

            swipeActive = false;
        }
    }

    private void ResolveSwipe(Vector2 mouse)
    {
        var delta = mouse - swipeStart;
        var threshold = 18f * ImGuiHelpers.GlobalScale;
        if (MathF.Abs(delta.X) < threshold && MathF.Abs(delta.Y) < threshold)
        {
            return;
        }

        if (MathF.Abs(delta.X) > MathF.Abs(delta.Y))
        {
            Move(delta.X > 0f ? SwipeDirection.Right : SwipeDirection.Left);
        }
        else
        {
            Move(delta.Y > 0f ? SwipeDirection.Down : SwipeDirection.Up);
        }
    }

    private void Move(SwipeDirection direction)
    {
        if (board.TryMove(direction))
        {
            phase = Phase.Sliding;
            slideTimer = 0f;
            return;
        }

        if (!board.CanMove())
        {
            Finish(false);
        }
    }

    private TileAnim BuildAnim()
    {
        if (phase == Phase.Sliding)
        {
            return new TileAnim(MathF.Min(1f, slideTimer / SlideDuration), true, 0f, board.SpawnIndex, 0f);
        }

        if (phase == Phase.Resolving)
        {
            var resolve = MathF.Min(1f, resolveTimer / ResolveDuration);
            return new TileAnim(1f, false, resolve, board.SpawnIndex, resolve);
        }

        return new TileAnim(1f, false, 1f, board.SpawnIndex, 1f);
    }

    private void DrawResult(in GameContext context, PhoneTheme theme, Rect body)
    {
        var title = finishedAsWin ? Loc.T(L.Games.YouWin) : Loc.T(L.Games.GameOver);
        var titleColor = finishedAsWin ? Accent : theme.TextStrong;
        var bestValue = board.Score > displayBest ? board.Score : displayBest;
        var secondary = $"{Loc.T(L.Games.Best)} {GameNumber.Label(bestValue)}";
        var result = new GameResult(title, titleColor, Loc.T(L.Games.Score), GameNumber.Label(board.Score), secondary, newBest);

        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartNewGame();
        }
    }
}
