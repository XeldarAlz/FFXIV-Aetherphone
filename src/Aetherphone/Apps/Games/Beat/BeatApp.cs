using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Beat;

internal sealed class BeatApp : IMiniGame
{
    private const string GameId = "beat";
    private const float FlashDecay = 3.6f;
    private readonly BeatBoard board = new();
    private readonly BeatRenderer renderer = new();
    private readonly ParticleSystem particles = new(256);
    private readonly FeedbackFx fx = new();
    private readonly float[] laneFlash = new float[BeatBoard.Lanes];
    private RollingValue scoreRoll;
    private string comboLabel = string.Empty;
    private int comboShown;
    private float comboPulse;
    private float resultAppear;
    private bool statsLoaded;
    private bool wasOver;
    private bool pendingSubmit;
    private bool newBest;
    private int bestScore;
    private int finalScore;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Beat);
    public bool RunsOnAClock => true;

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
        for (var lane = 0; lane < laneFlash.Length; lane++)
        {
            laneFlash[lane] = 0f;
        }

        comboShown = 0;
        comboLabel = string.Empty;
        comboPulse = 0f;
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

        var field = new Rect(new Vector2(body.Min.X + 8f * scale, body.Min.Y + 60f * scale),
            new Vector2(body.Max.X - 8f * scale, body.Max.Y - 6f * scale));
        var judgement = board.Step(deltaSeconds);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        for (var lane = 0; lane < laneFlash.Length; lane++)
        {
            laneFlash[lane] = MathF.Max(0f, laneFlash[lane] - deltaSeconds * FlashDecay);
        }

        comboPulse = MathF.Max(0f, comboPulse - deltaSeconds * 2.8f);
        if (judgement == BeatJudgement.Missed)
        {
            OnMissed(field, scale);
        }

        if (board.State == BeatState.Over && !wasOver)
        {
            OnGameOver();
        }

        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        HandleInput(body, field, theme, scale);
        DrawCombo(field, scale);
        renderer.Draw(board, field, laneFlash, Accent, scale);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        fx.DrawFlash(drawList, body, 0f);
        DrawHud(body, theme, scale, deltaSeconds);
        if (board.State == BeatState.Ready)
        {
            DrawStartHint(field);
        }

        if (board.State == BeatState.Over)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private void HandleInput(Rect body, Rect field, PhoneTheme theme, float scale)
    {
        if (GameHud.RestartButton(new Vector2(body.Max.X - 22f * scale, body.Min.Y + 30f * scale), 16f * scale, theme))
        {
            StartGame();
            return;
        }

        if (board.State == BeatState.Over || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        if (!UiInteract.Hover(field.Min, field.Max))
        {
            return;
        }

        if (board.State == BeatState.Ready)
        {
            board.Begin();
            return;
        }

        var laneWidth = BeatRenderer.LaneWidthOf(field);
        var lane = (int)((ImGui.GetMousePos().X - field.Min.X) / laneWidth);
        if (lane < 0)
        {
            lane = 0;
        }
        else if (lane >= BeatBoard.Lanes)
        {
            lane = BeatBoard.Lanes - 1;
        }

        var judgement = board.Tap(lane);
        if (judgement == BeatJudgement.Wrong)
        {
            OnWrong(field, lane, scale);
            return;
        }

        if (judgement != BeatJudgement.None)
        {
            OnHit(field, lane, judgement == BeatJudgement.Perfect, scale);
        }
    }

    private void OnHit(Rect field, int lane, bool perfect, float scale)
    {
        laneFlash[lane] = 1f;
        comboShown = board.Combo;
        comboLabel = "x" + GameNumber.Label(board.Combo);
        comboPulse = 1f;
        var color = BeatRenderer.LaneColor(lane);
        var center = new Vector2(field.Min.X + (lane + 0.5f) * BeatRenderer.LaneWidthOf(field),
            BeatRenderer.HitLineOf(field));
        particles.Burst(center, perfect ? 14 : 8, color, (perfect ? 190f : 120f) * scale, 2.6f, 0.45f, 260f);
        fx.Shockwave(center, (perfect ? 62f : 40f) * scale, GamePalette.Lighten(color, 0.4f), 0.4f, 2.6f);
        fx.AddText(Loc.T(perfect ? L.Games.Perfect : L.Games.Good), new Vector2(center.X, center.Y - 30f * scale),
            perfect ? GamePalette.Lighten(color, 0.5f) : new Vector4(0.92f, 0.94f, 0.98f, 0.9f), perfect ? 1.1f : 0.95f);
        if (!perfect)
        {
            return;
        }

        particles.Sparkle(center, 9, GamePalette.Lighten(color, 0.5f), 150f * scale, 2.4f, 0.6f);
        fx.AddTrauma(0.10f);
    }

    private void OnWrong(Rect field, int lane, float scale)
    {
        comboShown = 0;
        var center = new Vector2(field.Min.X + (lane + 0.5f) * BeatRenderer.LaneWidthOf(field),
            BeatRenderer.HitLineOf(field));
        fx.AddText(Loc.T(L.Games.Miss), new Vector2(center.X, center.Y - 26f * scale),
            new Vector4(0.95f, 0.45f, 0.45f, 1f), 0.95f);
        fx.AddTrauma(0.12f);
    }

    private void OnMissed(Rect field, float scale)
    {
        comboShown = 0;
        var lane = board.MissedLane;
        if (lane < 0)
        {
            return;
        }

        var center = new Vector2(field.Min.X + (lane + 0.5f) * BeatRenderer.LaneWidthOf(field),
            BeatRenderer.HitLineOf(field));
        fx.Flash(new Vector4(0.95f, 0.32f, 0.32f, 1f), 0.32f);
        fx.AddTrauma(0.38f);
        fx.AddText(Loc.T(L.Games.Miss), new Vector2(center.X, center.Y - 26f * scale),
            new Vector4(0.96f, 0.42f, 0.42f, 1f), 1.05f);
        particles.Burst(center, 12, new Vector4(0.95f, 0.40f, 0.40f, 1f), 180f * scale, 2.6f, 0.5f, 320f);
    }

    private void OnGameOver()
    {
        wasOver = true;
        finalScore = board.Score;
        pendingSubmit = true;
        resultAppear = 0f;
        fx.Flash(new Vector4(0.95f, 0.32f, 0.32f, 1f), 0.45f);
        fx.AddTrauma(0.6f);
    }

    private void DrawCombo(Rect field, float scale)
    {
        if (comboShown < 3)
        {
            return;
        }

        var center = new Vector2(field.Center.X, field.Min.Y + field.Height * 0.30f);
        var alpha = 0.11f + 0.09f * Easing.EaseOutCubic(comboPulse);
        Typography.DrawCentered(center, comboLabel, new Vector4(1f, 1f, 1f, alpha), TextStyles.LargeTitle);
    }

    private static void DrawStartHint(Rect field)
    {
        var pulse = 1f + 0.05f * Pulse.Wave(Pulse.Calm);
        var center = new Vector2(field.Center.X, field.Min.Y + field.Height * 0.46f);
        Typography.DrawCentered(center, Loc.T(L.Games.TapToStart), new Vector4(1f, 1f, 1f, 0.95f),
            TextStyles.Title2.Scale * pulse, TextStyles.Title2.Weight);
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
            StartGame();
        }
    }
}
