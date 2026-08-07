using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;

namespace Aetherphone.Core.Muster;

internal readonly record struct MusterChatResolution(MusterDto? Muster, bool Missed);

internal static class MusterChatBridge
{
    private static MusterStore? store;
    private static MusterLauncher? launcher;
    private static INavigator? navigation;

    public static void Bind(MusterStore musterStore, MusterLauncher musterLauncher, INavigator navigator)
    {
        store = musterStore;
        launcher = musterLauncher;
        navigation = navigator;
    }

    public static void Clear()
    {
        store = null;
        launcher = null;
        navigation = null;
    }

    public static MusterChatResolution Resolve(string musterId)
    {
        return store?.ResolveForChat(musterId) ?? new MusterChatResolution(null, Missed: false);
    }

    public static void Open(string musterId)
    {
        if (launcher is null || navigation is null || !navigation.IsAvailable(MusterStore.AppId))
        {
            return;
        }

        launcher.RequestDetail(musterId);
        navigation.Open(MusterStore.AppId);
    }
}
