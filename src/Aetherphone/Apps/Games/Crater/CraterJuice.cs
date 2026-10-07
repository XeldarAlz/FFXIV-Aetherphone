using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.World;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Crater;

internal sealed class CraterJuice
{
    public const float BannerSeconds = 1.3f;
    public const float AlertSeconds = 1.7f;
    public const float WinSeconds = 2.6f;
    public static readonly Vector4 Flash = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 Gold = new(1f, 0.82f, 0.32f, 1f);

    private const int BannerCapacity = 4;
    private const float BannerHeightShare = 0.3f;
    private const float BannerClearance = 10f;
    private const int EmberCapacity = 12;
    private const float EmberSeconds = 1.2f;
    private const float TrailSmokeRate = 26f;
    private const float TrailWidth = 0.09f;
    private const float TrailSpacing = 0.06f;
    private const float DrillDirtRate = 36f;
    private const int DirectHitDamage = 40;
    private const float TextRise = 52f;
    private const float HurtDecay = 3f;
    private const float Up = -MathF.PI * 0.5f;

    private static readonly ParticleSpec TrailSmoke = new(new Vector4(0.86f, 0.86f, 0.9f, 0.42f),
        new Vector4(0.6f, 0.6f, 0.64f, 0f), 0.09f, 0.35f, 0.7f, gravity: -0.5f, drag: 2f, curve: SizeCurve.Grow);
    private static readonly ParticleSpec Fireball = new(new Vector4(1f, 0.93f, 0.62f, 1f),
        new Vector4(1f, 0.32f, 0.08f, 0f), 0.34f, 3.4f, 0.45f, gravity: -1.5f, drag: 3.2f,
        shape: ParticleShape.GlowCircle, curve: SizeCurve.Grow, additive: true);
    private static readonly ParticleSpec Smoke = new(new Vector4(0.30f, 0.28f, 0.28f, 0.75f),
        new Vector4(0.52f, 0.52f, 0.55f, 0f), 0.36f, 1.6f, 1.4f, gravity: -0.9f, drag: 1.5f, curve: SizeCurve.Grow);
    private static readonly ParticleSpec Sparks = new(new Vector4(1f, 0.86f, 0.42f, 1f),
        new Vector4(1f, 0.38f, 0.1f, 0f), 0.05f, 9f, 0.55f, gravity: 9.8f, drag: 1.1f, shape: ParticleShape.Spark,
        additive: true);
    private static readonly ParticleSpec MuzzleFlash = new(new Vector4(1f, 0.95f, 0.75f, 1f),
        new Vector4(1f, 0.6f, 0.2f, 0f), 0.22f, 1.2f, 0.18f, drag: 4f, shape: ParticleShape.GlowCircle,
        additive: true);
    private static readonly ParticleSpec MuzzleStreaks = new(new Vector4(1f, 0.9f, 0.55f, 1f),
        new Vector4(1f, 0.45f, 0.1f, 0f), 0.06f, 7f, 0.22f, drag: 3f, spread: 0.5f, shape: ParticleShape.Streak,
        additive: true);
    private static readonly ParticleSpec Droplets = new(new Vector4(0.8f, 0.93f, 1f, 0.95f),
        new Vector4(0.4f, 0.7f, 0.95f, 0f), 0.06f, 4.6f, 0.8f, gravity: 9.8f, drag: 0.6f, spread: 1.3f,
        direction: Up);
    private static readonly ParticleSpec Bubbles = new(new Vector4(1f, 1f, 1f, 0.7f),
        new Vector4(0.7f, 0.9f, 1f, 0f), 0.06f, 0.8f, 1.2f, gravity: -1.6f, drag: 1.2f, spread: 1.2f, direction: Up,
        shape: ParticleShape.Ring);
    private static readonly ParticleSpec Feathers = new(CraterArt.Fur, CraterArt.Fur with { W = 0f }, 0.08f, 3.2f,
        1.4f, gravity: 1.2f, drag: 2.4f, spin: 9f, shape: ParticleShape.Square);
    private static readonly ParticleSpec Puff = new(new Vector4(1f, 1f, 1f, 0.8f),
        new Vector4(0.85f, 0.85f, 0.9f, 0f), 0.3f, 1.4f, 0.8f, gravity: -0.6f, drag: 2.2f, curve: SizeCurve.Grow);
    private static readonly ParticleSpec Dust = new(new Vector4(0.85f, 0.78f, 0.62f, 0.7f),
        new Vector4(0.7f, 0.64f, 0.52f, 0f), 0.14f, 1.4f, 0.6f, gravity: -0.2f, drag: 2.6f, spread: 1.4f,
        direction: Up, curve: SizeCurve.Grow);
    private static readonly ParticleSpec Sparkle = new(new Vector4(0.6f, 0.95f, 1f, 1f),
        new Vector4(1f, 1f, 1f, 0f), 0.09f, 2.6f, 0.8f, drag: 2f, spin: 6f, shape: ParticleShape.Star,
        additive: true);
    private static readonly ParticleSpec ShieldRing = new(CraterArt.ShieldTint, CraterArt.ShieldTint with { W = 0f },
        0.45f, 0.1f, 0.5f, drag: 1f, shape: ParticleShape.Ring, additive: true);
    private static readonly Vector4 DamageInk = new(1f, 0.5f, 0.38f, 1f);
    private static readonly Vector4 HitInk = new(1f, 0.86f, 0.4f, 1f);
    private static readonly Vector4 Shock = new(1f, 0.86f, 0.62f, 0.9f);
    private static readonly Vector4 Sea = new(0.35f, 0.65f, 0.98f, 1f);
    private static readonly Vector4 Danger = new(0.95f, 0.3f, 0.3f, 1f);
    private static readonly Vector4 TrailInk = new(1f, 1f, 1f, 0.5f);

