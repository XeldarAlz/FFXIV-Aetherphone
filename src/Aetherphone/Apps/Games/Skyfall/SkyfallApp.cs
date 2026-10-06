using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Skyfall;

internal sealed class SkyfallApp : IMiniGame
{
    private const string GameId = "skyfall";
    private const float WaveBannerSeconds = 1.6f;
    private const float ClearBannerSeconds = SkyfallBoard.WaveBreakSeconds;
    private const int LowAmmo = 5;
    private const int MaxLights = 16;
    private const float ShotLightSeconds = 0.9f;
    private const float ImpactLightSeconds = 1.4f;
    private const float CapsulePadX = 10f;
    private const float IconSize = 11f;
    private const float IconGap = 5f;
    private const float LastMeteorSlowFactor = 0.5f;
    private const float LastMeteorSlowSeconds = 0.3f;
    private const ulong IdleSeed = 0x534B5946414C4CUL;
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Skyfall, GameGenre.Action, L.Skyfall.Hook,
        Backdrop.Neon, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true);
    private static readonly TextStyle AmmoStyle = TextStyles.FootnoteEmphasized;
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 CityDust = new(0.6f, 0.55f, 0.6f, 1f);
    private static readonly Vector4 ImpactLight = new(0.98f, 0.40f, 0.22f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4[] CelebrationPalette =
    {
        new(1f, 0.62f, 0.30f, 1f), new(1f, 0.85f, 0.45f, 1f), new(0.98f, 0.98f, 0.9f, 1f),
        new(0.40f, 0.70f, 0.98f, 1f), new(0.72f, 0.50f, 0.96f, 1f), new(0.46f, 0.86f, 0.62f, 1f),
    };

    private static readonly ParticleSpec[] ConfettiSpecs = BuildConfetti();

    private readonly SkyfallBoard board = new();
    private readonly SkyfallBoard idleBoard = new();
    private readonly SkyfallRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly GroundLight[] lights = new GroundLight[MaxLights];
    private Camera2D camera = Camera2D.Create();
    private LabelSlot waveLabel;
    private LabelSlot clearLabel;
    private LabelSlot multiKillLabel;
    private int lightCount;
    private bool finished;
    private bool idleReady;
    private float bannerProgress = 1f;
    private float bannerLifetime = 1f;
    private string bannerText = string.Empty;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.StartGame(start.Random);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        lightCount = 0;
        finished = false;
        bannerProgress = 1f;
        ShowWaveBanner();
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        if (!idleReady || idleBoard.GameOver)
        {
            idleBoard.StartGame(GameRandom.FromSeed(IdleSeed));
            idleReady = true;
        }

        var scale = UiScale.Current;
        PlaceCamera(context, scale);
        idleBoard.Update(context.RawDeltaSeconds);
        renderer.Draw(ImGui.GetWindowDrawList(), idleBoard, in camera, context.Full, Accent, scale,
            ReadOnlySpan<GroundLight>.Empty);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var accent = Accent;
        PlaceCamera(context, scale);
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        if (!finished)
        {
            board.Update(simDelta);
            if (context.Session.State == StageFlow.Playing)
            {
                HandleInput(context.Full, scale);
            }

            ReactToEvents(context, accent);
        }

        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        AdvanceLights(context.RawDeltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, context.DeltaSeconds, bannerLifetime);
        renderer.Draw(drawList, board, in camera, context.Full, accent, scale, lights.AsSpan(0, lightCount));
        particles.Draw(drawList, in camera);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, camera.ToScreen(new Vector2(SkyfallBoard.Width * 0.5f, SkyfallBoard.Height * 0.3f)),
            bannerText, accent, context.Theme, bannerProgress);
        DrawAmmo(drawList, context, accent, scale);
        context.Hud.Score(board.Score);
        context.Hud.Lives(board.CitiesLeft, SkyfallBoard.CityCount);
        context.Hud.Level(board.Wave);
        context.Hud.Best(context.Session.Best);
        context.Hud.Custom(AmmoCapsuleWidth(scale));
        context.Session.Report(board.Score);
        if (board.GameOver && !finished)
        {
            finished = true;
            Finish(context);
        }
    }

    private void PlaceCamera(in GameContext context, float scale)
    {
        var full = context.Full;
        var view = new Rect(new Vector2(full.Min.X, context.Safe.Min.Y), full.Max);
        camera.Fit(view, SkyfallBoard.Width, SkyfallBoard.Height, FitMode.Contain);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, scale);
        context.Backdrop.SetCamera(in camera);
    }

    private void HandleInput(Rect full, float scale)
    {
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        var hitMin = new Vector2(full.Min.X, full.Min.Y + StageLayout.ChromeBand * scale);
        if (!UiInteract.Hover(hitMin, full.Max))
        {
            return;
        }

        board.Fire(camera.ToWorld(ImGui.GetMousePos()));
    }

    private void ReactToEvents(in GameContext context, Vector4 accent)
    {
        if (board.ShotFiredThisFrame)
        {
            UiFeedback.Play(UiSound.GameShoot);
            var barrel = new Vector2(SkyfallBoard.BatteryX, SkyfallBoard.BarrelY);
            particles.Burst(barrel, 4, GamePalette.Lighten(accent, 0.4f), 31f, 0.5f, 0.25f, 56f, 0.6f, -MathF.PI * 0.5f,
                ParticleShape.Streak);
        }

        if (board.DryFireThisFrame)
        {
            camera.Shake(0.04f);
        }

        for (var index = 0; index < board.BlastSpawnCount; index++)
        {
            var center = board.BlastSpawnPosition(index);
            fx.Shockwave(camera.ToScreen(center), camera.Px(SkyfallBoard.BlastMaxRadius * 1.6f),
                SkyfallRenderer.BlastFill with { W = 0.6f }, 0.4f, 2f);
            particles.Emit(new ParticleSpec(SkyfallRenderer.MeteorHead, SkyfallRenderer.MeteorColor, 0.56f, 23f, 0.5f,
                10f, 2.4f, 6f, shape: ParticleShape.Star), center, 6);
            var strength = Math.Clamp(1f - (SkyfallBoard.GroundY - center.Y) / SkyfallBoard.GroundY, 0.15f, 1f);
            AddLight(center.X, strength, ShotLightSeconds, SkyfallRenderer.MeteorColor);
        }

        for (var index = 0; index < board.DestroyedCount; index++)
        {
            var center = board.DestroyedPosition(index);
            particles.Burst(center, 10, SkyfallRenderer.MeteorColor, 41f, 0.62f, 0.5f, 62f);
            particles.Burst(center, 4, SkyfallRenderer.MeteorHead, 31f, 0.46f, 0.35f, 51f, MathF.PI * 2f, 0f,
                ParticleShape.Square);
        }

        if (board.DestroyedCount > 0)
        {
            UiFeedback.Play(UiSound.GameExplosion);
            camera.Shake(MathF.Min(0.25f, 0.04f * board.DestroyedCount));
            var last = camera.ToScreen(board.DestroyedPosition(board.DestroyedCount - 1));
            if (board.DestroyedCount >= 2)
            {
                fx.AddText(multiKillLabel.Get(L.Stage.Times, board.DestroyedCount), last,
                    GamePalette.Lighten(accent, 0.3f), 1.2f);
                fx.HitStop(0.03f);
                context.Fx.Punch(0.04f);
            }
            else
            {
                fx.AddText(GameNumber.Label(SkyfallBoard.MeteorPoints), last, SkyfallRenderer.MeteorHead, 0.9f);
            }
        }

        if (board.LastMeteorDestroyedThisFrame)
        {
            context.Fx.SlowMo(LastMeteorSlowFactor, LastMeteorSlowSeconds);
        }

        if (board.ShieldCollectedThisFrame)
        {
            UiFeedback.Play(UiSound.GameCollect);
            var center = board.ShieldPosition;
            particles.Emit(new ParticleSpec(SkyfallRenderer.ShieldColor, White, 0.7f, 36f, 0.7f, 10f, 2.4f, 6f,
                shape: ParticleShape.Star), center, 14);
            fx.Shockwave(camera.ToScreen(center), camera.Px(10f), SkyfallRenderer.ShieldColor with { W = 0.7f }, 0.4f, 2f);
            fx.AddText(Loc.T(L.Skyfall.Shield), camera.ToScreen(center), SkyfallRenderer.ShieldColor, 1.1f);
        }

        if (board.ShieldAbsorbedCityThisFrame >= 0)
        {
            UiFeedback.Play(UiSound.GameMatch);
            var city = SkyfallBoard.CityCenter(board.ShieldAbsorbedCityThisFrame);
            var screen = camera.ToScreen(city);
            fx.Shockwave(screen, camera.Px(14f), SkyfallRenderer.ShieldColor with { W = 0.8f }, 0.45f, 3f);
            fx.AddText(Loc.T(L.Skyfall.Shielded), screen - new Vector2(0f, camera.Px(8f)), SkyfallRenderer.ShieldColor,
                1.1f);
            camera.Shake(0.2f);
            AddLight(city.X, 0.8f, ImpactLightSeconds, SkyfallRenderer.ShieldColor);
        }

        if (board.CityLostThisFrame >= 0)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            var city = SkyfallBoard.CityCenter(board.CityLostThisFrame);
            particles.Burst(city, 18, CityDust, 36f, 0.77f, 0.8f, 97f, MathF.PI, -MathF.PI * 0.5f, ParticleShape.Square);
            camera.Shake(0.6f);
            fx.HitStop(0.08f);
            context.Fx.Punch(0.06f);
            context.Fx.Flash(Danger, 0.3f);
            AddLight(city.X, 1f, ImpactLightSeconds, ImpactLight);
            if (board.CitiesLeft <= 2 && !board.GameOver)
            {
                context.Fx.Vignette(Danger, 0.5f, 0.8f);
            }
        }

        if (board.WaveClearedThisFrame)
        {
            GameSfx.LevelClear();
            context.Fx.Sweep();
            EmitConfetti(new Vector2(SkyfallBoard.Width * 0.5f, SkyfallBoard.Height * 0.2f), 60);
            context.Fx.Flash(GamePalette.Lighten(accent, 0.4f), 0.16f);
            bannerText = clearLabel.Get(L.Skyfall.WaveClearBonus, board.LastWaveBonus);
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
        bannerText = waveLabel.Get(L.Skyfall.WaveNumber, board.Wave);
        bannerLifetime = WaveBannerSeconds;
        bannerProgress = 0f;
    }

    private void EmitConfetti(Vector2 origin, int count)
    {
        var perColor = Math.Max(1, count / ConfettiSpecs.Length);
        for (var index = 0; index < ConfettiSpecs.Length; index++)
        {
            particles.Emit(in ConfettiSpecs[index], origin, perColor);
        }
    }

    private void AddLight(float x, float strength, float seconds, Vector4 color)
    {
        var slot = lightCount;
        if (slot >= MaxLights)
        {
            slot = 0;
            for (var index = 1; index < MaxLights; index++)
            {
                if (lights[index].Life < lights[slot].Life)
                {
                    slot = index;
                }
            }
        }
        else
        {
            lightCount++;
        }

        lights[slot] = new GroundLight
        {
            X = x,
            Strength = strength,
            Life = seconds,
            MaxLife = seconds,
            Color = color,
        };
    }

    private void AdvanceLights(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        for (var index = lightCount - 1; index >= 0; index--)
        {
            lights[index].Life -= deltaSeconds;
            if (lights[index].Life > 0f)
            {
                continue;
            }

            lights[index] = lights[lightCount - 1];
            lightCount--;
        }
    }

    private float AmmoCapsuleWidth(float scale)
    {
        var text = Typography.Measure(GameNumber.Label(board.Ammo), AmmoStyle).X / scale;
        return CapsulePadX * 2f + IconSize + IconGap + text;
    }

    private void DrawAmmo(ImDrawListPtr drawList, in GameContext context, Vector4 accent, float scale)
    {
        var rect = context.Hud.CustomRect(0);
        if (rect.Width <= 0f)
        {
            return;
        }

        StageHud.Capsule(drawList, rect, scale);
        var low = board.Ammo <= LowAmmo && !board.InWaveBreak && !board.GameOver;
        var pulse = low ? 0.5f + 0.5f * Pulse.Wave(Pulse.Fast) : 0f;
        var ink = low ? Vector4.Lerp(context.Theme.TextStrong, Danger, pulse) : context.Theme.TextStrong;
        var iconSize = IconSize * scale;
        var left = rect.Min.X + CapsulePadX * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, rect.Center.Y), FontAwesomeIcon.Crosshairs,
            low ? Danger : accent, iconSize);
        var origin = new Vector2(left + iconSize + IconGap * scale, rect.Center.Y - Typography.LineHeight(AmmoStyle) * 0.5f);
        Typography.Draw(drawList, origin, GameNumber.Label(board.Ammo), ink, AmmoStyle);
    }

    private void Finish(in GameContext context)
    {
        var outcome = new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Skyfall.WavesCleared, GameNumber.Label(board.Wave - 1))
            .WithStat(L.Skyfall.Meteors, GameNumber.Label(board.MeteorsDestroyed));
        if (board.ShotsFired > 0)
        {
            var percent = board.ShotsHit * 100 / board.ShotsFired;
            outcome = outcome.WithStat(L.Skyfall.Accuracy, Loc.T(L.Skyfall.Percent, GameNumber.Label(percent)));
        }

        context.Session.Finish(outcome);
    }

    private static ParticleSpec[] BuildConfetti()
    {
        var specs = new ParticleSpec[CelebrationPalette.Length];
        for (var index = 0; index < specs.Length; index++)
        {
            specs[index] = new ParticleSpec(CelebrationPalette[index], CelebrationPalette[index], 1f, 67f, 1.4f, 138f,
                0.7f, 16f, 1.4f, -MathF.PI * 0.5f, ParticleShape.Square);
        }

        return specs;
    }
}
