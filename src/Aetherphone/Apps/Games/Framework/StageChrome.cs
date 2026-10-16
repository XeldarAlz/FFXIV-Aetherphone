using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Framework;

internal sealed class StageChrome
{
    private Vector2 backCenter;
    private Vector2 pauseCenter;
    private float radius;
    private Rect coinRect;
    private bool backShown;
    private bool pauseShown;
    private bool coinShown;

    public void BeginFrame()
    {
        backShown = false;
        pauseShown = false;
        coinShown = false;
    }

    public bool Consumes(Vector2 pointer)
    {
        if (backShown && Vector2.DistanceSquared(pointer, backCenter) <= radius * radius)
        {
            return true;
        }

        if (pauseShown && Vector2.DistanceSquared(pointer, pauseCenter) <= radius * radius)
        {
            return true;
        }

        return coinShown && coinRect.Contains(pointer);
    }

    public bool BackChip(ImDrawListPtr drawList, Vector2 center, float chipRadius, PhoneTheme theme, float scale)
    {
        backCenter = center;
        radius = HitRadius(chipRadius, scale);
        backShown = true;
        return Chip(drawList, center, chipRadius, FontAwesomeIcon.ChevronLeft, theme, scale);
    }

    public bool PauseChip(ImDrawListPtr drawList, Vector2 center, float chipRadius, PhoneTheme theme, float scale,
        bool paused)
    {
        pauseCenter = center;
        radius = HitRadius(chipRadius, scale);
        pauseShown = true;
        return Chip(drawList, center, chipRadius, paused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause, theme, scale);
    }

    public void RecordCoinChip(Rect rect)
    {
        coinRect = rect;
        coinShown = true;
    }

    private static bool Chip(ImDrawListPtr drawList, Vector2 center, float chipRadius, FontAwesomeIcon icon,
        PhoneTheme theme, float scale)
    {
        var corner = new Vector2(chipRadius, chipRadius);
        var hit = new Vector2(HitRadius(chipRadius, scale));
        var hovered = UiInteract.Hover(center - hit, center + hit);
        Material.Frosted(drawList, center - corner, center + corner, chipRadius, scale, hovered ? 1f : 0.85f);
        ProgressRing.CenterIcon(drawList, center, icon, hovered ? StageInks.Strong : theme.Accent, chipRadius * 0.9f);
        return UiInteract.HoverClickCircle(center, hit.X);
    }

    private static float HitRadius(float chipRadius, float scale) =>
        MathF.Max(chipRadius, Metrics.Size.TapTarget * 0.5f * scale);
}
