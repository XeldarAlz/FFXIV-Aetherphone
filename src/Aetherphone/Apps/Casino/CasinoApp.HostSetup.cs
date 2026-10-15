using Aetherphone.Core;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private Action? openHostRules;

    private void DrawHostRules(Rect body)
    {
        if (hostSheet.DrawRules(body, ui))
        {
            PopRoute();
        }
    }

    private void PushHostSheet(string gameId)
    {
        hostSheet.OpenRules = openHostRules ??= OpenHostRules;
        routes.Push(new CasinoRoute(CasinoScreen.HostTable, gameId));
    }

    private void OpenHostRules()
    {
        if (router.Current.Screen != CasinoScreen.HostTable)
        {
            return;
        }

        routes.Push(new CasinoRoute(CasinoScreen.HostRules, router.Current.GameId));
    }
}
