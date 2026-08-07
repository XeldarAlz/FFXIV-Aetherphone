namespace Aetherphone.Core.Notifications;

internal static class SoundTokens
{
    public const string FilePrefix = "file:";
    public const string Silent = "silent";
    public const string Default = "";
    private const string LegacyGamePrefix = "game:";
    private const string LegacySilent = "game:0";

    public static string File(string fileName) => string.Concat(FilePrefix, fileName);

    public static bool IsSilent(string token) => string.Equals(token, Silent, StringComparison.Ordinal);

    public static bool TryFile(string token, out string fileName)
    {
        if (!string.IsNullOrEmpty(token) && token.StartsWith(FilePrefix, StringComparison.Ordinal))
        {
            fileName = token[FilePrefix.Length..];
            return fileName.Length > 0;
        }

        fileName = string.Empty;
        return false;
    }

    public static bool TryUpgradeLegacy(string? token, out string upgraded)
    {
        if (string.IsNullOrEmpty(token) || !token.StartsWith(LegacyGamePrefix, StringComparison.Ordinal))
        {
            upgraded = string.Empty;
            return false;
        }

        upgraded = string.Equals(token, LegacySilent, StringComparison.Ordinal) ? Silent : Default;
        return true;
    }
}
