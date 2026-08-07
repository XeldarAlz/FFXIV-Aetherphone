using Aetherphone.Core;
using Aetherphone.Core.Animation;
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
    private const float RotateSeconds = 0.26f;
    private readonly PhoneShell shell;
    private readonly Configuration configuration;
    private int recenterFrames;
    private int pendingFrames;
    private float landscapeBlend;
    private int rotatePinFrames;
    private Vector2? pendingPosition;
    private Vector2? maximizedPosition;
    private Vector2? minimizedPosition;

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
        if (configuration.MaximizedPosition == maximizedPosition && configuration.MinimizedPosition == minimizedPosition)
        {
            return;
        }

        configuration.MaximizedPosition = maximizedPosition;
        configuration.MinimizedPosition = minimizedPosition;
        configuration.SaveNow();
    }

    public void Recenter()
    {
        shell.ForceMaximize();
        recenterFrames = RecenterFrameCount;
        pendingFrames = 0;
        minimizedPosition = null;
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
        var width = Components.PhoneBounds.ClampWidth(configuration.PhoneWidth);
        var zoom = PhoneSizeCatalog.ZoomFor(width);
        UiScale.SetPhone(zoom);
        Plugin.Fonts.SetPhoneZoom(zoom);
        var phase = shell.MinimizePhase;
        var minimized = phase == MinimizePhase.Minimized;
        var size = minimized ? MinimizeTransition.MinimizedSize : OrientedSize(width);
        Size = size;
        SizeCondition = ImGuiCond.Always;
        var locked = !minimized && configuration.LockPosition;
        var holdStill = !minimized && (shell.HomeEditing || Components.UiInteract.PointerOverGestureSurface);
        Flags = locked || holdStill
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
        else if (phase is MinimizePhase.Collapsing or MinimizePhase.Expanding &&
                 maximizedPosition is { } homePosition && minimizedPosition is { } dockPosition)
        {
            Position = Vector2.Lerp(homePosition, dockPosition, shell.MinimizeEased);
            PositionCondition = ImGuiCond.Always;
        }
        else if (!minimized && rotatePinFrames > 0 && LastSize.Y > 0f)
        {
            rotatePinFrames--;
            Position = CenterPinnedPosition(size);
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

    private Vector2 OrientedSize(float width)
    {
        var portrait = PhoneSizeCatalog.SizeFor(width);
        var target = shell.LandscapeActive ? 1f : 0f;
        if (landscapeBlend != target)
        {
            var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
            var step = delta / RotateSeconds;
            landscapeBlend = target > landscapeBlend
                ? MathF.Min(target, landscapeBlend + step)
                : MathF.Max(target, landscapeBlend - step);
            rotatePinFrames = RecenterFrameCount;
        }

        if (landscapeBlend <= 0f)
        {
            return portrait;
        }

        var landscape = new Vector2(portrait.Y, portrait.X);
        return Vector2.Lerp(portrait, landscape, Easing.SmootherStep(landscapeBlend));
    }

    private Vector2 CenterPinnedPosition(Vector2 size)
    {
        var scaledSize = size * UiScale.Global;
        var center = LastPosition + LastSize * 0.5f;
        var viewport = ImGui.GetMainViewport();
        var position = center - scaledSize * 0.5f;
        var maxPosition = viewport.Pos + viewport.Size - scaledSize;
        position.X = Math.Clamp(position.X, viewport.Pos.X, MathF.Max(viewport.Pos.X, maxPosition.X));
        position.Y = Math.Clamp(position.Y, viewport.Pos.Y, MathF.Max(viewport.Pos.Y, maxPosition.Y));
        return position;
    }

    public override void Draw()
    {
        LastPosition = ImGui.GetWindowPos();
        LastSize = ImGui.GetWindowSize();
        Components.UiInteract.SetWindowHovered(ImGui.IsWindowHovered(
            ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem));
        Components.UiInteract.SetWindowFocused(ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows));
        Plugin.Updates.Poll();
        using (Plugin.Fonts.Push(1f))
        {
            var origin = ImGui.GetCursorScreenPos();
            var available = ImGui.GetContentRegionAvail();
            ImGui.Dummy(available);
            var device = new Rect(origin, origin + available);
            shell.Draw(device);
        }

        var phase = shell.MinimizePhase;
        if (phase == MinimizePhase.None)
        {
            maximizedPosition = ImGui.GetWindowPos();
        }
        else if (phase == MinimizePhase.Minimized)
        {
            minimizedPosition = ImGui.GetWindowPos();
        }

        if (shell.ConsumeCloseRequest())
        {
            IsOpen = false;
        }
    }
}
