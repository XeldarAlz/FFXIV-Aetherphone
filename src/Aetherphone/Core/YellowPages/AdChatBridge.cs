using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;

namespace Aetherphone.Core.YellowPages;

internal readonly record struct AdChatResolution(AdDto? Ad, bool Missed);

/// <summary>Lets the shared chat transcript resolve and open Yellow Pages ad bubbles without every DM app
/// threading the store through its model. Bound once by the shell, cleared on dispose.</summary>
internal static class AdChatBridge
{
    private static YellowPagesStore? store;
    private static YellowPagesLauncher? launcher;
    private static INavigator? navigation;

    public static void Bind(YellowPagesStore adStore, YellowPagesLauncher adLauncher, INavigator navigator)
    {
        store = adStore;
        launcher = adLauncher;
        navigation = navigator;
    }

    public static void Clear()
    {
        store = null;
        launcher = null;
        navigation = null;
    }

    public static AdChatResolution Resolve(string adId)
    {
        return store?.ResolveForChat(adId) ?? new AdChatResolution(null, Missed: false);
    }

    public static void Open(string adId)
    {
        if (launcher is null || navigation is null || !navigation.IsAvailable(YellowPagesStore.AppId))
        {
            return;
        }

        launcher.RequestDetail(adId);
        navigation.Open(YellowPagesStore.AppId);
    }
}
