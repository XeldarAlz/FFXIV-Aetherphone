using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Maps;

internal static class MapCanvas
{
    public const float PinHitRadius = 22f;
    public const float PinRadius = 14f;
    public const uint AetheryteIconId = 60453;
    private const float PinIconSize = 22f;
    private const float PinRing = 1.6f;
    private const float PinSelectedGrow = 1.18f;
    private const float PinLabelGap = 6f;
    private const float PinLabelHalo = 1.2f;
    private const float PlayerDotRadius = 6.5f;
    private const float PlayerRingWidth = 2.4f;
    private const float PlayerHaloRadius = 24f;
    private const float PlayerConeLength = 40f;
    private const float PlayerConeHalfAngle = 0.42f;
    private const int PlayerConeSteps = 6;
    private const float PlotPinRadius = 17f;
    private const float PlotGlyphSize = 18f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.30f);
    private static readonly Vector4 Halo = new(0f, 0f, 0f, 0.62f);
    private static readonly Vector4 PinBody = new(0.10f, 0.12f, 0.16f, 0.82f);

    public static void Map(ImDrawListPtr drawList, Rect screen, IDalamudTextureWrap texture, MapCamera camera,
        float rounding)
    {
        var uv0 = camera.UvAt(screen.Min);
        var uv1 = camera.UvAt(screen.Max);
        drawList.AddImageRounded(texture.Handle, screen.Min, screen.Max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
    }

    public static void Pin(ImDrawListPtr drawList, Vector2 center, float scale, float grow, bool selected,
        Vector4 accent)
    {
        var radius = PinRadius * scale * grow * (selected ? PinSelectedGrow : 1f);
        drawList.AddCircleFilled(center + new Vector2(0f, 1.5f * scale), radius + 1.5f * scale,
            ImGui.GetColorU32(Shadow), 28);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(selected ? accent : PinBody), 28);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(White), 28, PinRing * scale);
        var half = PinIconSize * 0.5f * scale * grow;
        GameIconTile.Draw(drawList, Plugin.TextureProvider, AetheryteIconId, center - new Vector2(half, half),
            center + new Vector2(half, half), half, scale);
    }

    public static void PinLabel(Vector2 pinCenter, string label, float scale, float maxWidth)
    {
        var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var center = new Vector2(pinCenter.X, pinCenter.Y + (PinRadius + PinLabelGap) * scale + size.Y * 0.5f);
        Typography.DrawCenteredHalo(center, label, White, Halo, PinLabelHalo * scale, maxWidth,
            TextStyles.FootnoteEmphasized);
    }

    public static void Player(ImDrawListPtr drawList, Vector2 center, float facing, Vector4 accent, float scale)
    {
        drawList.AddCircleFilled(center, PlayerHaloRadius * scale,
            ImGui.GetColorU32(Palette.WithAlpha(accent, 0.16f)), 40);
        var forward = new Vector2(MathF.Sin(facing), MathF.Cos(facing));
        var length = PlayerConeLength * scale;
        drawList.PathLineTo(center);
        for (var stepIndex = 0; stepIndex <= PlayerConeSteps; stepIndex++)
        {
            var angle = -PlayerConeHalfAngle + PlayerConeHalfAngle * 2f * stepIndex / PlayerConeSteps;
            drawList.PathLineTo(center + Rotate(forward, angle) * length);
        }

        drawList.PathFillConvex(ImGui.GetColorU32(Palette.WithAlpha(accent, 0.34f)));
        var dot = PlayerDotRadius * scale;
        drawList.AddCircleFilled(center + new Vector2(0f, 1f * scale), dot + PlayerRingWidth * scale + 1f * scale,
            ImGui.GetColorU32(Shadow), 24);
        drawList.AddCircleFilled(center, dot + PlayerRingWidth * scale, ImGui.GetColorU32(White), 24);
        drawList.AddCircleFilled(center, dot, ImGui.GetColorU32(accent), 24);
    }

    public static void PlotPin(ImDrawListPtr drawList, Vector2 center, Vector4 accent, float scale)
    {
        var radius = PlotPinRadius * scale;
        drawList.AddCircleFilled(center, radius * 1.9f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.18f)), 40);
        drawList.AddCircleFilled(center + new Vector2(0f, 1.5f * scale), radius + 2f * scale,
            ImGui.GetColorU32(Shadow), 32);
        drawList.AddCircleFilled(center, radius + PlayerRingWidth * scale, ImGui.GetColorU32(White), 32);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(accent), 32);
        PhoneIcon.Draw(drawList, center, PhoneIcons.HomeFilled, White, PlotGlyphSize * scale);
    }

    public static Vector2 Preview(ImDrawListPtr drawList, Rect rect, IDalamudTextureWrap texture, float u, float v,
        float span, float rounding)
    {
        var halfU = MathF.Min(span * 0.5f, 0.5f);
        var halfV = MathF.Min(halfU * rect.Height / MathF.Max(rect.Width, 1f), 0.5f);
        var centerU = Math.Clamp(u, halfU, 1f - halfU);
        var centerV = Math.Clamp(v, halfV, 1f - halfV);
        var uv0 = new Vector2(centerU - halfU, centerV - halfV);
        var uv1 = new Vector2(centerU + halfU, centerV + halfV);
        Squircle.FillImage(drawList, rect.Min, rect.Max, rounding, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
        return new Vector2(rect.Min.X + (u - uv0.X) / (uv1.X - uv0.X) * rect.Width,
            rect.Min.Y + (v - uv0.Y) / (uv1.Y - uv0.Y) * rect.Height);
    }

    public static float PreviewPixels(Rect rect, float span) => rect.Width / MathF.Max(span, 0.01f);

    private static Vector2 Rotate(Vector2 direction, float angle)
    {
        var sine = MathF.Sin(angle);
        var cosine = MathF.Cos(angle);
        return new Vector2(direction.X * cosine - direction.Y * sine, direction.X * sine + direction.Y * cosine);
    }
}
