using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Skyfall;

internal sealed class SkyfallApp : ILegacyMiniGame
{
    private const string GameId = "skyfall";
    private const float WaveBannerSeconds = 1.6f;
    private const float ClearBannerSeconds = SkyfallBoard.WaveBreakSeconds;
    private const int LowAmmo = 5;
    private static readonly Vector4[] CelebrationPalette =
    {
        new(1f, 0.62f, 0.30f, 1f), new(1f, 0.85f, 0.45f, 1f), new(0.98f, 0.98f, 0.9f, 1f),
        new(0.40f, 0.70f, 0.98f, 1f), new(0.72f, 0.50f, 0.96f, 1f), new(0.46f, 0.86f, 0.62f, 1f),
    };

    private readonly SkyfallBoard board = new();
    private readonly SkyfallRenderer renderer = new();
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
    private string resultWave = string.Empty;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Skyfall);
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
        bannerProgress = 1f;
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
        var field = FieldRect(body, rowY, scale);
        var factor = field.Width / SkyfallBoard.Width;
        if (!finished)
        {
            HandleInput(field, factor);
            var simDelta = fx.ScaleDelta(deltaSeconds);
            board.Update(simDelta);
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
            resultWave = $"{Loc.T(L.Games.Wave)} {GameNumber.Label(board.Wave)}";
        }

        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        var shake = fx.ShakeOffset(scale);
        var shakenField = new Rect(field.Min + shake, field.Max + shake);
        DrawHud(body, rowY, theme, deltaSeconds, scale);
        GameScene.Arena(drawList, shakenField, 14f * scale, scale, Accent);
        renderer.Draw(board, shakenField, Accent, scale);
        fx.DrawFlash(drawList, field, 0f);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, new Vector2(field.Center.X, field.Min.Y + field.Height * 0.3f), bannerText, Accent,
            theme, bannerProgress);
        if (finished)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private static Rect FieldRect(Rect body, float rowY, float scale)
    {
        var pad = 6f * scale;
        var top = rowY + 26f * scale;
        var availableWidth = body.Width - pad * 2f;
        var availableHeight = body.Max.Y - pad - top;
        var factor = MathF.Min(availableWidth / SkyfallBoard.Width, availableHeight / SkyfallBoard.Height);
        var size = new Vector2(SkyfallBoard.Width, SkyfallBoard.Height) * factor;
        var min = new Vector2(body.Center.X - size.X * 0.5f, top + (availableHeight - size.Y) * 0.5f);
        return new Rect(min, min + size);
    }

    private void HandleInput(Rect field, float factor)
    {
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left) || !UiInteract.Hover(field.Min, field.Max))
        {
            return;
        }

        var target = (ImGui.GetMousePos() - field.Min) / factor;
        board.Fire(target);
    }

    private void ReactToEvents(Rect field, float factor, float scale)
    {
        if (board.ShotFiredThisFrame)
        {
            UiFeedback.Play(UiSound.GameShoot);
            var barrel = field.Min + new Vector2(SkyfallBoard.BatteryX, SkyfallBoard.BarrelY) * factor;
            particles.Streaks(barrel, 4, GamePalette.Lighten(Accent, 0.4f), 120f * scale, 2f, 0.25f, 0.6f,
                -MathF.PI * 0.5f);
        }

        if (board.DryFireThisFrame)
        {
            fx.AddTrauma(0.04f);
        }

        for (var index = 0; index < board.BlastSpawnCount; index++)
        {
            var center = field.Min + board.BlastSpawnPosition(index) * factor;
            fx.Shockwave(center, SkyfallBoard.BlastMaxRadius * factor * 1.6f, SkyfallRenderer.BlastFill with { W = 0.6f },
                0.4f, 2f);
            particles.Sparkle(center, 6, SkyfallRenderer.MeteorHead, 90f * scale, 2.2f, 0.5f);
        }

        for (var index = 0; index < board.DestroyedCount; index++)
        {
            var center = field.Min + board.DestroyedPosition(index) * factor;
            particles.Burst(center, 10, SkyfallRenderer.MeteorColor, 160f * scale, 2.4f, 0.5f, 240f);
            particles.Burst(center, 4, SkyfallRenderer.MeteorHead, 120f * scale, 1.8f, 0.35f, 200f, MathF.PI * 2f, 0f,
                ParticleShape.Square);
        }

        if (board.DestroyedCount > 0)
        {
            UiFeedback.Play(UiSound.GameExplosion);
            fx.AddTrauma(MathF.Min(0.25f, 0.04f * board.DestroyedCount));
            var last = field.Min + board.DestroyedPosition(board.DestroyedCount - 1) * factor;
            if (board.DestroyedCount >= 2)
            {
                fx.AddText($"x{board.DestroyedCount}", last, GamePalette.Lighten(Accent, 0.3f), 1.2f);
                fx.HitStop(0.03f);
            }
            else
            {
                fx.AddText("+25", last, SkyfallRenderer.MeteorHead, 0.9f);
            }
        }

        if (board.CityLostThisFrame >= 0)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            var city = field.Min + SkyfallBoard.CityCenter(board.CityLostThisFrame) * factor;
            particles.Burst(city, 18, new Vector4(0.6f, 0.55f, 0.6f, 1f), 140f * scale, 3f, 0.8f, 380f, MathF.PI, -MathF.PI * 0.5f,
                ParticleShape.Square);
            fx.AddTrauma(0.6f);
            fx.HitStop(0.08f);
            fx.Flash(new Vector4(0.95f, 0.3f, 0.3f, 1f), 0.3f);
        }

        if (board.WaveClearedThisFrame)
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            var top = new Vector2(field.Center.X, field.Min.Y + field.Height * 0.2f);
            particles.Confetti(top, 60, CelebrationPalette, 260f * scale, 4f, 1.4f);
            fx.Flash(GamePalette.Lighten(Accent, 0.4f), 0.16f);
            bannerText = $"{Loc.T(L.Games.WaveClear)}  +{GameNumber.Label(board.LastWaveBonus)}";
            bannerLifetime = ClearBannerSeconds;
            bannerProgress = 0f;
        }
        else if (board.WaveStartedThisFrame)
        {
            ShowWaveBanner();
        }
    }

    private void ShowWaveBanner()
    {
        bannerText = $"{Loc.T(L.Games.Wave)} {GameNumber.Label(board.Wave)}";
        bannerLifetime = WaveBannerSeconds;
        bannerProgress = 0f;
    }

    private void DrawHud(Rect body, float rowY, PhoneTheme theme, float deltaSeconds, float scale)
    {
        var scoreLabel = Loc.T(L.Games.Score);
        var scoreText = GameNumber.Label(board.Score);
        var waveLabel = Loc.T(L.Games.Wave);
        var waveText = GameNumber.Label(board.Wave);
        var ammoLabel = Loc.T(L.Games.Ammo);
        var ammoText = GameNumber.Label(board.Ammo);
        var restartRadius = 16f * scale;
        var restartCenter = new Vector2(body.Max.X - 22f * scale, rowY);
        var gap = 10f * scale;
        var available = restartCenter.X - restartRadius - gap - body.Min.X - 8f * scale;
        var natural = GameHud.PillWidth(scoreLabel, scoreText) + GameHud.PillWidth(waveLabel, waveText) +
            GameHud.PillWidth(ammoLabel, ammoText) + gap * 2f;
        var sizeScale = MathF.Min(1f, available / natural);
        var scoreWidth = GameHud.PillWidth(scoreLabel, scoreText, sizeScale);
        var waveWidth = GameHud.PillWidth(waveLabel, waveText, sizeScale);
        var ammoWidth = GameHud.PillWidth(ammoLabel, ammoText, sizeScale);
        var total = scoreWidth + waveWidth + ammoWidth + gap * 2f;
        var left = body.Min.X + 8f * scale + (available - total) * 0.5f;
        var beatingBest = board.Score > 0 && board.Score > loadedBest;
        GameHud.ScorePill(new Vector2(left + scoreWidth * 0.5f, rowY), scoreLabel, ref scoreRoll, board.Score, Accent,
            theme, deltaSeconds, beatingBest, sizeScale);
        left += scoreWidth + gap;
        GameHud.Pill(new Vector2(left + waveWidth * 0.5f, rowY), waveLabel, waveText, Accent, theme, false, sizeScale);
        left += waveWidth + gap;
        GameHud.Pill(new Vector2(left + ammoWidth * 0.5f, rowY), ammoLabel, ammoText, Accent, theme,
            board.Ammo <= LowAmmo && !board.InWaveBreak, sizeScale);
        if (GameHud.RestartButton(restartCenter, restartRadius, theme))
        {
            StartNewGame();
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
