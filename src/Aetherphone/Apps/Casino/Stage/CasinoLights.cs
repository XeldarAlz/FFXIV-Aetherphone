using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Stage;

internal static class CasinoLights
{
    public const float BulbDiameter = 6f;
    public const float BulbPitch = 14f;
    public const float ChaseBulbsPerSecond = 6f;
    public const float NeonHaloScale = 2.4f;
    public const float NeonHaloAlpha = 0.18f;
    public const float CoinGravity = 640f;
    public const float CoinRadius = 3.4f;
    public const float CoinLaunchSpeed = 300f;
    public const float CoinLife = 1.15f;
    public const float CoinDrag = 0.35f;
    public const float CoinSpin = 14f;
    public const float CoinSpread = 0.75f;
    public const float CoinRainSpeed = 70f;
    public const float CoinRainLife = 1.7f;
    public const float CoinRainGravity = 360f;
    public const float CoinRainSpread = 0.5f;
    public const int DarkEvery = 3;

    private const float DimBulbAlpha = 0.16f;
    private const float BulbHaloScale = 2.3f;
    private const float BulbHaloAlpha = 0.20f;
    private const int BulbSegments = 12;
    private const float QuarterTurn = MathF.PI * 0.5f;

    public static int BulbCount(Rect rect, float radius, float pitch)
    {
        if (pitch <= 0f)
        {
            return 0;
        }

        return Math.Max(0, (int)MathF.Floor(Perimeter(rect, radius) / pitch));
    }

    public static float Perimeter(Rect rect, float radius)
    {
        var corner = Math.Clamp(radius, 0f, MathF.Min(rect.Width, rect.Height) * 0.5f);
        var straight = (rect.Width - corner * 2f) * 2f + (rect.Height - corner * 2f) * 2f;
        return MathF.Max(0f, straight) + MathF.PI * 2f * corner;
    }

    public static Vector2 PointOnPerimeter(Rect rect, float radius, float distance)
    {
        var corner = Math.Clamp(radius, 0f, MathF.Min(rect.Width, rect.Height) * 0.5f);
        var width = MathF.Max(0f, rect.Width - corner * 2f);
        var height = MathF.Max(0f, rect.Height - corner * 2f);
        var arc = QuarterTurn * corner;
        var total = (width + height) * 2f + arc * 4f;
        if (total <= 0f)
        {
            return rect.Min;
        }

        var travel = distance - MathF.Floor(distance / total) * total;
        if (travel < width)
        {
            return new Vector2(rect.Min.X + corner + travel, rect.Min.Y);
        }

        travel -= width;
        if (travel < arc)
        {
            return Arc(new Vector2(rect.Max.X - corner, rect.Min.Y + corner), corner, -QuarterTurn, travel, arc);
        }

        travel -= arc;
        if (travel < height)
        {
            return new Vector2(rect.Max.X, rect.Min.Y + corner + travel);
        }

        travel -= height;
        if (travel < arc)
        {
            return Arc(new Vector2(rect.Max.X - corner, rect.Max.Y - corner), corner, 0f, travel, arc);
        }

        travel -= arc;
        if (travel < width)
        {
            return new Vector2(rect.Max.X - corner - travel, rect.Max.Y);
        }

        travel -= width;
        if (travel < arc)
        {
            return Arc(new Vector2(rect.Min.X + corner, rect.Max.Y - corner), corner, QuarterTurn, travel, arc);
        }

        travel -= arc;
        if (travel < height)
        {
            return new Vector2(rect.Min.X, rect.Max.Y - corner - travel);
        }

        travel -= height;
        return Arc(new Vector2(rect.Min.X + corner, rect.Min.Y + corner), corner, MathF.PI, travel, arc);
    }

    public static bool BulbLit(int bulbIndex, float phase)
    {
        var step = (int)MathF.Floor(phase * ChaseBulbsPerSecond);
        var slot = (bulbIndex + step) % DarkEvery;
        return slot != 0;
    }

