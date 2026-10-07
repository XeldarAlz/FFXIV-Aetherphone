using Aetherphone.Core.Apps;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp
{
    private void DrawProfile(in PhoneContext context) => DrawRecords(context);

    private void DrawBests(in PhoneContext context) => DrawRecords(context);

    private void ResetProfile()
    {
    }
}
