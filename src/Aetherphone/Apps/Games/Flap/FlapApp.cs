using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Flap;

internal sealed class FlapApp : IMiniGame
{
    private const string GameId = "flap";
    private const float TrailInterval = 0.05f;
    private readonly FlapBoard board = new();
    private readonly FlapRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private Spring tiltSpring = new(0f);
    private bool started;
    private bool statsLoaded;
    private int bestScore;
    private bool pendingSubmit;
    private bool newBest;
    private int finalScore;
    private float resultAppear;
    private float scorePulse;
    private float flapPulse;
    private float trailTimer;
    private int previousScore;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Flap);
    public string Genre => Loc.T(L.Games.GenreArcade);
    public void Open()
    {
        started = false;
        statsLoaded = false;
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    private void Restart(Rect area)
    {
        board.Reset(area);
        particles.Clear();
        fx.Clear();
        tiltSpring.SnapTo(0f);
        pendingSubmit = false;
        newBest = false;
        resultAppear = 0f;
        scorePulse = 0f;
        flapPulse = 0f;
        previousScore = 0;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var body = context.Body;
        var area = new Rect(new Vector2(body.Min.X, body.Min.Y + 2f * scale),
            new Vector2(body.Max.X, body.Max.Y - 2f * scale));
        if (!statsLoaded)
        {
            bestScore = context.Stats.Get(GameId).BestScore;
            statsLoaded = true;
        }

        if (!started)
        {
            board.Reset(area);
            previousScore = 0;
            started = true;
        }

        if (pendingSubmit)
        {
            newBest = context.Stats.SubmitScore(GameId, finalScore);
            if (newBest)
            {
                bestScore = finalScore;
            }

            pendingSubmit = false;
        }

        var crashed = board.Step(deltaSeconds, area);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        scorePulse = MathF.Max(0f, scorePulse - deltaSeconds * 4f);
        flapPulse = MathF.Max(0f, flapPulse - deltaSeconds * 5f);
        if (board.State != FlapState.Over)
        {
            HandleInput(area, theme, scale);
        }

        if (board.Score > previousScore)
        {
            OnScore(area, scale);
        }

        previousScore = board.Score;
        if (crashed)
        {
            OnCrash(area, scale);
        }

        EmitTrail(deltaSeconds, area, scale);
        var velocityFraction = board.BirdVelocity / (2f * area.Height);
        var tilt = tiltSpring.Step(Math.Clamp(velocityFraction * 1.3f, -0.5f, 1.15f), 0.05f, deltaSeconds);
        var displayY = board.BirdY;
        if (board.State == FlapState.Ready)
        {
            displayY += MathF.Sin(Pulse.Phase(1700.0) * MathF.PI * 2f) * area.Height * 0.02f;
        }

        var shake = fx.ShakeOffset(scale);
        renderer.Draw(board, area, displayY, tilt, scale, shake, flapPulse);
        var drawList = ImGui.GetWindowDrawList();
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawFlash(drawList, body, 0f);
        DrawHud(body, area, theme, scale);
        if (board.State == FlapState.Over)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private void HandleInput(Rect area, PhoneTheme theme, float scale)
    {
        if (GameHud.RestartButton(new Vector2(area.Max.X - 22f * scale, area.Min.Y + 24f * scale), 16f * scale, theme))
        {
            Restart(area);
            return;
        }

        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        board.Flap(area);
        flapPulse = 1f;
        var birdX = FlapBoard.BirdXOf(area);
        var radius = FlapBoard.RadiusOf(area);
        particles.Burst(new Vector2(birdX - radius * 0.6f, board.BirdY + radius * 0.5f), 6,
            new Vector4(1f, 1f, 1f, 0.85f), 90f * scale, 2.4f, 0.4f, 120f, 1.1f, 2.5f);
    }

    private void OnScore(Rect area, float scale)
    {
        scorePulse = 1f;
        var bird = new Vector2(FlapBoard.BirdXOf(area), board.BirdY);
        fx.Shockwave(bird, 52f * scale, new Vector4(1f, 1f, 1f, 0.9f), 0.42f, 2.6f);
        particles.Sparkle(bird, 8, new Vector4(1f, 0.95f, 0.6f, 1f), 130f * scale, 2.4f, 0.7f);
    }

    private void EmitTrail(float deltaSeconds, Rect area, float scale)
    {
        if (board.State != FlapState.Playing)
        {
            return;
        }

        trailTimer -= deltaSeconds;
        if (trailTimer > 0f)
        {
            return;
        }

        trailTimer = TrailInterval;
        var radius = FlapBoard.RadiusOf(area);
        var bird = new Vector2(FlapBoard.BirdXOf(area) - radius * 0.9f, board.BirdY);
        particles.Burst(bird, 1, new Vector4(1f, 0.9f, 0.55f, 0.4f), 20f * scale, radius * 0.14f, 0.3f, 0f,
            MathF.PI * 2f, 0f, ParticleShape.GlowCircle);
    }

    private void OnCrash(Rect area, float scale)
    {
        finalScore = board.Score;
        pendingSubmit = true;
        resultAppear = 0f;
        fx.Flash(new Vector4(0.95f, 0.3f, 0.3f, 1f), 0.5f);
        fx.AddTrauma(0.6f);
        var bird = new Vector2(FlapBoard.BirdXOf(area), board.BirdY);
        fx.Shockwave(bird, 120f * scale, new Vector4(1f, 0.8f, 0.4f, 1f), 0.6f, 3.4f);
        particles.Burst(bird, 26, new Vector4(0.98f, 0.7f, 0.3f, 1f), 280f * scale, 4f, 0.8f, 420f);
        particles.Streaks(bird, 12, new Vector4(1f, 0.92f, 0.6f, 1f), 420f * scale, 2.6f, 0.5f);
        particles.Burst(bird, 10, new Vector4(1f, 1f, 1f, 0.9f), 160f * scale, 3f, 0.6f, 200f, MathF.PI * 2f, 0f,
            ParticleShape.Square);
    }

    private void DrawHud(Rect body, Rect area, PhoneTheme theme, float scale)
    {
        if (board.State == FlapState.Playing)
        {
            var pop = 1f + 0.28f * Easing.EaseOutCubic(scorePulse);
            var scoreCenter = new Vector2(area.Center.X, area.Min.Y + 44f * scale);
            Typography.DrawCentered(scoreCenter + new Vector2(1.5f * scale, 1.5f * scale),
                GameNumber.Label(board.Score), new Vector4(0f, 0f, 0f, 0.35f), TextStyles.LargeTitle.Scale * pop,
                TextStyles.LargeTitle.Weight);
            Typography.DrawCentered(scoreCenter, GameNumber.Label(board.Score), new Vector4(1f, 1f, 1f, 1f),
                TextStyles.LargeTitle.Scale * pop, TextStyles.LargeTitle.Weight);
        }

        if (board.State == FlapState.Ready)
        {
            var pulse = 1f + 0.05f * Pulse.Wave(Pulse.Calm);
            var hintCenter = new Vector2(area.Center.X, area.Center.Y - area.Height * 0.16f);
            Typography.DrawCentered(hintCenter, Loc.T(L.Games.TapToStart), new Vector4(1f, 1f, 1f, 0.95f),
                TextStyles.Title2.Scale * pulse, TextStyles.Title2.Weight);
        }

        if (board.State != FlapState.Over)
        {
            var beatingBest = board.Score > 0 && board.Score > bestScore;
            var bestShown = board.Score > bestScore ? board.Score : bestScore;
            GameHud.Pill(new Vector2(area.Min.X + 42f * scale, area.Min.Y + 24f * scale), Loc.T(L.Games.Best),
                GameNumber.Label(bestShown), Accent, theme, beatingBest);
        }
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
            GameNumber.Label(finalScore), secondary, newBest);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            var area = new Rect(new Vector2(body.Min.X, body.Min.Y + 2f * ImGuiHelpers.GlobalScale),
                new Vector2(body.Max.X, body.Max.Y - 2f * ImGuiHelpers.GlobalScale));
            Restart(area);
        }
    }
}
