namespace Aetherphone.Core.Social;

internal static class SocialIdentity
{
    public const int HandleMinLength = 3;

    public const int HandleMaxLength = 15;

    public static bool IsHandleChar(char character) =>
        character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_';

    public static bool IsHandleValid(string handle)
    {
        var value = handle.Trim();
        if (value.Length < HandleMinLength || value.Length > HandleMaxLength)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
            {
                return false;
            }
        }

        return true;
    }

    public static string Name(string displayName, string handle) =>
        displayName.Length > 0 ? displayName : handle;

    public static string ProfileMeta(string handle, string regionCode)
    {
        if (handle.Length == 0)
        {
            return regionCode;
        }

        return regionCode.Length > 0 ? $"@{handle} · {regionCode}" : $"@{handle}";
    }

    public static string FeedMeta(string handle, string time) =>
        handle.Length > 0 ? $"@{handle} · {time}" : time;
}
