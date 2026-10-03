using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music.Components;

internal static class MusicRenderer
{
    private const float ShadowAlpha = 0.30f;
    private const float ShadowDrop = 1.5f;
    private const float GlyphFraction = 0.42f;
    private const float PlayNudge = 0.14f;
    private const int CircleSegments = 48;
    private static readonly Dictionary<string, Spring> Springs = new(StringComparer.Ordinal);

    public static bool PlayButton(string id, Vector2 center, float radius, Vector4 fill, Vector4 ink, bool playing,
        float alpha = 1f)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hit = new Vector2(radius, radius);
        var hovered = alpha > 0.6f && UiInteract.Hover(center - hit, center + hit);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var spring = Springs.TryGetValue(id, out var stored) ? stored : new Spring(1f);
        var grow = spring.Step(pressed ? Motion.PressScaleControl : hovered ? 1f + Motion.HoverLiftIcon : 1f,
            pressed ? Motion.PressIn : Motion.Release, MathF.Min(ImGui.GetIO().DeltaTime, 0.1f));
        Springs[id] = spring;
        var drawnRadius = radius * grow;
        drawList.AddCircleFilled(center + new Vector2(0f, ShadowDrop * UiScale.Current), drawnRadius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, ShadowAlpha * alpha)), CircleSegments);
        drawList.AddCircleFilled(center, drawnRadius, ImGui.GetColorU32(Palette.WithAlpha(fill, alpha)),
            CircleSegments);
        var glyphInk = ImGui.GetColorU32(Palette.WithAlpha(ink, alpha));
        var glyphSize = drawnRadius * GlyphFraction;
        if (playing)
        {
            MediaGlyph.Pause(drawList, center, glyphSize, glyphInk);
        }
        else
        {
            MediaGlyph.Play(drawList, center + new Vector2(glyphSize * PlayNudge, 0f), glyphSize, glyphInk);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - hit, center + hit, hovered);
    }
}
