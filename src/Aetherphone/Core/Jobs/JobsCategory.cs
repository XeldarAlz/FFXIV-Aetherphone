namespace Aetherphone.Core.Jobs;

[Serializable]
internal sealed class JobsCategory
{
    public string Name { get; set; } = string.Empty;
    public List<int> GearsetIds { get; set; } = new();
}
