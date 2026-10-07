using System.Globalization;
using Aetherphone.Core;

namespace Aetherphone.Apps.Games.MiniGolf;

internal readonly struct GolfZone
{
    public readonly Vector2 Min;
    public readonly Vector2 Max;
    public readonly float Radius;

    private GolfZone(Vector2 min, Vector2 max, float radius)
    {
        Min = min;
        Max = max;
        Radius = radius;
    }

    public bool Round => Radius > 0f;

    public Vector2 Center => (Min + Max) * 0.5f;

    public static GolfZone Rect(float x, float y, float width, float height) =>
        new(new Vector2(x, y), new Vector2(x + width, y + height), 0f);

    public static GolfZone Circle(float x, float y, float radius) =>
        new(new Vector2(x - radius, y - radius), new Vector2(x + radius, y + radius), radius);

    public bool Contains(Vector2 point) => Round
        ? Vector2.DistanceSquared(point, Center) <= Radius * Radius
        : point.X >= Min.X && point.X <= Max.X && point.Y >= Min.Y && point.Y <= Max.Y;
}

internal readonly struct GolfSlope
{
    public readonly GolfZone Zone;
    public readonly Vector2 Push;

    public GolfSlope(GolfZone zone, Vector2 push)
    {
        Zone = zone;
        Push = push;
    }
}

internal readonly struct GolfPost
{
    public readonly Vector2 Center;
    public readonly float Radius;

    public GolfPost(Vector2 center, float radius)
    {
        Center = center;
        Radius = radius;
    }
}

internal readonly struct GolfMill
{
    public readonly Vector2 Hub;
    public readonly float Reach;
    public readonly float Speed;

    public GolfMill(Vector2 hub, float reach, float speed)
    {
        Hub = hub;
        Reach = reach;
        Speed = speed;
    }
}

internal readonly struct GolfTunnel
{
    public readonly Vector2 Entry;
    public readonly Vector2 Exit;

    public GolfTunnel(Vector2 entry, Vector2 exit)
    {
        Entry = entry;
        Exit = exit;
    }
}

internal sealed class MiniGolfHole
{
    private const char EntrySeparator = ';';
    private const char TokenSeparator = ' ';
    private const float BoundsMargin = 0.35f;

    public readonly int Par;
    public readonly Vector2 Tee;
    public readonly Vector2 Cup;
    public readonly Vector2[] Course;
    public readonly int[] CourseTriangles;
    public readonly Vector2[][] Walls;
    public readonly Vector2[][] Blocks;
    public readonly int[][] BlockTriangles;
    public readonly GolfPost[] Posts;
    public readonly GolfZone[] Sand;
    public readonly GolfZone[] Water;
    public readonly GolfSlope[] Slopes;
    public readonly GolfMill[] Mills;
    public readonly GolfTunnel[] Tunnels;
    public readonly Rect Bounds;

    private MiniGolfHole(int par, Vector2 tee, Vector2 cup, Vector2[] course, Vector2[][] walls, Vector2[][] blocks,
        GolfPost[] posts, GolfZone[] sand, GolfZone[] water, GolfSlope[] slopes, GolfMill[] mills, GolfTunnel[] tunnels)
    {
        Par = par;
        Tee = tee;
        Cup = cup;
        Course = course;
        CourseTriangles = GolfGeometry.Triangulate(course);
        Walls = walls;
        Blocks = blocks;
        BlockTriangles = new int[blocks.Length][];
        for (var index = 0; index < blocks.Length; index++)
        {
            BlockTriangles[index] = GolfGeometry.Triangulate(blocks[index]);
        }

        Posts = posts;
        Sand = sand;
        Water = water;
        Slopes = slopes;
        Mills = mills;
        Tunnels = tunnels;
        var min = course[0];
        var max = course[0];
        for (var index = 1; index < course.Length; index++)
        {
            min = Vector2.Min(min, course[index]);
            max = Vector2.Max(max, course[index]);
        }

        Bounds = new Rect(min - new Vector2(BoundsMargin), max + new Vector2(BoundsMargin));
    }

