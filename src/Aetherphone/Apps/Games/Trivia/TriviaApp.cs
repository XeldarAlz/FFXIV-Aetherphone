using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Games.Trivia;

internal sealed class TriviaApp : IMiniGame
{
    private const string GameId = "trivia";
    private const float UrgentSeconds = 3f;
    private static readonly LocString[] Modes =
    {
        L.Games.CategoryAll, L.Games.CategoryMounts, L.Games.CategoryMinions, L.Games.CategoryActions,
        L.Games.CategoryEmotes,
    };
    private static readonly string[] ModeStatIds = { GameId, GameId, GameId, GameId, GameId };
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Trivia, GameGenre.Brain, L.Trivia.Hook,
        Backdrop.Paper, HudStyle.Standard, ScoreKind.Score, Modes, ModeStatIds, clocked: true);
    private static readonly Vector4 Danger = new(0.95f, 0.32f, 0.32f, 1f);
    private static readonly Vector4 RightRing = new(0.42f, 0.88f, 0.56f, 0.9f);
    private static readonly Vector4 RightInk = new(0.30f, 0.72f, 0.44f, 1f);
    private static readonly Vector4 RightSpark = new(0.62f, 0.94f, 0.70f, 1f);
    private static readonly Vector4 WrongSpark = new(0.95f, 0.40f, 0.42f, 1f);

    private readonly TriviaBoard board;
    private readonly ParticleSystem particles = new(256);
    private readonly FeedbackFx fx = new();
    private readonly ITextureProvider textures;
    private float entrance;
    private int hoveredOption = -1;
    private bool finished;
    private bool emptyDeck;

    public TriviaApp(GameData gameData, ITextureProvider textures)
    {
        this.textures = textures;
        board = new TriviaBoard(new GameDataTriviaSource(gameData));
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        particles.Clear();
        fx.Clear();
        entrance = 0f;
        hoveredOption = -1;
        finished = false;
        emptyDeck = !board.Start(TriviaBoard.PickableAt(start.Mode), start.Random) &&
                    !board.Start(TriviaCategory.All, start.Random);
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        DrawBoard(ImGui.GetWindowDrawList(), TriviaRenderer.Layout(context.Safe, UiScale.Current), false, 1f,
            UiScale.Current, context);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rawSeconds = context.RawDeltaSeconds;
        particles.Update(rawSeconds);
        fx.Update(rawSeconds);
        entrance = GameJuice.Advance(entrance, rawSeconds);
        var area = StageLayout.Punched(context.Safe, context.Fx.PlateScale).Translate(fx.ShakeOffset(scale));
        var layout = TriviaRenderer.Layout(area, scale);
        if (!finished)
        {
            Step(layout, scale, context);
        }

        DrawBoard(drawList, layout, true, entrance, scale, context);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        var asking = board.State == TriviaState.Asking;
        context.Hud.Score(board.Score);
        context.Hud.Timer(board.TimeLeft, TriviaBoard.QuestionSeconds, asking && board.TimeLeft <= UrgentSeconds);
        context.Hud.Lives(board.Lives, TriviaBoard.StartLives);
        context.Hud.Combo(board.Combo);
        context.Hud.Best(context.Session.Best);
        context.Session.Report(board.Score);
    }

    private void Step(in TriviaLayout layout, float scale, in GameContext context)
    {
        if (emptyDeck)
        {
            finished = true;
            context.Session.Finish(new GameOutcome(0, ScoreKind.Score, GameId, won: false));
            return;
        }

        if (board.Step(context.DeltaSeconds))
        {
            OnTimeout(layout.Subject, scale, context);
        }

        if (board.State == TriviaState.Over)
        {
            OnGameOver(context);
            return;
        }

        hoveredOption = -1;
        if (context.Session.State != StageFlow.Playing || board.State != TriviaState.Asking)
        {
            return;
        }

        if (board.TimeLeft <= UrgentSeconds)
        {
            context.Fx.Vignette(Danger, 0.08f + 0.10f * Pulse.Wave(Pulse.Fast), 0.3f);
        }

        hoveredOption = HoveredOption(layout.Options, scale);
        if (hoveredOption < 0)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        var cell = TriviaRenderer.OptionRect(layout.Options, hoveredOption, scale);
        var multiplierBefore = board.Multiplier;
        if (board.Answer(hoveredOption))
        {
            OnCorrect(cell, board.Multiplier > multiplierBefore, scale, context);
            return;
        }

        OnWrong(cell, scale, context);
    }

    private static int HoveredOption(Rect options, float scale)
    {
        for (var index = 0; index < TriviaBoard.Options; index++)
        {
            var cell = TriviaRenderer.OptionRect(options, index, scale);
            if (UiInteract.Hover(cell.Min, cell.Max))
            {
                return index;
            }
        }

        return -1;
    }

    private void OnCorrect(Rect cell, bool tierUp, float scale, in GameContext context)
    {
        UiFeedback.Play(UiSound.GamePowerUp);
        if (tierUp)
        {
            GameSfx.ComboTierUp();
        }

        fx.AddTrauma(0.10f);
        fx.Shockwave(cell.Center, cell.Width * 0.6f, RightRing, 0.45f, 2.6f);
        fx.AddText(GameNumber.Signed(board.LastPoints), new Vector2(cell.Center.X, cell.Min.Y + 6f * scale), RightInk,
            1.1f);
        particles.Sparkle(cell.Center, 12, RightSpark, 170f * scale, 2.4f, 0.6f);
        context.Fx.Punch(0.03f);
    }

    private void OnWrong(Rect cell, float scale, in GameContext context)
    {
        UiFeedback.Play(UiSound.GameWrong);
        fx.AddTrauma(0.45f);
        context.Fx.Vignette(Danger, 0.55f, 0.5f);
        context.Fx.Flash(Danger, 0.18f);
        particles.Burst(cell.Center, 12, WrongSpark, 190f * scale, 2.6f, 0.5f, 340f);
    }

    private void OnTimeout(Rect subject, float scale, in GameContext context)
    {
        UiFeedback.Play(UiSound.GameWrong);
        fx.AddTrauma(0.40f);
        context.Fx.Vignette(Danger, 0.55f, 0.5f);
        context.Fx.Flash(Danger, 0.16f);
        fx.AddText(Loc.T(L.Games.Miss), new Vector2(subject.Center.X, subject.Max.Y - 30f * scale), WrongSpark, 1.05f);
    }

    private void OnGameOver(in GameContext context)
    {
        finished = true;
        fx.AddTrauma(0.5f);
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Games.Combo, GameNumber.Label(board.BestCombo))
            .WithStat(L.Games.Time, TimeText.MinutesSeconds((int)context.Session.PlaySeconds)));
    }

    private void DrawBoard(ImDrawListPtr drawList, in TriviaLayout layout, bool showQuestion, float popProgress,
        float scale, in GameContext context)
    {
        var ink = context.Backdrop.Ink;
        var theme = context.Theme;
        var question = showQuestion && board.HasQuestion;
        if (question)
        {
            var prompt = Loc.T(board.Kind == TriviaKind.IconToName ? L.Games.WhatIsThis : L.Games.PickTheIcon);
            TriviaRenderer.DrawPrompt(drawList, layout.Prompt, prompt, StageInks.MutedOn(ink));
        }

        TriviaRenderer.DrawSubject(drawList, board, layout.Subject, showQuestion, textures, Accent, ink, scale);
        for (var index = 0; index < TriviaBoard.Options; index++)
        {
            var cell = TriviaRenderer.OptionRect(layout.Options, index, scale);
            var lift = StageCell.Lift(GameJuice.Stagger(popProgress, index, TriviaBoard.Options)) * scale;
            TriviaRenderer.DrawOption(drawList, board, cell.Translate(new Vector2(0f, -lift)), index, showQuestion,
                textures, Accent, ink, theme, hoveredOption == index, scale);
        }
    }

}
