namespace Aetherphone.Core.ControlCenter;

[Serializable]
internal sealed class ControlLayout
{
    public List<ControlItem> Items { get; set; } = new();
}

[Serializable]
internal sealed class ControlItem
{
    public string ModuleId { get; set; } = string.Empty;
    public string Span { get; set; } = string.Empty;
}
