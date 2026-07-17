namespace Aetherphone.Core.Apps;

internal enum SocialLinkKind
{
    Profile,
    Post,
}

internal readonly record struct SocialDeepLink(SocialLinkKind Kind, string Id);

internal sealed class SocialLauncher
{
    private readonly Dictionary<string, SocialDeepLink> pending = new();

    public void Request(string appId, SocialDeepLink link)
    {
        pending[appId] = link;
    }

    public bool TryConsume(string appId, out SocialDeepLink link)
    {
        return pending.Remove(appId, out link);
    }
}
