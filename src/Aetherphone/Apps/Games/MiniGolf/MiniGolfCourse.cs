namespace Aetherphone.Apps.Games.MiniGolf;

internal static class MiniGolfCourse
{
    public const int HoleCount = 18;
    public const int FrontNine = 9;

    public static readonly string[] Sources =
    {
        "par 2; course 1.2 1 4.8 1 4.8 11 1.2 11; tee 3 10; cup 3 2.4",
        "par 2; course 1.2 1 4.8 1 4.8 11 1.2 11; post 3 6 0.45; tee 3 10; cup 3 2",
        "par 3; course 0.6 11 2.6 11 2.6 4.2 5.4 4.2 5.4 1 0.6 1; tee 1.6 10; cup 4.4 2.6",
        "par 2; course 1 1 5 1 5 11 1 11; sand 1 4.6 2.6 2; sand 3.9 2.8 0.55; tee 3.2 10; cup 3.6 2",
        "par 3; course 1 1 5 1 5 11 1 11; slope 1 3.6 4 3 1.6 0; tee 3 10; cup 1.8 2",
        "par 3; course 1 1 5 1 5 11 1 11; water 1 5 2.7 1.3; tee 2 10; cup 2 2",
        "par 3; course 0.8 11 5.2 11 5.2 7.2 3.8 6.2 3.8 4.8 5.2 3.8 5.2 1 0.8 1 0.8 3.8 2.2 4.8 2.2 6.2 0.8 7.2; mill 3 5.5 0.72 1.6; tee 3 10; cup 3 2",
        "par 2; course 1.2 1 4.8 1 4.8 11 1.2 11; wall 1.2 6 4.8 6; tunnel 3 7.4 3 4.6; tee 3 10; cup 3 2.4",
        "par 3; course 1 1 5 1 5 11 1 11; wall 1 7.6 3.6 7.6; wall 2.4 4.4 5 4.4; tee 1.8 10; cup 4 2.2",
        "par 3; course 1.2 1 4.8 1 4.8 11 1.2 11; slope 1.2 3 3.6 1.6 0 2.2; tee 3 10; cup 3 2",
        "par 3; course 0.6 11 2.8 11 2.8 6.2 5.4 3.6 5.4 1 3.2 1 0.6 3.6; sand 4 3.4 0.55; tee 1.7 10; cup 4.4 1.9",
        "par 2; course 1 1 5 1 5 11 1 11; water 1 4 1.6 2.6; water 3.4 4 1.6 2.6; tee 3 10; cup 3 2.2",
        "par 2; course 1 1 5 1 5 11 1 11; post 2.1 5.2 0.35; post 3.9 5.2 0.35; post 3 4 0.35; post 2.3 7.2 0.3; post 3.7 7.2 0.3; tee 3 10.2; cup 3 2",
        "par 3; course 0.8 11 5.2 11 5.2 7.4 3.8 6.6 3.8 5.4 5.2 4.6 5.2 1 0.8 1 0.8 4.6 2.2 5.4 2.2 6.6 0.8 7.4; mill 3 6 0.75 -2; slope 0.8 1 4.4 2.4 1.2 0; tee 3 10; cup 2 2",
        "par 3; course 0.6 1 5.4 1 5.4 11 0.6 11; wall 3 11 3 3.2; tunnel 1.8 7.4 4.2 2.2; tee 1.8 10; cup 4.2 9.6",
        "par 3; course 1 1 5 1 5 11 1 11; water 2.2 5.4 1.6 1.2; sand 1 2.6 1.6 1.4; sand 3.6 7.2 0.6; tee 3 10; cup 2.8 2",
        "par 4; course 0.8 1 5.2 1 5.2 11 0.8 11; wall 0.8 8 3.8 8; wall 2.2 5 5.2 5; mill 1.5 3 0.62 1.4; tee 1.6 10; cup 4.2 2",
        "par 3; course 0.8 1 5.2 1 5.2 11 0.8 11; water 0.8 5.2 4.4 1.4; tunnel 3 6.9 3 4.4; mill 3 3.2 0.7 1.8; post 1.6 2.4 0.3; post 4.4 2.4 0.3; tee 3 10; cup 3 1.9",
    };

    private static readonly MiniGolfHole[] Parsed = ParseAll();

    public static MiniGolfHole Get(int hole) => Parsed[Math.Clamp(hole, 0, HoleCount - 1)];

    public static int Par(int holes)
    {
        var total = 0;
        for (var index = 0; index < holes && index < HoleCount; index++)
        {
            total += Parsed[index].Par;
        }

        return total;
    }

    private static MiniGolfHole[] ParseAll()
    {
        var holes = new MiniGolfHole[Sources.Length];
        for (var index = 0; index < Sources.Length; index++)
        {
            if (!MiniGolfHole.TryParse(Sources[index], out var hole, out var error))
            {
                throw new InvalidOperationException(string.Concat("Mini Golf hole ", Sources[index], ": ", error));
            }

            holes[index] = hole;
        }

        return holes;
    }
}