    public bool OnCourse(Vector2 point)
    {
        if (!GolfGeometry.InsidePolygon(point, Course))
        {
            return false;
        }

        for (var index = 0; index < Blocks.Length; index++)
        {
            if (GolfGeometry.InsidePolygon(point, Blocks[index]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryParse(string source, out MiniGolfHole hole, out string error)
    {
        hole = null!;
        var par = 0;
        var tee = new Vector2(float.NaN);
        var cup = new Vector2(float.NaN);
        Vector2[]? course = null;
        var walls = new List<Vector2[]>();
        var blocks = new List<Vector2[]>();
        var posts = new List<GolfPost>();
        var sand = new List<GolfZone>();
        var water = new List<GolfZone>();
        var slopes = new List<GolfSlope>();
        var mills = new List<GolfMill>();
        var tunnels = new List<GolfTunnel>();
        var entries = source.Split(EntrySeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
        {
            var tokens = entries[entryIndex].Split(TokenSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (!TryNumbers(tokens, out var numbers))
            {
                error = string.Concat(entries[entryIndex], ": not a number");
                return false;
            }

            var valid = tokens[0] switch
            {
                "par" when numbers.Length == 1 => SetPar(numbers, ref par),
                "tee" when numbers.Length == 2 => SetPoint(numbers, ref tee),
                "cup" when numbers.Length == 2 => SetPoint(numbers, ref cup),
                "course" when numbers.Length >= 6 && numbers.Length % 2 == 0 => SetCourse(numbers, ref course),
                "wall" when numbers.Length >= 4 && numbers.Length % 2 == 0 => Add(walls, Points(numbers)),
                "block" when numbers.Length >= 6 && numbers.Length % 2 == 0 => Add(blocks, Points(numbers)),
                "post" when numbers.Length == 3 && numbers[2] > 0f =>
                    Add(posts, new GolfPost(new Vector2(numbers[0], numbers[1]), numbers[2])),
                "sand" when numbers.Length is 3 or 4 => Add(sand, Zone(numbers)),
                "water" when numbers.Length is 3 or 4 => Add(water, Zone(numbers)),
                "slope" when numbers.Length == 6 => Add(slopes,
                    new GolfSlope(GolfZone.Rect(numbers[0], numbers[1], numbers[2], numbers[3]),
                        new Vector2(numbers[4], numbers[5]))),
                "mill" when numbers.Length == 4 && numbers[2] > 0f =>
                    Add(mills, new GolfMill(new Vector2(numbers[0], numbers[1]), numbers[2], numbers[3])),
                "tunnel" when numbers.Length == 4 => Add(tunnels,
                    new GolfTunnel(new Vector2(numbers[0], numbers[1]), new Vector2(numbers[2], numbers[3]))),
                _ => false,
            };
            if (!valid)
            {
                error = string.Concat(entries[entryIndex], ": unknown or malformed piece");
                return false;
            }
        }

        if (par <= 0 || float.IsNaN(tee.X) || float.IsNaN(cup.X) || course is null)
        {
            error = "a hole needs a par, a tee, a cup and a course";
            return false;
        }

        hole = new MiniGolfHole(par, tee, cup, course, walls.ToArray(), blocks.ToArray(), posts.ToArray(),
            sand.ToArray(), water.ToArray(), slopes.ToArray(), mills.ToArray(), tunnels.ToArray());
        error = string.Empty;
        return true;
    }

    private static bool TryNumbers(string[] tokens, out float[] numbers)
    {
        numbers = new float[Math.Max(0, tokens.Length - 1)];
        for (var index = 1; index < tokens.Length; index++)
        {
            if (!float.TryParse(tokens[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                !float.IsFinite(value))
            {
                return false;
            }

            numbers[index - 1] = value;
        }

        return tokens.Length > 0;
    }

    private static bool SetPar(float[] numbers, ref int par)
    {
        par = (int)numbers[0];
        return par > 0 && par == numbers[0];
    }

    private static bool SetPoint(float[] numbers, ref Vector2 point)
    {
        point = new Vector2(numbers[0], numbers[1]);
        return true;
    }

    private static bool SetCourse(float[] numbers, ref Vector2[]? course)
    {
        if (course is not null)
        {
            return false;
        }

        course = Points(numbers);
        return true;
    }

    private static Vector2[] Points(float[] numbers)
    {
        var points = new Vector2[numbers.Length / 2];
        for (var index = 0; index < points.Length; index++)
        {
            points[index] = new Vector2(numbers[index * 2], numbers[index * 2 + 1]);
        }

        return points;
    }

    private static GolfZone Zone(float[] numbers) => numbers.Length == 3
        ? GolfZone.Circle(numbers[0], numbers[1], numbers[2])
        : GolfZone.Rect(numbers[0], numbers[1], numbers[2], numbers[3]);

    private static bool Add<T>(List<T> list, T item)
    {
        list.Add(item);
        return true;
    }
}
