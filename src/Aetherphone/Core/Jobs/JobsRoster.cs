namespace Aetherphone.Core.Jobs;

internal static class JobsRoster
{
    public const uint WarCategoryId = 30;
    public const uint MagicCategoryId = 31;
    public const uint LandCategoryId = 32;
    public const uint HandCategoryId = 33;
    public const int LimitedJobCap = 80;

    public static int BucketFor(byte jobType, byte role, uint classJobCategoryId)
    {
        if (classJobCategoryId == HandCategoryId)
        {
            return (int)JobRole.Hand;
        }

        if (classJobCategoryId == LandCategoryId)
        {
            return (int)JobRole.Land;
        }

        if (classJobCategoryId != WarCategoryId && classJobCategoryId != MagicCategoryId)
        {
            return -1;
        }

        return jobType switch
        {
            1 => (int)JobRole.Tank,
            2 or 6 => (int)JobRole.Healer,
            3 => (int)JobRole.Melee,
            4 => (int)JobRole.PhysicalRanged,
            5 => (int)JobRole.MagicalRanged,
            _ => role switch
            {
                1 => (int)JobRole.Tank,
                2 => (int)JobRole.Melee,
                3 => classJobCategoryId == WarCategoryId ? (int)JobRole.PhysicalRanged : (int)JobRole.MagicalRanged,
                4 => (int)JobRole.Healer,
                _ => -1,
            },
        };
    }

    public static int BucketFor(in ClassJobFacts facts) => BucketFor(facts.JobType, facts.Role, facts.CategoryId);

    public static bool HasParent(in ClassJobFacts facts) => facts.ParentId != 0 && facts.ParentId != facts.Id;

    public static bool IsBaseClass(ReadOnlySpan<ClassJobFacts> facts, int index)
    {
        var id = facts[index].Id;
        for (var other = 0; other < facts.Length; other++)
        {
            if (other != index && facts[other].ParentId == id && facts[other].Id != id)
            {
                return true;
            }
        }

        return false;
    }

    public static int Compose(ReadOnlySpan<ClassJobFacts> facts, ReadOnlySpan<bool> unlocked, Span<JobSlot> output)
    {
        var count = 0;
        for (var index = 0; index < facts.Length && count < output.Length; index++)
        {
            var row = facts[index];
            var bucket = BucketFor(row);
            if (row.ExpArrayIndex < 0 || bucket < 0)
            {
                continue;
            }

            if (IsBaseClass(facts, index))
            {
                if (AnyChildUnlocked(facts, unlocked, row.Id))
                {
                    continue;
                }

                output[count] = new JobSlot(index, (JobRole)bucket, !unlocked[index]);
                count++;
                continue;
            }

            if (HasParent(row) && !unlocked[index])
            {
                if (!AnyChildUnlocked(facts, unlocked, row.ParentId))
                {
                    continue;
                }

                output[count] = new JobSlot(index, (JobRole)bucket, true);
                count++;
                continue;
            }

            output[count] = new JobSlot(index, (JobRole)bucket, !unlocked[index]);
            count++;
        }

        Sort(facts, output[..count]);
        return count;
    }

    private static bool AnyChildUnlocked(ReadOnlySpan<ClassJobFacts> facts, ReadOnlySpan<bool> unlocked, uint parentId)
    {
        for (var index = 0; index < facts.Length; index++)
        {
            if (facts[index].ParentId == parentId && facts[index].Id != parentId && unlocked[index])
            {
                return true;
            }
        }

        return false;
    }

    private static void Sort(ReadOnlySpan<ClassJobFacts> facts, Span<JobSlot> slots)
    {
        for (var outer = 1; outer < slots.Length; outer++)
        {
            var moving = slots[outer];
            var position = outer - 1;
            while (position >= 0 && Compare(facts, slots[position], moving) > 0)
            {
                slots[position + 1] = slots[position];
                position--;
            }

            slots[position + 1] = moving;
        }
    }

    private static int Compare(ReadOnlySpan<ClassJobFacts> facts, JobSlot left, JobSlot right)
    {
        if (left.Role != right.Role)
        {
            return left.Role.CompareTo(right.Role);
        }

        var leftFacts = facts[left.FactIndex];
        var rightFacts = facts[right.FactIndex];
        return leftFacts.UiPriority != rightFacts.UiPriority
            ? leftFacts.UiPriority.CompareTo(rightFacts.UiPriority)
            : leftFacts.Id.CompareTo(rightFacts.Id);
    }

    public static int CapFor(bool isLimited, int maxLevel) =>
        isLimited ? Math.Min(LimitedJobCap, Math.Max(maxLevel, 1)) : Math.Max(maxLevel, 1);

    public static float Fraction(long experience, long needed, bool capped)
    {
        if (capped)
        {
            return 1f;
        }

        return needed > 0 ? Math.Clamp(experience / (float)needed, 0f, 1f) : 0f;
    }

    public static long RestedShare(long rested, long experience, long needed, bool capped)
    {
        if (capped || needed <= 0 || rested <= 0)
        {
            return 0;
        }

        return Math.Clamp(needed - experience, 0, rested);
    }

    public static JobsSummary Summarize(ReadOnlySpan<int> levels, ReadOnlySpan<int> caps)
    {
        var capped = 0;
        var earned = 0;
        var possible = 0;
        for (var index = 0; index < levels.Length; index++)
        {
            var cap = caps[index];
            var level = Math.Clamp(levels[index], 0, cap);
            earned += level;
            possible += cap;
            if (cap > 0 && level >= cap)
            {
                capped++;
            }
        }

        return new JobsSummary(capped, levels.Length, earned, possible);
    }

    public static int OrderedByIds(ReadOnlySpan<int> available, IReadOnlyList<int> order, Span<int> output)
    {
        var count = 0;
        for (var orderIndex = 0; orderIndex < order.Count && count < output.Length; orderIndex++)
        {
            var wanted = order[orderIndex];
            for (var index = 0; index < available.Length; index++)
            {
                if (available[index] != wanted)
                {
                    continue;
                }

                if (!Contains(output[..count], wanted))
                {
                    output[count] = wanted;
                    count++;
                }

                break;
            }
        }

        return count;
    }

    private static bool Contains(ReadOnlySpan<int> values, int value)
    {
        for (var index = 0; index < values.Length; index++)
        {
            if (values[index] == value)
            {
                return true;
            }
        }

        return false;
    }
}
