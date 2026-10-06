using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Hop;

internal sealed class HopApp : ILegacyMiniGame
{
    private const string GameId = "hop";
    private const float PadBandFraction = 0.26f;
    private const float LevelBannerSeconds = 1.6f;
    private static readonly Vector4[] CelebrationPalette =
    {
        new(0.62f, 0.62f, 0.68f, 1f), new(1f, 0.85f, 0.30f, 1f), new(1f, 0.62f, 0.30f, 1f),
        new(0.40f, 0.70f, 0.98f, 1f), new(0.46f, 0.86f, 0.62f, 1f), new(0.98f, 0.98f, 0.9f, 1f),
    };

    private readonly HopBoard board = new();
    private readonly HopRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private RollingValue scoreRoll;
    private bool started;
    private bool finished;
    private bool pendingSubmit;
    private bool newBest;
    private int loadedBest;
    private float resultAppear;
    private float bannerProgress = 1f;
    private string bannerText = string.Empty;
    private string densText = string.Empty;
    private int densTextCount = -1;
    private string resultLevel = string.Empty;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Hop);
    public GameGenre Genre => GameGenre.Arcade;
    public bool RunsOnAClock => true;

    public void Open()
    {
        loadedBest = 0;
        started = false;
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    private void StartNewGame()
    {
        board.StartGame();
        particles.Clear();
        fx.Clear();
        scoreRoll.Snap(0);
        finished = false;
        pendingSubmit = false;
        newBest = false;
        resultAppear = 0f;
        densTextCount = -1;
        started = true;
        ShowLevelBanner();
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = UiScale.Current;
        var theme = context.Theme;
        var body = context.Body;
        if (loadedBest == 0)
        {
            loadedBest = context.Stats.Get(GameId).BestScore;
        }

        if (!started)
        {
            StartNewGame();
        }

        if (pendingSubmit)
        {
            newBest = context.Stats.SubmitScore(GameId, board.Score);
            if (newBest)
            {
                loadedBest = board.Score;
            }

            pendingSubmit = false;
        }

        var rowY = body.Min.Y + 30f * scale;
        var timerY = rowY + 30f * scale;
        var padHeight = MathF.Min(GamePad.DPadHeight(scale), body.Height * PadBandFraction);
        var padArea = new Rect(new Vector2(body.Min.X, body.Max.Y - padHeight), body.Max);
        var pad = 6f * scale;
        var area = new Rect(new Vector2(body.Min.X + pad, timerY + 12f * scale), new Vector2(body.Max.X - pad, padArea.Min.Y - pad));
        var boardRect = HopRenderer.BoardRect(area, out var cell);
        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        if (!finished)
        {
            var direction = GamePad.DPad(padArea, Accent, theme);
            HandleInput(direction);
            var simDelta = fx.ScaleDelta(deltaSeconds);
            board.Tick(simDelta);
            ReactToEvents(boardRect, cell, scale);
        }

        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, deltaSeconds, LevelBannerSeconds);
        if (board.GameOver && !finished)
        {
            finished = true;
            resultAppear = 0f;
            pendingSubmit = true;
            resultLevel = $"{Loc.T(L.Games.Level)} {GameNumber.Label(board.Level)}";
        }

        var shake = fx.ShakeOffset(scale);
        var shakenBoard = new Rect(boardRect.Min + shake, boardRect.Max + shake);
        DrawHud(body, rowY, timerY, boardRect, theme, deltaSeconds, scale);
        GameScene.Arena(drawList, new Rect(shakenBoard.Min - new Vector2(pad, pad), shakenBoard.Max + new Vector2(pad, pad)),
            14f * scale, scale, Accent);
        renderer.Draw(board, shakenBoard, cell, Accent, scale);
        fx.DrawFlash(drawList, boardRect, 0f);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, HopRenderer.CellCenter(boardRect, cell, 6f, HopBoard.MedianRow), bannerText, Accent, theme,
            bannerProgress);
        if (finished)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private void HandleInput(PadDirection pad)
    {
        if (pad == PadDirection.Up || GameInput.Pressed(ImGuiKey.W, ImGuiKey.UpArrow))
        {
            board.Hop(0, 1);
        }
        else if (pad == PadDirection.Down || GameInput.Pressed(ImGuiKey.S, ImGuiKey.DownArrow))
        {
            board.Hop(0, -1);
        }
        else if (pad == PadDirection.Left || GameInput.Pressed(ImGuiKey.A, ImGuiKey.LeftArrow))
        {
            board.Hop(-1, 0);
        }
        else if (pad == PadDirection.Right || GameInput.Pressed(ImGuiKey.D, ImGuiKey.RightArrow))
        {
            board.Hop(1, 0);
        }
    }

    private void ReactToEvents(Rect boardRect, float cell, float scale)
    {
        var hopper = HopRenderer.CellCenter(boardRect, cell, board.X, board.Row);
        if (board.HoppedThisFrame && !board.Dying)
        {
            UiFeedback.Play(UiSound.GameJump);
            particles.Burst(hopper + new Vector2(0f, cell * 0.4f), 3, new Vector4(1f, 1f, 1f, 0.5f), 50f * scale, 1.6f, 0.3f, 0f);
        }

        if (board.BankedBayThisFrame >= 0)
        {
            UiFeedback.Play(UiSound.GameCollect);
            var den = HopRenderer.CellCenter(boardRect, cell, HopBoard.BayColumns[board.BankedBayThisFrame], HopBoard.BankRow);
            particles.Sparkle(den, 14, GamePalette.Lighten(Accent, 0.4f), 140f * scale, 2.6f, 0.7f);
            fx.Shockwave(den, cell * 2.4f, GamePalette.Lighten(Accent, 0.4f) with { W = 0.7f }, 0.4f, 2.5f);
            fx.AddText(GameNumber.Label(HopBoard.BankPoints(board.TimerRemaining)), den, GamePalette.Lighten(Accent, 0.4f), 1.1f);
            fx.AddTrauma(0.12f);
        }

        if (board.BumpedThisFrame)
        {
            fx.AddTrauma(0.08f);
            fx.HitStop(0.03f);
        }

        if (board.DiedThisFrame)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            particles.Burst(hopper, 16, HopRenderer.HopperColor, 150f * scale, 2.6f, 0.7f, 300f);
            fx.AddTrauma(0.65f);
            fx.HitStop(0.1f);
            fx.Flash(new Vector4(0.95f, 0.3f, 0.3f, 1f), 0.35f);
        }

        if (board.LevelClearedThisFrame)
        {
            UiFeedback.Play(UiSound.GameClear);
            var top = new Vector2(boardRect.Center.X, boardRect.Min.Y + boardRect.Height * 0.15f);
            particles.Confetti(top, 70, CelebrationPalette, 280f * scale, 4f, 1.4f);
            fx.Flash(GamePalette.Lighten(Accent, 0.4f), 0.18f);
            fx.AddText($"+{GameNumber.Label(HopBoard.LevelClearBonus)}", top, GamePalette.Lighten(Accent, 0.3f), 1.3f);
        }

        if (board.LevelStartedThisFrame)
        {
            ShowLevelBanner();
        }
    }

    private void ShowLevelBanner()
    {
        bannerText = $"{Loc.T(L.Games.Level)} {GameNumber.Label(board.Level)}";
        bannerProgress = 0f;
    }

    private void DrawHud(Rect body, float rowY, float timerY, Rect boardRect, PhoneTheme theme, float deltaSeconds, float scale)
    {
        if (densTextCount != board.BankedThisLevel)
        {
            densTextCount = board.BankedThisLevel;
            densText = $"{GameNumber.Label(board.BankedThisLevel)}/{GameNumber.Label(HopBoard.BayCount)}";
        }

        var scoreLabel = Loc.T(L.Games.Score);
        var scoreText = GameNumber.Label(board.Score);
        var levelLabel = Loc.T(L.Games.Level);
        var levelText = GameNumber.Label(board.Level);
        var densLabel = Loc.T(L.Games.Dens);
        var restartRadius = 16f * scale;
        var restartCenter = new Vector2(body.Max.X - 22f * scale, rowY);
        var gap = 10f * scale;
        var available = restartCenter.X - restartRadius - gap - body.Min.X - 40f * scale;
        var natural = GameHud.PillWidth(scoreLabel, scoreText) + GameHud.PillWidth(levelLabel, levelText) +
            GameHud.PillWidth(densLabel, densText) + gap * 2f;
        var sizeScale = MathF.Min(1f, available / natural);
        var scoreWidth = GameHud.PillWidth(scoreLabel, scoreText, sizeScale);
        var levelWidth = GameHud.PillWidth(levelLabel, levelText, sizeScale);
        var densWidth = GameHud.PillWidth(densLabel, densText, sizeScale);
        var total = scoreWidth + levelWidth + densWidth + gap * 2f;
        var left = body.Min.X + 40f * scale + (available - total) * 0.5f;
        var beatingBest = board.Score > 0 && board.Score > loadedBest;
        GameHud.ScorePill(new Vector2(left + scoreWidth * 0.5f, rowY), scoreLabel, ref scoreRoll, board.Score, Accent, theme,
            deltaSeconds, beatingBest, sizeScale);
        left += scoreWidth + gap;
        GameHud.Pill(new Vector2(left + levelWidth * 0.5f, rowY), levelLabel, levelText, Accent, theme, false, sizeScale);
        left += levelWidth + gap;
        GameHud.Pill(new Vector2(left + densWidth * 0.5f, rowY), densLabel, densText, Accent, theme, false, sizeScale);
        if (GameHud.RestartButton(restartCenter, restartRadius, theme))
        {
            StartNewGame();
        }

        DrawLives(body, rowY, scale);
        DrawTimer(boardRect, timerY, scale);
    }

    private void DrawLives(Rect body, float rowY, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var size = 11f * scale;
        var origin = new Vector2(body.Min.X + 20f * scale, rowY);
        var lastLife = board.Lives == 1;
        for (var life = 0; life < HopBoard.StartLives; life++)
        {
            var center = new Vector2(origin.X, origin.Y + (life - 1) * size * 1.3f);
            if (life >= board.Lives)
            {
                drawList.AddCircle(center, size * 0.4f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.25f)), 12, MathF.Max(1f, scale));
                continue;
            }

            var alpha = lastLife ? 0.6f + 0.4f * Pulse.Wave(Pulse.Fast) : 1f;
            HopRenderer.DrawHopperSprite(drawList, center, size, false, alpha);
        }
    }

    private void DrawTimer(Rect boardRect, float timerY, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var height = 5f * scale;
        var min = new Vector2(boardRect.Min.X, timerY);
        var max = new Vector2(boardRect.Max.X, timerY + height);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.12f)), height * 0.5f);
        var low = board.TimerRemaining < HopBoard.LowTimerSeconds;
        var color = low ? new Vector4(0.95f, 0.35f, 0.35f, 0.45f + 0.55f * Pulse.Wave(Pulse.Fast)) : Accent;
        var fillMax = new Vector2(min.X + (max.X - min.X) * board.TimerFraction, max.Y);
        if (fillMax.X > min.X + height)
        {
            drawList.AddRectFilled(min, fillMax, ImGui.GetColorU32(color), height * 0.5f);
        }
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        var result = new GameResult(Loc.T(L.Games.GameOver), theme.Danger, Loc.T(L.Games.Score),
            GameNumber.Label(board.Score), resultLevel, newBest);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartNewGame();
        }
    }
}
