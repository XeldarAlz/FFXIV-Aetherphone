using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Home;

internal readonly struct WidgetContext
{
    public const string PreviewKey = "preview";

    public readonly ImDrawListPtr DrawList;
    public readonly Rect Bounds;
    public readonly PhoneTheme Theme;
    public readonly WidgetSize Size;
    public readonly float Scale;
    public readonly float Delta;
    public readonly float Opacity;
    public readonly WidgetMode Mode;
    public readonly Vector4 Tint;
    public readonly bool Interactive;
    public readonly bool Preview;
    public readonly string InstanceKey;
    public readonly string Config;
    public readonly WidgetActions Actions;

    public WidgetContext(ImDrawListPtr drawList, Rect bounds, PhoneTheme theme, WidgetSize size, float scale,
        float delta, float opacity, WidgetMode mode, Vector4 tint, bool interactive, bool preview, string instanceKey,
        string config, WidgetActions actions)
    {
        DrawList = drawList;
        Bounds = bounds;
        Theme = theme;
        Size = size;
        Scale = scale;
        Delta = delta;
        Opacity = opacity;
        Mode = mode;
        Tint = tint;
        Interactive = interactive;
        Preview = preview;
        InstanceKey = instanceKey;
        Config = config;
        Actions = actions;
    }
}

internal interface IHomeWidget : IDisposable
{
    string Id { get; }
    string DisplayName { get; }
    string Description { get; }
    string AppId { get; }
    WidgetSizeSet Sizes { get; }
    IReadOnlyList<WidgetOption> Options => WidgetOption.None;
    void Draw(in WidgetContext context);
    WidgetRoute Target(in WidgetContext context) => WidgetRoute.App(AppId);
    float Relevance(string config) => 0f;
}
