using System;
using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Snake;

internal sealed class SnakeApp : IMiniGame
{
    private const string GameId = "snake";

    private readonly SnakeBoard board = new();

    private readonly SnakeRenderer renderer = new();

    private readonly ParticleSystem particles = new();

    private readonly FeedbackFx fx = new();

    private bool started;

    private bool statsLoaded;

    private int bestScore;

    private bool pendingSubmit;

    private bool newBest;

    private int finalScore;

    private float resultAppear;

    public string Id => GameId;

    public string Title => Loc.T(L.Games.Snake);

    public string Genre => Loc.T(L.Games.GenreArcade);

    public Vector4 Accent => new(0.42f, 0.84f, 0.48f, 1f);

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
        var area = new Rect(new Vector2(body.Min.X + 6f * scale, body.Min.Y + 56f * scale), new Vector2(body.Max.X - 6f * scale, body.Max.Y - 8f * scale));

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

        var mouse = ImGui.GetMousePos();
        var crashed = board.Step(deltaSeconds, area, mouse);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);

        if (board.State == SnakeState.Ready && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            board.Begin(mouse);
        }

        if (board.AteLastStep)
        {
            particles.Burst(board.Head, 14, Accent, 170f * scale, 3f, 0.5f, 220f);
            fx.AddTrauma(0.1f);
        }

        if (crashed)
        {
            OnCrash(scale);
        }

        renderer.Draw(board, area, scale);

        var drawList = ImGui.GetWindowDrawList();
        particles.Draw(drawList, scale);
        fx.DrawFlash(drawList, body, 0f);

        DrawHud(body, area, theme, scale);

        if (board.State == SnakeState.Over)
        {
            DrawResult(theme, body, deltaSeconds, area);
        }
    }

    private void OnCrash(float scale)
    {
        finalScore = board.Score;
        pendingSubmit = true;
        resultAppear = 0f;
        fx.AddTrauma(0.6f);
        fx.Flash(new Vector4(0.95f, 0.3f, 0.3f, 1f), 0.45f);
        particles.Burst(board.Head, 26, Accent, 280f * scale, 4f, 0.8f, 360f);
    }

    private void DrawHud(Rect body, Rect area, PhoneTheme theme, float scale)
    {
        var rowY = body.Min.Y + 30f * scale;
        GameHud.Pill(new Vector2(body.Center.X - 50f * scale, rowY), Loc.T(L.Games.Score), GameNumber.Label(board.Score), Accent, theme);
        GameHud.Pill(new Vector2(body.Center.X + 50f * scale, rowY), Loc.T(L.Games.Best), GameNumber.Label(bestScore), Accent, theme);

        if (board.State != SnakeState.Over && GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 16f * scale, theme))
        {
            Restart(area);
        }

        if (board.State == SnakeState.Ready)
        {
            Typography.DrawCentered(new Vector2(area.Center.X, area.Center.Y - area.Height * 0.16f), Loc.T(L.Games.TapToStart), new Vector4(1f, 1f, 1f, 0.92f), TextStyles.Title2);
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

        var result = new GameResult(Loc.T(L.Games.GameOver), theme.Danger, Loc.T(L.Games.Score), GameNumber.Label(finalScore), secondary, newBest);

        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            Restart(area);
        }
    }
}
