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

internal enum PrismRewardKind : byte
{
    Badge,
    Frame,
    Coin,
    Gil,
}

internal sealed class PrismPrizeRow
{
    public readonly int Rank;
    public readonly long Coins;
    public readonly long Gil;
    public readonly BadgeStyle? Badge;
    public readonly FrameStyle? Frame;
    public readonly int FrameDays;

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
    public const int KindCount = 4;

    private static readonly Comparison<PrismPrizeRow> ByRank = (left, right) => left.Rank.CompareTo(right.Rank);

    public static int Offered(PrismPrizesDto? prizes, PrismJudgeRewardDto? judging, PrismRewardKind[] into)
    {
        var badge = false;
        var frame = false;
        var coins = judging is { Amount: > 0 };
        var gil = false;
        if (prizes is not null)
        {
            var placings = prizes.Placings ?? Array.Empty<PrismPrizeDto>();
            for (var index = 0; index < placings.Length; index++)
            {
                var placing = placings[index];
                badge |= placing.Badge is not null;
                frame |= placing.Frame is not null;
                coins |= placing.Coins > 0;
                gil |= placing.Gil > 0;
            }

            coins |= AnyPositive(prizes.Heat) || AnyPositive(prizes.Final);
        }

        var count = 0;
        if (badge)
        {
            into[count++] = PrismRewardKind.Badge;
        }

        if (frame)
        {
            into[count++] = PrismRewardKind.Frame;
        }

        if (coins)
        {
            into[count++] = PrismRewardKind.Coin;
        }

        if (gil)
        {
            into[count++] = PrismRewardKind.Gil;
        }

        return count;
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
}
