using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Animation;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.GemSwap;

internal sealed class GemSwapApp : IMiniGame
{
    private const string GameId = "match3";
    private const float SwapDuration = 0.14f;
    private const float SwapBackDuration = 0.12f;
    private const float ClearDuration = 0.26f;
    private const float FallDuration = 0.30f;
    private const float HintDelay = 6f;
    private readonly GemSwapBoard board = new();
    private readonly GemSwapRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private RollingValue scoreRoll;
    private float entrance;
    private int previousScore;
    private GemPhase phase;
    private int swapA = -1;
    private int swapB = -1;
    private float swapTimer;
    private float clearTimer;
    private float fallTimer;
    private int selectedIndex = -1;
    private int chain;
    private float idleTime;
    private int hintA = -1;
    private int hintB = -1;
    private int loadedBest;
    private int displayBest;
    private GameStatsStore? statsRef;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.GemSwap);
    public string Genre => Loc.T(L.Games.GenreMatch);
    public void Open()
    {
        loadedBest = 0;
        StartNewGame();
    }

    public void Close()
    {
        PersistBest();
    }

    public void Dispose()
    {
        PersistBest();
    }

    private void PersistBest()
    {
        statsRef?.SubmitScore(GameId, board.Score);
    }

    private void StartNewGame()
    {
        PersistBest();
        if (board.Score > loadedBest)
        {
            loadedBest = board.Score;
        }

        board.Reset();
        particles.Clear();
        fx.Clear();
        scoreRoll.Snap(0);
        entrance = 0f;
        previousScore = 0;
        phase = GemPhase.Idle;
        swapA = -1;
        swapB = -1;
        selectedIndex = -1;
        chain = 0;
        idleTime = 0f;
        hintA = -1;
        hintB = -1;
        displayBest = loadedBest;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var body = context.Body;
        statsRef = context.Stats;
        if (loadedBest == 0)
        {
            loadedBest = context.Stats.Get(GameId).BestScore;
            displayBest = loadedBest;
        }

        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        entrance = GameJuice.Advance(entrance, deltaSeconds);
        var rowY = body.Min.Y + 30f * scale;
        var comboText = chain > 1 ? $"x{chain}" : string.Empty;
        var shake = fx.ShakeOffset(scale);
        var gridArea = new Rect(new Vector2(body.Min.X, body.Min.Y + 64f * scale) + shake,
            new Vector2(body.Max.X, body.Max.Y - 6f * scale) + shake);
        var grid = GameGrid.Centered(gridArea, GemSwapBoard.Columns, GemSwapBoard.Rows, 0.06f);
        AdvanceAnimation(fx.ScaleDelta(deltaSeconds), grid);
        if (phase == GemPhase.Idle)
        {
            HandleInput(grid, deltaSeconds);
        }

        if (board.Score > displayBest)
        {
            displayBest = board.Score;
        }

        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        var beatingBest = board.Score > 0 && board.Score > loadedBest;
        GameHud.ScorePill(new Vector2(body.Center.X - 68f * scale, rowY), Loc.T(L.Games.Score), ref scoreRoll,
            board.Score, Accent, theme, deltaSeconds, beatingBest);
        GameHud.Pill(new Vector2(body.Center.X + 20f * scale, rowY), Loc.T(L.Games.Best), GameNumber.Label(displayBest),
            Accent, theme, displayBest > loadedBest);
        if (GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 16f * scale, theme))
        {
            StartNewGame();
            return;
        }

        if (chain > 1)
        {
            var comboPulse = 1f + 0.08f * Pulse.Wave(Pulse.Fast);
            Typography.DrawCentered(new Vector2(body.Center.X, rowY + 28f * scale), comboText, Accent,
                TextStyles.Headline.Scale * comboPulse, TextStyles.Headline.Weight);
        }

        var anim = new GemAnim(phase, swapA, swapB, MathF.Min(1f, swapTimer), MathF.Min(1f, clearTimer),
            MathF.Min(1f, fallTimer), selectedIndex, hintA, hintB, idleTime);
        renderer.Draw(board, grid, anim, theme, scale, Accent, entrance);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
    }

    private void AdvanceAnimation(float deltaSeconds, GameGrid grid)
    {
        switch (phase)
        {
            case GemPhase.Swapping:
                swapTimer += deltaSeconds / SwapDuration;
                if (swapTimer >= 1f)
                {
                    CompleteSwap(grid);
                }

                break;
            case GemPhase.SwapBack:
                swapTimer += deltaSeconds / SwapBackDuration;
                if (swapTimer >= 1f)
                {
                    board.Swap(swapA, swapB);
                    phase = GemPhase.Idle;
                    swapA = -1;
                    swapB = -1;
                    idleTime = 0f;
                }

                break;
            case GemPhase.Clearing:
                clearTimer += deltaSeconds / ClearDuration;
                if (clearTimer >= 1f)
                {
                    board.RemoveMatched();
                    board.ApplyGravity();
                    phase = GemPhase.Falling;
                    fallTimer = 0f;
                }

                break;
            case GemPhase.Falling:
                fallTimer += deltaSeconds / FallDuration;
                if (fallTimer >= 1f)
                {
                    board.ClearFall();
                    chain++;
                    if (board.ResolveMatches(chain) > 0)
                    {
                        OnCleared(grid);
                    }
                    else
                    {
                        chain = 0;
                        board.ReshuffleIfStuck();
                        phase = GemPhase.Idle;
                        idleTime = 0f;
                        hintA = -1;
                        hintB = -1;
                    }
                }

                break;
            case GemPhase.Idle:
                idleTime += deltaSeconds;
                if (idleTime >= HintDelay && hintA < 0)
                {
                    board.FindHint(out hintA, out hintB);
                }

                break;
        }
    }

    private void CompleteSwap(GameGrid grid)
    {
        board.Swap(swapA, swapB);
        if (board.HasAnyMatch())
        {
            chain = 1;
            board.ResolveMatches(chain);
            OnCleared(grid);
        }
        else
        {
            phase = GemPhase.SwapBack;
            swapTimer = 0f;
        }
    }

    private void OnCleared(GameGrid grid)
    {
        phase = GemPhase.Clearing;
        clearTimer = 0f;
        var scale = ImGuiHelpers.GlobalScale;
        var cleared = board.LastClearCount;
        fx.AddTrauma(MathF.Min(0.55f, 0.05f + cleared * 0.03f));
        for (var index = 0; index < GemSwapBoard.CellCount; index++)
        {
            if (!board.Matched(index) || board.Color(index) < 0)
            {
                continue;
            }

            var center = grid.CellCenter(index % GemSwapBoard.Columns, index / GemSwapBoard.Columns);
            var color = GemSwapRenderer.ColorOf(board.Color(index));
            particles.Burst(center, 7, color, 140f * scale, 2.8f, 0.5f, 260f);
            particles.Sparkle(center, 3, GamePalette.Lighten(color, 0.4f), 110f * scale, 2f, 0.6f);
        }

        var scoreDelta = board.Score - previousScore;
        if (scoreDelta > 0)
        {
            fx.AddText($"+{GameNumber.Label(scoreDelta)}",
                new Vector2(grid.Center.X, grid.Bounds.Min.Y - 14f * scale), Accent, 1.1f);
        }

        previousScore = board.Score;
        if (chain > 1)
        {
            fx.AddText($"x{chain}", grid.Center, Accent, 1.5f);
            fx.Shockwave(grid.Center, grid.Pitch * (1.2f + 0.3f * chain), GamePalette.Lighten(Accent, 0.3f), 0.55f, 3f);
            fx.HitStop(MathF.Min(0.08f, 0.03f + chain * 0.01f));
        }
    }

    private void HandleInput(GameGrid grid, float deltaSeconds)
    {
        var mouse = ImGui.GetMousePos();
        if (!grid.Bounds.Contains(mouse) || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            UpdateHoverCursor(grid, mouse);
            return;
        }

        var local = mouse - grid.Origin;
        var column = (int)(local.X / grid.Pitch);
        var row = (int)(local.Y / grid.Pitch);
        if (column < 0 || column >= GemSwapBoard.Columns || row < 0 || row >= GemSwapBoard.Rows)
        {
            return;
        }

        var index = row * GemSwapBoard.Columns + column;
        if (!grid.Cell(column, row).Contains(mouse))
        {
            return;
        }

        idleTime = 0f;
        hintA = -1;
        hintB = -1;
        if (selectedIndex < 0)
        {
            selectedIndex = index;
            return;
        }

        if (selectedIndex == index)
        {
            selectedIndex = -1;
            return;
        }

        if (!GemSwapBoard.AreAdjacent(selectedIndex, index))
        {
            selectedIndex = index;
            return;
        }

        swapA = selectedIndex;
        swapB = index;
        selectedIndex = -1;
        phase = GemPhase.Swapping;
        swapTimer = 0f;
    }

    private void UpdateHoverCursor(GameGrid grid, Vector2 mouse)
    {
        if (grid.Bounds.Contains(mouse))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }
}
