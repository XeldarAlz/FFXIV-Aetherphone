using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Flap;

internal sealed class FlapApp : IMiniGame
{
    private const string GameId = "flap";
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
            started = true;
        }

        if (pendingSubmit)
        {
            newBest = context.Stats.SubmitScore(GameId, finalScore);
            pendingSubmit = false;
        }

        var crashed = board.Step(deltaSeconds, area);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        if (board.State != FlapState.Over)
        {
            HandleInput(area, theme, scale);
        }

        if (crashed)
        {
            OnCrash(area, scale);
        }

        var velocityFraction = board.BirdVelocity / (2f * area.Height);
        var tilt = tiltSpring.Step(Math.Clamp(velocityFraction * 1.3f, -0.5f, 1.15f), 0.05f, deltaSeconds);
        var displayY = board.BirdY;
        if (board.State == FlapState.Ready)
        {
            displayY += MathF.Sin(Styling.Phase(1700.0) * MathF.PI * 2f) * area.Height * 0.02f;
        }

        renderer.Draw(board, area, displayY, tilt, scale);
        var drawList = ImGui.GetWindowDrawList();
        particles.Draw(drawList, scale);
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
        var birdX = FlapBoard.BirdXOf(area);
        var radius = FlapBoard.RadiusOf(area);
        particles.Burst(new Vector2(birdX - radius * 0.6f, board.BirdY + radius * 0.5f), 6,
            new Vector4(1f, 1f, 1f, 0.85f), 90f * scale, 2.4f, 0.4f, 120f, 1.1f, 2.5f);
    }

    private void OnCrash(Rect area, float scale)
    {
        finalScore = board.Score;
        pendingSubmit = true;
        resultAppear = 0f;
        fx.Flash(new Vector4(0.95f, 0.3f, 0.3f, 1f), 0.5f);
        var birdX = FlapBoard.BirdXOf(area);
        particles.Burst(new Vector2(birdX, board.BirdY), 26, new Vector4(0.98f, 0.7f, 0.3f, 1f), 280f * scale, 4f, 0.8f,
            420f);
    }

    private void DrawHud(Rect body, Rect area, PhoneTheme theme, float scale)
    {
        if (board.State == FlapState.Playing)
        {
            var scoreCenter = new Vector2(area.Center.X, area.Min.Y + 44f * scale);
            Typography.DrawCentered(scoreCenter + new Vector2(1.5f * scale, 1.5f * scale),
                GameNumber.Label(board.Score), new Vector4(0f, 0f, 0f, 0.35f), TextStyles.LargeTitle);
            Typography.DrawCentered(scoreCenter, GameNumber.Label(board.Score), new Vector4(1f, 1f, 1f, 1f),
                TextStyles.LargeTitle);
        }

        if (board.State == FlapState.Ready)
        {
            var hintCenter = new Vector2(area.Center.X, area.Center.Y - area.Height * 0.16f);
            Typography.DrawCentered(hintCenter, Loc.T(L.Games.TapToStart), new Vector4(1f, 1f, 1f, 0.95f),
                TextStyles.Title2);
        }

        if (board.State != FlapState.Over)
        {
            GameHud.Pill(new Vector2(area.Min.X + 42f * scale, area.Min.Y + 24f * scale), Loc.T(L.Games.Best),
                GameNumber.Label(bestScore), Accent, theme);
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
