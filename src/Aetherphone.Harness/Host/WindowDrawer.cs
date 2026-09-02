using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Harness.Host;

internal sealed class WindowDrawer
{
    private readonly HashSet<Window> open = new();
    private Window? pinned;
    private Vector2 pinnedPosition;

    public void Pin(Window window, Vector2 position)
    {
        pinned = window;
        pinnedPosition = position;
    }

    public void Draw(IReadOnlyList<IWindow> windows)
    {
        for (var index = 0; index < windows.Count; index++)
        {
            if (windows[index] is Window window)
            {
                DrawWindow(window);
            }
        }
    }

    private void DrawWindow(Window window)
    {
        window.PreOpenCheck();
        var wasOpen = open.Contains(window);
        if (!window.IsOpen)
        {
            if (wasOpen)
            {
                open.Remove(window);
                window.OnClose();
            }

            return;
        }

        if (!wasOpen)
        {
            open.Add(window);
            window.OnOpen();
        }

        window.Update();
        if (!window.DrawConditions())
        {
            return;
        }

        var hasNamespace = !string.IsNullOrEmpty(window.Namespace);
        if (hasNamespace)
        {
            ImGui.PushID(window.Namespace);
        }

        window.PreDraw();
        ApplyConditionals(window);
        if (ReferenceEquals(window, pinned))
        {
            ImGui.SetNextWindowPos(pinnedPosition, ImGuiCond.Always);
        }

        bool drawn;
        if (window.ShowCloseButton)
        {
            var stillOpen = true;
            drawn = ImGui.Begin(window.WindowName, ref stillOpen, window.Flags);
            if (!stillOpen)
            {
                window.IsOpen = false;
            }
        }
        else
        {
            drawn = ImGui.Begin(window.WindowName, window.Flags);
        }

        if (drawn)
        {
            window.IsFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
            window.IsHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows);
            window.Draw();
        }

        ImGui.End();
        window.PostDraw();
        if (hasNamespace)
        {
            ImGui.PopID();
        }
    }

    private static void ApplyConditionals(Window window)
    {
        if (window.Position.HasValue)
        {
            ImGui.SetNextWindowPos(window.Position.Value, window.PositionCondition);
        }

        if (window.Size.HasValue)
        {
            ImGui.SetNextWindowSize(window.Size.Value, window.SizeCondition);
        }

        if (window.Collapsed.HasValue)
        {
            ImGui.SetNextWindowCollapsed(window.Collapsed.Value, window.CollapsedCondition);
        }

        if (window.SizeConstraints.HasValue)
        {
            var constraints = window.SizeConstraints.Value;
            ImGui.SetNextWindowSizeConstraints(constraints.MinimumSize, constraints.MaximumSize);
        }

        if (window.BgAlpha.HasValue)
        {
            ImGui.SetNextWindowBgAlpha(window.BgAlpha.Value);
        }
    }
}
