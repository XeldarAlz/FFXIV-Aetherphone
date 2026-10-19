using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Apps.Prism;

internal enum PrismRewardLine : byte
{
    Hidden,
    Each,
    Left,
    Spent,
}

internal static class PrismRewards
{
    public static PrismRewardLine LineFor(PrismJudgeRewardDto? reward)
    {
        if (reward is not { Amount: > 0 })
        {
            return PrismRewardLine.Hidden;
        }

        if (reward.LeftThisWeek < 0)
        {
            return PrismRewardLine.Each;
        }

        return reward.LeftThisWeek == 0 ? PrismRewardLine.Spent : PrismRewardLine.Left;
    }
}
