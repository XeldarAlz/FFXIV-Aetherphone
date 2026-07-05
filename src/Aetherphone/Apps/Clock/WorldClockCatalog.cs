namespace Aetherphone.Apps.Clock;

internal readonly record struct WorldCity(string City, string TimeZoneId);

internal static class WorldClockCatalog
{
    public static readonly IReadOnlyList<WorldCity> All = new WorldCity[]
    {
        new("Honolulu", "Hawaiian Standard Time"),
        new("Anchorage", "Alaskan Standard Time"),
        new("Los Angeles", "Pacific Standard Time"),
        new("Denver", "Mountain Standard Time"),
        new("Chicago", "Central Standard Time"),
        new("New York", "Eastern Standard Time"),
        new("Sao Paulo", "E. South America Standard Time"),
        new("London", "GMT Standard Time"),
        new("Paris", "Romance Standard Time"),
        new("Berlin", "W. Europe Standard Time"),
        new("Cairo", "Egypt Standard Time"),
        new("Moscow", "Russian Standard Time"),
        new("Dubai", "Arabian Standard Time"),
        new("Mumbai", "India Standard Time"),
        new("Bangkok", "SE Asia Standard Time"),
        new("Singapore", "Singapore Standard Time"),
        new("Hong Kong", "China Standard Time"),
        new("Tokyo", "Tokyo Standard Time"),
        new("Sydney", "AUS Eastern Standard Time"),
        new("Auckland", "New Zealand Standard Time"),
    };

    public static bool TryResolve(string timeZoneId, out TimeZoneInfo zone)
    {
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (Exception)
        {
            zone = TimeZoneInfo.Utc;
            return false;
        }
    }
}
