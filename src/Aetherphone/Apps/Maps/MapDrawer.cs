using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Maps;

internal enum MapDrawerDetent : byte
{
    Peek,
    Medium,
    Large,
}

internal sealed class MapDrawer
{
    private const float VelocitySmoothing = 0.6f;
    private const float OvershootSlack = 0.85f;

    private Spring height;
    private MapDrawerDetent detent = MapDrawerDetent.Medium;
    private bool snapNext = true;
    private bool pressed;
    private bool pressOnGrabber;
    private float pressMouseY;
    private float pressHeight;
    private float lastMouseY;
    private float dragVelocity;
    private float peekHeight;
    private float mediumHeight;
    private float largeHeight;

    public MapDrawerDetent Detent => detent;

    public bool Dragging { get; private set; }

    public float MediumHeight => mediumHeight;

    public void Reset(MapDrawerDetent value)
    {
        detent = value;
        snapNext = true;
        pressed = false;
        Dragging = false;
    }

    public void SetDetent(MapDrawerDetent value)
    {
        if (detent == value)
        {
            return;
        }

        detent = value;
        UiFeedback.Play(UiSound.Tap);
    }

    public Rect Update(Rect screen, float peek, Rect dragZone, Rect excluded, float delta)
    {
        var scale = UiScale.Current;
        largeHeight = screen.Height * SheetMetrics.LargeFraction;
        mediumHeight = Math.Clamp(screen.Height * SheetMetrics.MediumFraction, peek, largeHeight);
        peekHeight = MathF.Min(peek, mediumHeight);
        TrackDrag(dragZone, excluded, scale, delta);
        var target = TargetHeight();
        if (snapNext)
        {
            snapNext = false;
            height.SnapTo(target);
        }
        else if (!Dragging)
        {
            height.Step(target, SheetMetrics.PresentSmoothTime, delta);
        }

        var shown = Math.Clamp(height.Value, peekHeight * OvershootSlack, screen.Height);
        return new Rect(new Vector2(screen.Min.X, screen.Max.Y - shown), screen.Max);
    }

    public static void Draw(ImDrawListPtr drawList, Rect panel, PhoneTheme theme, float scale)
    {
        var rounding = theme.ScreenRounding * scale;
        Elevation.Floating(drawList, panel.Min, panel.Max, rounding, scale, 1f);
        Material.ThemedGlass(drawList, panel.Min, panel.Max, rounding, scale, theme);
        var ink = Material.ToneFor(theme) == GlassTone.Light ? theme.TextStrong : Vector4.One;
        Sheet.DrawGrabber(drawList, panel, ink, scale);
    }

    private float TargetHeight() => detent switch
    {
        MapDrawerDetent.Peek => peekHeight,
        MapDrawerDetent.Large => largeHeight,
        _ => mediumHeight,
    };

    private void TrackDrag(Rect dragZone, Rect excluded, float scale, float delta)
    {
        var mouseY = ImGui.GetMousePos().Y;
        if (!pressed)
        {
            if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left) || !UiInteract.Hover(dragZone.Min, dragZone.Max) ||
                UiInteract.Hover(excluded.Min, excluded.Max))
            {
                return;
            }

            pressed = true;
            Dragging = false;
            pressOnGrabber = mouseY <= dragZone.Min.Y + SheetMetrics.GrabberZone * scale;
            pressMouseY = mouseY;
            pressHeight = height.Value;
            lastMouseY = mouseY;
            dragVelocity = 0f;
            return;
        }

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var travel = mouseY - pressMouseY;
            if (!Dragging && MathF.Abs(travel) >= SheetMetrics.DragThreshold * scale)
            {
                Dragging = true;
                UiInteract.CancelPendingTap();
            }

            if (!Dragging)
            {
                return;
            }

            UiInteract.BlockThisFrame();
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNs);
            var instant = delta > 0f ? (lastMouseY - mouseY) / delta : 0f;
            dragVelocity += (instant - dragVelocity) * VelocitySmoothing;
            lastMouseY = mouseY;
            height.SnapTo(Math.Clamp(pressHeight - travel, peekHeight * OvershootSlack, largeHeight));
            return;
        }

        pressed = false;
        if (!Dragging)
        {
            if (pressOnGrabber)
            {
                SetDetent(detent == MapDrawerDetent.Medium ? MapDrawerDetent.Large : MapDrawerDetent.Medium);
            }

            return;
        }

        Dragging = false;
        height.Velocity = dragVelocity;
        SetDetent(Snap(height.Value, dragVelocity, scale));
    }

    private MapDrawerDetent Snap(float current, float velocity, float scale)
    {
        var fling = SheetMetrics.FlingVelocity * MathF.Max(scale, 0.0001f);
        if (velocity >= fling)
        {
            return current >= mediumHeight ? MapDrawerDetent.Large : MapDrawerDetent.Medium;
        }

        if (velocity <= -fling)
        {
            return current <= mediumHeight ? MapDrawerDetent.Peek : MapDrawerDetent.Medium;
        }

        var toPeek = MathF.Abs(current - peekHeight);
        var toMedium = MathF.Abs(current - mediumHeight);
        var toLarge = MathF.Abs(current - largeHeight);
        if (toPeek <= toMedium && toPeek <= toLarge)
        {
            return MapDrawerDetent.Peek;
        }

        return toMedium <= toLarge ? MapDrawerDetent.Medium : MapDrawerDetent.Large;
    }
}
