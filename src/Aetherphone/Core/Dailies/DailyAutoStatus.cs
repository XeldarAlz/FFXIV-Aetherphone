namespace Aetherphone.Core.Dailies;

internal readonly record struct DailyAutoStatus(bool Available, bool Complete, int Remaining, int Goal)
{
    public static readonly DailyAutoStatus Unavailable = new(false, false, 0, 0);
}
