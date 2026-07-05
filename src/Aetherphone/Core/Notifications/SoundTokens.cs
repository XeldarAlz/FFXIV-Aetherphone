using System.Globalization;

namespace Aetherphone.Core.Notifications;

internal static class SoundTokens
{
    public const string GamePrefix = "game:";
    public const string FilePrefix = "file:";
    public const uint DefaultGameSoundId = 7;
    public static readonly string DefaultGame = Game(DefaultGameSoundId);
    public static readonly string Silent = Game(0);

    public static string Game(uint soundId) =>
        string.Concat(GamePrefix, soundId.ToString(CultureInfo.InvariantCulture));

    public static string File(string fileName) => string.Concat(FilePrefix, fileName);

    public static bool TryGame(string token, out uint soundId)
    {
        soundId = 0;
        if (string.IsNullOrEmpty(token) || !token.StartsWith(GamePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return uint.TryParse(token.AsSpan(GamePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture,
            out soundId);
    }

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
}
