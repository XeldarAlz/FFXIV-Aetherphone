namespace Aetherphone.Core;

internal static class AepConstants
{
    public const string Name = "Aetherphone";
    public const string PrimaryCommand = "/phone";
    public const string AliasCommand = "/aetherphone";
    public const string DiscordUrl = "https://discord.gg/fgE8QFj7Y";
    public const string WebsiteUrl = "https://www.aetherphone.net";
    public static readonly string Version = typeof(AepConstants).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
