namespace Aetherphone.Core.Telephony;

internal static class CallFormat
{
    public static string Duration(int seconds)
    {
        if (seconds < 0)
        {
            seconds = 0;
        }

        var minutes = seconds / 60;
        var remainder = seconds % 60;
        if (minutes >= 60)
        {
            var hours = minutes / 60;
            minutes %= 60;
            return $"{hours}:{minutes:00}:{remainder:00}";
        }

        return $"{minutes}:{remainder:00}";
    }
}
