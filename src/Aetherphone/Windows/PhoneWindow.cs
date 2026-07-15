using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Analytics;
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
    private int recenterFrames;
    private int pendingFrames;
    private Vector2? pendingPosition;
    private Vector2? maximizedPosition;
    private Vector2? minimizedPosition;
    private string pendingOpenTrigger = "toggle";
    private DateTime shellOpenedAt;

    public PhoneWindow(PhoneShell shell) : base(AepConstants.Name, BaseFlags)
    {
        this.shell = shell;
        Size = PhoneSizeCatalog.SizeFor(Plugin.Cfg.PhoneScale);
        SizeCondition = ImGuiCond.Always;
        RespectCloseHotkey = false;
        maximizedPosition = Plugin.Cfg.MaximizedPosition;
        minimizedPosition = Plugin.Cfg.MinimizedPosition;
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

    public void MarkOpenTrigger(string trigger) => pendingOpenTrigger = trigger;

    public void PersistPositions()
    {
        if (Plugin.Cfg.MaximizedPosition == maximizedPosition && Plugin.Cfg.MinimizedPosition == minimizedPosition)
        {
            return;
        }

        Plugin.Cfg.MaximizedPosition = maximizedPosition;
        Plugin.Cfg.MinimizedPosition = minimizedPosition;
        Plugin.Cfg.Save();
    }

    public void Recenter()
    {
        shell.ForceMaximize();
        recenterFrames = RecenterFrameCount;
        pendingFrames = 0;
        minimizedPosition = null;
        pendingOpenTrigger = "command";
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
        pendingOpenTrigger = "toggle";
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
        shellOpenedAt = DateTime.UtcNow;
        Plugin.Analytics.Track(AnalyticsEvents.ShellOpened(pendingOpenTrigger));
        pendingOpenTrigger = "toggle";
        shell.OnOpened();
    }

    public override void OnClose()
    {
        var durationMs = (DateTime.UtcNow - shellOpenedAt).TotalMilliseconds;
        Plugin.Analytics.Track(AnalyticsEvents.ShellClosed(durationMs));
        PersistPositions();
        shell.OnClosed();
    }

    public override void PreDraw()
    {
        var phase = shell.MinimizePhase;
        var minimized = phase == MinimizePhase.Minimized;
        var size = minimized ? MinimizeTransition.MinimizedSize : PhoneSizeCatalog.SizeFor(Plugin.Cfg.PhoneScale);
        Size = size;
        SizeCondition = ImGuiCond.Always;
        Flags = !minimized && Plugin.Cfg.LockPosition ? BaseFlags | ImGuiWindowFlags.NoMove : BaseFlags;

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
