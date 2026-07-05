using System.Text;

namespace Aetherphone.Core.Collections;

internal sealed class CollectionItem
{
    public readonly int Id;
    public readonly string Name;
    public readonly string NameLower;
    public readonly string SearchLower;
    public readonly string Description;
    public readonly string Patch;
    public readonly string SourceType;
    public readonly string SourceText;
    public readonly string GroupName;
    public readonly string IconUrl;
    public readonly string Community;
    public readonly bool Tradeable;
    public readonly bool HasTradeable;
    public readonly int Points;
    public readonly int Stars;
    public readonly CollectionStatStrip? Stats;
    public readonly CollectionSource[] Sources;

    public CollectionItem(CollectionItemDto dto)
    {
        Id = dto.Id;
        Name = dto.Name ?? string.Empty;
        NameLower = Name.ToLowerInvariant();
        Description = dto.Description ?? string.Empty;
        Patch = dto.Patch ?? string.Empty;
        IconUrl = dto.Icon ?? string.Empty;
        Community = dto.Owned ?? string.Empty;
        HasTradeable = dto.Tradeable.HasValue;
        Tradeable = dto.Tradeable ?? false;
        Points = dto.Points ?? 0;
        Stars = dto.Stars ?? 0;
        Stats = dto.Stats?.Numeric;
        Sources = dto.Sources ?? Array.Empty<CollectionSource>();

        var primary = Sources.Length > 0 ? Sources[0] : null;
        SourceType = primary?.Type ?? string.Empty;
        SourceText = primary?.Text ?? string.Empty;

        if (SourceType.Length == 0 && dto.Type?.Name is { Length: > 0 } typeName)
        {
            SourceType = typeName;
        }

        GroupName = dto.Category?.Name ?? dto.Type?.Name ?? string.Empty;
        SearchLower = BuildSearchKey(Name, Sources);
    }

    private static string BuildSearchKey(string name, CollectionSource[] sources)
    {
        var builder = new StringBuilder(name);

        for (var index = 0; index < sources.Length; index++)
        {
            var text = sources[index].Text;
            if (!string.IsNullOrEmpty(text))
            {
                builder.Append(' ').Append(text);
            }
        }

        return builder.ToString().ToLowerInvariant();
    }
}
