using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Blade;

internal sealed class BladeApp : IMiniGame
{
    private const string GameId = "blade";
    private const float ResultDelay = 0.5f;
    private readonly BladeBoard board = new();
    private readonly BladeRenderer renderer = new();
    private readonly ParticleSystem particles = new(288);
    private readonly FeedbackFx fx = new();
    private RollingValue scoreRoll;
    private float overSeconds;
    private float resultAppear;
    private bool statsLoaded;
    private bool pendingSubmit;
    private bool newBest;
    private int bestScore;
    private int finalScore;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Blade);
    public string Genre => Loc.T(L.Games.GenreArcade);

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
        particles.Clear();
        fx.Clear();
        scoreRoll.Snap(0);
        overSeconds = 0f;
        resultAppear = 0f;
        pendingSubmit = false;
        newBest = false;
    }

    public void Draw(in GameContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var body = context.Body;
        var deltaSeconds = fx.ScaleDelta(context.DeltaSeconds);
        if (!statsLoaded)
        {
            bestScore = context.Stats.Get(GameId).BestScore;
            statsLoaded = true;
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

        var area = new Rect(new Vector2(body.Min.X, body.Min.Y + 58f * scale), body.Max);
        var result = board.Step(deltaSeconds);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        if (result == BladeThrow.Stuck)
        {
            OnStuck(area, scale);
        }
        else if (result == BladeThrow.LevelCleared)
        {
            OnLevelCleared(area, scale);
        }
        else if (result == BladeThrow.Blocked)
        {
            OnBlocked(area, scale);
        }

        if (board.State == BladeState.Over)
        {
            overSeconds += deltaSeconds;
        }

        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        HandleInput(area, theme, scale);
        renderer.Draw(board, area, Accent, fx.ShakeOffset(scale), scale);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        fx.DrawFlash(drawList, body, 0f);
        DrawHud(body, theme, scale, deltaSeconds);
        if (board.State == BladeState.Ready)
        {
            DrawStartHint(area);
        }

        if (board.State == BladeState.Over && overSeconds >= ResultDelay)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private void HandleInput(Rect area, PhoneTheme theme, float scale)
    {
        if (GameHud.RestartButton(new Vector2(area.Max.X - 22f * scale, area.Min.Y - 28f * scale), 16f * scale, theme))
        {
            StartGame();
            return;
        }

        if (board.State == BladeState.Over || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        if (board.State == BladeState.Ready)
        {
            board.Begin();
            return;
        }

        if (board.Throw())
        {
            fx.AddTrauma(0.05f);
        }
    }

    private Vector2 ImpactPoint(Rect area)
    {
        return BladeRenderer.WheelCenterOf(area) + new Vector2(0f, BladeRenderer.WheelRadiusOf(area));
    }

    private void OnStuck(Rect area, float scale)
    {
        var impact = ImpactPoint(area);
        fx.AddTrauma(0.18f);
        fx.HitStop(0.03f);
        fx.Shockwave(impact, 34f * scale, new Vector4(1f, 0.95f, 0.85f, 0.8f), 0.32f, 2.2f);
        particles.Burst(impact, 8, new Vector4(0.72f, 0.54f, 0.38f, 1f), 140f * scale, 2.2f, 0.4f, 420f,
            MathF.PI * 0.9f, MathF.PI * 0.5f);
    }

    private void OnLevelCleared(Rect area, float scale)
    {
        var center = BladeRenderer.WheelCenterOf(area);
        var radius = BladeRenderer.WheelRadiusOf(area);
        fx.HitStop(0.06f);
        fx.AddTrauma(0.32f);
        fx.Flash(GamePalette.Lighten(Accent, 0.4f), 0.28f);
        fx.Shockwave(center, radius * 2.1f, GamePalette.Lighten(Accent, 0.45f), 0.55f, 3.2f, radius);
        fx.AddText($"{Loc.T(L.Games.Level)} {GameNumber.Label(board.Level + 1)}",
            new Vector2(center.X, center.Y - radius - 20f * scale), GamePalette.Lighten(Accent, 0.5f), 1.15f);
        particles.Sparkle(center, 20, GamePalette.Lighten(Accent, 0.5f), 210f * scale, 2.8f, 0.8f);
        for (var index = 0; index < board.StuckCount; index++)
        {
            var angle = board.StuckAngle(index);
            var point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            particles.Streaks(point, 3, new Vector4(0.86f, 0.88f, 0.94f, 1f), 320f * scale, 2.2f, 0.5f, 0.5f, angle);
        }
    }

    private void OnBlocked(Rect area, float scale)
    {
        var impact = ImpactPoint(area);
        finalScore = board.Score;
        pendingSubmit = true;
        overSeconds = 0f;
        resultAppear = 0f;
        fx.Flash(new Vector4(0.95f, 0.32f, 0.32f, 1f), 0.5f);
        fx.AddTrauma(0.75f);
        fx.HitStop(0.07f);
        fx.Shockwave(impact, 130f * scale, new Vector4(1f, 0.78f, 0.42f, 1f), 0.6f, 3.4f);
        particles.Burst(impact, 20, new Vector4(0.88f, 0.90f, 0.96f, 1f), 300f * scale, 3f, 0.7f, 520f);
        particles.Streaks(impact, 12, new Vector4(1f, 0.94f, 0.72f, 1f), 420f * scale, 2.4f, 0.5f);
    }

    private static void DrawStartHint(Rect area)
    {
        var pulse = 1f + 0.05f * Pulse.Wave(Pulse.Calm);
        var center = new Vector2(area.Center.X, area.Min.Y + area.Height * 0.74f);
        Typography.DrawCentered(center, Loc.T(L.Games.TapToStart), new Vector4(1f, 1f, 1f, 0.95f),
            TextStyles.Title2.Scale * pulse, TextStyles.Title2.Weight);
    }

    private void DrawHud(Rect body, PhoneTheme theme, float scale, float deltaSeconds)
    {
        var rowY = body.Min.Y + 30f * scale;
        var beatingBest = board.Score > 0 && board.Score > bestScore;
        GameHud.ScorePill(new Vector2(body.Center.X - 50f * scale, rowY), Loc.T(L.Games.Score), ref scoreRoll,
            board.Score, Accent, theme, deltaSeconds, beatingBest);
        var bestShown = board.Score > bestScore ? board.Score : bestScore;
        GameHud.Pill(new Vector2(body.Center.X + 50f * scale, rowY), Loc.T(L.Games.Best), GameNumber.Label(bestShown),
            Accent, theme);
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        var secondary = $"{Loc.T(L.Games.Level)} {GameNumber.Label(board.Level)}";
        var result = new GameResult(Loc.T(L.Games.GameOver), theme.Danger, Loc.T(L.Games.Score),
            GameNumber.Label(finalScore), secondary, newBest);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartGame();
        }
    }
}
