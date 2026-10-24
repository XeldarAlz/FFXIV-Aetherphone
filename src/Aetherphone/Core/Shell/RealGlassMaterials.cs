using Aetherphone.Windows.Components;
using Glasswork.Glass;

namespace Aetherphone.Core.Shell;

internal static class RealGlassMaterials
{
    public const float AccentTintAmount = 0.55f;
    public const float FrostedFrost = 1f;
    public const int FrostedBlurLevels = 4;
    private const float CapsuleTolerance = 0.5f;

    public static GlassMaterial Build(RealGlassKind kind, GlassTone tone, Vector4 accent, float opacity, float amount,
        float radius, Vector2 size)
    {
        var material = Base(kind, tone == GlassTone.Light, accent).WithAmount(amount);
        material.Opacity = Math.Clamp(opacity, 0f, 1f);
        material.CornerExponent = CornerExponent(radius, size);
        return material;
    }

    public static GlassShape Shape(Vector2 min, Vector2 max, float radius) => GlassShape.FromRect(min, max - min, radius);

    public static GlassShape Ring(Vector2 min, Vector2 max, float radius, float thickness) =>
        GlassShape.Ring(min, max - min, radius, thickness);

    public static float CornerExponent(float radius, Vector2 size) =>
        radius >= MathF.Min(size.X, size.Y) * 0.5f - CapsuleTolerance
            ? Glasswork.CornerCurve.CircularExponent
            : Glasswork.CornerCurve.ContinuousExponent;

    private static GlassMaterial Base(RealGlassKind kind, bool light, Vector4 accent)
    {
        switch (kind)
        {
            case RealGlassKind.Frosted:
                return GlassMaterial.ForAppearance(light) with { Frost = FrostedFrost, BlurLevels = FrostedBlurLevels };
            case RealGlassKind.Accent:
                return GlassMaterial.Tinted(Glasswork.Color.FromVector4(accent), AccentTintAmount, light);
            default:
                return GlassMaterial.ForAppearance(light);
        }
    }
}
