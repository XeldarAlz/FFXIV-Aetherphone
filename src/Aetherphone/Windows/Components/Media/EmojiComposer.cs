using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal sealed class EmojiComposer
{
    private const float PanelHeightUnits = 244f;

    private readonly EmojiPicker picker = new();
    private int openedFrame = -1;
    private int closedFrame = -1;
    private bool open;

    public bool Open => open;

    public Action<ImDrawListPtr, Vector2, float, Vector4>? IconPainter { get; set; }

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
        var hovered = UiInteract.Hover(min, max);
        var color = open ? activeColor : hovered ? ui.Theme.TextStrong : idleColor;
        if (IconPainter is { } painter)
        {
            painter(ImGui.GetWindowDrawList(), center, radius, color);
        }
        else
        {
            AppSkin.Icon(center, IconGlyph.Of(FontAwesomeIcon.Smile), color, 0.95f);
        }

        HoverTooltip.Show(new Rect(min, max), tooltip, side);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.GetFrameCount() == closedFrame)
        {
            return;
        }

        open = !open;
        openedFrame = ImGui.GetFrameCount();
    }

    public void DrawPanel(Rect panel, in AppSkin ui, ref string draft, int maxLength)
    {
        if (!open)
        {
            return;
        }

        var picked = picker.Draw(panel, ui);
        if (picked is null)
        {
            DismissOnOutsideClick(panel);
            return;
        }

        if (draft.Length + picked.Length > maxLength)
        {
            return;
        }

        draft += picked;
    }

    private void DismissOnOutsideClick(Rect panel)
    {
        var frame = ImGui.GetFrameCount();
        if (frame == openedFrame || !UiInteract.ClickedOutside(panel.Min, panel.Max))
        {
            return;
        }

        open = false;
        closedFrame = frame;
    }
}
