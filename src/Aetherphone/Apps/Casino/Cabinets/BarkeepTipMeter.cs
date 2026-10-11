using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Cabinets;

internal enum BarkeepFeverChange : byte
{
    None,
    Started,
    Ended,
}

internal sealed class BarkeepTipMeter
{
    public const int FeverStreak = 5;

    private ComboMeter combo = ComboMeter.Untimed();

    public int Count => combo.Count;

    public int Multiplier => combo.Multiplier;

    public float Heat => combo.Heat;

    public int PerfectStreak { get; private set; }

    public bool Fever { get; private set; }

    public BarkeepFeverChange Grade(int grade)
    {
        if (grade >= BarkeepGrading.GoodGrade)
        {
            combo.Hit();
        }
        else
        {
            combo.Reset();
        }

        if (grade != BarkeepGrading.PerfectGrade)
        {
            PerfectStreak = 0;
            if (!Fever)
            {
                return BarkeepFeverChange.None;
            }

            Fever = false;
            return BarkeepFeverChange.Ended;
        }

        PerfectStreak++;
        if (Fever || PerfectStreak < FeverStreak)
        {
            return BarkeepFeverChange.None;
        }

        Fever = true;
        return BarkeepFeverChange.Started;
    }

    public void Update(float deltaSeconds)
    {
        combo.Update(deltaSeconds);
    }

    public void Reset()
    {
        combo.Reset();
        PerfectStreak = 0;
        Fever = false;
    }
}
