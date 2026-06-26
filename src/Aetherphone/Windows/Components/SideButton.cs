using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal enum SideButtonAction
{
    None,
    Close,
    Lock,
}

internal sealed class SideButton
{
    private const float HoldSeconds = 0.45f;

    private bool armed;
    private float held;
    private bool lockFired;

    public SideButtonAction Update(Rect bounds, PhoneTheme theme, float delta)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var hitMin = new Vector2(bounds.Min.X - 8f * scale, bounds.Min.Y - 8f * scale);
        var hitMax = new Vector2(bounds.Max.X + 4f * scale, bounds.Max.Y + 8f * scale);
        var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);

        var action = SideButtonAction.None;

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            armed = true;
            held = 0f;
            lockFired = false;
        }

        if (armed && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            held += delta;
            if (held >= HoldSeconds && !lockFired)
            {
                lockFired = true;
                action = SideButtonAction.Lock;
            }
        }

        if (armed && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (!lockFired && hovered)
            {
                action = SideButtonAction.Close;
            }

            armed = false;
            held = 0f;
        }

        var progress = armed ? Math.Clamp(held / HoldSeconds, 0f, 1f) : 0f;
        DrawButton(bounds, theme, hovered, progress);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ImGui.SetTooltip(Loc.T(L.Plugin.SideButtonHint));
        }

        return action;
    }

    private static void DrawButton(Rect bounds, PhoneTheme theme, bool hovered, float progress)
    {
        var fill = hovered ? Palette.Mix(theme.BezelRim, theme.TextStrong, 0.7f) : theme.BezelRim;
        if (progress > 0f)
        {
            fill = Palette.Mix(fill, theme.Accent, progress);
        }

        ImGui.GetWindowDrawList().AddRectFilled(bounds.Min, bounds.Max, ImGui.GetColorU32(fill), bounds.Width * 0.5f);
    }
}
