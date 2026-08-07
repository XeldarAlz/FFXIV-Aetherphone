using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Games.Trivia;

internal sealed class TriviaApp : IMiniGame
{
    private const string GameId = "trivia";
    private readonly TriviaBoard board;
    private readonly TriviaRenderer renderer = new();
    private readonly ParticleSystem particles = new(256);
    private readonly FeedbackFx fx = new();
    private readonly ITextureProvider textures;
    private RollingValue scoreRoll;
    private float resultAppear;
    private bool statsLoaded;
    private bool wasOver;
    private bool pendingSubmit;
    private bool newBest;
    private int bestScore;
    private int finalScore;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Trivia);
    public bool RunsOnAClock => true;

    public string Genre => Loc.T(L.Games.GenreMemory);

    public TriviaApp(GameData gameData, ITextureProvider textures)
    {
        this.textures = textures;
        board = new TriviaBoard(gameData);
    }

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
        resultAppear = 0f;
        wasOver = false;
        pendingSubmit = false;
        newBest = false;
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
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

        var area = new Rect(new Vector2(body.Min.X + 4f * scale, body.Min.Y + 62f * scale),
            new Vector2(body.Max.X - 4f * scale, body.Max.Y - 6f * scale));
        if (board.Step(deltaSeconds))
        {
            OnTimeout(area, scale);
        }

        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        if (board.State == TriviaState.Over && !wasOver)
        {
            OnGameOver();
        }

        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        var hovered = HandleInput(area, body, theme, scale);
        if (board.State == TriviaState.Ready)
        {
            renderer.DrawPicker(board, area, theme, Accent, hovered, scale);
            return;
        }

        DrawPrompt(area, theme, scale);
        renderer.Draw(board, area, textures, theme, Accent, hovered, scale);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        fx.DrawFlash(drawList, body, 0f);
        DrawHud(body, theme, scale, deltaSeconds);
        if (board.State == TriviaState.Over)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private int HandleInput(Rect area, Rect body, PhoneTheme theme, float scale)
    {
        if (board.State == TriviaState.Ready)
        {
            return HandlePicker(area, scale);
        }

        if (GameHud.RestartButton(new Vector2(body.Max.X - 22f * scale, body.Min.Y + 30f * scale), 16f * scale, theme))
        {
            StartGame();
            return -1;
        }

        if (board.State != TriviaState.Asking)
        {
            return -1;
        }

        var hovered = -1;
        for (var index = 0; index < TriviaBoard.Options; index++)
        {
            var optionRect = TriviaRenderer.OptionRect(area, index, scale);
            if (!UiInteract.Hover(optionRect.Min, optionRect.Max))
            {
                continue;
            }

            hovered = index;
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            break;
        }

        if (hovered < 0 || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return hovered;
        }

        var cell = TriviaRenderer.OptionRect(area, hovered, scale);
        if (board.Answer(hovered))
        {
            OnCorrect(cell, scale);
            return hovered;
        }

        OnWrong(cell, scale);
        return hovered;
    }

    private int HandlePicker(Rect area, float scale)
    {
        var hovered = -1;
        for (var index = 0; index < TriviaBoard.PickableCount; index++)
        {
            if (!board.IsAvailable(TriviaBoard.PickableAt(index)))
            {
                continue;
            }

            var categoryRect = TriviaRenderer.CategoryRect(area, index, scale);
            if (!UiInteract.Hover(categoryRect.Min, categoryRect.Max))
            {
                continue;
            }

            hovered = index;
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            break;
        }

        if (hovered < 0 || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return hovered;
        }

        board.Start(TriviaBoard.PickableAt(hovered));
        return hovered;
    }

    private void RestartRun()
    {
        particles.Clear();
        fx.Clear();
        scoreRoll.Snap(0);
        resultAppear = 0f;
        wasOver = false;
        pendingSubmit = false;
        newBest = false;
        if (!board.Start(board.Category))
        {
            board.Reset();
        }
    }

    private void OnCorrect(Rect cell, float scale)
    {
        fx.AddTrauma(0.10f);
        fx.Shockwave(cell.Center, cell.Width * 0.6f, new Vector4(0.42f, 0.88f, 0.56f, 0.9f), 0.45f, 2.6f);
        fx.AddText("+" + GameNumber.Label(board.LastPoints), new Vector2(cell.Center.X, cell.Min.Y + 6f * scale),
            new Vector4(0.52f, 0.92f, 0.62f, 1f), 1.1f);
        particles.Sparkle(cell.Center, 12, new Vector4(0.62f, 0.94f, 0.70f, 1f), 170f * scale, 2.4f, 0.6f);
    }

    private void OnWrong(Rect cell, float scale)
    {
        fx.AddTrauma(0.45f);
        fx.Flash(new Vector4(0.95f, 0.32f, 0.32f, 1f), 0.30f);
        particles.Burst(cell.Center, 12, new Vector4(0.95f, 0.40f, 0.42f, 1f), 190f * scale, 2.6f, 0.5f, 340f);
    }

    private void OnTimeout(Rect area, float scale)
    {
        fx.AddTrauma(0.40f);
        fx.Flash(new Vector4(0.95f, 0.32f, 0.32f, 1f), 0.28f);
        var subject = TriviaRenderer.SubjectOf(area, scale);
        fx.AddText(Loc.T(L.Games.Miss), new Vector2(subject.Center.X, subject.Max.Y - 30f * scale),
            new Vector4(0.96f, 0.42f, 0.42f, 1f), 1.05f);
    }

    private void OnGameOver()
    {
        wasOver = true;
        finalScore = board.Score;
        pendingSubmit = true;
        resultAppear = 0f;
        fx.AddTrauma(0.5f);
    }

    private void DrawPrompt(Rect area, PhoneTheme theme, float scale)
    {
        if (!board.HasQuestion)
        {
            return;
        }

        var prompt = Loc.T(board.Kind == TriviaKind.IconToName ? L.Games.WhatIsThis : L.Games.PickTheIcon);
        Typography.DrawCentered(new Vector2(area.Center.X, area.Min.Y + 16f * scale), prompt, theme.TextMuted,
            TextStyles.FootnoteEmphasized);
    }

    private void DrawHud(Rect body, PhoneTheme theme, float scale, float deltaSeconds)
    {
        var rowY = body.Min.Y + 30f * scale;
        var beatingBest = board.Score > 0 && board.Score > bestScore;
        GameHud.ScorePill(new Vector2(body.Center.X - 46f * scale, rowY), Loc.T(L.Games.Score), ref scoreRoll,
            board.Score, Accent, theme, deltaSeconds, beatingBest);
        GameHud.Pill(new Vector2(body.Center.X + 46f * scale, rowY), Loc.T(L.Games.Combo),
            GameNumber.Label(board.Combo), Accent, theme, board.Multiplier > 1);
        DrawLives(body, scale);
    }

    private void DrawLives(Rect body, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = 4f * scale;
        var origin = new Vector2(body.Min.X + 22f * scale, body.Min.Y + 30f * scale);
        for (var life = 0; life < 3; life++)
        {
            var center = new Vector2(origin.X, origin.Y + (life - 1) * radius * 3.2f);
            if (life < board.Lives)
            {
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0.96f, 0.42f, 0.46f, 1f)));
                continue;
            }

            drawList.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.25f)), 0,
                MathF.Max(1f, scale));
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
            RestartRun();
        }
    }
}
