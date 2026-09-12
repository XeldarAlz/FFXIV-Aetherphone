using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Windows;

internal sealed class PhoneWindow : Window
{
    private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar |
                                               ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse |
                                               ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoBackground;

    private const int RecenterFrameCount = 3;
    private const int ScaledStyleVarCount = 6;
    private readonly PhoneShell shell;
    private readonly Configuration configuration;
    private int recenterFrames;
    private int pendingFrames;
    private int rotatePinFrames;
    private bool dockGrowLeft;
    private bool dockGrowUp;
    private bool turning;
    private bool turnToLandscape;
    private float turnScale = 1f;
    private float turnTravelOrigin;
    private Vector2 deviceSize;
    private Vector2 turnFootprint;
    private Vector2 turnTarget;
    private Vector2 pinCenter;
    private Vector2? turnStart;
    private Vector2? pendingPosition;
    private Vector2? maximizedPosition;
    private Vector2? minimizedPosition;
    private Vector2? landscapePosition;

    public PhoneWindow(PhoneShell shell, Configuration configuration)
        : base(AepConstants.Name, BaseFlags)
    {
        this.shell = shell;
        this.configuration = configuration;
        Size = PhoneSizeCatalog.SizeFor(configuration.PhoneWidth);
        SizeCondition = ImGuiCond.Always;
        RespectCloseHotkey = false;
        maximizedPosition = configuration.MaximizedPosition;
        minimizedPosition = configuration.MinimizedPosition;
        landscapePosition = configuration.LandscapePosition;
    }

    public bool IsMinimized => shell.MinimizedResting;

    public Vector2 LastPosition { get; private set; }

    public Vector2 LastSize { get; private set; }

    public bool ShowsChrome => IsOpen && shell.MinimizePhase == MinimizePhase.None && LastSize.Y > 0f;

    public void Maximize()
    {
        RequestPosition(maximizedPosition);
        shell.ForceMaximize();
    }

    public void StartMinimized()
    {
        RequestPosition(minimizedPosition);
        shell.ForceMinimized();
    }

    public void PersistPositions()
    {
        if (configuration.MaximizedPosition == maximizedPosition && configuration.MinimizedPosition == minimizedPosition &&
            configuration.LandscapePosition == landscapePosition)
        {
            return;
        }

        configuration.MaximizedPosition = maximizedPosition;
        configuration.MinimizedPosition = minimizedPosition;
        configuration.LandscapePosition = landscapePosition;
        configuration.SaveNow();
    }

    public void Recenter()
    {
        shell.ForceMaximize();
        recenterFrames = RecenterFrameCount;
        pendingFrames = 0;
        maximizedPosition = null;
        minimizedPosition = null;
        landscapePosition = null;
        IsOpen = true;
    }

    public void ToggleShell()
    {
        if (IsOpen)
        {
            IsOpen = false;
            return;
        }

        Maximize();
        IsOpen = true;
    }

    public void OpenSettings()
    {
        Maximize();
        IsOpen = true;
        shell.OpenApp("settings");
    }

    private void RequestPosition(Vector2? target)
    {
        if (target is not { } position)
        {
            return;
        }

        pendingPosition = position;
        pendingFrames = RecenterFrameCount;
    }

    public override void OnOpen()
    {
        shell.OnOpened();
    }

    public override void OnClose()
    {
        PersistPositions();
        shell.OnClosed();
    }

