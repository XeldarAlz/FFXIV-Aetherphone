using Dalamud.Interface.Utility;

namespace Aetherphone;

internal static class UiScale
{
    private const float MinimumZoom = 0.5f;
    private const float MaximumZoom = 3f;

    public static float Phone { get; private set; } = 1f;

    public static float Minimized { get; private set; } = 1f;

    public static float Current => ImGuiHelpers.GlobalScale * Phone;

    public static float Global => ImGuiHelpers.GlobalScale;

    public static void SetPhone(float zoom) => Phone = Math.Clamp(zoom, MinimumZoom, MaximumZoom);

    public static void SetMinimized(float zoom) => Minimized = Math.Clamp(zoom, MinimumZoom, MaximumZoom);

    public static float MinimizedText(float scale) => scale * Minimized / Phone;
}
