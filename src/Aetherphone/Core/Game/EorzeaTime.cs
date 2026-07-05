namespace Aetherphone.Core.Game;

// Eorzea time runs 144/7x real time: a 24h Eorzean day elapses in 70 real minutes.
internal readonly record struct EorzeaTime(int Hour, int Minute)
{
    public static EorzeaTime Now()
    {
        var seconds = CurrentSeconds();
        return new EorzeaTime((int)(seconds / 3600 % 24), (int)(seconds / 60 % 60));
    }

    public static long CurrentSeconds() => DateTimeOffset.UtcNow.ToUnixTimeSeconds() * 144 / 7;
    public string Formatted => $"{Hour:D2}:{Minute:D2}";
}
