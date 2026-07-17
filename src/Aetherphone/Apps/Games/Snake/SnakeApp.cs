using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Animation;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Snake;

internal sealed class SnakeApp : IMiniGame
{
    private const string GameId = "snake";
    private const float TrailInterval = 0.045f;
    private readonly SnakeBoard board = new();
    private readonly SnakeRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private RollingValue scoreRoll;
    private bool started;
    private bool statsLoaded;
    private int bestScore;
    private bool pendingSubmit;
    private bool newBest;
    private int finalScore;
    private float resultAppear;
    private float eatPulse;
    private float trailTimer;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Snake);
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
        scoreRoll.Snap(0);
        pendingSubmit = false;
        newBest = false;
        resultAppear = 0f;
        eatPulse = 0f;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var body = context.Body;
        var area = new Rect(new Vector2(body.Min.X + 6f * scale, body.Min.Y + 56f * scale),
            new Vector2(body.Max.X - 6f * scale, body.Max.Y - 8f * scale));
        if (!statsLoaded)
        {
            bestScore = context.Stats.Get(GameId).BestScore;
            statsLoaded = true;
        }

        if (!started)
        {
            board.Reset(area);
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

        var mouse = ImGui.GetMousePos();
        var simDelta = fx.ScaleDelta(deltaSeconds);
        var crashed = board.Step(simDelta, area, mouse);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        eatPulse = MathF.Max(0f, eatPulse - deltaSeconds * 3.4f);
        if (board.State == SnakeState.Ready && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            board.Begin(mouse);
        }

        if (board.AteLastStep)
        {
            OnEat(scale);
        }

        if (crashed)
        {
            OnCrash(scale);
        }

        EmitTrail(simDelta, area);
        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        var shake = fx.ShakeOffset(scale);
        renderer.Draw(board, area, scale, shake, eatPulse);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawFlash(drawList, body, 0f);
        fx.DrawText();
        DrawHud(body, area, theme, scale, deltaSeconds);
        if (board.State == SnakeState.Over)
        {
            DrawResult(theme, body, deltaSeconds, area);
        }
    }

    private void OnEat(float scale)
    {
        eatPulse = 1f;
        particles.Burst(board.Head, 12, GamePalette.Lighten(Accent, 0.2f), 190f * scale, 3f, 0.5f, 220f);
        particles.Sparkle(board.Head, 6, new Vector4(1f, 0.95f, 0.7f, 1f), 120f * scale, 2.2f, 0.6f);
        fx.Shockwave(board.Head, 42f * scale, GamePalette.Lighten(Accent, 0.35f), 0.4f, 2.6f);
        fx.AddTrauma(0.12f);
        fx.HitStop(0.045f);
    }

    private void EmitTrail(float deltaSeconds, Rect area)
    {
        if (board.State != SnakeState.Playing || deltaSeconds <= 0f)
        {
            return;
        }

        trailTimer -= deltaSeconds;
        if (trailTimer > 0f)
        {
            return;
        }

        trailTimer = TrailInterval;
        particles.Burst(board.Head, 1, GamePalette.Lighten(Accent, 0.15f) with { W = 0.35f },
            14f, SnakeBoard.SegRadiusOf(area) * 0.16f, 0.34f, 0f, MathF.PI * 2f, 0f, ParticleShape.GlowCircle);
    }

    private void OnCrash(float scale)
    {
        finalScore = board.Score;
        pendingSubmit = true;
        resultAppear = 0f;
        fx.AddTrauma(0.6f);
        fx.HitStop(0.12f);
        fx.Flash(new Vector4(0.95f, 0.3f, 0.3f, 1f), 0.45f);
        fx.Shockwave(board.Head, 110f * scale, new Vector4(0.98f, 0.55f, 0.45f, 1f), 0.6f, 3.4f);
        particles.Burst(board.Head, 30, Accent, 300f * scale, 4f, 0.8f, 360f);
        particles.Streaks(board.Head, 14, new Vector4(0.98f, 0.72f, 0.5f, 1f), 420f * scale, 2.6f, 0.5f);
    }

    private void DrawHud(Rect body, Rect area, PhoneTheme theme, float scale, float deltaSeconds)
    {
        var rowY = body.Min.Y + 30f * scale;
        var beatingBest = board.Score > 0 && board.Score > bestScore;
        GameHud.ScorePill(new Vector2(body.Center.X - 50f * scale, rowY), Loc.T(L.Games.Score), ref scoreRoll,
            board.Score, Accent, theme, deltaSeconds, beatingBest);
        var bestShown = board.Score > bestScore ? board.Score : bestScore;
        GameHud.Pill(new Vector2(body.Center.X + 50f * scale, rowY), Loc.T(L.Games.Best), GameNumber.Label(bestShown),
            Accent, theme);
        if (board.State != SnakeState.Over &&
            GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 16f * scale, theme))
        {
            Restart(area);
        }

        if (board.State == SnakeState.Ready)
        {
            var pulse = 1f + 0.05f * Pulse.Wave(Pulse.Calm);
            Typography.DrawCentered(new Vector2(area.Center.X, area.Center.Y - area.Height * 0.16f),
                Loc.T(L.Games.TapToStart), new Vector4(1f, 1f, 1f, 0.92f), TextStyles.Title2.Scale * pulse,
                TextStyles.Title2.Weight);
        }
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds, Rect area)
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
            Restart(area);
        }
    }
}
