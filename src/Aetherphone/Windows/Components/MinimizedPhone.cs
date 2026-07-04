using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class MinimizedPhone
{
    public static bool Draw(Rect device, PhoneTheme theme)
    {
        var dl = ImGui.GetWindowDrawList();
        var body = device.Inset(ImGuiHelpers.GlobalScale);
        var rounding = body.Width * 0.30f;
        var bezel = body.Width * 0.09f;
        var screen = body.Inset(bezel);
        var screenRounding = MathF.Max(rounding - bezel, 0f);

        dl.AddRectFilled(body.Min, body.Max, ImGui.GetColorU32(theme.BezelOuter), rounding);
        dl.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(theme.ScreenBase), screenRounding);
        dl.AddRect(body.Min, body.Max, ImGui.GetColorU32(theme.BezelRim), rounding);

        var radius = MathF.Min(screen.Width, screen.Height) * 0.32f;
        var clicked = LockButton.Draw(screen.Center, radius, FontAwesomeIcon.Expand, true, theme);

        if (ImGui.IsMouseHoveringRect(device.Min, device.Max))
        {
            ImGui.SetTooltip(Loc.T(L.Plugin.MaximizeHint));
        }

        return clicked;
    }
}
