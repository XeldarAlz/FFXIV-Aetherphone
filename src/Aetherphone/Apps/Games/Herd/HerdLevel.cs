namespace Aetherphone.Apps.Games.Herd;

internal sealed class HerdLevel
{
    public const int Columns = 16;
    public const int Rows = 22;
    public const int SkillCount = 6;
    public const char Air = '.';
    public const char Solid = '#';
    public const char TopHalf = '-';
    public const char BottomHalf = '_';
    public const char Rising = '/';
    public const char Falling = '\\';
    public const char Water = '~';
    public const char DoorRight = 'D';
    public const char DoorLeft = 'd';
    public const char Exit = 'E';

    private readonly byte[] skills = new byte[SkillCount];
    private readonly string[] map;

    public HerdLevel(int count, int required, int releaseTicks, int seconds, string skillCounts, params string[] map)
    {
        Count = count;
        Required = required;
        ReleaseTicks = releaseTicks;
        Seconds = seconds;
        this.map = map;
        for (var skill = 0; skill < SkillCount && skill < skillCounts.Length; skill++)
        {
            skills[skill] = (byte)Math.Clamp(skillCounts[skill] - '0', 0, 9);
        }

        DoorDirection = 1;
        for (var row = 0; row < map.Length; row++)
        {
            var line = map[row];
            for (var column = 0; column < line.Length; column++)
            {
                switch (line[column])
                {
                    case DoorRight:
                    case DoorLeft:
                        DoorX = column;
                        DoorY = row;
                        DoorDirection = line[column] == DoorLeft ? -1 : 1;
                        Doors++;
                        break;
                    case Exit:
                        ExitX = column;
                        ExitY = row;
                        Exits++;
                        break;
                    default:
                        break;
                }
            }
        }
    }

    public int Count { get; }

    public int Required { get; }

    public int ReleaseTicks { get; }

    public int Seconds { get; }

    public int DoorX { get; }

    public int DoorY { get; }

    public int DoorDirection { get; }

    public int ExitX { get; }

    public int ExitY { get; }

    public int Doors { get; }

    public int Exits { get; }

    public int MapRows => map.Length;

    public int SkillsFor(HerdSkill skill) => skills[(int)skill];

    public string Row(int row) => map[row];

    public char At(int column, int row)
    {
        if (row < 0 || row >= map.Length)
        {
            return Air;
        }

        var line = map[row];
        return column >= 0 && column < line.Length ? line[column] : Air;
    }
}
