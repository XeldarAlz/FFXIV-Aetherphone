using Aetherphone.Core.Apps;

namespace Aetherphone.Core.Home;

internal sealed class WidgetRegistry
{
    private readonly List<IHomeWidget> widgets;
    private readonly Dictionary<string, IHomeWidget> byId = new();
    private readonly Dictionary<string, IPhoneApp> appsById = new();

    public WidgetRegistry(IReadOnlyList<IHomeWidget> widgets, IReadOnlyList<IPhoneApp> apps)
    {
        this.widgets = new List<IHomeWidget>(widgets.Count);
        for (var index = 0; index < apps.Count; index++)
        {
            appsById[apps[index].Id] = apps[index];
        }

        for (var index = 0; index < widgets.Count; index++)
        {
            var widget = widgets[index];
            this.widgets.Add(widget);
            byId[widget.Id] = widget;
        }
    }

    public IReadOnlyList<IHomeWidget> All => widgets;

    public bool TryGet(string id, out IHomeWidget widget) => byId.TryGetValue(id, out widget!);

    public bool IsAvailable(IHomeWidget widget) =>
        appsById.TryGetValue(widget.AppId, out var app) && app.IsAvailable;

    public IPhoneApp? AppFor(IHomeWidget widget) =>
        appsById.TryGetValue(widget.AppId, out var app) ? app : null;
}
