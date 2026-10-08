using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal static class StageInks
{
    public static readonly Vector4 Strong = CasinoColors.InkTitle;

    public static readonly Vector4 Body = CasinoColors.InkBody;

    public static readonly Vector4 Money = CasinoColors.Money;

    public static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.55f);
}

internal static class StageText
{
    public const float MinimumFit = 0.75f;

    private const float ShadowOffset = 1.5f;
    private const float PlatePadX = 9f;
    private const float PlatePadY = 3f;

    public static float StateLine(ImDrawListPtr drawList, Vector2 center, string text, float maxWidth, Vector4 ink)
    {
        return Shadowed(drawList, center, text, maxWidth, ink, TextStyles.Title2);
    }

    public static float Amount(ImDrawListPtr drawList, Vector2 center, string text, float maxWidth, Vector4 ink)
    {
        return Shadowed(drawList, center, text, maxWidth, ink, TextStyles.Title3);
    }

    public static float Shadowed(ImDrawListPtr drawList, Vector2 center, string text, float maxWidth, Vector4 ink,
        in TextStyle style)
    {
        if (text.Length == 0)
        {
            return 0f;
        }

        var fit = Typography.FitScale(text, maxWidth, style.Scale, style.Scale * MinimumFit, style.Weight);
        var shown = Typography.FitText(text, maxWidth, fit, style.Weight);
        var size = Typography.Measure(shown, fit, style.Weight);
        var offset = ShadowOffset * UiScale.Current;
        Typography.DrawCentered(drawList, center + new Vector2(0f, offset), shown, StageInks.Shadow, fit,
            style.Weight);
        Typography.DrawCentered(drawList, center, shown, ink, fit, style.Weight);
        return size.Y;
    }

    public static Rect Plate(ImDrawListPtr drawList, Vector2 center, string text, float maxWidth, Vector4 ink,
        in TextStyle style, float scale)
    {
        if (text.Length == 0)
        {
            return new Rect(center, center);
        }

        var padX = PlatePadX * scale;
        var padY = PlatePadY * scale;
        var fit = Typography.FitScale(text, MathF.Max(1f, maxWidth - padX * 2f), style.Scale,
            style.Scale * MinimumFit, style.Weight);
        var shown = Typography.FitText(text, MathF.Max(1f, maxWidth - padX * 2f), fit, style.Weight);
        var size = Typography.Measure(shown, fit, style.Weight);
        var half = new Vector2(size.X * 0.5f + padX, size.Y * 0.5f + padY);
        var min = center - half;
        var max = center + half;
        Material.LiquidGlass(drawList, min, max, half.Y, scale, GlassTone.Dark, 0f);
        Typography.DrawCentered(drawList, center, shown, ink, fit, style.Weight);
        return new Rect(min, max);
    }

    public static Rect Status(ImDrawListPtr drawList, Vector2 center, string text, float maxWidth, float scale)
    {
        return Plate(drawList, center, text, maxWidth, StageInks.Strong, TextStyles.Footnote, scale);
    }
}
