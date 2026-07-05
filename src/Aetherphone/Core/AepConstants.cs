namespace Aetherphone.Core;

internal static class AepConstants
{
    public const string Name = "Aetherphone";
    public const string PrimaryCommand = "/phone";
    public const string AliasCommand = "/aetherphone";
    public static readonly string Version = typeof(AepConstants).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
