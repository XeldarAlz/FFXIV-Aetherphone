using Aetherphone.Core.Home;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell.Home;

internal sealed class WidgetHost
{
    private readonly Configuration configuration;
    private readonly Dictionary<(string WidgetId, WidgetSize Size), string> previewKeys = new();

    public WidgetHost(WidgetActions actions, Configuration configuration)
    {
        Actions = actions;
        this.configuration = configuration;
    }

    public WidgetActions Actions { get; }

    public WidgetMode Mode => WidgetModes.From(configuration.IconAppearance);

    public WidgetContext Tile(ImDrawListPtr drawList, Rect bounds, PhoneTheme theme, HomeTile tile, float scale,
        float delta, float opacity, bool interactive) =>
        new(drawList, bounds, theme, tile.Size, scale, delta, opacity, Mode, theme.Accent, interactive, false,
            tile.InstanceKey, tile.Config, Actions);

    public WidgetContext Preview(ImDrawListPtr drawList, Rect bounds, PhoneTheme theme, string widgetId,
        WidgetSize size, float scale, float delta) =>
        new(drawList, bounds, theme, size, scale, delta, 1f, Mode, theme.Accent, false, true,
            PreviewKey(widgetId, size), string.Empty, Actions);

    public void OpenTarget(HomeTile tile, Rect origin, PhoneTheme theme, float scale)
    {
        if (tile.Widget is not { } widget)
        {
            return;
        }

        var context = Tile(ImGui.GetWindowDrawList(), origin, theme, tile, scale, 0f, 1f, false);
        var route = widget.Target(context);
        Actions.Open(route.IsEmpty ? WidgetRoute.App(widget.AppId) : route, origin);
    }

    private string PreviewKey(string widgetId, WidgetSize size)
    {
        if (previewKeys.TryGetValue((widgetId, size), out var key))
        {
            return key;
        }

        key = string.Concat(WidgetContext.PreviewKey, ":", widgetId, ":", size.ToString());
        previewKeys[(widgetId, size)] = key;
        return key;
    }
}
