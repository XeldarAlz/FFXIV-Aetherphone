using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private static int DrawTrackSegments(string id, Rect row, IReadOnlyList<string> labels, int selected, float scale)
    {
        var picked = DrawSegments(id, row, labels, selected);
        if (selected < 0)
        {
            return picked;
        }

        var thumb = SegmentStrip.ThumbRect(id, row, labels.Count, out var position, Metrics.Size.TapTarget);
        var vanilla = Math.Clamp(position, 0f, 1f);
        var drawList = ImGui.GetWindowDrawList();
        var radius = thumb.Height * 0.5f;
        PrismLight.Edge(drawList, thumb, radius, PrismGlow.Spectrum, PrismLight.EdgeAlpha * (1f - vanilla), scale);
        PrismLight.Edge(drawList, thumb, radius, PrismGlow.Pearl, PrismLight.EdgeAlpha * vanilla, scale);
        return picked;
    }
}
