using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.MiniGolf;

internal readonly record struct GolfImpact(GolfEvents Events, Vector2 Point, float Strength, Vector2 Velocity,
    Vector2 TunnelFrom);

internal sealed class MiniGolfJuice
{
    public const float TrailSpeed = 2.2f;
    private const float TrailWidth = 0.13f;
    private const float FlashDecay = 4f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Danger = new(0.95f, 0.32f, 0.32f, 1f);
    private static readonly Vector4 Dust = new(0.86f, 0.80f, 0.62f, 0.9f);
    private static readonly Vector4 AceFlash = new(1f, 0.86f, 0.4f, 1f);
    private static readonly Vector4[] AcePalette =
    {
        new(1f, 0.84f, 0.30f, 1f), new(0.98f, 0.72f, 0.18f, 1f), new(1f, 0.93f, 0.62f, 1f),
        new(0.42f, 0.86f, 0.52f, 1f), new(1f, 1f, 1f, 1f), new(0.40f, 0.78f, 1f, 1f),
    };
    private static readonly ParticleSpec GrassBits = new(MiniGolfRenderer.Grass, MiniGolfRenderer.Fairway with { W = 0f },
        0.05f, 2.2f, 0.45f, 0f, 3f, 8f, shape: ParticleShape.Square);
    private static readonly ParticleSpec WallDust = new(White with { W = 0.8f }, White with { W = 0f }, 0.06f, 1.6f,
        0.35f, 0f, 3f, shape: ParticleShape.GlowCircle, curve: SizeCurve.Grow);
    private static readonly ParticleSpec SandPuff = new(Dust, MiniGolfRenderer.SandTint with { W = 0f }, 0.1f, 1.4f,
        0.6f, 0f, 2.6f, shape: ParticleShape.GlowCircle, curve: SizeCurve.Grow);
    private static readonly ParticleSpec Droplets = new(White, MiniGolfRenderer.WaterTint with { W = 0f }, 0.06f, 3.2f,
        0.6f, 0f, 2.2f);
    private static readonly ParticleSpec CupSparkle = new(White, new Vector4(1f, 0.84f, 0.3f, 0f), 0.08f, 2.6f, 0.8f,
        0f, 2f, 6f, shape: ParticleShape.Star, additive: true);

    public ParticleSystem Particles { get; } = new(320);

    public ParticleSystem Celebration { get; } = new(160);

    public FeedbackFx Fx { get; } = new();

    public Ribbon Trail { get; } = new();

    public float BumpFlash { get; private set; }

    public Vector2 FlashPoint { get; private set; }

    public void Clear()
    {
        Particles.Clear();
        Celebration.Clear();
        Fx.Clear();
    }

    public void Update(float rawSeconds)
    {
        Particles.Update(rawSeconds);
        Celebration.Update(rawSeconds);
        Fx.Update(rawSeconds);
        BumpFlash = MathF.Max(0f, BumpFlash - rawSeconds * FlashDecay);
    }