    public static void BulbChase(ImDrawListPtr drawList, Rect rect, float radius, float scale, float phase,
        float spacing, Vector4 colorA, Vector4 colorB, float lit)
    {
        var pitch = (spacing > 0f ? spacing : BulbPitch) * scale;
        var count = BulbCount(rect, radius, pitch);
        if (count <= 0)
        {
            return;
        }

        var bulbRadius = BulbDiameter * 0.5f * scale;
        var level = Math.Clamp(lit, 0f, 1f);
        var step = Perimeter(rect, radius) / count;
        for (var bulbIndex = 0; bulbIndex < count; bulbIndex++)
        {
            var center = PointOnPerimeter(rect, radius, bulbIndex * step);
            var tint = (bulbIndex & 1) == 0 ? colorA : colorB;
            var on = level > 0f && BulbLit(bulbIndex, phase);
            if (!on)
            {
                drawList.AddCircleFilled(center, bulbRadius, ImGui.GetColorU32(tint with { W = DimBulbAlpha }),
                    BulbSegments);
                continue;
            }

            drawList.AddCircleFilled(center, bulbRadius * BulbHaloScale,
                ImGui.GetColorU32(tint with { W = BulbHaloAlpha * level }), BulbSegments);
            drawList.AddCircleFilled(center, bulbRadius,
                ImGui.GetColorU32(Vector4.Lerp(tint, Vector4.One, 0.45f) with { W = MathF.Max(DimBulbAlpha, level) }),
                BulbSegments);
        }
    }

    public static void NeonTube(ImDrawListPtr drawList, ReadOnlySpan<Vector2> path, Vector4 color, float width,
        float glow)
    {
        if (path.Length < 2 || color.W <= 0f)
        {
            return;
        }

        var halo = ImGui.GetColorU32(color with { W = color.W * NeonHaloAlpha * Math.Clamp(glow, 0f, 2f) });
        drawList.PathClear();
        for (var index = 0; index < path.Length; index++)
        {
            drawList.PathLineTo(path[index]);
        }

        drawList.PathStroke(halo, ImDrawFlags.None, width * NeonStroke.HaloScale * NeonHaloScale * 0.5f);
        NeonStroke.Path(drawList, path, false, color, width);
    }

    public static void Spotlight(ImDrawListPtr drawList, Vector2 origin, float direction, float length, float spread,
        Vector4 color, float alpha)
    {
        if (alpha <= 0f || length <= 0f)
        {
            return;
        }

        StageBackdrop.Cone(drawList, origin, direction, length, spread, color, alpha);
    }

    public static void Bokeh(ImDrawListPtr drawList, Rect rect, float phase, float density, float scale)
    {
        StageBackdrop.Bokeh(drawList, rect, phase, density, Vector2.Zero, scale);
    }

    public static ParticleSpec CoinShower(float scale) =>
        new(CasinoColors.Money, CasinoColors.MoneyHighlight, CoinRadius * scale, CoinRainSpeed * scale, CoinRainLife,
            CoinRainGravity * scale, CoinDrag, CoinSpin, CoinRainSpread, MathF.PI * 0.5f, ParticleShape.Coin,
            SizeCurve.Shrink, false);

    public static ParticleSpec CoinFountain(float scale) =>
        new(CasinoColors.Money, CasinoColors.MoneyHighlight, CoinRadius * scale, CoinLaunchSpeed * scale, CoinLife,
            CoinGravity * scale, CoinDrag, CoinSpin, CoinSpread, -MathF.PI * 0.5f, ParticleShape.Coin, SizeCurve.Shrink,
            false);

    public static ParticleSpec Sparkle(float scale) =>
        new(CasinoColors.MoneyHighlight, CasinoColors.Money, 3f * scale, 150f * scale, 0.8f, 40f * scale, 2.2f, 6f,
            MathF.PI * 2f, 0f, ParticleShape.Star, SizeCurve.Shrink, true);

    public static ParticleSpec Shard(float scale) =>
        new(CasinoColors.MoneyHighlight, CasinoColors.LightA, 5f * scale, 320f * scale, 1.2f, 260f * scale, 1.2f, 14f,
            MathF.PI * 2f, 0f, ParticleShape.Shard, SizeCurve.Shrink, true);

    public static ParticleSpec Ring(float scale) =>
        new(CasinoColors.Money, CasinoColors.LightA, 60f * scale, 0f, 0.9f, 0f, 0f, 0f, 0f, 0f, ParticleShape.Ring,
            SizeCurve.Grow, true);

    public static Emitter CoinShowerEmitter(float scale, float rate) => new(CoinShower(scale), rate);

    public static void LightSweep(StageBackdrop backdrop, float strength)
    {
        backdrop.Sweep(strength);
    }

    private static Vector2 Arc(Vector2 center, float radius, float startAngle, float travel, float arcLength)
    {
        var angle = startAngle + QuarterTurn * (arcLength <= 0f ? 0f : travel / arcLength);
        return center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
    }
}
