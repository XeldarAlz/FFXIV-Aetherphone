using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Venues;

internal sealed partial class VenuesApp
{
    private readonly GeoScopeScreen scopeScreen;

    private void DrawScopeScreen(Rect area)
    {
        CheckLanguage();
        var choice = scopeScreen.Draw(area, Loc.T(L.Venues.ScopeTitle), back, CurrentWorld(),
            configuration.VenueScope, configuration.VenueScopeValue, true);
        if (!choice.Picked)
        {
            return;
        }

        SetScope(choice.Kind, choice.Value);
        router.Pop();
    }
}
