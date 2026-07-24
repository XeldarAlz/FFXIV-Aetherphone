namespace Aetherphone.Core;

internal static class AepConstants
{
    public const string Name = "Aetherphone";
    public const string PrimaryCommand = "/phone";
    public const string AliasCommand = "/aetherphone";
    public const string DiscordUrl = "https://discord.gg/3HbJCscMyS";
    public const string WebsiteUrl = "https://www.aetherphone.net";
    public const string PatreonUrl = "https://www.patreon.com/XeldarAlz";
    public static readonly string Version = typeof(AepConstants).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
