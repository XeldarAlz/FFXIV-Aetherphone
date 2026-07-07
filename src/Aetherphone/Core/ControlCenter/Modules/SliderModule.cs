using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Core.ControlCenter.Modules;

internal sealed class SliderModule : IControlModule
{
    private static readonly ControlSpan[] SpanOptions = { ControlSpan.Tall };

    private readonly string label;
    private readonly Func<FontAwesomeIcon> icon;
    private readonly Func<float> read;
    private readonly Action<float> write;
    private readonly Action onReleased;

    public SliderModule(string id, string label, Func<FontAwesomeIcon> icon, Func<float> read, Action<float> write,
        Action onReleased)
    {
        Id = id;
        this.label = label;
        this.icon = icon;
        this.read = read;
        this.write = write;
        this.onReleased = onReleased;
    }

    public string Id { get; }
    public string GalleryLabel => label;
    public FontAwesomeIcon GalleryIcon => icon();
    public IReadOnlyList<ControlSpan> Sizes => SpanOptions;
    public ControlSpan DefaultSpan => ControlSpan.Tall;

    public void Draw(in ControlModuleContext context)
    {
        var current = read();
        var next = ControlTile.VerticalSlider(context.DrawList, context.Rect, current, icon(), label, context.Theme,
            context.Opacity, context.Interactive, out var released);
        if (MathF.Abs(next - current) > 0.0005f)
        {
            write(next);
        }

        if (released)
        {
            onReleased();
        }
    }
}
