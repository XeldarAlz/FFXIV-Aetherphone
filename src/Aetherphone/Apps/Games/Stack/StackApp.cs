using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Stack;

internal sealed class StackApp : IMiniGame
{
    private const string GameId = "stack";
    private const float CameraFollowSpeed = 8.5f;
    private const float ResultDelay = 0.45f;
    private readonly StackBoard board = new();
    private readonly StackRenderer renderer = new();
    private readonly ParticleSystem particles = new(256);
    private readonly FeedbackFx fx = new();
    private RollingValue scoreRoll;
    private string comboLabel = string.Empty;
    private int comboShown;
    private float camera;
    private float comboPulse;
    private float overSeconds;
    private float resultAppear;
    private bool statsLoaded;
    private bool pendingSubmit;
    private bool newBest;
    private int bestScore;
    private int finalScore;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Stack);
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
        camera = board.Level;
        comboShown = 0;
        comboLabel = string.Empty;
        comboPulse = 0f;
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
        board.Step(deltaSeconds);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        camera += (board.Level - camera) * MathF.Min(1f, deltaSeconds * CameraFollowSpeed);
        comboPulse = MathF.Max(0f, comboPulse - deltaSeconds * 2.6f);
        if (board.State == StackState.Over)
        {
            overSeconds += deltaSeconds;
        }

        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        HandleInput(area, theme, scale);
        var shake = fx.ShakeOffset(scale);
        renderer.Draw(board, area, camera, shake, scale);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        fx.DrawFlash(drawList, body, 0f);
        DrawCombo(area, scale);
        DrawHud(body, theme, scale, deltaSeconds);
        if (board.State == StackState.Ready)
        {
            DrawStartHint(area);
        }

        if (board.State == StackState.Over && overSeconds >= ResultDelay)
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

        if (board.State == StackState.Over || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        if (board.State == StackState.Ready)
        {
            board.Begin();
            return;
        }

        var result = board.Drop();
        if (result == StackDrop.Perfect)
        {
            OnPerfect(area, scale);
            return;
        }

        if (result == StackDrop.Placed)
        {
            OnPlaced(area, scale);
            return;
        }

        if (result == StackDrop.Missed)
        {
            OnMissed(area, scale);
        }
    }

    private void OnPlaced(Rect area, float scale)
    {
        comboShown = 0;
        var levelHeight = StackRenderer.LevelHeightOf(area);
        var slicePosition = StackRenderer.ScreenPosition(area, camera, board.LastSliceCenterX, board.Level - 1);
        var color = StackRenderer.ColorOf(board.Level - 1);
        particles.Burst(new Vector2(slicePosition.X, slicePosition.Y - levelHeight * 0.5f), 10, color, 150f * scale, 2.6f,
            0.45f, 320f);
        fx.AddTrauma(0.10f);
    }

    private void OnPerfect(Rect area, float scale)
    {
        comboShown = board.Combo;
        comboLabel = "x" + GameNumber.Label(board.Combo);
        comboPulse = 1f;
        var top = StackRenderer.ScreenPosition(area, camera, 0.5f, board.Level - 1);
        var color = StackRenderer.ColorOf(board.Level - 1);
        var center = new Vector2(area.Min.X + board.Block(board.Level - 1).CenterX * area.Width, top.Y);
        fx.HitStop(0.045f);
        fx.AddTrauma(0.22f);
        fx.Shockwave(center, area.Width * 0.42f, GamePalette.Lighten(color, 0.4f), 0.5f, 3.2f);
        fx.AddText(Loc.T(L.Games.Perfect), new Vector2(center.X, center.Y - 26f * scale),
            GamePalette.Lighten(color, 0.45f), 1.15f);
        particles.Sparkle(center, 14, GamePalette.Lighten(color, 0.5f), 190f * scale, 2.8f, 0.7f);
        particles.Burst(center, 8, new Vector4(1f, 1f, 1f, 0.9f), 130f * scale, 2.2f, 0.4f, 120f);
    }

    private void OnMissed(Rect area, float scale)
    {
        finalScore = board.Score;
        pendingSubmit = true;
        overSeconds = 0f;
        resultAppear = 0f;
        comboShown = 0;
        var top = StackRenderer.ScreenPosition(area, camera, board.MovingCenterX, board.Level);
        fx.Flash(new Vector4(0.95f, 0.32f, 0.32f, 1f), 0.42f);
        fx.AddTrauma(0.65f);
        fx.Shockwave(top, area.Width * 0.6f, new Vector4(1f, 0.72f, 0.4f, 1f), 0.6f, 3.4f);
        particles.Burst(top, 22, StackRenderer.ColorOf(board.Level), 260f * scale, 3.6f, 0.7f, 420f);
        particles.Streaks(top, 10, new Vector4(1f, 0.92f, 0.66f, 1f), 380f * scale, 2.4f, 0.5f);
    }

    private void DrawCombo(Rect area, float scale)
    {
        if (comboShown < 2)
        {
            return;
        }

        var pop = 1f + 0.32f * Easing.EaseOutCubic(comboPulse);
        var center = new Vector2(area.Center.X, area.Min.Y + area.Height * 0.16f);
        var color = StackRenderer.ColorOf(board.Level);
        Typography.DrawCentered(center + new Vector2(1.5f * scale, 1.5f * scale), comboLabel,
            new Vector4(0f, 0f, 0f, 0.3f), TextStyles.Title1.Scale * pop, TextStyles.Title1.Weight);
        Typography.DrawCentered(center, comboLabel, GamePalette.Lighten(color, 0.45f), TextStyles.Title1.Scale * pop,
            TextStyles.Title1.Weight);
    }

    private static void DrawStartHint(Rect area)
    {
        var pulse = 1f + 0.05f * Pulse.Wave(Pulse.Calm);
        var center = new Vector2(area.Center.X, area.Min.Y + area.Height * 0.30f);
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
        string? secondary = null;
        if (bestScore > 0)
        {
            secondary = $"{Loc.T(L.Games.Best)} {GameNumber.Label(bestScore)}";
        }

        var result = new GameResult(Loc.T(L.Games.GameOver), theme.Danger, Loc.T(L.Games.Score),
            GameNumber.Label(finalScore), secondary, newBest);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartGame();
        }
    }
}
