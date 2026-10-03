using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly record struct SheetDetents(float Medium, float Large)
{
    private const float ResizableSlack = 0.5f;

    public static SheetDetents Fitted(float height) => new(height, height);

    public static SheetDetents Standard(float screenHeight) =>
        new(screenHeight * SheetMetrics.MediumFraction, screenHeight * SheetMetrics.LargeFraction);

    public bool Resizable => Large > Medium + ResizableSlack;
}

internal readonly struct SheetFrame
{
    public readonly bool Visible;
    public readonly ImDrawListPtr DrawList;
    public readonly Rect Panel;
    public readonly Rect Content;
    public readonly float Opacity;
    public readonly bool Interactive;
    public readonly Vector4 Ink;

    internal SheetFrame(ImDrawListPtr drawList, Rect panel, Rect content, float opacity, bool interactive,
        Vector4 ink)
    {
        Visible = true;
        DrawList = drawList;
        Panel = panel;
        Content = content;
        Opacity = opacity;
        Interactive = interactive;
        Ink = ink;
    }
}

internal static class SheetMetrics
{
    public const float MediumFraction = 0.50f;
    public const float LargeFraction = 0.92f;
    public const float GrabberWidth = 36f;
    public const float GrabberHeight = 5f;
    public const float GrabberTop = 8f;
    public const float GrabberZone = 24f;
    public const float DragThreshold = 6f;
    public const float DismissFraction = 0.35f;
    public const float FlingVelocity = 900f;
    public const float HomeVeil = 0.35f;
    public const float AppVeil = 0.45f;
    public const float PresentSmoothTime = Motion.Sheet;
    private const float MinimumScale = 0.0001f;

    public static float VeilFor(bool insideApp) => insideApp ? AppVeil : HomeVeil;

    public static float Snap(float height, float velocity, in SheetDetents detents, float scale)
    {
        var fling = FlingVelocity * MathF.Max(scale, MinimumScale);
        if (velocity <= -fling)
        {
            return detents.Resizable && height > detents.Medium ? detents.Medium : 0f;
        }

        if (velocity >= fling)
        {
            return detents.Large;
        }

        if (height < detents.Medium * (1f - DismissFraction))
        {
            return 0f;
        }

        if (!detents.Resizable)
        {
            return detents.Medium;
        }

        return height >= (detents.Medium + detents.Large) * 0.5f ? detents.Large : detents.Medium;
    }
}

internal sealed class Sheet
{
    private const float RestPositionEpsilon = 0.5f;
    private const float RestVelocityEpsilon = 2f;
    private const float VelocitySmoothing = 0.6f;
    private const float InteractiveThreshold = 0.5f;
    private const float GrabberAlpha = 0.30f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private Spring shown;
    private bool open;
    private bool launchPending;
    private bool largeDetent;
    private int openedFrame;
    private bool pressed;
    private bool dragging;
    private float pressMouseY;
    private float pressHeight;
    private float lastMouseY;
    private float dragVelocity;

    public bool IsOpen => open;

    public void ReleasePress()
    {
        if (dragging)
        {
            return;
        }

        pressed = false;
    }

    public bool IsDragging => dragging;

    public bool CapturesPointer => open || !shown.IsResting(0f, RestPositionEpsilon, RestVelocityEpsilon);

    public void Open()
    {
        if (open)
        {
            return;
        }

        open = true;
        launchPending = true;
        largeDetent = false;
        openedFrame = ImGui.GetFrameCount();
        UiFeedback.Play(UiSound.SheetPresent);
    }

    public void Close()
    {
        var wasOpen = open;
        open = false;
        pressed = false;
        dragging = false;
        if (wasOpen)
        {
            UiFeedback.Play(UiSound.SheetDismiss);
        }
    }

    public void YieldPointer()
    {
        if (dragging)
        {
            return;
        }

        pressed = false;
    }

    public void CloseImmediately()
    {
        open = false;
        pressed = false;
        dragging = false;
        shown.SnapTo(0f);
    }

