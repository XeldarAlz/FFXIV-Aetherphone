using System.Numerics;
using Aetherphone.Core;
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

    private static readonly Vector2 MinimizedSize = new(78f, 152f);
    private readonly PhoneShell shell;
    private bool minimized;
    private bool recenterRequested;

    public PhoneWindow(PhoneShell shell) : base(AepConstants.Name, BaseFlags)
    {
        this.shell = shell;
        Size = PhoneSizeCatalog.SizeFor(Plugin.Cfg.PhoneScale);
        SizeCondition = ImGuiCond.Always;
        RespectCloseHotkey = false;
    }

    public void Maximize() => minimized = false;
    public void StartMinimized() => minimized = true;

    public void Recenter()
    {
        minimized = false;
        recenterRequested = true;
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
        IsOpen = true;
    }

    public override void OnOpen() => shell.OnOpened();
    public override void OnClose() => shell.OnClosed();

    public override void PreDraw()
    {
        var size = minimized ? MinimizedSize : PhoneSizeCatalog.SizeFor(Plugin.Cfg.PhoneScale);
        Size = size;
        SizeCondition = ImGuiCond.Always;
        Flags = !minimized && Plugin.Cfg.LockPosition ? BaseFlags | ImGuiWindowFlags.NoMove : BaseFlags;
        if (recenterRequested)
        {
            var viewport = ImGui.GetMainViewport();
            Position = viewport.Pos + (viewport.Size - size) * 0.5f;
            PositionCondition = ImGuiCond.Always;
            recenterRequested = false;
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
        }
    }
}
