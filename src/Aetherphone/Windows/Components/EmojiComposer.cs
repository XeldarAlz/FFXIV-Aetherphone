using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

// Shared emoji-drawer state for any composer: a smile toggle button plus the picker panel it opens.
// Callers own an instance, place the toggle where their layout allows, and hand it a panel rect.
internal sealed class EmojiComposer
{
    private const float PanelHeightUnits = 244f;

    private readonly EmojiPicker picker = new();
    private bool open;

    public bool Open => open;

    public void Close()
    {
        open = false;
    }

    public float PanelHeight(float scale)
    {
        return open ? PanelHeightUnits * scale : 0f;
    }

    public void DrawToggle(in AppSkin ui, Vector2 center, float radius, Vector4 activeColor, Vector4 idleColor,
        string tooltip, HoverLabelSide side = HoverLabelSide.Above)
    {
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var color = open ? activeColor : hovered ? ui.Theme.TextStrong : idleColor;
        AppSkin.Icon(center, FontAwesomeIcon.Smile.ToIconString(), color, 0.95f);
        HoverTooltip.Show(new Rect(min, max), tooltip, side);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            open = !open;
        }
    }

    public void DrawPanel(Rect panel, in AppSkin ui, ref string draft, int maxLength)
    {
        if (!open)
        {
            return;
        }

        var picked = picker.Draw(panel, ui);
        if (picked is null || draft.Length + picked.Length > maxLength)
        {
            return;
        }

        draft += picked;
    }
}
