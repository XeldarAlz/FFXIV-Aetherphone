namespace Aetherphone.Core.Casino;

public sealed class RaceScriptPlan
{
    public RaceScriptPlan(int[] order, int[] finishSubTicks, bool photoFinish, int[][] speeds, long[][] covered,
        int[][] surges, int[] fadeTick)
    {
        Order = order;
        FinishSubTicks = finishSubTicks;
        PhotoFinish = photoFinish;
        Speeds = speeds;
        Covered = covered;
        Surges = surges;
        FadeTick = fadeTick;
    }

    public int[] Order { get; }

    public int[] FinishSubTicks { get; }

    public bool PhotoFinish { get; }

    public int[][] Speeds { get; }

    public long[][] Covered { get; }

    public int[][] Surges { get; }

    public int[] FadeTick { get; }
}

public static class RaceScript
{
    public const int TicksPerSecond = 25;

    public const int RaceTicks = 800;

    public const int SubTicks = 256;

    public const int TrackUnits = 1_000_000;

    public const int MinGapSubTicks = 960;

    public const int MaxGapSubTicks = 5760;

    public const int PhotoGapSubTicks = 1280;

    public const int PhotoOneIn = 4;

    private const int SeedHexDigits = 16;

    private const int BaseSpeed = 1000;

    private const int BaseSpeedSpread = 81;

    private const int MinSpeed = 250;

    private const int GateTicks = 38;

    private const int GateSpeed = 450;

    private const int MinSurges = 2;

    private const int ExtraSurges = 2;

    private const int SurgeLeadTicks = 2 * TicksPerSecond;

    private const int SurgeTailTicks = 3 * TicksPerSecond;

    private const int MinSurgeTicks = TicksPerSecond;

    private const int SurgeTickSpread = 2 * TicksPerSecond;

    private const int MinSurgeGain = 150;

    private const int SurgeGainSpread = 251;

    private const int MinFadeTicks = TicksPerSecond;

    private const int FadeTickSpread = 2 * TicksPerSecond;

    private const int MinFadeDrop = 100;

    private const int FadeDropSpread = 201;

    public static RaceScriptPlan Build(int[] order, string seedHex)
    {
        var runners = order.Length;
        var random = new ScriptRandom(SeedFrom(seedHex));
        var gaps = new int[runners > 1 ? runners - 1 : 0];
        var photoFinish = false;
        for (var gapIndex = 0; gapIndex < gaps.Length; gapIndex++)
        {
            if (gapIndex == 0)
            {
                photoFinish = random.Below(PhotoOneIn) == 0;
                gaps[gapIndex] = photoFinish
                    ? MinGapSubTicks + random.Below(PhotoGapSubTicks - MinGapSubTicks)
                    : PhotoGapSubTicks + random.Below(MaxGapSubTicks - PhotoGapSubTicks + 1);
                continue;
            }

            gaps[gapIndex] = MinGapSubTicks + random.Below(MaxGapSubTicks - MinGapSubTicks + 1);
        }

        var finishSubTicks = new int[runners];
        var lastFinish = (RaceTicks * SubTicks) - random.Below(TicksPerSecond * SubTicks);
        if (runners > 0)
        {
            finishSubTicks[order[runners - 1]] = lastFinish;
        }

        for (var place = runners - 2; place >= 0; place--)
        {
            finishSubTicks[order[place]] = finishSubTicks[order[place + 1]] - gaps[place];
        }

        var speeds = new int[runners][];
        var covered = new long[runners][];
        var surges = new int[runners][];
        var fadeTick = new int[runners];
        for (var slot = 0; slot < runners; slot++)
        {
            var finishTick = finishSubTicks[slot] / SubTicks;
            var profile = new int[finishTick + 1];
            var baseSpeed = BaseSpeed - (BaseSpeedSpread / 2) + random.Below(BaseSpeedSpread);
            for (var tick = 0; tick < profile.Length; tick++)
            {
                profile[tick] = baseSpeed;
            }

            var surgeCount = MinSurges + random.Below(ExtraSurges);
            var starts = new int[surgeCount];
            var surgeWindow = Positive(finishTick - SurgeLeadTicks - SurgeTailTicks);
            for (var surgeIndex = 0; surgeIndex < surgeCount; surgeIndex++)
            {
                var start = SurgeLeadTicks + random.Below(surgeWindow);
                var length = MinSurgeTicks + random.Below(SurgeTickSpread);
                var gain = MinSurgeGain + random.Below(SurgeGainSpread);
                starts[surgeIndex] = start;
                Shape(profile, start, length, gain);
            }

            var fadeStart = (finishTick / 2) + random.Below(Positive((finishTick / 2) - TicksPerSecond));
            var fadeLength = MinFadeTicks + random.Below(FadeTickSpread);
            var fadeDrop = MinFadeDrop + random.Below(FadeDropSpread);
            Shape(profile, fadeStart, fadeLength, -fadeDrop);
            fadeTick[slot] = fadeStart;

            var sum = new long[profile.Length + 1];
            for (var tick = 0; tick < profile.Length; tick++)
            {
                if (tick < GateTicks)
                {
                    profile[tick] = GateSpeed + ((profile[tick] - GateSpeed) * tick / GateTicks);
                }

                if (profile[tick] < MinSpeed)
                {
                    profile[tick] = MinSpeed;
                }

                sum[tick + 1] = sum[tick] + profile[tick];
            }

            SortAscending(starts);
            speeds[slot] = profile;
            covered[slot] = sum;
            surges[slot] = starts;
        }

        return new RaceScriptPlan(order, finishSubTicks, photoFinish, speeds, covered, surges, fadeTick);
    }

