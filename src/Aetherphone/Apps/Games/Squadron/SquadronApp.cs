using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Squadron;

internal sealed class SquadronApp : ILegacyMiniGame
{
    private const string GameId = "squadron";
    private static readonly Vector4[] CelebrationPalette =
    {
        new(0.98f, 0.95f, 0.90f, 1f), new(0.98f, 0.45f, 0.62f, 1f), new(1f, 0.62f, 0.30f, 1f),
        new(0.40f, 0.70f, 0.98f, 1f), new(0.72f, 0.50f, 0.96f, 1f), new(0.46f, 0.86f, 0.62f, 1f),
    };

    private readonly SquadronBoard board = new();
    private readonly SquadronRenderer renderer = new();
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
    private float bannerLifetime = 1f;
    private string bannerText = string.Empty;
    private string resultStage = string.Empty;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Squadron);
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
        started = true;
        ShowStageBanner();
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
        var padHeight = GamePad.ShooterHeight(scale);
        var padArea = new Rect(new Vector2(body.Min.X, body.Max.Y - padHeight), body.Max);
        var field = FieldRect(body, rowY, padArea.Min.Y, scale);
        var factor = field.Width / SquadronBoard.Width;
        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        if (!finished)
        {
            var pad = GamePad.Shooter(padArea, Accent, theme);
            HandleInput(pad, deltaSeconds);
            var simDelta = fx.ScaleDelta(deltaSeconds);
            board.Tick(simDelta);
            ReactToEvents(field, factor, scale);
        }

        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, deltaSeconds, bannerLifetime);
        if (board.GameOver && !finished)
        {
            finished = true;
            resultAppear = 0f;
            pendingSubmit = true;
            resultStage = $"{Loc.T(L.Games.Stage)} {GameNumber.Label(board.Stage)}";
        }

        var shake = fx.ShakeOffset(scale);
        var shakenField = new Rect(field.Min + shake, field.Max + shake);
        DrawHud(body, rowY, theme, deltaSeconds, scale);
        GameScene.Arena(drawList, shakenField, 14f * scale, scale, Accent);
        renderer.Draw(board, shakenField, Accent, scale);
        fx.DrawFlash(drawList, field, 0f);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, new Vector2(field.Center.X, field.Min.Y + field.Height * 0.62f), bannerText, Accent, theme,
            bannerProgress);
        if (finished)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private static Rect FieldRect(Rect body, float rowY, float bottom, float scale)
    {
        var pad = 6f * scale;
        var top = rowY + 26f * scale;
        var availableWidth = body.Width - pad * 2f;
        var availableHeight = bottom - pad - top;
        var factor = MathF.Min(availableWidth / SquadronBoard.Width, availableHeight / SquadronBoard.Height);
        var size = new Vector2(SquadronBoard.Width, SquadronBoard.Height) * factor;
        var min = new Vector2(body.Center.X - size.X * 0.5f, top + (availableHeight - size.Y) * 0.5f);
        return new Rect(min, min + size);
    }

    private void HandleInput(in ShooterPadInput pad, float deltaSeconds)
    {
        var left = pad.Left || GameInput.Held(ImGuiKey.A, ImGuiKey.LeftArrow);
        var right = pad.Right || GameInput.Held(ImGuiKey.D, ImGuiKey.RightArrow);
        var direction = (right ? 1f : 0f) - (left ? 1f : 0f);
        board.Move(direction, deltaSeconds);
        var fire = pad.Fire || GameInput.Pressed(ImGuiKey.Space, ImGuiKey.W) || GameInput.Pressed(ImGuiKey.UpArrow);
        if (fire)
        {
            board.Fire();
        }
    }

    private void ReactToEvents(Rect field, float factor, float scale)
    {
        if (board.ShotFiredThisFrame)
        {
            UiFeedback.Play(UiSound.GameShoot);
            var muzzle = field.Min + new Vector2(board.PlayerX, SquadronBoard.PlayerRowY - SquadronBoard.PlayerHeight) * factor;
            particles.Streaks(muzzle, 3, GamePalette.Lighten(Accent, 0.4f), 110f * scale, 2f, 0.2f, 0.5f, -MathF.PI * 0.5f);
        }

        for (var index = 0; index < board.KillCount; index++)
        {
            var center = field.Min + board.KillPosition(index) * factor;
            var color = SquadronRenderer.KindColor(board.KillKind(index), Accent);
            particles.Burst(center, 10, color, 150f * scale, 2.4f, 0.5f, 220f, MathF.PI * 2f, 0f, ParticleShape.Square);
            particles.Sparkle(center, 4, new Vector4(1f, 1f, 1f, 1f), 100f * scale, 2f, 0.4f);
            fx.Shockwave(center, SquadronBoard.ShipWidth * factor * 1.3f, color with { W = 0.6f }, 0.3f, 2f);
            var points = board.KillPoints(index);
            if (points > 0)
            {
                fx.AddText(GameNumber.Label(points), center, color, points >= SquadronBoard.WardenDivingPoints ? 1.2f : 0.9f);
            }
        }

        if (board.KillCount > 0)
        {
            UiFeedback.Play(UiSound.GameExplosion);
            fx.AddTrauma(0.06f);
            fx.HitStop(0.03f);
        }

        if (board.CaptureStartedThisFrame)
        {
            fx.Flash(SquadronRenderer.WardenColor, 0.25f);
            fx.AddTrauma(0.3f);
        }

        if (board.RescueStartedThisFrame)
        {
            var from = field.Min + board.RescuePosition * factor;
            particles.Sparkle(from, 12, GamePalette.Lighten(Accent, 0.4f), 140f * scale, 2.6f, 0.8f);
        }

        if (board.RescueCompletedThisFrame)
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            var center = field.Min + board.PlayerCenter * factor;
            particles.Confetti(center, 40, CelebrationPalette, 220f * scale, 3.5f, 1.2f);
            fx.Shockwave(center, SquadronBoard.PlayerWidth * factor * 3f, GamePalette.Lighten(Accent, 0.4f) with { W = 0.7f }, 0.5f, 3f);
            fx.AddText($"+{GameNumber.Label(SquadronBoard.RescueBonus)}", center, GamePalette.Lighten(Accent, 0.4f), 1.3f);
            fx.HitStop(0.06f);
        }

        if (board.DualLostThisFrame)
        {
            var center = field.Min + board.PlayerCenter * factor;
            particles.Burst(center, 12, Accent, 150f * scale, 2.4f, 0.6f, 260f);
            fx.AddTrauma(0.35f);
            fx.HitStop(0.05f);
            fx.Flash(new Vector4(0.95f, 0.5f, 0.3f, 1f), 0.2f);
        }

        if (board.PlayerHitThisFrame)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            var center = field.Min + board.PlayerCenter * factor;
            particles.Burst(center, 16, Accent, 170f * scale, 2.6f, 0.7f, 300f);
            fx.AddTrauma(0.7f);
            fx.HitStop(0.1f);
            fx.Flash(new Vector4(0.95f, 0.3f, 0.3f, 1f), 0.35f);
        }

        if (board.StageClearedThisFrame)
        {
            UiFeedback.Play(UiSound.GameClear);
            var top = new Vector2(field.Center.X, field.Min.Y + field.Height * 0.2f);
            particles.Confetti(top, 50, CelebrationPalette, 260f * scale, 4f, 1.4f);
            fx.Flash(GamePalette.Lighten(Accent, 0.4f), 0.14f);
        }

        if (board.ChallengeEndedThisFrame)
        {
            bannerText = board.LastChallengeWasPerfect
                ? $"{Loc.T(L.Games.Perfect)}  +{GameNumber.Label(SquadronBoard.PerfectBonus)}"
                : Loc.T(L.Games.HitsOf, board.LastChallengeHits, SquadronBoard.ChallengeShipCount);
            bannerLifetime = SquadronBoard.ResultBannerSeconds;
            bannerProgress = 0f;
            if (board.LastChallengeWasPerfect)
            {
                var top = new Vector2(field.Center.X, field.Min.Y + field.Height * 0.2f);
                particles.Confetti(top, 80, CelebrationPalette, 300f * scale, 4f, 1.6f);
            }
        }
        else if (board.StageStartedThisFrame)
        {
            ShowStageBanner();
        }
    }

    private void ShowStageBanner()
    {
        bannerText = board.IsChallenge
            ? Loc.T(L.Games.ChallengeStage)
            : $"{Loc.T(L.Games.Stage)} {GameNumber.Label(board.Stage)}";
        bannerLifetime = SquadronBoard.StageBannerSeconds;
        bannerProgress = 0f;
    }

    private void DrawHud(Rect body, float rowY, PhoneTheme theme, float deltaSeconds, float scale)
    {
        var scoreLabel = Loc.T(L.Games.Score);
        var scoreText = GameNumber.Label(board.Score);
        var stageLabel = Loc.T(L.Games.Stage);
        var stageText = GameNumber.Label(board.Stage);
        var scoreWidth = GameHud.PillWidth(scoreLabel, scoreText);
        var stageWidth = GameHud.PillWidth(stageLabel, stageText);
        var gap = 12f * scale;
        var scoreX = body.Center.X - gap * 0.5f - scoreWidth * 0.5f;
        var stageX = body.Center.X + gap * 0.5f + stageWidth * 0.5f;
        var beatingBest = board.Score > 0 && board.Score > loadedBest;
        GameHud.ScorePill(new Vector2(scoreX, rowY), scoreLabel, ref scoreRoll, board.Score, Accent, theme, deltaSeconds,
            beatingBest);
        GameHud.Pill(new Vector2(stageX, rowY), stageLabel, stageText, Accent, theme, board.IsChallenge);
        if (GameHud.RestartButton(new Vector2(body.Max.X - 22f * scale, rowY), 16f * scale, theme))
        {
            StartNewGame();
        }

        DrawLives(body, rowY, scale);
    }

    private void DrawLives(Rect body, float rowY, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var unit = 1.6f * scale;
        var pitch = SquadronRenderer.Fighter.Height * unit + 4f * scale;
        var origin = new Vector2(body.Min.X + 22f * scale, rowY);
        var lastLife = board.Lives == 1;
        for (var life = 0; life < SquadronBoard.StartLives; life++)
        {
            var center = new Vector2(origin.X, origin.Y + (life - 1) * pitch);
            if (life >= board.Lives)
            {
                SquadronRenderer.Fighter.DrawCentered(drawList, center, unit, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.2f)));
                continue;
            }

            var color = lastLife
                ? new Vector4(0.95f, 0.35f, 0.35f, 0.6f + 0.4f * Pulse.Wave(Pulse.Fast))
                : Accent;
            SquadronRenderer.Fighter.DrawCentered(drawList, center, unit, ImGui.GetColorU32(color));
        }
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        var result = new GameResult(Loc.T(L.Games.GameOver), theme.Danger, Loc.T(L.Games.Score),
            GameNumber.Label(board.Score), resultStage, newBest);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartNewGame();
        }
    }
}
