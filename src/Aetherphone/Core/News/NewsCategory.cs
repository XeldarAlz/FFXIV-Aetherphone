namespace Aetherphone.Core.News;

internal enum NewsCategory : byte
{
    Topics,
    Notices,
    Maintenance,
    Updates
}

internal static class NewsCategories
{
    public static readonly NewsCategory[] All =
    {
        NewsCategory.Topics,
        NewsCategory.Notices,
        NewsCategory.Maintenance,
        NewsCategory.Updates
    };

    public static string Path(NewsCategory category)
    {
        return category switch
        {
            NewsCategory.Notices => "notices",
            NewsCategory.Maintenance => "maintenance",
            NewsCategory.Updates => "updates",
            _ => "topics"
        };
    }
}