    public override void PreDraw()
    {
        FrameClock.Advance(ImGui.GetFrameCount(), ImGui.GetIO().DeltaTime);
        shell.PrepareFrame(FrameClock.Delta);
        var portraitWidth = Components.PhoneBounds.ClampWidth(configuration.PhoneWidth);
        var landscapeWidth = Components.PhoneBounds.LandscapeWidth(configuration);
        var turn = shell.Turn;
        var phase = shell.MinimizePhase;
        var minimized = phase == MinimizePhase.Minimized;
        var landscape = turn.ShowsLandscape;
        var minimizedZoom = shell.MinimizedZoom;
        UiScale.SetMinimized(minimizedZoom);
        var zoom = minimized ? minimizedZoom : PhoneSizeCatalog.ZoomFor(landscape ? landscapeWidth : portraitWidth);
        UiScale.SetPhone(zoom);
        Plugin.Fonts.SetPhoneZoom(zoom);
        var dockSize = shell.MinimizedSize;
        var portraitSize = PhoneSizeCatalog.SizeFor(portraitWidth);
        var landscapeSize = PhoneSizeCatalog.LandscapeSizeFor(landscapeWidth);
        var size = minimized
            ? dockSize / UiScale.Global
            : landscape
                ? landscapeSize
                : portraitSize;
        deviceSize = size;
        turning = !minimized && turn.Turning && LastSize.Y > 0f;
        turnFootprint = size;
        turnScale = 1f;
        if (turning)
        {
            pinCenter = GlideCenter(turn, portraitSize, landscapeSize);
            turnScale = turn.ScaleFor(landscapeWidth / MathF.Max(portraitWidth, 1f));
            turnFootprint = TurnFootprint(size, turn.Angle, turnScale);
            var room = Components.PhoneBounds.ViewportRoom();
            var fit = MathF.Min(1f, MathF.Min(room.X / turnFootprint.X, room.Y / turnFootprint.Y));
            turnScale *= fit;
            turnFootprint *= fit;
            size = Vector2.Max(size, turnFootprint);
            rotatePinFrames = RecenterFrameCount;
        }
        else if (rotatePinFrames > 0 && phase == MinimizePhase.None)
        {
            pinCenter = turnTarget;
        }
        else
        {
            rotatePinFrames = 0;
            turnStart = null;
        }

        Size = size;
        SizeCondition = ImGuiCond.Always;
        var locked = !minimized && configuration.LockPosition;
        var holdStill = !minimized &&
                        (turning || shell.HomeEditing || Components.UiInteract.PointerOverGestureSurface);
        Flags = minimized || locked || holdStill
            ? BaseFlags | ImGuiWindowFlags.NoMove
            : BaseFlags;
        Components.DragScrollHost.Enabled = locked;

        if (recenterFrames > 0)
        {
            var viewport = ImGui.GetMainViewport();
            var scaledSize = size * UiScale.Global;
            Position = viewport.Pos + (viewport.Size - scaledSize) * 0.5f;
            PositionCondition = ImGuiCond.Always;
            recenterFrames--;
        }
        else if (pendingFrames > 0 && pendingPosition is { } pendingTarget)
        {
            Position = pendingTarget;
            PositionCondition = ImGuiCond.Always;
            pendingFrames--;
        }
        else if (minimized)
        {
            Position = DockedPosition(dockSize);
            PositionCondition = ImGuiCond.Always;
        }
        else if (phase is MinimizePhase.Collapsing or MinimizePhase.Expanding &&
                 maximizedPosition is { } homePosition)
        {
            Position = Vector2.Lerp(homePosition, DockedPosition(dockSize), shell.MinimizeEased);
            PositionCondition = ImGuiCond.Always;
        }
        else if (!minimized && rotatePinFrames > 0 && LastSize.Y > 0f)
        {
            rotatePinFrames--;
            Position = CenterPinnedPosition(size, turnFootprint, pinCenter);
            PositionCondition = ImGuiCond.Always;
        }
        else
        {
            Position = null;
            pendingFrames = 0;
        }

        PushScaledStyle(zoom);
    }

    public override void PostDraw() => ImGui.PopStyleVar(ScaledStyleVarCount);

    private Rect DeviceRect()
    {
        var origin = ImGui.GetCursorScreenPos();
        var available = ImGui.GetContentRegionAvail();
        var scaled = deviceSize * UiScale.Global;
        var offset = (available - scaled) * 0.5f;
        var min = origin + new Vector2(MathF.Round(offset.X), MathF.Round(offset.Y));
        return new Rect(min, min + scaled);
    }

    private void ApplyTurn(Rect device)
    {
        var window = ImGuiP.GetCurrentWindowRead();
        var turn = shell.Turn;
        var alpha = turn.ContentAlpha;
        if (alpha < 1f)
        {
            LayerCompositor.TransformChildren(window, LayerTransform.Fade(alpha));
        }

        var viewport = ImGui.GetMainViewport();
        var clip = new Rect(viewport.Pos, viewport.Pos + viewport.Size);
        LayerCompositor.Transform(window, LayerTransform.Turn(device.Center, turn.Angle, turnScale, clip));
    }

