using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.LoveIsland;

internal static class EclipseFx
{
    public const double GlintPeriod = 4600.0;
    public const double OrbitPeriod = 5200.0;
    public const double BeaconPeriod = 1700.0;

    private const float SparkleWaist = 0.22f;
    private const float SparkleCore = 0.18f;
    private const float SparkleHalo = 1.9f;
    private const float SparkleHaloAlpha = 0.22f;
    private const float GlintWindow = 0.34f;
    private const float GlintHalfWidth = 0.42f;
    private const float GlintSlant = 0.75f;
    private const float OrbitSharpness = 6f;
    private const float OrbitTrailAlpha = 0.45f;
    private const float BeaconReach = 2.3f;
    private const float BeaconAlpha = 0.55f;
    private const int BeaconRipples = 2;
    private const int BloomLayers = 6;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static void Sparkle(ImDrawListPtr drawList, Vector2 center, float extent, Vector4 color)
    {
        if (extent <= 0.3f || color.W <= 0.01f)
        {
            return;
        }

        drawList.AddCircleFilled(center, extent * SparkleHalo * 0.5f,
            ImGui.GetColorU32(color with { W = color.W * SparkleHaloAlpha }), 12);
        var packed = ImGui.GetColorU32(color);
        var waist = extent * SparkleWaist;
        drawList.AddQuadFilled(new Vector2(center.X, center.Y - extent), new Vector2(center.X + waist, center.Y),
            new Vector2(center.X, center.Y + extent), new Vector2(center.X - waist, center.Y), packed);
        drawList.AddQuadFilled(new Vector2(center.X - extent, center.Y), new Vector2(center.X, center.Y - waist),
            new Vector2(center.X + extent, center.Y), new Vector2(center.X, center.Y + waist), packed);
        drawList.AddCircleFilled(center, extent * SparkleCore, ImGui.GetColorU32(White with { W = color.W }), 8);
    }

    public static void Beacon(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color, float scale)
    {
        var phase = Pulse.Phase(BeaconPeriod);
        for (var ripple = 0; ripple < BeaconRipples; ripple++)
        {
            var progress = (phase + ripple / (float)BeaconRipples) % 1f;
            var eased = 1f - (1f - progress) * (1f - progress);
            var fade = (1f - progress) * (1f - progress);
            drawList.AddCircle(center, radius * (1f + (BeaconReach - 1f) * eased),
                ImGui.GetColorU32(color with { W = color.W * BeaconAlpha * fade }), 24, 1.2f * scale);
        }

        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(color), 20);
    }

    public static void OrbitBorder(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, Vector4 color,
        float scale, float offset = 0f)
    {
        var angle = (Pulse.Phase(OrbitPeriod) + offset) * MathF.Tau;
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        Squircle.StrokeDirectional(drawList, min, max, radius, ImGui.GetColorU32(color), 1.6f * scale, direction,
            OrbitSharpness);
        Squircle.StrokeDirectional(drawList, min, max, radius,
            ImGui.GetColorU32(color with { W = color.W * OrbitTrailAlpha }), 1.2f * scale, -direction,
            OrbitSharpness);
    }

    public static void Glint(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float peak,
        float offset = 0f)
    {
        var sweep = ((Pulse.Phase(GlintPeriod) + offset) % 1f) / GlintWindow;
        if (sweep <= 0f || sweep >= 1f || peak <= 0.005f)
        {
            return;
        }

        var height = max.Y - min.Y;
        var normal = Vector2.Normalize(new Vector2(height, height * GlintSlant));
        var halfWidth = height * GlintHalfWidth;
        var near = Vector2.Dot(min, normal) - halfWidth;
        var far = Vector2.Dot(max, normal) + halfWidth;
        var eased = sweep * sweep * (3f - 2f * sweep);
        Squircle.FillGlint(drawList, min, max, radius, normal, near + (far - near) * eased, halfWidth,
            ImGui.GetColorU32(White with { W = peak }));
    }

    public static void Bloom(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float reach,
        Vector4 color)
    {
        if (color.W <= 0.002f || reach <= 0f)
        {
            return;
        }

        var weightTotal = BloomLayers * (BloomLayers + 1) * 0.5f;
        for (var layer = BloomLayers; layer >= 1; layer--)
        {
            var pad = reach * layer / BloomLayers;
            var alpha = color.W * (BloomLayers - layer + 1) / weightTotal;
            Squircle.Fill(drawList, new Vector2(min.X - pad, min.Y - pad), new Vector2(max.X + pad, max.Y + pad),
                radius + pad, ImGui.GetColorU32(color with { W = alpha }));
        }
    }

    public static void Rays(ImDrawListPtr drawList, Vector2 center, float radius, int count, float rotation,
        float halfAngle, Vector4 color)
    {
        if (count <= 0 || radius <= 1f || color.W <= 0.002f)
        {
            return;
        }

        var inner = ImGui.GetColorU32(color);
        var outer = ImGui.GetColorU32(color with { W = 0f });
        var whitePixel = ImGui.GetFontTexUvWhitePixel();
        drawList.PushTextureID(ImGui.GetFontTexIdWhitePixel());
        drawList.PrimReserve(count * 3, count * 3);
        for (var ray = 0; ray < count; ray++)
        {
            var angle = rotation + ray * MathF.Tau / count;
            var left = new Vector2(MathF.Cos(angle - halfAngle), MathF.Sin(angle - halfAngle)) * radius;
            var right = new Vector2(MathF.Cos(angle + halfAngle), MathF.Sin(angle + halfAngle)) * radius;
            drawList.PrimVtx(center, whitePixel, inner);
            drawList.PrimVtx(center + left, whitePixel, outer);
            drawList.PrimVtx(center + right, whitePixel, outer);
        }

        drawList.PopTextureID();
    }

    public static void Heart(ImDrawListPtr drawList, Vector2 center, float size, Vector4 color)
    {
        if (size <= 0.5f || color.W <= 0.01f)
        {
            return;
        }

        var packed = ImGui.GetColorU32(color);
        var lobe = size * 0.29f;
        var lobeY = center.Y - size * 0.16f;
        drawList.AddCircleFilled(new Vector2(center.X - lobe, lobeY), lobe * 1.04f, packed, 12);
        drawList.AddCircleFilled(new Vector2(center.X + lobe, lobeY), lobe * 1.04f, packed, 12);
        drawList.AddTriangleFilled(new Vector2(center.X - lobe * 2.02f, lobeY + lobe * 0.18f),
            new Vector2(center.X + lobe * 2.02f, lobeY + lobe * 0.18f), new Vector2(center.X, center.Y + size * 0.52f),
            packed);
    }

    public static float Breath(double periodMilliseconds, float offset = 0f) =>
        0.5f + 0.5f * MathF.Sin((Pulse.Phase(periodMilliseconds) + offset) * MathF.Tau);

    public static Vector4 Rose(Vector4 accent, float lift) => Palette.Mix(accent, White, lift);
}
