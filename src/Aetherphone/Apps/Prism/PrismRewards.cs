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
    public readonly long ChineseCoins;
    public readonly BadgeStyle? Badge;
    public readonly FrameStyle? Frame;
    public readonly int FrameDays;

    public PrismPrizeRow(int rank, long coins, long gil = 0, BadgeStyle? badge = null, FrameStyle? frame = null,
        int frameDays = 0, long chineseCoins = 0)
    {
        Rank = rank;
        Coins = coins;
        Gil = gil;
        Badge = badge;
        Frame = frame;
        FrameDays = frameDays;
        ChineseCoins = chineseCoins;
    }

    public long CoinsFor(bool chinese) => chinese ? Coins + ChineseCoins : Coins;

    public long GilFor(bool chinese) => chinese ? 0 : Gil;
}

internal static class PrismRewards
{
    private static readonly Comparison<PrismPrizeRow> ByRank = (left, right) => left.Rank.CompareTo(right.Rank);

    public static bool Any(PrismPrizesDto? prizes, PrismJudgeRewardDto? judging)
    {
        if (judging is { Amount: > 0 })
        {
            return true;
        }

        if (prizes is null)
        {
            return false;
        }

        var placings = prizes.Placings ?? Array.Empty<PrismPrizeDto>();
        for (var index = 0; index < placings.Length; index++)
        {
            var placing = placings[index];
            if (placing.Badge is not null || placing.Frame is not null || placing.Coins > 0 || placing.Gil > 0 ||
                placing.ChineseCoins > 0)
            {
                return true;
            }
        }

        return AnyPositive(prizes.Heat) || AnyPositive(prizes.Final);
    }

    public static bool Same(PrismPrizesDto? left, PrismPrizesDto? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || !SameAmounts(left.Heat, right.Heat) ||
            !SameAmounts(left.Final, right.Final))
        {
            return false;
        }

        var leftPlacings = left.Placings ?? Array.Empty<PrismPrizeDto>();
        var rightPlacings = right.Placings ?? Array.Empty<PrismPrizeDto>();
        if (leftPlacings.Length != rightPlacings.Length)
        {
            return false;
        }

        for (var index = 0; index < leftPlacings.Length; index++)
        {
            var first = leftPlacings[index];
            var second = rightPlacings[index];
            if (first.Stage != second.Stage || first.Rank != second.Rank || first.Coins != second.Coins ||
                first.Gil != second.Gil || first.ChineseCoins != second.ChineseCoins ||
                first.FrameDays != second.FrameDays || first.Badge?.Id != second.Badge?.Id ||
                first.Frame?.Id != second.Frame?.Id)
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameAmounts(long[]? left, long[]? right)
    {
        var first = left ?? Array.Empty<long>();
        var second = right ?? Array.Empty<long>();
        if (first.Length != second.Length)
        {
            return false;
        }

        for (var index = 0; index < first.Length; index++)
        {
            if (first[index] != second[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool AnyPositive(long[]? amounts)
    {
        if (amounts is null)
        {
            return false;
        }

        for (var index = 0; index < amounts.Length; index++)
        {
            if (amounts[index] > 0)
            {
                return true;
            }
        }

        return false;
    }

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
                placing.Frame is null ? null : FrameStyle.From(placing.Frame), Math.Max(0, placing.FrameDays),
                Math.Max(0, placing.ChineseCoins)));
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
}
