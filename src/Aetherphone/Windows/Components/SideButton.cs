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
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var rounding = bounds.Width * 0.5f;
        var resting = Palette.Mix(theme.BezelRim, theme.Accent, 0.45f);
        var fill = hovered ? Palette.Mix(resting, theme.Accent, 0.6f) : resting;
        dl.AddRectFilled(bounds.Min, bounds.Max, ImGui.GetColorU32(fill), rounding);
        if (progress > 0f)
        {
            var top = bounds.Max.Y - bounds.Height * progress;
            dl.AddRectFilled(new Vector2(bounds.Min.X, top), bounds.Max, ImGui.GetColorU32(theme.Accent), rounding,
                ImDrawFlags.RoundCornersBottom);
        }

        var glowAlpha = hovered || progress > 0f ? 0.95f : 0.6f;
        var glow = ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, glowAlpha));
        dl.AddLine(new Vector2(bounds.Max.X - scale, bounds.Min.Y + rounding),
            new Vector2(bounds.Max.X - scale, bounds.Max.Y - rounding), glow, 2f * scale);
    }
}
