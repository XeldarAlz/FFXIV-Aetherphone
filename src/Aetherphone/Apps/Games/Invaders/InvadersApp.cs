using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Invaders;

internal sealed class InvadersApp : ILegacyMiniGame
{
    private const string GameId = "invaders";
    private const float WaveBannerSeconds = 1.6f;
    private static readonly Vector4[] CelebrationPalette =
    {
        new(0.98f, 0.95f, 0.90f, 1f), new(0.98f, 0.45f, 0.62f, 1f), new(1f, 0.62f, 0.30f, 1f),
        new(0.40f, 0.70f, 0.98f, 1f), new(0.72f, 0.50f, 0.96f, 1f), new(0.46f, 0.86f, 0.62f, 1f),
    };

    private readonly InvadersBoard board = new();
    private readonly InvadersRenderer renderer = new();
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
    private string resultWave = string.Empty;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Invaders);
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
        ShowWaveBanner();
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
        var factor = field.Width / InvadersBoard.Width;
        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        if (!finished)
        {
            var pad = GamePad.Shooter(padArea, Accent, theme);
            HandleInput(pad, deltaSeconds);
            var simDelta = fx.ScaleDelta(deltaSeconds);
            board.Update(simDelta);
            ReactToEvents(field, factor, scale);
        }

        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, deltaSeconds, WaveBannerSeconds);
        if (board.GameOver && !finished)
        {
            finished = true;
            resultAppear = 0f;
            pendingSubmit = true;
            resultWave = $"{Loc.T(L.Games.Wave)} {GameNumber.Label(board.Wave)}";
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
        GameBanner.Draw(drawList, new Vector2(field.Center.X, field.Min.Y + field.Height * 0.62f), bannerText, Accent,
            theme, bannerProgress);
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
        var factor = MathF.Min(availableWidth / InvadersBoard.Width, availableHeight / InvadersBoard.Height);
        var size = new Vector2(InvadersBoard.Width, InvadersBoard.Height) * factor;
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
            var muzzle = field.Min + new Vector2(board.PlayerX, InvadersBoard.PlayerY - InvadersBoard.PlayerHeight) * factor;
            particles.Streaks(muzzle, 3, GamePalette.Lighten(Accent, 0.4f), 110f * scale, 2f, 0.2f, 0.5f, -MathF.PI * 0.5f);
        }

        for (var index = 0; index < board.KillCount; index++)
        {
            var center = field.Min + board.KillPosition(index) * factor;
            var color = InvadersRenderer.KindColor(board.KillKind(index), Accent);
            particles.Burst(center, 10, color, 150f * scale, 2.4f, 0.5f, 220f, MathF.PI * 2f, 0f, ParticleShape.Square);
            particles.Sparkle(center, 4, new Vector4(1f, 1f, 1f, 1f), 100f * scale, 2f, 0.4f);
            fx.Shockwave(center, InvadersBoard.InvaderWidth * factor * 1.3f, color with { W = 0.6f }, 0.3f, 2f);
            fx.AddText(GameNumber.Label(InvadersBoard.RowPoints[RowForKind(board.KillKind(index))]), center, color, 0.9f);
        }

        if (board.KillCount > 0)
        {
            UiFeedback.Play(UiSound.GameExplosion);
            fx.AddTrauma(0.06f);
            fx.HitStop(0.03f);
        }

        for (var index = 0; index < board.ChipCount; index++)
        {
            var center = field.Min + board.ChipPosition(index) * factor;
            particles.Burst(center, 4, GamePalette.Lighten(Accent, 0.12f), 90f * scale, 1.6f, 0.35f, 260f, MathF.PI * 2f, 0f,
                ParticleShape.Square);
        }

        if (board.SaucerKilledThisFrame)
        {
            var center = field.Min + board.SaucerKillPosition * factor;
            particles.Sparkle(center, 16, InvadersRenderer.SaucerColor, 170f * scale, 2.8f, 0.8f);
            fx.Shockwave(center, InvadersBoard.SaucerHalfWidth * factor * 3f, InvadersRenderer.SaucerColor with { W = 0.7f },
                0.45f, 2.5f);
            fx.AddText($"+{GameNumber.Label(InvadersBoard.SaucerPoints)}", center, InvadersRenderer.SaucerColor, 1.2f);
            fx.HitStop(0.05f);
        }

        if (board.PlayerHitThisFrame)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            var center = field.Min + new Vector2(board.PlayerX, InvadersBoard.PlayerY) * factor;
            particles.Burst(center, 16, Accent, 170f * scale, 2.6f, 0.7f, 300f);
            fx.AddTrauma(0.7f);
            fx.HitStop(0.1f);
            fx.Flash(new Vector4(0.95f, 0.3f, 0.3f, 1f), 0.35f);
        }

        if (board.LandedThisFrame)
        {
            fx.AddTrauma(0.8f);
            fx.Flash(new Vector4(0.95f, 0.3f, 0.3f, 1f), 0.45f);
        }

        if (board.WaveClearedThisFrame)
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            var top = new Vector2(field.Center.X, field.Min.Y + field.Height * 0.2f);
            particles.Confetti(top, 60, CelebrationPalette, 260f * scale, 4f, 1.4f);
            fx.Flash(GamePalette.Lighten(Accent, 0.4f), 0.16f);
        }

        if (board.WaveStartedThisFrame)
        {
            ShowWaveBanner();
        }
    }

    private static int RowForKind(int kind)
    {
        for (var row = 0; row < InvadersBoard.Rows; row++)
        {
            if (InvadersBoard.RowKinds[row] == kind)
            {
                return row;
            }
        }

        return InvadersBoard.Rows - 1;
    }

    private void ShowWaveBanner()
    {
        bannerText = $"{Loc.T(L.Games.Wave)} {GameNumber.Label(board.Wave)}";
        bannerProgress = 0f;
    }

    private void DrawHud(Rect body, float rowY, PhoneTheme theme, float deltaSeconds, float scale)
    {
        var scoreLabel = Loc.T(L.Games.Score);
        var scoreText = GameNumber.Label(board.Score);
        var waveLabel = Loc.T(L.Games.Wave);
        var waveText = GameNumber.Label(board.Wave);
        var scoreWidth = GameHud.PillWidth(scoreLabel, scoreText);
        var waveWidth = GameHud.PillWidth(waveLabel, waveText);
        var gap = 12f * scale;
        var scoreX = body.Center.X - gap * 0.5f - scoreWidth * 0.5f;
        var waveX = body.Center.X + gap * 0.5f + waveWidth * 0.5f;
        var beatingBest = board.Score > 0 && board.Score > loadedBest;
        GameHud.ScorePill(new Vector2(scoreX, rowY), scoreLabel, ref scoreRoll, board.Score, Accent, theme, deltaSeconds,
            beatingBest);
        GameHud.Pill(new Vector2(waveX, rowY), waveLabel, waveText, Accent, theme);
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
        var pitch = InvadersRenderer.Cannon.Height * unit + 4f * scale;
        var origin = new Vector2(body.Min.X + 22f * scale, rowY);
        var lastLife = board.Lives == 1;
        for (var life = 0; life < InvadersBoard.StartingLives; life++)
        {
            var center = new Vector2(origin.X, origin.Y + (life - 1) * pitch);
            if (life >= board.Lives)
            {
                InvadersRenderer.Cannon.DrawCentered(drawList, center, unit,
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.2f)));
                continue;
            }

            var color = lastLife
                ? new Vector4(0.95f, 0.35f, 0.35f, 0.6f + 0.4f * Pulse.Wave(Pulse.Fast))
                : Accent;
            InvadersRenderer.Cannon.DrawCentered(drawList, center, unit, ImGui.GetColorU32(color));
        }
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        var result = new GameResult(Loc.T(L.Games.GameOver), theme.Danger, Loc.T(L.Games.Score),
            GameNumber.Label(board.Score), resultWave, newBest);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartNewGame();
        }
    }
}
