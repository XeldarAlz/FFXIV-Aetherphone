using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VToggle
{
    public const float TrackWidth = 44f;
    public const float TrackHeight = 24f;

    private const float KnobInset = 3f;
    private const float OffFill = 0.16f;
    private const float SmoothTime = 0.12f;
    private const int KnobSegments = 24;

    public static bool Draw(ImDrawListPtr drawList, string id, Rect row, bool value, float scale)
    {
        var trackWidth = TrackWidth * scale;
        var trackHeight = TrackHeight * scale;
        var trackMin = new Vector2(row.Max.X - trackWidth, row.Center.Y - trackHeight * 0.5f);
        var trackMax = new Vector2(row.Max.X, row.Center.Y + trackHeight * 0.5f);
        var travel = VAnim.Toggle(id, value, ImGui.GetIO().DeltaTime, SmoothTime);
        var track = VelvetTheme.Lerp(VelvetTheme.Alpha(VelvetTheme.OnAccent, OffFill), VelvetTheme.Rose, travel);
        Squircle.Fill(drawList, trackMin, trackMax, trackHeight * 0.5f, track.Packed());
        var knobLeft = trackMin.X + trackHeight * 0.5f;
        var knobRight = trackMax.X - trackHeight * 0.5f;
        drawList.AddCircleFilled(new Vector2(knobLeft + (knobRight - knobLeft) * travel, row.Center.Y),
            trackHeight * 0.5f - KnobInset * scale, VelvetTheme.OnAccent.Packed(), KnobSegments);
        return UiInteract.HoverClick(row.Min, row.Max) ? !value : value;
    }
}
