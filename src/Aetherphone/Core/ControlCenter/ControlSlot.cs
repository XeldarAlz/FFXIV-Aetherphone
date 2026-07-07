using Aetherphone.Core.Home;

namespace Aetherphone.Core.ControlCenter;

internal sealed class ControlSlot : IGridTile
{
    public required IControlModule Module { get; init; }
    public ControlSpan Span { get; set; }

    public string Id => Module.Id;
    public int ColumnSpan => ControlSpans.ColumnSpan(Span);
    public int RowSpan => ControlSpans.RowSpan(Span);

    public static ControlSlot For(IControlModule module, ControlSpan span) => new() { Module = module, Span = span };
}