    private readonly ParticleSystem particles = new(900);
    private readonly FeedbackFx fx = new();
    private readonly Ribbon[] trails = new Ribbon[CraterBoard.MaxProjectiles];
    private readonly Emitter[] smoke = new Emitter[CraterBoard.MaxProjectiles];
    private readonly float[] hurt = new float[CraterRules.MaxMoogles];
    private readonly string[] bannerTexts = new string[BannerCapacity];
    private readonly Vector4[] bannerColors = new Vector4[BannerCapacity];
    private readonly float[] bannerLifetimes = new float[BannerCapacity];
    private readonly Vector2[] emberCenters = new Vector2[EmberCapacity];
    private readonly float[] emberRadii = new float[EmberCapacity];
    private readonly float[] emberAges = new float[EmberCapacity];
    private int bannerCount;
    private int emberNext;
    private float bannerProgress;
    private float frameSeconds;
    private float drillDirt;

    public CraterJuice()
    {
        for (var index = 0; index < trails.Length; index++)
        {
            trails[index] = new Ribbon();
            smoke[index] = new Emitter(TrailSmoke, TrailSmokeRate);
        }
    }

    public ParticleSystem Particles => particles;

    public FeedbackFx Fx => fx;

    public ReadOnlySpan<float> Hurt => hurt;

    public void Reset(ulong seed)
    {
        particles.Clear();
        particles.Reseed(seed);
        fx.Clear();
        for (var index = 0; index < trails.Length; index++)
        {
            trails[index].Release();
            smoke[index].Reset();
        }

        Array.Clear(hurt);
        bannerCount = 0;
        bannerProgress = 0f;
        emberNext = 0;
        drillDirt = 0f;
        Array.Fill(emberAges, EmberSeconds);
    }

    public void Clear()
    {
        particles.Clear();
        fx.Clear();
    }

    public void Advance(float rawSeconds)
    {
        particles.Update(rawSeconds);
        fx.Update(rawSeconds);
        frameSeconds = rawSeconds;
        for (var index = 0; index < hurt.Length; index++)
        {
            hurt[index] = MathF.Max(0f, hurt[index] - rawSeconds * HurtDecay);
        }

        for (var index = 0; index < EmberCapacity; index++)
        {
            emberAges[index] = MathF.Min(EmberSeconds, emberAges[index] + rawSeconds);
        }

        if (bannerCount == 0)
        {
            return;
        }

        bannerProgress = GameBanner.Advance(bannerProgress, rawSeconds, bannerLifetimes[0]);
        if (bannerProgress < 1f)
        {
            return;
        }

        for (var index = 1; index < bannerCount; index++)
        {
            bannerTexts[index - 1] = bannerTexts[index];
            bannerColors[index - 1] = bannerColors[index];
            bannerLifetimes[index - 1] = bannerLifetimes[index];
        }

        bannerCount--;
        bannerProgress = 0f;
    }

