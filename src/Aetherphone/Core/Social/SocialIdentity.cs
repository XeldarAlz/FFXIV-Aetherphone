namespace Aetherphone.Core.Social;

internal static class SocialIdentity
{
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
