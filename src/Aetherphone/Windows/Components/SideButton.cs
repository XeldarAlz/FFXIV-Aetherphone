using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal enum SideButtonAction
{
    None,
    Minimize,
    Close,
}

internal sealed class SideButton
{
    private const float HoldSeconds = 0.45f;
    private bool armed;
    private float held;
    private bool closeFired;

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
            closeFired = false;
        }

        if (armed && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            held += delta;
            if (held >= HoldSeconds && !closeFired)
            {
                closeFired = true;
                action = SideButtonAction.Close;
            }
        }

        if (armed && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (!closeFired && hovered)
            {
                action = SideButtonAction.Minimize;
            }

            armed = false;
            held = 0f;
        }

        var progress = armed ? Math.Clamp(held / HoldSeconds, 0f, 1f) : 0f;
        DrawButton(bounds, theme, hovered, progress, armed);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(bounds, Loc.T(L.Plugin.SideButtonHint));
        return action;
    }

    private static void DrawButton(Rect bounds, PhoneTheme theme, bool hovered, float progress, bool pressing)
    {
        var press = pressing ? 0.35f + 0.65f * progress : 0f;
        HardwareButton.Draw(ImGui.GetWindowDrawList(), bounds, theme, RailSide.Right, hovered, press, 0f);
    }
}