    public void QueueBanner(string text, Vector4 color, float seconds)
    {
        var slot = Math.Min(bannerCount, BannerCapacity - 1);
        bannerTexts[slot] = text;
        bannerColors[slot] = color;
        bannerLifetimes[slot] = seconds;
        if (slot == 0)
        {
            bannerProgress = 0f;
        }

        bannerCount = slot + 1;
    }

    public void DrawBanners(ImDrawListPtr drawList, Rect full, PhoneTheme theme, float clearBelow)
    {
        if (bannerCount == 0)
        {
            return;
        }

        var lowest = clearBelow + BannerClearance * UiScale.Current + GameBanner.HalfHeight(TextStyles.Title2);
        var center = new Vector2(full.Center.X, MathF.Max(full.Min.Y + full.Height * BannerHeightShare, lowest));
        GameBanner.Draw(drawList, center, bannerTexts[0], bannerColors[0], theme, bannerProgress);
    }

    public void DrawEmbers(ImDrawListPtr drawList, in Camera2D camera)
    {
        for (var index = 0; index < EmberCapacity; index++)
        {
            var fade = 1f - emberAges[index] / EmberSeconds;
            CraterRenderer.Embers(drawList, in camera, emberCenters[index], emberRadii[index], fade * fade);
        }
    }

    public void DrawTrails(ImDrawListPtr drawList, in Camera2D camera, ReadOnlySpan<CraterProjectile> projectiles,
        TerrainMaterial material, float scale)
    {
        var width = MathF.Max(1.5f * scale, camera.Px(TrailWidth));
        for (var index = 0; index < projectiles.Length && index < trails.Length; index++)
        {
            ref readonly var projectile = ref projectiles[index];
            var trail = trails[index];
            if (!projectile.Alive || projectile.Drilling)
            {
                if (trail.Owner != -1)
                {
                    trail.Release();
                }

                if (projectile.Alive)
                {
                    EmitDrillDirt(projectile.Position, material);
                }

                continue;
            }

            trail.Claim(projectile.Id);
            if (trail.Count == 0 ||
                Vector2.DistanceSquared(trail.Point(0), projectile.Position) > TrailSpacing * TrailSpacing)
            {
                trail.Push(projectile.Position);
            }

            smoke[index].Advance(frameSeconds, projectile.Position, particles);
            trail.Draw(drawList, in camera, TrailInk, width, additive: true);
        }
    }

