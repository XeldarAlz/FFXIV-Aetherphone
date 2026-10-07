using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp
{
    private bool LaunchActive => false;

    private bool BeginLaunch(IMiniGame game, Rect localSource) => false;

    private bool DrawLaunchLayer(Rect area) => false;

    private bool TryDismissLaunch() => false;

    private void ResetLaunch()
    {
    }
}
