using Aetherphone.Core;
using Aetherphone.Core.Games;
using Aetherphone.Core.Theme;

namespace Aetherphone.Apps.Games.Framework;

internal readonly struct GameContext
{
    public readonly Rect Body;

    public readonly PhoneTheme Theme;

    public readonly GameStatsStore Stats;

    public readonly float DeltaSeconds;

    public GameContext(Rect body, PhoneTheme theme, GameStatsStore stats, float deltaSeconds)
    {
        Body = body;
        Theme = theme;
        Stats = stats;
        DeltaSeconds = deltaSeconds;
    }
}