    public void Play(in CraterEvent entry, ref Camera2D camera, ScreenFx screen, TerrainMaterial material, bool quiet)
    {
        switch (entry.Kind)
        {
            case CraterEventKind.Launched:
                OnLaunched(entry, ref camera, quiet);
                break;
            case CraterEventKind.Bounced:
                particles.Emit(Dust, entry.Position, 3);
                Sound(UiSound.GameHitWood, quiet);
                break;
            case CraterEventKind.Exploded:
                OnExploded(entry, ref camera, screen, material, quiet);
                break;
            case CraterEventKind.Damaged:
                OnDamaged(entry, in camera, quiet);
                break;
            case CraterEventKind.Shielded:
                particles.Emit(ShieldRing, entry.Position, 2);
                particles.Emit(Sparkle, entry.Position, 10);
                Say(Loc.T(L.Crater.Blocked), entry.Position, CraterArt.ShieldTint, 1.15f, in camera);
                Sound(UiSound.GameMatch, quiet);
                break;
            case CraterEventKind.Died:
                OnDied(entry, ref camera, screen, quiet);
                break;
            case CraterEventKind.Drowned:
                particles.Emit(Droplets, entry.Position, 16);
                particles.Emit(Bubbles, entry.Position, 8);
                fx.Shockwave(camera.ToScreen(entry.Position), camera.Px(1.2f), Sea, 0.5f, 3f);
                Say(Loc.T(L.Crater.Drowned), entry.Position, GamePalette.Lighten(Sea, 0.3f), 1.15f, in camera);
                Sound(UiSound.GamePop, quiet);
                break;
            case CraterEventKind.Splashed:
                particles.Emit(Droplets, entry.Position, 10);
                fx.Shockwave(camera.ToScreen(entry.Position), camera.Px(0.7f), Sea, 0.4f, 2.5f);
                Sound(UiSound.GamePop, quiet);
                break;
            case CraterEventKind.ClusterSplit:
                particles.Emit(Sparks, entry.Position, 12);
                Sound(UiSound.GamePop, quiet);
                break;
            case CraterEventKind.DrillStarted:
                particles.Emit(Debris(0.07f, 5f, material), entry.Position, 10);
                Sound(UiSound.GameHitWood, quiet);
                break;
            case CraterEventKind.Jumped:
                particles.Emit(Dust, Feet(entry.Position), 4);
                Sound(UiSound.GameJump, quiet);
                break;
            case CraterEventKind.Landed:
                particles.Emit(Dust, Feet(entry.Position), 6);
                break;
            case CraterEventKind.FallHurt:
                particles.Emit(Dust, Feet(entry.Position), 10);
                camera.Shake(0.15f);
                break;
            case CraterEventKind.Teleported:
                particles.Emit(Sparkle, entry.Position, 14);
                particles.Emit(Sparkle, entry.Target, 14);
                particles.Emit(ShieldRing, entry.Target, 1);
                Sound(UiSound.GamePowerUp, quiet);
                break;
            case CraterEventKind.ShieldRaised:
                particles.Emit(ShieldRing, entry.Position, 2);
                particles.Emit(Sparkle, entry.Position, 10);
                Say(Loc.T(L.Crater.ShieldUp), entry.Position, CraterArt.ShieldTint, 1.15f, in camera);
                Sound(UiSound.GamePowerUp, quiet);
                break;
            case CraterEventKind.TimeUp:
                Alert(Loc.T(L.Crater.TimeUp), Danger, BannerSeconds, quiet);
                break;
            case CraterEventKind.SuddenDeath:
                Alert(Loc.T(L.Crater.SuddenDeath), Danger, AlertSeconds, quiet);
                VignetteSea(screen, 0.35f, quiet);
                break;
            case CraterEventKind.WaterRising:
                Alert(Loc.T(L.Crater.WaterRises), Sea, BannerSeconds, quiet);
                VignetteSea(screen, 0.2f, quiet);
                break;
            default:
                break;
        }
    }

    public void Celebrate(ReadOnlySpan<CraterMoogle> moogles, int winner, Vector4 color)
    {
        for (var index = 0; index < moogles.Length; index++)
        {
            if (!moogles[index].Alive || moogles[index].Team != winner)
            {
                continue;
            }

            var origin = moogles[index].Position;
            particles.Emit(Confetti(color), origin, 18);
            particles.Emit(Confetti(Gold), origin, 10);
        }
    }

    public static void Sound(UiSound sound, bool quiet)
    {
        if (!quiet)
        {
            UiFeedback.Play(sound);
        }
    }

    private void OnLaunched(in CraterEvent entry, ref Camera2D camera, bool quiet)
    {
        var angle = MathF.Atan2(entry.Target.Y, entry.Target.X);
        particles.Emit(MuzzleFlash, entry.Position, 3);
        particles.Emit(MuzzleStreaks.WithDirection(angle, 0.5f), entry.Position, 8);
        particles.Emit(Smoke, entry.Position, 3);
        camera.Shake(0.12f);
        Sound(UiSound.GameShoot, quiet);
    }

    private void OnExploded(in CraterEvent entry, ref Camera2D camera, ScreenFx screen, TerrainMaterial material,
        bool quiet)
    {
        var radius = entry.Radius;
        var center = entry.Position;
        var big = radius >= 1f;
        particles.Emit(Fireball, center, big ? 12 : 7);
        particles.Emit(Smoke, center, big ? 9 : 5);
        particles.Emit(Sparks, center, big ? 16 : 9);
        particles.Emit(Debris(0.075f, 6.5f, material), center, big ? 18 : 10);
        particles.Emit(new ParticleSpec(material.Edge, material.Surface, 0.05f, 5f, 1f, gravity: 9.8f, drag: 0.6f,
            spin: 8f, spread: 2f, direction: Up, shape: ParticleShape.Square), center, big ? 8 : 4);
        fx.Shockwave(camera.ToScreen(center), camera.Px(radius * 2.2f), Shock, 0.45f, 4f);
        AddEmber(center, radius);
        camera.Shake(0.22f + radius * 0.2f);
        if (quiet)
        {
            return;
        }

        screen.Punch(0.03f + radius * 0.025f);
        if (big)
        {
            screen.Flash(Flash, 0.1f);
        }

        UiFeedback.Play(UiSound.GameExplosion);
    }

