using Aetherphone.Core;
using Aetherphone.Core.Shell;
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

    public static float ClampMinimizedScale(float scale, Vector2 idleUnits)
    {
        var room = ViewportRoom();
        var ceiling = MathF.Min(room.X / MathF.Max(idleUnits.X, 1f), room.Y / MathF.Max(idleUnits.Y, 1f));
        var limit = MathF.Max(MathF.Min(ceiling, MinimizedShapes.MaxScale), MinimizedShapes.MinScale);
        return Math.Clamp(scale, MinimizedShapes.MinScale, limit);
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