    public void React(in GolfImpact impact, ref Camera2D camera, ScreenFx screen, Vector4 accent, bool live)
    {
        var events = impact.Events;
        if (events == GolfEvents.None)
        {
            return;
        }

        var point = impact.Point;
        if ((events & GolfEvents.Shot) != 0)
        {
            var direction = Vector2.Normalize(impact.Velocity + new Vector2(0.0001f, 0f));
            Particles.Emit(GrassBits.WithDirection(MathF.Atan2(-direction.Y, -direction.X), 1.4f), point, 8);
            Trail.Clear();
            if (live)
            {
                UiFeedback.Play(UiSound.GameHitWood);
                camera.Shake(0.03f + 0.06f * impact.Strength);
                screen.Punch(0.01f + 0.025f * impact.Strength);
            }
        }

        if ((events & GolfEvents.Wall) != 0)
        {
            Particles.Emit(WallDust, point, 5);
            if (live && impact.Strength > 2f)
            {
                UiFeedback.Play(UiSound.GameHitSoft);
                camera.Shake(MathF.Min(0.2f, impact.Strength * 0.025f));
            }
        }

        if ((events & GolfEvents.Post) != 0)
        {
            BumpFlash = 1f;
            FlashPoint = point;
            Fx.Shockwave(camera.ToScreen(point), camera.Px(0.9f), MiniGolfRenderer.BallWhite, 0.35f, 2.4f);
            if (live)
            {
                UiFeedback.Play(UiSound.GamePop);
                screen.Punch(0.03f);
            }
        }

        if ((events & GolfEvents.Mill) != 0 && live)
        {
            UiFeedback.Play(UiSound.GameHitWood);
            camera.Shake(0.15f);
        }

        if ((events & GolfEvents.Tunnel) != 0)
        {
            Trail.Clear();
            Fx.Shockwave(camera.ToScreen(impact.TunnelFrom), camera.Px(0.8f), White, 0.3f, 2f);
            Fx.Shockwave(camera.ToScreen(point), camera.Px(1f), accent, 0.4f, 2.6f);
            if (live)
            {
                UiFeedback.Play(UiSound.GameJump);
            }
        }

        if ((events & GolfEvents.Sand) != 0)
        {
            Particles.Emit(SandPuff, point, 10);
        }

        if ((events & GolfEvents.Splash) != 0)
        {
            Particles.Emit(Droplets, point, 22);
            Fx.Shockwave(camera.ToScreen(point), camera.Px(1f), MiniGolfRenderer.WaterTint, 0.5f, 2.6f);
            if (live)
            {
                UiFeedback.Play(UiSound.GameWrong);
                Fx.AddText(Loc.T(L.MiniGolf.Penalty), camera.ToScreen(point), Danger, 1.1f);
                camera.Shake(0.12f);
            }
        }

        if ((events & GolfEvents.Reset) != 0 && live)
        {
            UiFeedback.Play(UiSound.GameWrong);
        }

        if ((events & GolfEvents.LipOut) != 0 && live)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            Fx.AddText(Loc.T(L.MiniGolf.LipOut), camera.ToScreen(point) - new Vector2(0f, camera.Px(0.4f)), White, 1f);
        }

        if ((events & GolfEvents.Drop) != 0)
        {
            Fx.Shockwave(camera.ToScreen(point), camera.Px(0.6f), White, 0.3f, 2.2f);
            if (live)
            {
                UiFeedback.Play(UiSound.GameCollect);
                screen.SlowMo(0.5f, 0.3f);
            }
        }

        if ((events & GolfEvents.Stopped) != 0)
        {
            Trail.Clear();
        }
    }

    public void Holed(HoleResult result, Vector2 cup, Rect full, in Camera2D camera, ScreenFx screen)
    {
        var color = MiniGolfScorecard.ResultColor(result);
        Particles.Emit(CupSparkle, cup, 18 + (result <= HoleResult.Birdie ? 18 : 0));
        Fx.Shockwave(camera.ToScreen(cup), camera.Px(1.4f), color, 0.55f, 3.2f);
        if (result == HoleResult.HoleInOne)
        {
            UiFeedback.Play(UiSound.GameWin);
            Celebration.Confetti(new Vector2(full.Center.X, full.Min.Y + full.Height * 0.3f), 110, AcePalette,
                320f * UiScale.Current, 4.2f, 1.6f);
            screen.Flash(AceFlash, 0.35f);
            screen.Punch(0.1f);
            screen.Sweep();
            return;
        }

        GameSfx.LevelClear();
        if (result > HoleResult.Birdie)
        {
            return;
        }

        screen.Punch(0.05f);
        screen.Sweep();
    }

    public void TrackBall(Vector2 ball, bool rolling, float speed)
    {
        if (rolling && speed > TrailSpeed)
        {
            Trail.Push(ball);
        }
    }

    public void DrawTrail(ImDrawListPtr drawList, in Camera2D camera)
    {
        Trail.Draw(drawList, in camera, White with { W = 0.45f }, camera.Px(TrailWidth), additive: true);
    }

    public void DrawEffects(ImDrawListPtr drawList, in Camera2D camera, float scale)
    {
        Particles.Draw(drawList, in camera);
        Fx.DrawRings(drawList, scale);
        Fx.DrawText();
    }
}
