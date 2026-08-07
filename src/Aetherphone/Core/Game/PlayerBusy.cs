using Dalamud.Game.ClientState.Conditions;

namespace Aetherphone.Core.Game;

internal static class PlayerBusy
{
    public static bool Now
    {
        get
        {
            var condition = Plugin.Condition;
            return condition[ConditionFlag.InCombat] || condition[ConditionFlag.BoundByDuty] ||
                   condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.WatchingCutscene] ||
                   condition[ConditionFlag.OccupiedInCutSceneEvent] || condition[ConditionFlag.BetweenAreas];
        }
    }
}
