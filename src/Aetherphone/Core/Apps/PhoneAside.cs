namespace Aetherphone.Core.Apps;

internal static class PhoneAside
{
    private const long MaxHoldMilliseconds = 15_000;

    private static string holder = string.Empty;
    private static long requestedAt;

    public static bool Requested => holder.Length > 0 && Environment.TickCount64 - requestedAt < MaxHoldMilliseconds;

    public static void Request(string appId)
    {
        holder = appId;
        requestedAt = Environment.TickCount64;
    }

    public static void Release(string appId)
    {
        if (string.Equals(holder, appId, StringComparison.Ordinal))
        {
            holder = string.Empty;
        }
    }
}
