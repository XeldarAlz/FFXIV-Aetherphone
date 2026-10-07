using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Broadside;

internal static class BroadsideJuice
{
    public const float FlightSeconds = 0.42f;
    public const float MissHold = 0.55f;
    public const float HitHold = 0.8f;
    public const float SunkHold = 1.6f;
    public const float SinkSeconds = 1.4f;
    private const float RibbonWidth = 5f;
    private const float ArcFraction = 0.16f;
    private const float TextRise = 34f;
    private const float MuzzleWindow = 0.08f;
    private static readonly ParticleSpec Embers = new(BroadsideArt.Flame, BroadsideArt.Ember with { W = 0f }, 3.2f,
        260f, 0.6f, 320f, 1.6f, shape: ParticleShape.Spark, additive: true);
    private static readonly ParticleSpec Debris = new(BroadsideArt.Wreck, BroadsideArt.Smoke, 3.4f, 220f, 0.8f, 460f,
        1.2f, 12f, shape: ParticleShape.Shard);
    private static readonly ParticleSpec SmokePuffs = new(BroadsideArt.Smoke with { W = 0.7f },
        BroadsideArt.Smoke with { W = 0f }, 7f, 50f, 1.1f, -40f, 1.4f, curve: SizeCurve.Grow);
    private static readonly ParticleSpec CloudPuffs = new(BroadsideArt.Cloud with { W = 0.85f },
        BroadsideArt.Cloud with { W = 0f }, 6f, 90f, 0.7f, -20f, 2.4f, shape: ParticleShape.GlowCircle,
        curve: SizeCurve.Grow);
    private static readonly ParticleSpec Muzzle = new(BroadsideArt.Flame, BroadsideArt.Ember, 2.6f, 200f, 0.4f, 0f,
        2f, shape: ParticleShape.Spark, additive: true);

    public static void Splash(ParticleSystem particles, FeedbackFx fx, Vector2 center, float pitch, float scale)
    {
        particles.Emit(CloudPuffs, center, 10);
        fx.Shockwave(center, pitch * 1.2f, BroadsideArt.Cloud with { W = 0.6f }, 0.4f, 2f);
        fx.AddText(Loc.T(L.Broadside.Miss), center - new Vector2(0f, pitch * 0.6f), BroadsideArt.Muted, 1f,
            TextRise * scale);
    }

    public static void Struck(FeedbackFx fx, Vector2 center, float pitch, float scale)
    {
        fx.AddText(Loc.T(L.Broadside.Hit), center - new Vector2(0f, pitch * 0.6f), BroadsideArt.Flame, 1.2f,
            TextRise * scale);
    }

    public static void Explode(ParticleSystem particles, FeedbackFx fx, ScreenFx screen, bool live, Vector2 center,
        float pitch, bool mine, bool big)
    {
        var unit = pitch / 30f;
        var count = big ? 2 : 1;
        particles.Emit(Embers, center, 14 * count);
        particles.Emit(Debris, center, 8 * count);
        particles.Emit(SmokePuffs, center, 5 * count);
        particles.Burst(center, 10 * count, BroadsideArt.Ember, 150f * unit * 3f, 2.4f, 0.45f, 200f);
        fx.Shockwave(center, pitch * (big ? 2.6f : 1.6f), BroadsideArt.Flame, big ? 0.6f : 0.45f, big ? 3.6f : 2.8f);
        fx.AddTrauma(mine ? 0.45f : big ? 0.35f : 0.22f);
        if (!live)
        {
            return;
        }

        UiFeedback.Play(big ? UiSound.GameBreak : UiSound.GameExplosion);
        screen.Punch(big ? 0.06f : 0.035f);
        if (mine)
        {
            screen.Flash(BroadsideArt.Danger, 0.18f);
            screen.Vignette(BroadsideArt.Danger, 0.3f, 0.6f);
            return;
        }

        screen.Flash(BroadsideArt.Flame, big ? 0.16f : 0.08f);
    }

    public static void SinkBurst(ParticleSystem particles, FeedbackFx fx, Rect grid, ReadOnlySpan<int> cells,
        float pitch, bool live)
    {
        if (cells.Length == 0)
        {
            return;
        }

        for (var index = 0; index < cells.Length; index++)
        {
            var center = BroadsideLayout.CellCenter(grid, cells[index]);
            particles.Emit(SmokePuffs, center, 3);
            particles.Emit(Embers, center, 4);
        }

        fx.Shockwave(BroadsideLayout.CellCenter(grid, cells[cells.Length / 2]), pitch * cells.Length * 0.8f,
            BroadsideArt.Ember, 0.7f, 3f, pitch * 0.5f);
        if (live)
        {
            UiFeedback.Play(UiSound.GameClear);
        }
    }

    public static void DrawFlight(ImDrawListPtr drawList, Ribbon ribbon, ParticleSystem particles, Vector2 origin,
        Vector2 destination, float progress, float scale)
    {
        var clamped = Easing.Clamp01(progress);
        var eased = Easing.EaseInOutCubic(clamped);
        var distance = Vector2.Distance(origin, destination);
        var position = Vector2.Lerp(origin, destination, eased) +
                       new Vector2(MathF.Sin(clamped * MathF.PI) * distance * ArcFraction, 0f);
        ribbon.Push(position);
        ribbon.Draw(drawList, BroadsideArt.Flame with { W = 0.7f }, RibbonWidth * scale, true);
        BroadsideArt.DrawProjectile(drawList, position, 4.5f * scale, BroadsideArt.Flame);
        if (clamped < MuzzleWindow)
        {
            particles.Emit(Muzzle, position, 2);
        }
    }

    public static void DrawTrail(ImDrawListPtr drawList, Ribbon ribbon, float scale)
    {
        ribbon.Draw(drawList, BroadsideArt.Flame with { W = 0.5f }, RibbonWidth * scale, true);
    }
}
