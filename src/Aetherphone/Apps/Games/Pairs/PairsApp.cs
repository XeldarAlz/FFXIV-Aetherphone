using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Pairs;

internal sealed class PairsApp : IMiniGame
{
    private const string GameId = "memory";

    private const string AttemptsStatId = "memory.attempts";

    private const float FlipDuration = 0.22f;

    private const float RevealDelay = 0.55f;

    private const float CelebrateDuration = 0.45f;

    private const float ShakeDuration = 0.35f;

    private enum Phase
    {
        Selecting,
        Revealing,
        Celebrating,
        Shaking,
        FlippingBack,
        Won,
    }

    private readonly PairsBoard board = new();

    private readonly PairsRenderer renderer = new();

    private readonly ParticleSystem particles = new();

    private readonly FeedbackFx fx = new();

    private readonly float[] flipProgress = new float[PairsBoard.CardCount];

    private readonly float[] flipTarget = new float[PairsBoard.CardCount];

    private readonly float[] matchGlow = new float[PairsBoard.CardCount];

    private readonly float[] shakePhase = new float[PairsBoard.CardCount];

    private Phase phase;

    private float phaseTimer;

    private float elapsed;

    private bool matchBurstPending;

    private int streak;

    private float resultAppear;

    private string resultTimeText = "0:00";

    private bool newBestTime;

    private bool pendingWinSubmit;

    private bool statsLoaded;

    private GameStats loadedStats;

    public string Id => GameId;

    public string Title => Loc.T(L.Games.Pairs);

    public Vector4 Accent => Styling.AccentAmber;

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
        particles.Clear();
        fx.Clear();
        for (var index = 0; index < PairsBoard.CardCount; index++)
        {
            flipProgress[index] = 0f;
            flipTarget[index] = 0f;
            matchGlow[index] = 0f;
            shakePhase[index] = 0f;
        }

        phase = Phase.Selecting;
        phaseTimer = 0f;
        elapsed = 0f;
        matchBurstPending = false;
        resultAppear = 0f;
        newBestTime = false;
        pendingWinSubmit = false;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var body = context.Body;

        if (!statsLoaded)
        {
            loadedStats = context.Stats.Get(GameId);
            streak = loadedStats.Streak;
            statsLoaded = true;
        }

        if (pendingWinSubmit)
        {
            var seconds = (int)elapsed;
            newBestTime = context.Stats.SubmitTime(GameId, seconds);
            context.Stats.SubmitTime(AttemptsStatId, board.Attempts);
            streak = context.Stats.RecordWin(GameId);
            pendingWinSubmit = false;
        }

        if (phase != Phase.Won)
        {
            elapsed += deltaSeconds;
        }

