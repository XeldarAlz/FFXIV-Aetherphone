using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly struct TextFold
{
    private const float Tolerance = 0.5f;

    public readonly float Height;
    public readonly float VisibleHeight;
    public readonly int VisibleLines;
    public readonly bool Folded;

    private TextFold(float height, float visibleHeight, int visibleLines, bool folded)
    {
        Height = height;
        VisibleHeight = visibleHeight;
        VisibleLines = visibleLines;
        Folded = folded;
    }

    public static TextFold Measure(float fullHeight, float lineHeight, int maxLines, bool expanded)
    {
        var linkedHeight = (maxLines + 1) * lineHeight;
        if (expanded || lineHeight <= 0f || fullHeight <= linkedHeight + Tolerance)
        {
            return new TextFold(fullHeight, fullHeight, int.MaxValue, false);
        }

        return new TextFold(linkedHeight, maxLines * lineHeight, maxLines, true);
    }

    public bool DrawReadMore(ImDrawListPtr drawList, Vector2 textTop, Vector4 ink, Vector4 hoverInk)
    {
        if (!Folded)
        {
            return false;
        }

        var label = Loc.T(L.Social.ReadMore);
        Plugin.Fonts.NoticeText(label);
        var min = new Vector2(textTop.X, textTop.Y + VisibleHeight);
        var max = new Vector2(min.X + ImGui.CalcTextSize(label).X, textTop.Y + Height);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        drawList.AddText(min, ImGui.GetColorU32(hovered ? hoverInk : ink), label);
        return UiInteract.Click(min, max, hovered);
    }
}
