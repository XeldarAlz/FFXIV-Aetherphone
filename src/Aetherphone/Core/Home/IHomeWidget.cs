using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Home;

internal readonly struct WidgetContext
{
    public readonly ImDrawListPtr DrawList;
    public readonly Rect Bounds;
    public readonly PhoneTheme Theme;
    public readonly WidgetSize Size;
    public readonly float Scale;
    public readonly float Delta;
    public readonly float Opacity;

    public WidgetContext(ImDrawListPtr drawList, Rect bounds, PhoneTheme theme, WidgetSize size, float scale,
        float delta, float opacity)
    {
        DrawList = drawList;
        Bounds = bounds;
        Theme = theme;
        Size = size;
        Scale = scale;
        Delta = delta;
        Opacity = opacity;
    }
}

internal interface IHomeWidget : IDisposable
{
    string Id { get; }
    string DisplayName { get; }
    string AppId { get; }
    WidgetSizeSet Sizes { get; }
    void Draw(in WidgetContext context);
}
