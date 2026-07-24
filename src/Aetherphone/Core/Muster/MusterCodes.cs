namespace Aetherphone.Core.Muster;

internal static class MusterStatuses
{
    public const int None = 0;
    public const int OnMyWay = 1;
    public const int RunningLate = 2;
    public const int Here = 3;
    public const int WhereExactly = 4;
}

internal static class MusterNotices
{
    public const int None = 0;
    public const int StartingNow = 1;
    public const int MovedSpots = 2;
    public const int WrappingUp = 3;
}

internal static class MusterScopes
{
    public const int MyDataCenter = 0;
    public const int Region = 1;
    public const int Everywhere = 2;
}
