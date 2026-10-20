using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Widgets;

namespace Aetherphone.Apps.Prism;

internal enum PrismRewardLine : byte
{
    Hidden,
    Each,
    Left,
    Spent,
}

internal sealed class PrismPrizeRow
{
    public readonly int Rank;
    public readonly long Coins;
    public readonly long Gil;
    public readonly BadgeStyle? Badge;
    public readonly FrameStyle? Frame;
    public readonly int FrameDays;
    public CachedText CoinText;
    public CachedText GilText;
    public CachedText DaysText;

    public PrismPrizeRow(int rank, long coins, long gil = 0, BadgeStyle? badge = null, FrameStyle? frame = null,
        int frameDays = 0)
    {
        Rank = rank;
        Coins = coins;
        Gil = gil;
        Badge = badge;
        Frame = frame;
        FrameDays = frameDays;
    }
}

internal static class PrismRewards
{
    private static readonly Comparison<PrismPrizeRow> ByRank = (left, right) => left.Rank.CompareTo(right.Rank);

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

    public static bool HasPrizes(PrismPrizesDto? prizes) =>
        prizes is not null && (prizes.Placings is { Length: > 0 } || AnyCoins(prizes.Final) || AnyCoins(prizes.Heat));

    public static void Fill(List<PrismPrizeRow> into, PrismPrizesDto? prizes, string stage)
    {
        into.Clear();
        if (prizes is null)
        {
            return;
        }

        var placings = prizes.Placings ?? Array.Empty<PrismPrizeDto>();
        for (var index = 0; index < placings.Length; index++)
        {
            var placing = placings[index];
            if (placing.Stage != stage || placing.Rank <= 0)
            {
                continue;
            }

            into.Add(new PrismPrizeRow(placing.Rank, Math.Max(0, placing.Coins), Math.Max(0, placing.Gil),
                placing.Badge is null ? null : BadgeStyle.From(placing.Badge),
                placing.Frame is null ? null : FrameStyle.From(placing.Frame), Math.Max(0, placing.FrameDays)));
        }

        if (into.Count > 0)
        {
            into.Sort(ByRank);
            return;
        }

        var coins = (stage == PrismPhases.StageFinal ? prizes.Final : prizes.Heat) ?? Array.Empty<long>();
        for (var index = 0; index < coins.Length; index++)
        {
            if (coins[index] > 0)
            {
                into.Add(new PrismPrizeRow(index + 1, coins[index]));
            }
        }
    }

    private static bool AnyCoins(long[]? coins)
    {
        if (coins is null)
        {
            return false;
        }

        for (var index = 0; index < coins.Length; index++)
        {
            if (coins[index] > 0)
            {
                return true;
            }
        }

        return false;
    }
}
