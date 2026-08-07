using Aetherphone.Core.Housing;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Housing;

internal readonly record struct HousingMarkerStyle(
    HousingPlotSize Size,
    HousingLotteryPhase Phase,
    bool Watched,
    bool Selected,
    bool Stale,
    bool Hovered);

internal static class HousingMarkers
{
    public const float Radius = 9f;
    public const float HitRadius = 15f;

    private static readonly Vector4 Ink = new(0.06f, 0.08f, 0.07f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static Vector4 PhaseColor(HousingLotteryPhase phase, Vector4 accent) => phase switch
    {
        HousingLotteryPhase.Entry => accent,
        HousingLotteryPhase.Results => AppPalettes.HousingResults,
        HousingLotteryPhase.Unavailable => AppPalettes.HousingClosed,
        HousingLotteryPhase.Expired => AppPalettes.HousingClosed,
        _ => AppPalettes.HousingParchment,
    };

    public static void DrawBackground(ImDrawListPtr drawList, Vector2 center, float scale, Vector4 tint)
    {
        drawList.AddCircleFilled(center, 2.6f * scale, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.30f)), 10);
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 center, in HousingMarkerStyle style, Vector4 accent,
        float scale, float emphasis)
    {
        var radius = Radius * scale * SizeScale(style.Size) * (1f + emphasis * 0.16f);
        var color = PhaseColor(style.Phase, accent);
        if (style.Hovered)
        {
            color = Palette.Mix(color, White, 0.22f);
        }

        var packed = ImGui.GetColorU32(color);
        var shadow = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.34f));
        var shadowCenter = new Vector2(center.X, center.Y + 1.2f * scale);
        DrawShape(drawList, shadowCenter, radius, style.Size, shadow);
        DrawShape(drawList, center, radius, style.Size, packed);
        DrawPhaseMark(drawList, center, radius, style.Phase, scale);
        if (style.Stale)
        {
            HousingGlyphs.DashedRing(drawList, center, radius + 3.4f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(AppPalettes.HousingParchment, 0.85f)), 1.5f * scale);
        }

        if (style.Watched)
        {
            HousingGlyphs.WatchNotch(drawList, center, radius * 0.62f,
                ImGui.GetColorU32(AppPalettes.HousingBrass));
        }

        if (!style.Selected)
        {
            return;
        }

        var ringRadius = radius + (6.5f + emphasis * 2.5f) * scale;
        drawList.AddCircle(center, ringRadius, ImGui.GetColorU32(White), 32, 2f * scale);
        drawList.AddCircle(center, ringRadius + 2.5f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(White, 0.28f * (1f - emphasis))), 32, 1.4f * scale);
    }

    public static void DrawSwatch(ImDrawListPtr drawList, Vector2 center, HousingPlotSize size, Vector4 color,
        float scale)
    {
        DrawShape(drawList, center, Radius * scale * SizeScale(size) * 0.8f, size, ImGui.GetColorU32(color));
    }

    public static void DrawLabel(ImDrawListPtr drawList, Vector2 center, float radius, string label, float scale)
    {
        var style = TextStyles.Caption2;
        var size = Typography.Measure(label, style);
        var origin = new Vector2(center.X - size.X * 0.5f, center.Y + radius + 2.5f * scale);
        var padX = 3.5f * scale;
        var padY = 1.5f * scale;
        var min = new Vector2(origin.X - padX, origin.Y - padY);
        var max = new Vector2(origin.X + size.X + padX, origin.Y + size.Y + padY);
        Squircle.Fill(drawList, min, max, 3f * scale, ImGui.GetColorU32(new Vector4(0.05f, 0.07f, 0.06f, 0.74f)));
        Typography.Draw(drawList, origin, label, new Vector4(0.96f, 0.96f, 0.92f, 1f), style);
    }

    public static float SizeScale(HousingPlotSize size) => size switch
    {
        HousingPlotSize.Small => 0.86f,
        HousingPlotSize.Medium => 1f,
        HousingPlotSize.Large => 1.16f,
        _ => 0.9f,
    };

    private static void DrawShape(ImDrawListPtr drawList, Vector2 center, float radius, HousingPlotSize size,
        uint color)
    {
        switch (size)
        {
            case HousingPlotSize.Medium:
                HousingGlyphs.Diamond(drawList, center, radius * 1.15f, color);
                break;
            case HousingPlotSize.Large:
                HousingGlyphs.Hexagon(drawList, center, radius * 1.1f, color);
                break;
            case HousingPlotSize.Small:
                HousingGlyphs.Circle(drawList, center, radius, color);
                break;
            default:
                drawList.AddRectFilled(new Vector2(center.X - radius * 0.8f, center.Y - radius * 0.8f),
                    new Vector2(center.X + radius * 0.8f, center.Y + radius * 0.8f), color, radius * 0.25f);
                break;
        }
    }

    private static void DrawPhaseMark(ImDrawListPtr drawList, Vector2 center, float radius,
        HousingLotteryPhase phase, float scale)
    {
        var inkPacked = ImGui.GetColorU32(Ink);
        switch (phase)
        {
            case HousingLotteryPhase.Entry:
                drawList.AddCircleFilled(center, radius * 0.34f, inkPacked, 14);
                break;
            case HousingLotteryPhase.Results:
                drawList.AddCircle(center, radius * 0.40f, inkPacked, 16, 1.6f * scale);
                break;
            case HousingLotteryPhase.Unavailable:
            case HousingLotteryPhase.Expired:
                HousingGlyphs.Slash(drawList, center, radius * 0.42f, inkPacked, 1.7f * scale);
                break;
        }
    }
}
