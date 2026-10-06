using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.CapMan;

internal sealed class CapManApp : ILegacyMiniGame
{
    private const string GameId = "capman";
    private const float PadBandFraction = 0.26f;
    private static readonly Vector4[] CelebrationPalette =
    {
        new(1f, 0.85f, 0.30f, 1f), new(0.98f, 0.35f, 0.35f, 1f), new(0.98f, 0.55f, 0.85f, 1f),
        new(0.40f, 0.90f, 0.95f, 1f), new(1f, 0.70f, 0.35f, 1f), new(0.98f, 0.98f, 0.9f, 1f),
    };

    private readonly CapManBoard board = new();
    private readonly CapManRenderer renderer = new();
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
    private string resultLevel = string.Empty;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.CapMan);
    public GameGenre Genre => GameGenre.Action;
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
        bannerProgress = 0f;
        started = true;
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
        var padHeight = MathF.Min(GamePad.DPadHeight(scale), body.Height * PadBandFraction);
        var padArea = new Rect(new Vector2(body.Min.X, body.Max.Y - padHeight), body.Max);
        var pad = 6f * scale;
        var area = new Rect(new Vector2(body.Min.X + pad, rowY + 26f * scale), new Vector2(body.Max.X - pad, padArea.Min.Y - pad));
        var boardRect = CapManRenderer.BoardRect(area, out var cell);
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
        bannerProgress = GameBanner.Advance(bannerProgress, deltaSeconds, CapManBoard.ReadySeconds);
        if (board.GameOver && !finished)
        {
            finished = true;
            resultAppear = 0f;
            pendingSubmit = true;
            resultLevel = $"{Loc.T(L.Games.Level)} {GameNumber.Label(board.Level)}";
        }

        var shake = fx.ShakeOffset(scale);
        var shakenBoard = new Rect(boardRect.Min + shake, boardRect.Max + shake);
        DrawHud(body, rowY, theme, deltaSeconds, scale);
        GameScene.Arena(drawList, new Rect(shakenBoard.Min - new Vector2(pad, pad), shakenBoard.Max + new Vector2(pad, pad)),
            14f * scale, scale, Accent);
        renderer.Draw(board, shakenBoard, cell, Accent, scale);
        fx.DrawFlash(drawList, boardRect, 0f);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, CapManRenderer.ToScreen(boardRect, cell, new Vector2(7f, 11f)), Loc.T(L.Games.Ready),
            Accent, theme, bannerProgress);
        if (finished)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private void HandleInput(PadDirection pad)
    {
        if (pad == PadDirection.Up || GameInput.Pressed(ImGuiKey.W, ImGuiKey.UpArrow))
        {
            board.Turn(CapManBoard.Up);
        }
        else if (pad == PadDirection.Down || GameInput.Pressed(ImGuiKey.S, ImGuiKey.DownArrow))
        {
            board.Turn(CapManBoard.Down);
        }
        else if (pad == PadDirection.Left || GameInput.Pressed(ImGuiKey.A, ImGuiKey.LeftArrow))
        {
            board.Turn(CapManBoard.Left);
        }
        else if (pad == PadDirection.Right || GameInput.Pressed(ImGuiKey.D, ImGuiKey.RightArrow))
        {
            board.Turn(CapManBoard.Right);
        }
    }

    private void ReactToEvents(Rect boardRect, float cell, float scale)
    {
        if (board.DotsEatenThisFrame > 0)
        {
            var center = CapManRenderer.ToScreen(boardRect, cell, board.LastDotPosition);
            particles.Burst(center, 2, CapManRenderer.DotColor with { W = 0.7f }, 40f * scale, 1.4f, 0.25f, 0f);
        }

        if (board.PelletEatenThisFrame)
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            var center = CapManRenderer.ToScreen(boardRect, cell, board.LastDotPosition);
            particles.Sparkle(center, 10, CapManRenderer.PlayerColor, 120f * scale, 2.4f, 0.6f);
            fx.Shockwave(center, cell * 4f, CapManRenderer.FrightColor with { W = 0.7f }, 0.5f, 3f);
            fx.Flash(CapManRenderer.FrightColor, 0.14f);
            fx.AddTrauma(0.12f);
        }

        if (board.GhostsEatenThisFrame > 0)
        {
            UiFeedback.Play(UiSound.GameCollect);
        }

        for (var index = 0; index < board.GhostsEatenThisFrame; index++)
        {
            var center = CapManRenderer.ToScreen(boardRect, cell, board.GhostEatPosition(index));
            particles.Burst(center, 12, CapManRenderer.FrightColor, 150f * scale, 2.6f, 0.5f, 220f);
            fx.AddText(GameNumber.Label(board.GhostEatPoints(index)), center, CapManRenderer.DotColor, 1.1f);
            fx.Shockwave(center, cell * 2.5f, CapManRenderer.DotColor with { W = 0.6f }, 0.35f, 2f);
            fx.HitStop(0.06f);
            fx.AddTrauma(0.18f);
        }

        if (board.PlayerDiedThisFrame)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            var center = CapManRenderer.ToScreen(boardRect, cell, board.PlayerPosition);
            particles.Burst(center, 18, CapManRenderer.PlayerColor, 160f * scale, 2.8f, 0.8f, 300f);
            fx.AddTrauma(0.7f);
            fx.HitStop(0.1f);
            fx.Flash(new Vector4(0.95f, 0.3f, 0.3f, 1f), 0.35f);
        }

        if (board.LevelClearedThisFrame)
        {
            UiFeedback.Play(UiSound.GameClear);
            var top = new Vector2(boardRect.Center.X, boardRect.Min.Y + boardRect.Height * 0.2f);
            particles.Confetti(top, 70, CelebrationPalette, 280f * scale, 4f, 1.4f);
            fx.Flash(GamePalette.Lighten(Accent, 0.4f), 0.18f);
        }

        if (board.ReadyStartedThisFrame)
        {
            bannerProgress = 0f;
        }
    }

    private void DrawHud(Rect body, float rowY, PhoneTheme theme, float deltaSeconds, float scale)
    {
        var scoreLabel = Loc.T(L.Games.Score);
        var scoreText = GameNumber.Label(board.Score);
        var levelLabel = Loc.T(L.Games.Level);
        var levelText = GameNumber.Label(board.Level);
        var scoreWidth = GameHud.PillWidth(scoreLabel, scoreText);
        var levelWidth = GameHud.PillWidth(levelLabel, levelText);
        var gap = 12f * scale;
        var scoreX = body.Center.X - gap * 0.5f - scoreWidth * 0.5f;
        var levelX = body.Center.X + gap * 0.5f + levelWidth * 0.5f;
        var beatingBest = board.Score > 0 && board.Score > loadedBest;
        GameHud.ScorePill(new Vector2(scoreX, rowY), scoreLabel, ref scoreRoll, board.Score, Accent, theme, deltaSeconds,
            beatingBest);
        GameHud.Pill(new Vector2(levelX, rowY), levelLabel, levelText, Accent, theme);
        if (GameHud.RestartButton(new Vector2(body.Max.X - 22f * scale, rowY), 16f * scale, theme))
        {
            StartNewGame();
        }

        DrawLives(body, rowY, scale);
    }

    private void DrawLives(Rect body, float rowY, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = 5f * scale;
        var origin = new Vector2(body.Min.X + 22f * scale, rowY);
        var lastLife = board.Lives == 1;
        for (var life = 0; life < CapManBoard.StartLives; life++)
        {
            var center = new Vector2(origin.X, origin.Y + (life - 1) * radius * 2.8f);
            if (life >= board.Lives)
            {
                drawList.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.25f)), 12,
                    MathF.Max(1f, scale));
                continue;
            }

            var color = lastLife
                ? new Vector4(0.95f, 0.35f, 0.35f, 0.6f + 0.4f * Pulse.Wave(Pulse.Fast))
                : CapManRenderer.PlayerColor;
            drawList.PathClear();
            drawList.PathLineTo(center);
            drawList.PathArcTo(center, radius, 0.3f, MathF.PI * 2f - 0.3f, 16);
            drawList.PathFillConvex(ImGui.GetColorU32(color));
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
