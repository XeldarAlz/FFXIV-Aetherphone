using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal readonly struct BarkeepPatronScript
{
    public readonly int ArrivalSecond;
    public readonly int[] StepKinds;

    public BarkeepPatronScript(int arrivalSecond, int[] stepKinds)
    {
        ArrivalSecond = arrivalSecond;
        StepKinds = stepKinds;
    }
}

internal sealed class BarkeepShift
{
    public const int FinishEarliestSeconds = 61;

    public const int AutoFinishSeconds = 290;

    public const int LastCallSeconds = 240;

    private readonly BarkeepPatronScript[] patrons;
    private readonly int[][] committedGrades;
    private readonly long startedAtUnixSeconds;
    private readonly string roundId;

    private int completedOrders;
    private int stepIndex;
    private int runningScore;

    public BarkeepShift(string roundId, BarkeepPatronScript[] patrons, long startedAtUnixSeconds)
    {
        this.roundId = roundId;
        this.patrons = patrons;
        this.startedAtUnixSeconds = startedAtUnixSeconds;
        committedGrades = new int[patrons.Length][];
        for (var patronIndex = 0; patronIndex < patrons.Length; patronIndex++)
        {
            committedGrades[patronIndex] = new int[patrons[patronIndex].StepKinds.Length];
        }
    }

    public static BarkeepShift? FromStart(CasinoBarkeepStartDto start)
    {
        if (!start.Granted || start.Patrons is null || start.Patrons.Length == 0
            || start.Patrons.Length > BarkeepRules.MaxPatrons || start.StartedAtUnix <= 0
            || start.RoundId.Length == 0)
        {
            return null;
        }

        var patrons = new BarkeepPatronScript[start.Patrons.Length];
        for (var patronIndex = 0; patronIndex < patrons.Length; patronIndex++)
        {
            var patron = start.Patrons[patronIndex];
            var stepKinds = patron.StepKinds;
            if (stepKinds is null || stepKinds.Length == 0)
            {
                return null;
            }

            for (var kindIndex = 0; kindIndex < stepKinds.Length; kindIndex++)
            {
                if (stepKinds[kindIndex] < 0 || stepKinds[kindIndex] >= BarkeepRules.StepKindCount)
                {
                    return null;
                }
            }

            patrons[patronIndex] = new BarkeepPatronScript(patron.ArrivalSecond, stepKinds);
        }

        return new BarkeepShift(start.RoundId, patrons, start.StartedAtUnix);
    }

    public static BarkeepPatronScript[] PracticeScript(Random random)
    {
        var patronCount = BarkeepRules.PatronBuckets[random.Next(BarkeepRules.PatronBuckets.Length)];
        var patrons = new BarkeepPatronScript[patronCount];
        for (var patronIndex = 0; patronIndex < patronCount; patronIndex++)
        {
            var arrival = patronIndex * BarkeepRules.ShiftSeconds / patronCount + random.Next(3);
            if (arrival > BarkeepRules.ShiftSeconds - 1)
            {
                arrival = BarkeepRules.ShiftSeconds - 1;
            }

            var stepCount = 2 + random.Next(3);
            var stepKinds = new int[stepCount];
            for (var kindIndex = 0; kindIndex < stepCount; kindIndex++)
            {
                stepKinds[kindIndex] = random.Next(BarkeepRules.StepKindCount);
            }

            patrons[patronIndex] = new BarkeepPatronScript(arrival, stepKinds);
        }

        return patrons;
    }

    public string RoundId => roundId;

    public long StartedAtUnixSeconds => startedAtUnixSeconds;

    public int PatronCount => patrons.Length;

    public int CompletedOrders => completedOrders;

    public bool AllServed => completedOrders == patrons.Length;

    public int Score => runningScore;

    public int MaxScore => patrons.Length * BarkeepRules.PerfectGrade;

    public int CurrentPatronIndex => completedOrders;

    public int CurrentStepIndex => stepIndex;

    public int CurrentStepCount => AllServed ? 0 : patrons[completedOrders].StepKinds.Length;

    public int CurrentStepKind => AllServed ? -1 : patrons[completedOrders].StepKinds[stepIndex];

    public int StepKindAt(int patronIndex, int step)
    {
        return patrons[patronIndex].StepKinds[step];
    }

    public int ArrivalOf(int patronIndex)
    {
        return patrons[patronIndex].ArrivalSecond;
    }

    public int CurrentOrderGrade(int step)
    {
        return AllServed ? 0 : committedGrades[completedOrders][step];
    }

    public double ElapsedSeconds(double nowUnixSeconds)
    {
        var elapsed = nowUnixSeconds - startedAtUnixSeconds;
        return elapsed > 0 ? elapsed : 0;
    }

    public bool CurrentPatronArrived(double elapsedSeconds)
    {
        return !AllServed && elapsedSeconds >= patrons[completedOrders].ArrivalSecond;
    }

    public double SecondsUntilCurrentPatron(double elapsedSeconds)
    {
        if (AllServed)
        {
            return 0;
        }

        var wait = patrons[completedOrders].ArrivalSecond - elapsedSeconds;
        return wait > 0 ? wait : 0;
    }

    public void CommitStepGrade(int grade)
    {
        if (AllServed)
        {
            return;
        }

        committedGrades[completedOrders][stepIndex] = BarkeepRules.IsValidGrade(grade)
            ? grade
            : BarkeepGrading.MissGrade;
        stepIndex++;
        if (stepIndex < patrons[completedOrders].StepKinds.Length)
        {
            return;
        }

        runningScore += BarkeepRules.OrderScore(committedGrades[completedOrders]);
        completedOrders++;
        stepIndex = 0;
    }

    public bool CanFinish(double elapsedSeconds)
    {
        return elapsedSeconds >= FinishEarliestSeconds;
    }

    public bool ShouldAutoFinish(double elapsedSeconds)
    {
        return elapsedSeconds >= AutoFinishSeconds;
    }

    public bool InLastCall(double elapsedSeconds)
    {
        return elapsedSeconds >= LastCallSeconds;
    }

    public double SecondsUntilForfeit(double elapsedSeconds)
    {
        var left = BarkeepRules.MaxFinishSeconds - elapsedSeconds;
        return left > 0 ? left : 0;
    }

    public CasinoBarkeepOrderRequest[] BuildFinishOrders()
    {
        var orders = new CasinoBarkeepOrderRequest[completedOrders];
        for (var orderIndex = 0; orderIndex < completedOrders; orderIndex++)
        {
            var grades = committedGrades[orderIndex];
            var copy = new int[grades.Length];
            Array.Copy(grades, copy, grades.Length);
            orders[orderIndex] = new CasinoBarkeepOrderRequest(copy);
        }

        return orders;
    }
}
