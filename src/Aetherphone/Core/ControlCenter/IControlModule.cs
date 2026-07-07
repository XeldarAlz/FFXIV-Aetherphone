using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Core.ControlCenter;

internal readonly struct ControlModuleContext
{
    public readonly ImDrawListPtr DrawList;
    public readonly Rect Rect;
    public readonly PhoneTheme Theme;
    public readonly ControlSpan Span;
    public readonly float Scale;
    public readonly float Opacity;
    public readonly bool Interactive;

    public ControlModuleContext(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, ControlSpan span, float scale,
        float opacity, bool interactive)
    {
        DrawList = drawList;
        Rect = rect;
        Theme = theme;
        Span = span;
        Scale = scale;
        Opacity = opacity;
        Interactive = interactive;
    }
}

internal interface IControlModule
{
    string Id { get; }
    string GalleryLabel { get; }
    FontAwesomeIcon GalleryIcon { get; }
    IReadOnlyList<ControlSpan> Sizes { get; }
    ControlSpan DefaultSpan { get; }
    void Draw(in ControlModuleContext context);
}
