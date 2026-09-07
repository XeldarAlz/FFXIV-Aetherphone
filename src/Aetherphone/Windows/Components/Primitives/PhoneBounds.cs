using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class PhoneBounds
{
    private const float ViewportMarginUnits = 16f;

    public static float ClampWidth(float width)
    {
        var room = ViewportRoom();
        var widest = room.X;
        var tallest = room.Y / PhoneSizeCatalog.AspectRatio;
        return ClampTo(width, MathF.Min(widest, tallest));
    }

    public static float ClampLandscapeWidth(float width)
    {
        var room = ViewportRoom();
        var widest = room.X / PhoneSizeCatalog.AspectRatio;
        var tallest = room.Y;
        return ClampTo(width, MathF.Min(widest, tallest));
    }

    public static float LandscapeWidth(Configuration configuration)
    {
        var portrait = ClampWidth(configuration.PhoneWidth);
        return ClampLandscapeWidth(PhoneSizeCatalog.LandscapeWidthFor(portrait, configuration.LandscapePhoneWidth));
    }

    public static Rect Viewport()
    {
        var viewport = ImGui.GetMainViewport();
        return new Rect(viewport.Pos, viewport.Pos + viewport.Size);
    }

    public static Vector2 ViewportRoom()
    {
        var viewport = ImGui.GetMainViewport();
        var scale = MathF.Max(UiScale.Global, 0.01f);
        return viewport.Size / scale - new Vector2(ViewportMarginUnits, ViewportMarginUnits);
    }

    private static float ClampTo(float width, float ceiling)
    {
        var limit = MathF.Min(ceiling, PhoneSizeCatalog.MaximumWidth);
        return Math.Clamp(width, PhoneSizeCatalog.MinimumWidth, MathF.Max(limit, PhoneSizeCatalog.MinimumWidth));
    }
}
