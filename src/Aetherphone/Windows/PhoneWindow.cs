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
    private int restoreFrames;
    private Vector2? maximizedPosition;
    private Vector2 expandFromPosition;
    private MinimizePhase lastPhase;
    private string pendingOpenTrigger = "toggle";
    private DateTime shellOpenedAt;

    public PhoneWindow(PhoneShell shell) : base(AepConstants.Name, BaseFlags)
    {
        this.shell = shell;
        Size = PhoneSizeCatalog.SizeFor(Plugin.Cfg.PhoneScale);
        SizeCondition = ImGuiCond.Always;
        RespectCloseHotkey = false;
    }

    public bool IsMinimized => shell.MinimizedResting;

    public Vector2 LastPosition { get; private set; }

    public Vector2 LastSize { get; private set; }

    public bool ShowsChrome => IsOpen && shell.MinimizePhase == MinimizePhase.None && LastSize.Y > 0f;

    public void Maximize()
    {
        if (shell.MinimizePhase != MinimizePhase.None)
        {
            restoreFrames = RecenterFrameCount;
        }

        shell.ForceMaximize();
    }

    public void StartMinimized() => shell.ForceMinimized();

    public void MarkOpenTrigger(string trigger) => pendingOpenTrigger = trigger;

    public void Recenter()
    {
        shell.ForceMaximize();
        recenterFrames = RecenterFrameCount;
        restoreFrames = 0;
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

        shell.ForceMaximize();
        restoreFrames = RecenterFrameCount;
        pendingOpenTrigger = "toggle";
        IsOpen = true;
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
        else if (restoreFrames > 0 && maximizedPosition is { } restoreTarget)
        {
            Position = restoreTarget;
            PositionCondition = ImGuiCond.Always;
            restoreFrames--;
        }
        else if (phase == MinimizePhase.Expanding && maximizedPosition is { } homePosition)
        {
            Position = Vector2.Lerp(homePosition, expandFromPosition, shell.MinimizeEased);
            PositionCondition = ImGuiCond.Always;
        }
        else
        {
            Position = null;
            restoreFrames = 0;
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
        if (phase == MinimizePhase.Expanding && lastPhase != MinimizePhase.Expanding)
        {
            expandFromPosition = ImGui.GetWindowPos();
        }

        if (phase == MinimizePhase.None)
        {
            maximizedPosition = ImGui.GetWindowPos();
        }

        lastPhase = phase;

        if (shell.ConsumeCloseRequest())
        {
            IsOpen = false;
        }
    }
}
