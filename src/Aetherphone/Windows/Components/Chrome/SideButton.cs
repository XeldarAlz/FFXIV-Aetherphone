using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

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

    public SideButtonAction Update(Rect bounds, RailSide side, PhoneTheme theme, float delta)
    {
        var hit = HardwareButton.HitRect(bounds, side);
        var hovered = UiInteract.Hover(hit.Min, hit.Max);
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
        DrawButton(bounds, side, theme, hovered, progress, armed);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(hit, Loc.T(L.Plugin.SideButtonHint));
        return action;
    }

    private static void DrawButton(Rect bounds, RailSide side, PhoneTheme theme, bool hovered, float progress,
        bool pressing)
    {
        var press = pressing ? 0.35f + 0.65f * progress : 0f;
        HardwareButton.Draw(ImGui.GetWindowDrawList(), bounds, theme, side, HardwareKey.Side, hovered, press);
    }
}
