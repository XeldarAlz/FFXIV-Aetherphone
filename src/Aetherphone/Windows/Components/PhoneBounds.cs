using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class PhoneBounds
{
    private const float ViewportMarginUnits = 16f;

    public static float ClampWidth(float width)
    {
        var viewport = ImGui.GetMainViewport();
        var scale = MathF.Max(UiScale.Global, 0.01f);
        var widest = viewport.Size.X / scale - ViewportMarginUnits;
        var tallest = (viewport.Size.Y / scale - ViewportMarginUnits) / PhoneSizeCatalog.AspectRatio;
        var ceiling = MathF.Min(MathF.Min(widest, tallest), PhoneSizeCatalog.MaximumWidth);
        return Math.Clamp(width, PhoneSizeCatalog.MinimumWidth, MathF.Max(ceiling, PhoneSizeCatalog.MinimumWidth));
    }
}
