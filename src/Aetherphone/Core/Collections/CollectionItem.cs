using System.Text;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Collections;

internal sealed class CollectionItem
{
    public readonly CollectionCategory Category;
    public readonly int Id;
    public readonly uint ItemId;
    public readonly string Name;
    public readonly string NameLower;
    public readonly string SearchLower;
    public readonly string Description;
    public readonly string Patch;
    public readonly int PatchOrder;
    public readonly string SourceType;
    public readonly string SourceText;
    public readonly string Subtitle;
    public readonly string GroupName;
    public readonly string IconUrl;
    public readonly float Rarity;
    public readonly bool Tradeable;
    public readonly bool HasTradeable;
    public readonly bool Obtainable;
    public readonly int Points;
    public readonly int Stars;
    public readonly int Order;
    public readonly CollectionStatStrip? Stats;
    public readonly string StatsText;
    public readonly string PointsText;
    public readonly CollectionSource[] Sources;
    private string rarityText = string.Empty;
    private string rarityLanguage = string.Empty;

    public CollectionItem(CollectionCategory category, CollectionItemDto dto, int order)
    {
        Category = category;
        Order = order;
        Id = dto.Id;
        ItemId = dto.ItemId is > 0 ? (uint)dto.ItemId.Value : 0u;
        Name = dto.Name ?? string.Empty;
        NameLower = Name.ToLowerInvariant();
        Description = dto.Description ?? string.Empty;
        Patch = dto.Patch ?? string.Empty;
        PatchOrder = CollectionPatch.Order(Patch);
        IconUrl = dto.Icon ?? string.Empty;
        Rarity = CollectionRarity.Parse(dto.Owned);
        HasTradeable = dto.Tradeable.HasValue;
        Tradeable = dto.Tradeable ?? false;
        Points = dto.Points ?? 0;
        Stars = dto.Stars ?? 0;
        Stats = dto.Stats?.Numeric;
        StatsText = Stats is { } stats
            ? string.Concat(Digit(stats.Top), " · ", Digit(stats.Right), " · ", Digit(stats.Bottom), " · ",
                Digit(stats.Left))
            : string.Empty;
        PointsText = Points > 0 ? Points.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        Sources = dto.Sources ?? Array.Empty<CollectionSource>();

        var primary = Sources.Length > 0 ? Sources[0] : null;
        SourceType = primary?.Type ?? string.Empty;
        SourceText = primary?.Text ?? string.Empty;

        if (SourceType.Length == 0 && dto.Type?.Name is { Length: > 0 } typeName)
        {
            SourceType = typeName;
        }

        Subtitle = BuildSubtitle(SourceType, SourceText);
        Obtainable = CollectionSuggestions.IsObtainable(Sources);
        GroupName = dto.Category?.Name ?? dto.Type?.Name ?? string.Empty;
        SearchLower = BuildSearchKey(Name, Sources);
    }

    public bool HasRarity => Rarity >= 0f;

    public string RarityText
    {
        get
        {
            if (!HasRarity)
            {
                return string.Empty;
            }

            var code = Loc.Current.Code;
            if (!string.Equals(rarityLanguage, code, StringComparison.Ordinal))
            {
                rarityLanguage = code;
                rarityText = CollectionText.Percent(Rarity);
            }

            return rarityText;
        }
    }

    private static string Digit(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string BuildSubtitle(string type, string text)
    {
        if (type.Length > 0 && text.Length > 0)
        {
            return string.Concat(type, " · ", text);
        }

        return text.Length > 0 ? text : type;
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
