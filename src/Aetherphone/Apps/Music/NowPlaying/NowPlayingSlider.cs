using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music.NowPlaying;

internal readonly record struct SliderResult(float Value, bool Dragging, bool Released);

internal sealed class NowPlayingSlider
{
    private const float RestThickness = 5f;
    private const float GrabThickness = 10f;
    private const float HitPadding = 12f;
    private const float GrabWidthGrowth = 0.035f;
    private const float HoverGrow = 0.3f;

    private Spring grow;
    private bool dragging;
    private float dragValue;

    public bool Dragging => dragging;

    public float Thickness(float scale) => (RestThickness + (GrabThickness - RestThickness) * Grow) * scale;

    private float Grow => Math.Clamp(grow.Value, 0f, 1f);

    public void Cancel()
    {
        dragging = false;
        grow.SnapTo(0f);
    }

    public SliderResult Draw(ImDrawListPtr drawList, float left, float right, float centerY, float value,
        bool enabled, Vector4 fill, Vector4 rail, float delta)
    {
        var scale = UiScale.Current;
        var hitHalf = (GrabThickness * 0.5f + HitPadding) * scale;
        var hitMin = new Vector2(left, centerY - hitHalf);
        var hitMax = new Vector2(right, centerY + hitHalf);
        var hovered = enabled && UiInteract.Hover(hitMin, hitMax);
        if (hovered || dragging)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (hovered && !dragging && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            dragging = true;
            UiInteract.CancelPendingTap();
        }

        var released = false;
        if (dragging)
        {
            var width = MathF.Max(1f, right - left);
            dragValue = Math.Clamp((ImGui.GetMousePos().X - left) / width, 0f, 1f);
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                dragging = false;
                released = true;
            }
        }

        grow.Step(dragging ? 1f : hovered ? HoverGrow : 0f, Motion.HoverLift, delta);
        var shown = dragging || released ? dragValue : Math.Clamp(value, 0f, 1f);
        var growth = (right - left) * GrabWidthGrowth * Grow;
        var drawLeft = left - growth;
        var drawRight = right + growth;
        var half = Thickness(scale) * 0.5f;
        var railMin = new Vector2(drawLeft, centerY - half);
        var railMax = new Vector2(drawRight, centerY + half);
        drawList.AddRectFilled(railMin, railMax, ImGui.GetColorU32(rail), half);
        var fillRight = drawLeft + (drawRight - drawLeft) * shown;
        if (fillRight > drawLeft + 0.5f)
        {
            drawList.AddRectFilled(railMin, new Vector2(fillRight, railMax.Y), ImGui.GetColorU32(fill), half);
        }

        return new SliderResult(shown, dragging, released);
    }
}