    private static void PushScaledStyle(float zoom)
    {
        var style = ImGui.GetStyle();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, style.FramePadding * zoom);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, style.ItemSpacing * zoom);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, style.ItemInnerSpacing * zoom);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, style.ScrollbarSize * zoom);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabMinSize, style.GrabMinSize * zoom);
    }

    private static Vector2 TurnFootprint(Vector2 size, float angle, float scale)
    {
        var cosine = MathF.Abs(MathF.Cos(angle));
        var sine = MathF.Abs(MathF.Sin(angle));
        return new Vector2(size.X * cosine + size.Y * sine, size.X * sine + size.Y * cosine) * scale;
    }

    private Vector2 DockedPosition(Vector2 dockSize)
    {
        var viewport = ImGui.GetMainViewport();
        var drag = shell.ConsumeMinimizedDrag();
        var idle = shell.MinimizedIdleSize;
        var extra = Vector2.Max(dockSize - idle, Vector2.Zero);
        if (minimizedPosition is not { } anchor)
        {
            anchor = maximizedPosition ?? LastPosition;
            dockGrowLeft = PastCenterX(anchor.X + idle.X * 0.5f, viewport);
            dockGrowUp = PastCenterY(anchor.Y + idle.Y * 0.5f, viewport);
        }

        anchor += drag.Delta;
        if (drag.Released)
        {
            var visual = LastPosition;
            dockGrowLeft = PastCenterX(visual.X + dockSize.X * 0.5f, viewport);
            dockGrowUp = PastCenterY(visual.Y + dockSize.Y * 0.5f, viewport);
            anchor = new Vector2(visual.X + (dockGrowLeft ? extra.X : 0f), visual.Y + (dockGrowUp ? extra.Y : 0f));
        }

        anchor = ClampToViewport(anchor, idle, viewport);
        minimizedPosition = anchor;
        var position = new Vector2(dockGrowLeft ? anchor.X - extra.X : anchor.X,
            dockGrowUp ? anchor.Y - extra.Y : anchor.Y);
        return ClampToViewport(position, dockSize, viewport);
    }

    private static bool PastCenterX(float x, ImGuiViewportPtr viewport) =>
        x > viewport.Pos.X + viewport.Size.X * 0.5f;

    private static bool PastCenterY(float y, ImGuiViewportPtr viewport) =>
        y > viewport.Pos.Y + viewport.Size.Y * 0.5f;

    private Vector2 GlideCenter(OrientationTurn turn, Vector2 portraitSize, Vector2 landscapeSize)
    {
        var wantsLandscape = shell.LandscapeActive;
        if (turnStart is not { } start || wantsLandscape != turnToLandscape)
        {
            start = LastPosition + LastSize * 0.5f;
            turnStart = start;
            turnToLandscape = wantsLandscape;
            turnTravelOrigin = turn.TravelTo(wantsLandscape);
        }

        var home = wantsLandscape ? landscapePosition : maximizedPosition;
        var restingSize = wantsLandscape ? landscapeSize : portraitSize;
        turnTarget = home is { } spot ? spot + restingSize * UiScale.Global * 0.5f : start;
        var travel = Easing.Segment(turn.TravelTo(wantsLandscape), turnTravelOrigin, 1f);
        return Vector2.Lerp(start, turnTarget, travel);
    }

    private static Vector2 CenterPinnedPosition(Vector2 windowSize, Vector2 contentSize, Vector2 center)
    {
        var viewport = ImGui.GetMainViewport();
        var scaledWindow = windowSize * UiScale.Global;
        var scaledContent = contentSize * UiScale.Global;
        var middle = viewport.Pos + viewport.Size * 0.5f;
        var slack = Vector2.Max((viewport.Size - scaledContent) * 0.5f, Vector2.Zero);
        var pinned = Vector2.Clamp(center, middle - slack, middle + slack);
        return pinned - scaledWindow * 0.5f;
    }

    private static Vector2 ClampToViewport(Vector2 position, Vector2 size, ImGuiViewportPtr viewport)
    {
        var maxPosition = viewport.Pos + viewport.Size - size;
        return new Vector2(Math.Clamp(position.X, viewport.Pos.X, MathF.Max(viewport.Pos.X, maxPosition.X)),
            Math.Clamp(position.Y, viewport.Pos.Y, MathF.Max(viewport.Pos.Y, maxPosition.Y)));
    }

    public override void Draw()
    {
        var device = DeviceRect();
        LastPosition = device.Min;
        LastSize = device.Size;
        Components.UiInteract.SetWindowHovered(ImGui.IsWindowHovered(
            ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem));
        Components.UiInteract.SetWindowFocused(ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows));
        var io = ImGui.GetIO();
        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && io.WantTextInput &&
            io.InputQueueCharacters.Size > 0)
        {
            UiFeedback.Play(UiSound.Keystroke);
        }

        Plugin.Updates.Poll();
        using (Plugin.Fonts.Push(1f))
        {
            ImGui.Dummy(ImGui.GetContentRegionAvail());
            using (InputShield.Engage(turning))
            {
                if (configuration.ShowPerfHud)
                {
                    Components.PerfHud.BeginShell();
                    shell.Draw(device);
                    Components.PerfHud.EndShell();
                    Components.PerfHud.Draw(device, UiScale.Current);
                }
                else
                {
                    shell.Draw(device);
                }
            }

            if (turning)
            {
                ApplyTurn(device);
            }
        }

        if (shell.MinimizePhase == MinimizePhase.None && !turning)
        {
            if (shell.Turn.ShowsLandscape)
            {
                landscapePosition = device.Min;
            }
            else
            {
                maximizedPosition = device.Min;
            }
        }

        if (shell.ConsumeCloseRequest())
        {
            IsOpen = false;
        }
    }
}
