using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Core.ControlCenter.Modules;

internal sealed class ToggleModule : IControlModule
{
    private static readonly ControlSpan[] SpanOptions = { ControlSpan.Small, ControlSpan.Wide, ControlSpan.Large };

    private readonly FontAwesomeIcon icon;
    private readonly LocString label;
    private readonly Func<bool> isActive;
    private readonly Action onActivate;
    private readonly string detailId;

    public ToggleModule(string id, FontAwesomeIcon icon, LocString label, Func<bool> isActive, Action onActivate)
    {
        Id = id;
        this.icon = icon;
        this.label = label;
        this.isActive = isActive;
        this.onActivate = onActivate;
        detailId = "cc.detail." + id;
    }

    public string Id { get; }
    public string GalleryLabel => Loc.T(label);
    public FontAwesomeIcon GalleryIcon => icon;
    public IReadOnlyList<ControlSpan> Sizes => SpanOptions;
    public ControlSpan DefaultSpan => ControlSpan.Small;
    public bool IsActive => isActive();

    public void Activate()
    {
        var before = isActive();
        onActivate();
        var after = isActive();
        if (before != after)
        {
            UiFeedback.Play(after ? UiSound.ToggleOn : UiSound.ToggleOff);
        }
    }

    public void Draw(in ControlModuleContext context)
    {
        var expanded = context.Expanded;
        if (ControlTile.Toggle(context.DrawList, expanded ? detailId : Id, context.Rect, icon, Loc.T(label),
                isActive(), context.Theme.Accent, context.Theme, context.Opacity, context.Interactive,
                context.Span != ControlSpan.Small || expanded))
        {
            Activate();
        }
    }
}
