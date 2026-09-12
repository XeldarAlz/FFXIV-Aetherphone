using Aetherphone.Core;
using Aetherphone.Core.Input;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class ResizeGrip
{
    public readonly struct Result
    {
        public readonly float Width;
        public readonly bool Adjusting;
        public readonly bool Committed;

        public Result(float width, bool adjusting, bool committed)
        {
            Width = width;
            Adjusting = adjusting;
            Committed = committed;
        }
    }

    public readonly struct Grab
    {
        public readonly Vector2 Delta;
        public readonly bool Started;
        public readonly bool Active;
        public readonly bool Committed;
        public readonly bool Engaged;

        public Grab(Vector2 delta, bool started, bool active, bool committed, bool engaged)
        {
            Delta = delta;
            Started = started;
            Active = active;
            Committed = committed;
            Engaged = engaged;
        }
    }

    private const float ZoneUnits = 34f;
    private const float NearInsetUnits = 15f;
    private const float FarInsetUnits = 23f;
    private const float StepUnits = 5.5f;
    private const int LineCount = 3;
    private const float RevealSeconds = 0.12f;
    private const float RestAlpha = 0.42f;
    private const float ActiveAlpha = 0.85f;

    private readonly DragTracker drag = new();
    private float startWidth;
    private float reveal;

    public Result Update(in ChassisGeometry chassis, float width, bool landscape, float deltaSeconds)
    {
        var grab = Track(ImGui.GetWindowDrawList(), chassis.Body.Max, UiScale.Current, deltaSeconds);
        if (grab.Started)
        {
            startWidth = width;
        }

        var next = grab.Active ? WidthFromDrag(startWidth, grab.Delta, landscape) : width;
        return new Result(next, grab.Active, grab.Committed);
    }

    public Grab Track(ImDrawListPtr drawList, Vector2 corner, float scale, float deltaSeconds)
    {
        var zone = new Rect(corner - new Vector2(ZoneUnits * scale, ZoneUnits * scale), corner);
        var hovered = UiInteract.Hover(zone.Min, zone.Max);
        var started = drag.Begin(zone);
        var active = drag.Active;
        var delta = drag.Delta;
        if (active)
        {
            UiInteract.BlockThisFrame();
            UiInteract.CancelPendingTap();
        }

        var committed = drag.Released(out _, out _);
        var engaged = hovered || active;
        if (engaged)
        {
            UiInteract.ReportGestureSurface();
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNwse);
            HoverTooltip.Show(zone, Loc.T(L.Plugin.ResizeHint), HoverLabelSide.Above);
        }

        reveal = Approach(reveal, engaged ? 1f : 0f, deltaSeconds);
        Draw(drawList, corner, scale, active);
        return new Grab(delta, started, active, committed, engaged);
    }

    private static float WidthFromDrag(float startWidth, Vector2 delta, bool landscape)
    {
        const float aspect = PhoneSizeCatalog.AspectRatio;
        var along = landscape ? delta.X * aspect + delta.Y : delta.X + delta.Y * aspect;
        var change = along / (1f + aspect * aspect);
        var units = startWidth + change / MathF.Max(UiScale.Global, 0.01f);
        return landscape
            ? MathF.Round(PhoneBounds.ClampLandscapeWidth(units))
            : PhoneSizeCatalog.Snap(PhoneBounds.ClampWidth(units));
    }

    private static float Approach(float value, float target, float deltaSeconds)
    {
        var step = deltaSeconds / RevealSeconds;
        return target > value ? MathF.Min(target, value + step) : MathF.Max(target, value - step);
    }

    private void Draw(ImDrawListPtr drawList, Vector2 corner, float scale, bool active)
    {
        if (reveal <= 0.001f)
        {
            return;
        }

        var alpha = (active ? ActiveAlpha : RestAlpha) * reveal;
        var color = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));
        var thickness = MathF.Max(1f, Metrics.Stroke.Thin * scale);
        for (var lineIndex = 0; lineIndex < LineCount; lineIndex++)
        {
            var offset = lineIndex * StepUnits * scale;
            var near = NearInsetUnits * scale + offset;
            var far = FarInsetUnits * scale + offset;
            drawList.AddLine(new Vector2(corner.X - far, corner.Y - near),
                new Vector2(corner.X - near, corner.Y - far), color, thickness);
        }
    }
}
