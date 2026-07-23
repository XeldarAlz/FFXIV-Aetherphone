using Aetherphone.Core;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Windows;

internal sealed class PhoneWindow : Window
{
    private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar |
                                               ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse |
                                               ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoBackground;

    private const int RecenterFrameCount = 3;
    private readonly PhoneShell shell;
    private readonly Configuration configuration;
    private int recenterFrames;
    private int pendingFrames;
    private Vector2? pendingPosition;
    private Vector2? maximizedPosition;
    private Vector2? minimizedPosition;
    private bool landscape;

    public PhoneWindow(PhoneShell shell, Configuration configuration)
        : base(AepConstants.Name, BaseFlags)
    {
        this.shell = shell;
        this.configuration = configuration;
        Size = PhoneSizeCatalog.SizeFor(configuration.PhoneScale);
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
        configuration.Save();
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
        var phase = shell.MinimizePhase;
        var minimized = phase == MinimizePhase.Minimized;
        var wantsLandscape = shell.WantsLandscape;
        var requestedSize = minimized
            ? MinimizeTransition.MinimizedSize
            : wantsLandscape
                ? PhoneSizeCatalog.LandscapeSizeFor(configuration.PhoneScale)
                : PhoneSizeCatalog.SizeFor(configuration.PhoneScale);
        var size = SnapLogicalSize(requestedSize);
        if (!minimized && wantsLandscape != landscape)
        {
            if (LastSize.X > 0f && LastSize.Y > 0f)
            {
                var center = LastPosition + LastSize * 0.5f;
                Position = center - size * ImGuiHelpers.GlobalScale * 0.5f;
                PositionCondition = ImGuiCond.Always;
            }

            landscape = wantsLandscape;
        }

        Size = size;
        SizeCondition = ImGuiCond.Always;
        Flags = !minimized && configuration.LockPosition ? BaseFlags | ImGuiWindowFlags.NoMove : BaseFlags;

        if (recenterFrames > 0)
        {
            var viewport = ImGui.GetMainViewport();
            var scaledSize = size * ImGuiHelpers.GlobalScale;
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
        else
        {
            Position = null;
            pendingFrames = 0;
        }

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
    }

    public override void PostDraw() => ImGui.PopStyleVar();

    public override void Draw()
    {
        LastPosition = ImGui.GetWindowPos();
        LastSize = ImGui.GetWindowSize();
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
            if (!landscape)
            {
                maximizedPosition = ImGui.GetWindowPos();
            }
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

    private static Vector2 SnapLogicalSize(Vector2 logicalSize)
    {
        var scale = MathF.Max(0.01f, ImGuiHelpers.GlobalScale);
        var physical = logicalSize * scale;
        var snapped = new Vector2(MathF.Round(physical.X), MathF.Round(physical.Y));
        return snapped / scale;
    }
}
