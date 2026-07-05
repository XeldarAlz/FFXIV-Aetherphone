using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Analytics;
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
    private bool lastMinimized;
    private bool recenterRequested;
    private bool restorePending;
    private Vector2? maximizedCenter;
    private Vector2? minimizedCenter;
    private string pendingOpenTrigger = "toggle";
    private DateTime shellOpenedAt;

    public PhoneWindow(PhoneShell shell) : base(AepConstants.Name, BaseFlags)
    {
        this.shell = shell;
        Size = PhoneSizeCatalog.SizeFor(Plugin.Cfg.PhoneScale);
        SizeCondition = ImGuiCond.Always;
        RespectCloseHotkey = false;
        maximizedCenter = Plugin.Cfg.MaximizedCenter;
        minimizedCenter = Plugin.Cfg.MinimizedCenter;
    }

    public void Maximize() => minimized = false;
    public void StartMinimized() => minimized = true;

    public void MarkOpenTrigger(string trigger) => pendingOpenTrigger = trigger;

    public void Recenter()
    {
        minimized = false;
        recenterRequested = true;
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
        restorePending = true;
        Plugin.Analytics.Track(AnalyticsEvents.ShellOpened(pendingOpenTrigger));
        pendingOpenTrigger = "toggle";
        shell.OnOpened();
    }

    public override void OnClose()
    {
        PersistCenter();
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
        if (recenterRequested)
        {
            ApplyCenter(ViewportCenter(), size);
            recenterRequested = false;
            restorePending = false;
        }
        else if (restorePending || minimized != lastMinimized)
        {
            var remembered = minimized ? minimizedCenter : maximizedCenter;
            if (remembered is { } center)
            {
                ApplyCenter(center, size);
            }
            else
            {
                Position = null;
            }

            restorePending = false;
        }
        else
        {
            Position = null;
        }

        lastMinimized = minimized;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
    }

    private void ApplyCenter(Vector2 center, Vector2 size)
    {
        var viewport = ImGui.GetMainViewport();
        var lower = viewport.Pos;
        var upper = Vector2.Max(lower, viewport.Pos + viewport.Size - size);
        Position = Vector2.Clamp(center - size * 0.5f, lower, upper);
        PositionCondition = ImGuiCond.Always;
    }

    private static Vector2 ViewportCenter()
    {
        var viewport = ImGui.GetMainViewport();
        return viewport.Pos + viewport.Size * 0.5f;
    }

    private void PersistCenter()
    {
        if (maximizedCenter is null && minimizedCenter is null)
        {
            return;
        }

        Plugin.Cfg.MaximizedCenter = maximizedCenter;
        Plugin.Cfg.MinimizedCenter = minimizedCenter;
        Plugin.Cfg.Save();
    }

    public override void PostDraw() => ImGui.PopStyleVar();

    public override void Draw()
    {
        var center = ImGui.GetWindowPos() + ImGui.GetWindowSize() * 0.5f;
        if (minimized)
        {
            minimizedCenter = center;
        }
        else
        {
            maximizedCenter = center;
        }

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
