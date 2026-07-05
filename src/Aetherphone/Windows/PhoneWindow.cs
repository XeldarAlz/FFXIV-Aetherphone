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
    private static readonly Vector2 MinimizedSize = new(78f, 152f);
    private readonly PhoneShell shell;
    private bool minimized;
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

    public void Maximize() => minimized = false;
    public void StartMinimized() => minimized = true;

    public void MarkOpenTrigger(string trigger) => pendingOpenTrigger = trigger;

    public void Recenter()
    {
        minimized = false;
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

        minimized = false;
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
        var size = minimized ? MinimizedSize : PhoneSizeCatalog.SizeFor(Plugin.Cfg.PhoneScale);
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
            if (minimized)
            {
                if (shell.DrawMinimized(device))
                {
                    minimized = false;
                }

                return;
            }

            shell.Draw(device);
        }

        if (shell.ConsumeCloseRequest())
        {
            IsOpen = false;
        }

        if (shell.ConsumeMinimizeRequest())
        {
            minimized = true;
            if (Plugin.Cfg.LockPosition)
            {
                Plugin.Cfg.LockPosition = false;
                Plugin.Cfg.Save();
            }
        }
    }
}