        UpdateAnimations(deltaSeconds);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);

        var rowY = body.Min.Y + 32f * scale;
        GameHud.Pill(new Vector2(body.Center.X - 58f * scale, rowY), Loc.T(L.Games.Attempts), GameNumber.Label(board.Attempts), Accent, theme);
        GameHud.Pill(new Vector2(body.Center.X + 38f * scale, rowY), Loc.T(L.Games.Time), GameNumber.Label((int)elapsed), Accent, theme);
        if (GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 16f * scale, theme))
        {
            if (phase != Phase.Won)
            {
                context.Stats.ResetStreak(GameId);
                streak = 0;
            }

            StartNewGame();
            return;
        }

        var gridArea = new Rect(new Vector2(body.Min.X, body.Min.Y + 70f * scale), new Vector2(body.Max.X, body.Max.Y - 8f * scale));
        var grid = GameGrid.Centered(gridArea, PairsBoard.Columns, PairsBoard.Rows, 0.10f);

        if (matchBurstPending)
        {
            EmitMatchBurst(grid);
            matchBurstPending = false;
        }

        DrawCards(grid, theme, scale);

        var drawList = ImGui.GetWindowDrawList();
        particles.Draw(drawList, scale);
        fx.DrawText();

        if (phase == Phase.Won)
        {
            DrawResult(context, theme, body);
        }
    }

    private void DrawCards(GameGrid grid, PhoneTheme theme, float scale)
    {
        for (var row = 0; row < PairsBoard.Rows; row++)
        {
            for (var column = 0; column < PairsBoard.Columns; column++)
            {
                var index = row * PairsBoard.Columns + column;
                var cell = grid.Cell(column, row);

                var hovered = phase == Phase.Selecting
                    && board.CanReveal(index)
                    && flipProgress[index] < 0.02f
                    && ImGui.IsMouseHoveringRect(cell.Min, cell.Max);

                var shakeX = shakePhase[index] > 0f
                    ? MathF.Sin(shakePhase[index] * MathF.PI * 8f) * 5f * scale * shakePhase[index]
                    : 0f;

                renderer.DrawCard(cell, board.Symbol(index), board.State(index), flipProgress[index], matchGlow[index], shakeX, hovered, theme, scale);

                if (hovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        OnCardClicked(index);
                    }
                }
            }
        }
    }

    private void OnCardClicked(int index)
    {
        if (!board.CanReveal(index))
        {
            return;
        }

        if (board.FirstCard < 0)
        {
            board.RevealFirst(index);
            flipTarget[index] = 1f;
            return;
        }

        board.RevealSecond(index);
        flipTarget[index] = 1f;
        phase = Phase.Revealing;
        phaseTimer = 0f;
    }

    private void UpdateAnimations(float deltaSeconds)
    {
        for (var index = 0; index < PairsBoard.CardCount; index++)
        {
            var target = flipTarget[index];
            var current = flipProgress[index];
            if (MathF.Abs(current - target) >= 0.001f)
            {
                var step = deltaSeconds / FlipDuration;
                flipProgress[index] = Math.Clamp(current + (target > current ? step : -step), 0f, 1f);
            }
            else
            {
                flipProgress[index] = target;
            }

            if (matchGlow[index] > 0f)
            {
                matchGlow[index] = MathF.Max(0f, matchGlow[index] - deltaSeconds / CelebrateDuration);
            }

            if (shakePhase[index] > 0f)
            {
                shakePhase[index] = MathF.Max(0f, shakePhase[index] - deltaSeconds / ShakeDuration);
            }
        }

        AdvancePhase(deltaSeconds);
    }

    private void AdvancePhase(float deltaSeconds)
    {
        switch (phase)
        {
            case Phase.Revealing:
                phaseTimer += deltaSeconds;
                if (phaseTimer >= RevealDelay)
                {
                    ResolveSelection();
                }

                break;

            case Phase.Celebrating:
                phaseTimer += deltaSeconds;
                if (phaseTimer >= CelebrateDuration)
                {
                    board.ConfirmMatch();
                    if (board.AllMatched())
                    {
                        OnWin();
                    }
                    else
                    {
                        phase = Phase.Selecting;
                    }
                }

                break;

            case Phase.Shaking:
                phaseTimer += deltaSeconds;
                if (phaseTimer >= ShakeDuration)
                {
                    flipTarget[board.FirstCard] = 0f;
                    flipTarget[board.SecondCard] = 0f;
                    phase = Phase.FlippingBack;
                }

                break;

            case Phase.FlippingBack:
                if (flipProgress[board.FirstCard] <= 0.001f && flipProgress[board.SecondCard] <= 0.001f)
                {
                    board.HideSelection();
                    phase = Phase.Selecting;
                }

                break;

            case Phase.Won:
                resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
                break;
        }
    }

    private void ResolveSelection()
    {
        if (board.SelectionMatches)
        {
            matchGlow[board.FirstCard] = 1f;
            matchGlow[board.SecondCard] = 1f;
            matchBurstPending = true;
            phase = Phase.Celebrating;
            phaseTimer = 0f;
            return;
        }

        shakePhase[board.FirstCard] = 1f;
        shakePhase[board.SecondCard] = 1f;
        phase = Phase.Shaking;
        phaseTimer = 0f;
    }

    private void EmitMatchBurst(GameGrid grid)
    {
        EmitAtCard(grid, board.FirstCard);
        EmitAtCard(grid, board.SecondCard);
    }

    private void EmitAtCard(GameGrid grid, int index)
    {
        if (index < 0)
        {
            return;
        }

        var center = grid.CellCenter(index % PairsBoard.Columns, index / PairsBoard.Columns);
        particles.Burst(center, 14, PairsRenderer.ColorFor(board.Symbol(index)), 170f * ImGuiHelpers.GlobalScale, 3.2f, 0.6f, 240f);
    }

    private void OnWin()
    {
        phase = Phase.Won;
        resultAppear = 0f;
        pendingWinSubmit = true;

        var seconds = (int)elapsed;
        var minutes = seconds / 60;
        resultTimeText = $"{minutes}:{seconds % 60:D2}";
    }

    private void DrawResult(in GameContext context, PhoneTheme theme, Rect body)
    {
        var secondaryParts = $"{Loc.Plural(L.Games.AttemptsCount, board.Attempts)}  ·  {Loc.T(L.Games.Streak)} {GameNumber.Label(streak)}";
        var result = new GameResult(Loc.T(L.Games.YouWin), Accent, Loc.T(L.Games.Time), resultTimeText, secondaryParts, newBestTime);

        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartNewGame();
        }
    }
}
