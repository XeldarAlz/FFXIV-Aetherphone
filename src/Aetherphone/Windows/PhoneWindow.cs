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

    public void Maximize() => shell.ForceMaximize();
    public void StartMinimized() => shell.ForceMinimized();

    public void MarkOpenTrigger(string trigger) => pendingOpenTrigger = trigger;

    public void Recenter()
    {
        shell.ForceMaximize();
        recenterFrames = RecenterFrameCount;
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
        var minimized = shell.MinimizedResting;
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
        else
        {
            Position = null;
        }

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
    }

    public override void PostDraw() => ImGui.PopStyleVar();

    public override void Draw()
    {
        using (Plugin.Fonts.Push(1f))
        {
            var origin = ImGui.GetCursorScreenPos();
            var available = ImGui.GetContentRegionAvail();
            ImGui.Dummy(available);
            var device = new Rect(origin, origin + available);
            shell.Draw(device);
        }

        if (shell.ConsumeCloseRequest())
        {
            IsOpen = false;
        }
    }
}
