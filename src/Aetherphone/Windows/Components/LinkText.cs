using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class LinkText
{
    private static readonly RichTextCache Cache = new();

    public static RichTextLayout? LayoutFor(string text, float wrapWidth)
    {
        return Cache.LayoutFor(text, text, null, wrapWidth);
    }

    public static void Draw(ImDrawListPtr drawList, RichTextLayout layout, Vector2 origin, float pop, Vector4 ink,
        Vector4 linkInk, float alpha, bool interactive)
    {
        var richInk = new RichTextInk(ink, linkInk, linkInk, alpha, pop, interactive);
        RichText.Draw(drawList, layout, origin, richInk, out var hit);
        if (hit.Kind == RichTextRunKind.Link && hit.Clicked)
        {
            UrlActions.OpenInBrowser(layout.Urls[hit.TargetIndex]);
        }
    }
}
