using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class ReplyMention
{
    public static string Prefill(string draft, string handle, int maxLength)
    {
        if (handle.Length == 0)
        {
            return draft;
        }

        var mention = "@" + handle + " ";
        if (draft.StartsWith(mention, StringComparison.OrdinalIgnoreCase))
        {
            return draft;
        }

        var prefilled = mention + draft;
        return prefilled.Length <= maxLength ? prefilled : draft;
    }

    public static float Height(TextStyle style) => Typography.LineHeight(style);

    public static bool DrawLink(ImDrawListPtr drawList, Vector2 position, string label, float maxWidth,
        TextStyle style, Vector4 idleInk, Vector4 hoverInk)
    {
        label = Typography.FitText(label, maxWidth, style);
        var size = Typography.Measure(label, style);
        var max = position + size;
        var hovered = UiInteract.Hover(position, max);
        Typography.Draw(drawList, position, label, hovered ? hoverInk : idleInk, style);
        return UiInteract.HoverClick(position, max);
    }
}