    private void OnDamaged(in CraterEvent entry, in Camera2D camera, bool quiet)
    {
        if (entry.Moogle >= 0 && entry.Moogle < hurt.Length)
        {
            hurt[entry.Moogle] = 1f;
        }

        var scale = UiScale.Current;
        var head = camera.ToScreen(entry.Position - new Vector2(0f, CraterRules.MoogleRadius * 1.8f));
        fx.AddText(GameNumber.Label(-entry.Value), head, DamageInk, 1.1f + MathF.Min(0.6f, entry.Value / 80f),
            TextRise * scale);
        if (entry.Value >= DirectHitDamage)
        {
            fx.AddText(Loc.T(L.Crater.DirectHit), head - new Vector2(0f, 24f * scale), HitInk, 1.2f, TextRise * scale);
            if (!quiet)
            {
                fx.HitStop(0.05f);
            }
        }

        Sound(UiSound.GameHitSoft, quiet);
    }

    private void OnDied(in CraterEvent entry, ref Camera2D camera, ScreenFx screen, bool quiet)
    {
        particles.Emit(Feathers, entry.Position, 16);
        particles.Emit(Puff, entry.Position, 8);
        Say(Loc.T(L.Crater.KnockedOut), entry.Position, GamePalette.Lighten(GameSeats.Color(entry.Team), 0.3f), 1.2f,
            in camera);
        camera.Shake(0.3f);
        if (quiet)
        {
            return;
        }

        UiFeedback.Play(UiSound.GameBreak);
        fx.HitStop(0.06f);
        screen.Punch(0.05f);
    }

    private void AddEmber(Vector2 center, float radius)
    {
        emberCenters[emberNext] = center;
        emberRadii[emberNext] = radius;
        emberAges[emberNext] = 0f;
        emberNext = (emberNext + 1) % EmberCapacity;
    }

    private void EmitDrillDirt(Vector2 position, TerrainMaterial material)
    {
        drillDirt += frameSeconds * DrillDirtRate;
        var count = (int)drillDirt;
        if (count <= 0)
        {
            return;
        }

        drillDirt -= count;
        particles.Emit(Debris(0.06f, 3.5f, material), position, count);
    }

    private static ParticleSpec Debris(float size, float speed, TerrainMaterial material) =>
        new(material.Surface, material.Deep, size, speed, 1.2f, gravity: 9.8f, drag: 0.5f, spin: 10f, spread: 2.2f,
            direction: Up, shape: ParticleShape.Shard);

    private static ParticleSpec Confetti(Vector4 color) => new(color, color with { W = 0f }, 0.09f, 6f, 2.2f,
        gravity: 4f, drag: 1.2f, spin: 12f, spread: 1.6f, direction: Up, shape: ParticleShape.Square);

    private void Say(string text, Vector2 world, Vector4 color, float size, in Camera2D camera)
    {
        var scale = UiScale.Current;
        var screen = camera.ToScreen(world - new Vector2(0f, CraterRules.MoogleRadius * 2.6f));
        fx.AddText(text, screen, color, size, TextRise * scale);
    }

    private void Alert(string text, Vector4 color, float seconds, bool quiet)
    {
        if (quiet)
        {
            return;
        }

        QueueBanner(text, color, seconds);
        UiFeedback.Play(UiSound.GameWrong);
    }

    private static void VignetteSea(ScreenFx screen, float strength, bool quiet)
    {
        if (!quiet)
        {
            screen.Vignette(Sea, strength, 1.4f);
        }
    }

    private static Vector2 Feet(Vector2 center) => center + new Vector2(0f, CraterRules.MoogleRadius);
}
