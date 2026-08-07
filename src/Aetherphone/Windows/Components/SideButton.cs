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
    private const float InwardHitPadding = 8f;
    private const float OutwardHitPadding = 4f;
    private const float AlongHitPadding = 8f;
    private bool armed;
    private float held;
    private bool closeFired;

    public SideButtonAction Update(Rect bounds, RailSide side, PhoneTheme theme, float delta)
    {
        var scale = UiScale.Current;
        var hit = HitRect(bounds, side, scale);
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

        HoverTooltip.Show(bounds, Loc.T(L.Plugin.SideButtonHint));
        return action;
    }

    private static Rect HitRect(Rect bounds, RailSide side, float scale)
    {
        var inward = InwardHitPadding * scale;
        var outward = OutwardHitPadding * scale;
        var along = AlongHitPadding * scale;
        return side switch
        {
            RailSide.Top => new Rect(new Vector2(bounds.Min.X - along, bounds.Min.Y - outward),
                new Vector2(bounds.Max.X + along, bounds.Max.Y + inward)),
            RailSide.Bottom => new Rect(new Vector2(bounds.Min.X - along, bounds.Min.Y - inward),
                new Vector2(bounds.Max.X + along, bounds.Max.Y + outward)),
            RailSide.Left => new Rect(new Vector2(bounds.Min.X - outward, bounds.Min.Y - along),
                new Vector2(bounds.Max.X + inward, bounds.Max.Y + along)),
            _ => new Rect(new Vector2(bounds.Min.X - inward, bounds.Min.Y - along),
                new Vector2(bounds.Max.X + outward, bounds.Max.Y + along)),
        };
    }

    private static void DrawButton(Rect bounds, RailSide side, PhoneTheme theme, bool hovered, float progress,
        bool pressing)
    {
        var press = pressing ? 0.35f + 0.65f * progress : 0f;
        HardwareButton.Draw(ImGui.GetWindowDrawList(), bounds, theme, side, hovered, press, 0f);
    }
}