    public SheetFrame Begin(ImDrawListPtr drawList, Rect screen, PhoneTheme theme, in SheetDetents detents,
        float veil)
    {
        var scale = UiScale.Current;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var detentHeight = largeDetent ? detents.Large : detents.Medium;
        if (open && launchPending)
        {
            launchPending = false;
            shown.Launch(MathF.Max(shown.Value, 0f),
                TransitionTiming.LaunchVelocity(SheetMetrics.PresentSmoothTime) * detentHeight);
        }

        if (open)
        {
            TrackDrag(screen, in detents, scale, delta);
        }

        if (!dragging)
        {
            shown.Step(open ? detentHeight : 0f, SheetMetrics.PresentSmoothTime, delta);
        }

        if (!open && shown.IsResting(0f, RestPositionEpsilon, RestVelocityEpsilon))
        {
            shown.SnapTo(0f);
            return default;
        }

        var height = Math.Clamp(shown.Value, 0f, screen.Height);
        var opacity = Easing.Clamp01(height / MathF.Max(1f, detents.Medium));
        var panel = new Rect(new Vector2(screen.Min.X, screen.Max.Y - height), screen.Max);
        var rounding = theme.ScreenRounding * scale;
        var tone = Material.ToneFor(theme);
        var ink = tone == GlassTone.Light ? theme.TextStrong : White;
        drawList.PushClipRect(screen.Min, screen.Max, false);
        Material.Veil(drawList, screen.Min, screen.Max, veil * opacity);
        Elevation.Floating(drawList, panel.Min, panel.Max, rounding, scale, opacity);
        Material.LiquidGlass(drawList, panel.Min, panel.Max, rounding, scale, tone, 0f);
        DrawGrabber(drawList, panel, ink, scale);
        var content = new Rect(new Vector2(panel.Min.X, panel.Min.Y + SheetMetrics.GrabberZone * scale), panel.Max);
        drawList.PushClipRect(panel.Min, panel.Max, true);
        var interactive = open && !dragging && opacity > InteractiveThreshold;
        return new SheetFrame(drawList, panel, content, opacity, interactive, ink);
    }

    public void End(in SheetFrame frame)
    {
        if (!frame.Visible)
        {
            return;
        }

        frame.DrawList.PopClipRect();
        frame.DrawList.PopClipRect();
        if (!open || !frame.Interactive || pressed || ImGui.GetFrameCount() == openedFrame)
        {
            return;
        }

        if (UiInteract.ClickedOutside(frame.Panel.Min, frame.Panel.Max, false))
        {
            Close();
        }
    }

    private void TrackDrag(Rect screen, in SheetDetents detents, float scale, float delta)
    {
        var mouseY = ImGui.GetMousePos().Y;
        if (!pressed)
        {
            var panelTop = screen.Max.Y - MathF.Max(shown.Value, 0f);
            var overPanel = UiInteract.HoverWindowOnly(new Vector2(screen.Min.X, panelTop), screen.Max, false);
            if (shown.Value <= 0f || !overPanel || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                return;
            }

            pressed = true;
            dragging = false;
            pressMouseY = mouseY;
            pressHeight = shown.Value;
            lastMouseY = mouseY;
            dragVelocity = 0f;
            return;
        }

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var travel = mouseY - pressMouseY;
            if (!dragging && MathF.Abs(travel) >= SheetMetrics.DragThreshold * scale)
            {
                dragging = true;
                UiInteract.CancelPendingTap();
            }

            if (!dragging)
            {
                return;
            }

            UiInteract.BlockThisFrame();
            var instant = delta > 0f ? (lastMouseY - mouseY) / delta : 0f;
            dragVelocity += (instant - dragVelocity) * VelocitySmoothing;
            lastMouseY = mouseY;
            shown.SnapTo(Math.Clamp(pressHeight - travel, 0f, detents.Large));
            return;
        }

        pressed = false;
        if (!dragging)
        {
            return;
        }

        dragging = false;
        var target = SheetMetrics.Snap(shown.Value, dragVelocity, in detents, scale);
        if (target <= 0f)
        {
            Close();
            shown.Velocity = MathF.Min(dragVelocity, 0f);
            return;
        }

        shown.Velocity = dragVelocity;
        largeDetent = detents.Resizable && target >= detents.Large;
    }

    public static void DrawGrabber(ImDrawListPtr drawList, Rect panel, Vector4 ink, float scale)
    {
        var width = SheetMetrics.GrabberWidth * scale;
        var height = SheetMetrics.GrabberHeight * scale;
        var min = new Vector2(panel.Center.X - width * 0.5f, panel.Min.Y + SheetMetrics.GrabberTop * scale);
        drawList.AddRectFilled(min, min + new Vector2(width, height),
            ImGui.GetColorU32(ink with { W = ink.W * GrabberAlpha }), height * 0.5f);
    }
}