    public static int PositionAt(RaceScriptPlan plan, int slot, long subTick)
    {
        var finish = plan.FinishSubTicks[slot];
        if (subTick >= finish)
        {
            return TrackUnits;
        }

        if (subTick <= 0)
        {
            return 0;
        }

        var total = CoveredAt(plan, slot, finish);
        if (total <= 0)
        {
            return TrackUnits;
        }

        return (int)(TrackUnits * CoveredAt(plan, slot, subTick) / total);
    }

    public static int PositionAtTick(RaceScriptPlan plan, int slot, int tick)
    {
        return PositionAt(plan, slot, (long)tick * SubTicks);
    }

    public static int LeaderAt(RaceScriptPlan plan, long subTick)
    {
        var leader = -1;
        var best = -1;
        for (var slot = 0; slot < plan.FinishSubTicks.Length; slot++)
        {
            var position = PositionAt(plan, slot, subTick);
            if (position > best || (position == best && plan.FinishSubTicks[slot] < plan.FinishSubTicks[leader]))
            {
                best = position;
                leader = slot;
            }
        }

        return leader;
    }

    private static long CoveredAt(RaceScriptPlan plan, int slot, long subTick)
    {
        var profile = plan.Speeds[slot];
        var tick = (int)(subTick / SubTicks);
        var fraction = subTick % SubTicks;
        if (tick >= profile.Length)
        {
            tick = profile.Length - 1;
            fraction = SubTicks;
        }

        return (plan.Covered[slot][tick] * SubTicks) + (profile[tick] * fraction);
    }

    private static void Shape(int[] profile, int start, int length, int delta)
    {
        var end = start + length;
        if (end > profile.Length)
        {
            end = profile.Length;
        }

        for (var tick = start; tick < end; tick++)
        {
            profile[tick] += delta;
        }
    }

    private static void SortAscending(int[] values)
    {
        for (var outer = 1; outer < values.Length; outer++)
        {
            var value = values[outer];
            var inner = outer - 1;
            while (inner >= 0 && values[inner] > value)
            {
                values[inner + 1] = values[inner];
                inner--;
            }

            values[inner + 1] = value;
        }
    }

    private static int Positive(int value)
    {
        return value < 1 ? 1 : value;
    }

    private static ulong SeedFrom(string seedHex)
    {
        var seed = 0UL;
        var digits = seedHex.Length < SeedHexDigits ? seedHex.Length : SeedHexDigits;
        for (var index = 0; index < digits; index++)
        {
            seed = (seed << 4) | NibbleOf(seedHex[index]);
        }

        return seed;
    }

    private static ulong NibbleOf(char digit)
    {
        if (digit >= '0' && digit <= '9')
        {
            return (ulong)(digit - '0');
        }

        if (digit >= 'a' && digit <= 'f')
        {
            return (ulong)(digit - 'a' + 10);
        }

        if (digit >= 'A' && digit <= 'F')
        {
            return (ulong)(digit - 'A' + 10);
        }

        return 0;
    }

    private struct ScriptRandom
    {
        private const ulong Increment = 0x9E3779B97F4A7C15UL;

        private const ulong FirstMix = 0xBF58476D1CE4E5B9UL;

        private const ulong SecondMix = 0x94D049BB133111EBUL;

        private ulong state;

        public ScriptRandom(ulong seed)
        {
            state = seed;
        }

        public int Below(int bound)
        {
            if (bound <= 1)
            {
                return 0;
            }

            return (int)(Next() % (ulong)bound);
        }

        private ulong Next()
        {
            unchecked
            {
                state += Increment;
                var mixed = state;
                mixed = (mixed ^ (mixed >> 30)) * FirstMix;
                mixed = (mixed ^ (mixed >> 27)) * SecondMix;
                return mixed ^ (mixed >> 31);
            }
        }
    }
}
