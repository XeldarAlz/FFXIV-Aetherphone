using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Games.Framework;

internal static class StarTotal
{
    public static int Max(int levelCount) => Math.Max(0, levelCount) * GameStatsStore.MaxStars;

    public static string Label(int stars, int levelCount) =>
        Loc.T(L.Stage.StarsTotal, GameNumber.Label(stars), GameNumber.Label(Max(levelCount)));
}
