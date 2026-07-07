using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Core.ControlCenter.Modules;

internal sealed class ToggleModule : IControlModule
{
    private static readonly ControlSpan[] SpanOptions = { ControlSpan.Small, ControlSpan.Wide };

    private readonly FontAwesomeIcon icon;
    private readonly string label;
    private readonly Func<bool> isActive;
    private readonly Action onActivate;

    public ToggleModule(string id, FontAwesomeIcon icon, string label, Func<bool> isActive, Action onActivate)
    {
        Id = id;
        this.icon = icon;
        this.label = label;
        this.isActive = isActive;
        this.onActivate = onActivate;
    }

    public string Id { get; }
    public string GalleryLabel => label;
    public FontAwesomeIcon GalleryIcon => icon;
    public IReadOnlyList<ControlSpan> Sizes => SpanOptions;
    public ControlSpan DefaultSpan => ControlSpan.Small;

    public void Draw(in ControlModuleContext context)
    {
        if (ControlTile.Toggle(context.DrawList, context.Rect, icon, label, isActive(), context.Theme.Accent,
                context.Theme, context.Opacity, context.Interactive))
        {
            onActivate();
        }
    }
}
