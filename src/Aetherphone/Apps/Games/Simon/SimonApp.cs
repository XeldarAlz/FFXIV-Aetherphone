using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Simon;

internal sealed class SimonApp : IMiniGame
{
    private const string GameId = "simon";
    private const float OnDuration = 0.4f;
    private const float GapDuration = 0.18f;
    private const float StartDelay = 0.55f;
    private const float RewardDuration = 0.55f;
    private const float LitDecay = 4.2f;

    private enum Phase
    {
        Showing,
        Input,
        Reward,
        Over,
    }

    private readonly SimonBoard board = new();
    private readonly SimonRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly float[] lit = new float[SimonBoard.PadCount];
    private RollingValue scoreRoll;
    private Phase phase;
    private int showStep;
    private bool showOn;
    private float phaseTimer;
    private int inputIndex;
    private int score;
    private int bestScore;
    private bool statsLoaded;
    private float rewardTimer;
    private bool pendingSubmit;
    private bool newBest;
    private float resultAppear;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Simon);
    public string Genre => Loc.T(L.Games.GenreMemory);
    public void Open()
    {
        statsLoaded = false;
        StartGame();
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    private void StartGame()
    {
        board.Reset();
        board.AddStep();
        Array.Clear(lit, 0, lit.Length);
        particles.Clear();
        fx.Clear();
        scoreRoll.Snap(0);
        score = 0;
        pendingSubmit = false;
        newBest = false;
        resultAppear = 0f;
        phase = Phase.Showing;
        BeginShow();
    }

    private void BeginShow()
    {
        showStep = 0;
        showOn = false;
        phaseTimer = StartDelay;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var body = context.Body;
        if (!statsLoaded)
        {
            bestScore = context.Stats.Get(GameId).BestScore;
            statsLoaded = true;
        }

        if (pendingSubmit)
        {
            newBest = context.Stats.SubmitScore(GameId, score);
            if (newBest)
            {
                bestScore = score;
            }

            pendingSubmit = false;
        }

        for (var index = 0; index < lit.Length; index++)
        {
            lit[index] = MathF.Max(0f, lit[index] - deltaSeconds * LitDecay);
        }

        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        var rowY = body.Min.Y + 30f * scale;
        var beatingBest = score > 0 && score > bestScore;
        GameHud.ScorePill(new Vector2(body.Center.X - 50f * scale, rowY), Loc.T(L.Games.Score), ref scoreRoll, score,
            Accent, theme, deltaSeconds, beatingBest);
        var bestShown = score > bestScore ? score : bestScore;
        GameHud.Pill(new Vector2(body.Center.X + 50f * scale, rowY), Loc.T(L.Games.Best), GameNumber.Label(bestShown),
            Accent, theme);
        if (GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 16f * scale, theme))
        {
            StartGame();
            return;
        }

        var shake = fx.ShakeOffset(scale);
        var area = new Rect(new Vector2(body.Min.X + 10f * scale, rowY + 28f * scale) + shake,
            new Vector2(body.Max.X - 10f * scale, body.Max.Y - 10f * scale) + shake);
        var grid = GameGrid.Centered(area, 2, 2, 0.08f);
        if (phase == Phase.Showing)
        {
            UpdateShowing(deltaSeconds, grid, scale);
        }
        else if (phase == Phase.Input)
        {
            HandleInput(grid, scale);
        }
        else if (phase == Phase.Reward)
        {
            rewardTimer -= deltaSeconds;
            if (rewardTimer <= 0f)
            {
                board.AddStep();
                phase = Phase.Showing;
                BeginShow();
            }
        }

        var hubLabel = phase == Phase.Showing ? Loc.T(L.Games.Watch) : Loc.T(L.Games.YourTurn);
        renderer.Draw(grid, lit, GameNumber.Label(board.Length), hubLabel, Accent, theme, scale,
            phase == Phase.Showing ? 0.16f : 0f, phase == Phase.Input);
        fx.DrawFlash(drawList, body, 0f);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        if (phase == Phase.Over)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private void UpdateShowing(float deltaSeconds, GameGrid grid, float scale)
    {
        if (showOn && showStep < board.Length)
        {
            lit[board.PadAt(showStep)] = 1f;
        }

        phaseTimer -= deltaSeconds;
        if (phaseTimer > 0f)
        {
            return;
        }

        if (showOn)
        {
            showOn = false;
            phaseTimer = GapDuration;
            showStep++;
            if (showStep >= board.Length)
            {
                phase = Phase.Input;
                inputIndex = 0;
            }

            return;
        }

        if (showStep < board.Length)
        {
            showOn = true;
            phaseTimer = OnDuration;
            var pad = board.PadAt(showStep);
            lit[pad] = 1f;
            var center = SimonRenderer.PadRect(grid, pad).Center;
            fx.Shockwave(center, grid.Pitch * 0.42f, SimonRenderer.ColorOf(pad) with { W = 0.7f }, 0.42f, 2.4f);
        }
    }

    private void HandleInput(GameGrid grid, float scale)
    {
        var hovered = PadHitTest(grid);
        if (hovered >= 0)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left) || hovered < 0)
        {
            return;
        }

        lit[hovered] = 1f;
        if (!board.Matches(inputIndex, hovered))
        {
            OnFail(grid, scale);
            return;
        }

        BurstPad(grid, hovered, 10, scale);
        fx.Shockwave(SimonRenderer.PadRect(grid, hovered).Center, grid.Pitch * 0.5f,
            SimonRenderer.ColorOf(hovered) with { W = 0.8f }, 0.36f, 2.4f);
        inputIndex++;
        if (inputIndex < board.Length)
        {
            return;
        }

        score = board.Length;
        phase = Phase.Reward;
        rewardTimer = RewardDuration;
        fx.AddTrauma(0.12f);
        particles.Sparkle(grid.Center, 14, new Vector4(1f, 0.95f, 0.65f, 1f), 160f * scale, 2.6f, 0.8f);
        fx.Shockwave(grid.Center, grid.Pitch * 0.9f, GamePalette.Lighten(Accent, 0.3f), 0.55f, 3f);
        fx.AddText($"+{GameNumber.Label(board.Length)}", grid.Center - new Vector2(0f, grid.Pitch * 0.3f), Accent,
            1.15f);
    }

    private void OnFail(GameGrid grid, float scale)
    {
        phase = Phase.Over;
        pendingSubmit = true;
        resultAppear = 0f;
        fx.AddTrauma(0.7f);
        fx.HitStop(0.1f);
        fx.Flash(new Vector4(0.95f, 0.3f, 0.3f, 1f), 0.45f);
        fx.Shockwave(grid.Center, grid.Pitch * 1.3f, new Vector4(0.95f, 0.4f, 0.4f, 1f), 0.6f, 3.4f);
        for (var pad = 0; pad < SimonBoard.PadCount; pad++)
        {
            BurstPad(grid, pad, 14, scale);
        }
    }

    private int PadHitTest(GameGrid grid)
    {
        var mouse = ImGui.GetMousePos();
        if (!grid.Bounds.Contains(mouse))
        {
            return -1;
        }

        for (var pad = 0; pad < SimonBoard.PadCount; pad++)
        {
            if (SimonRenderer.PadRect(grid, pad).Contains(mouse))
            {
                return pad;
            }
        }

        return -1;
    }

    private void BurstPad(GameGrid grid, int pad, int count, float scale)
    {
        var center = SimonRenderer.PadRect(grid, pad).Center;
        particles.Burst(center, count, SimonRenderer.ColorOf(pad), 150f * scale, 3f, 0.5f, 240f);
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        string? secondary = null;
        if (bestScore > 0)
        {
            secondary = $"{Loc.T(L.Games.Best)} {GameNumber.Label(bestScore)}";
        }

        var result = new GameResult(Loc.T(L.Games.GameOver), theme.Danger, Loc.T(L.Games.Score),
            GameNumber.Label(score), secondary, newBest);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartGame();
        }
    }
}
