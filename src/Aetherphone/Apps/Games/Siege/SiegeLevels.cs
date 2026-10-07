using System.Globalization;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.World;

namespace Aetherphone.Apps.Games.Siege;

internal static class SiegeLevels
{
    public const int Count = 30;
    public const int BossEvery = 10;
    public const int EndlessBossEvery = 10;
    public const float WaveLead = 1.5f;
    private const char WaveSeparator = '|';
    private const char SpacingSeparator = ':';
    private const char Rest = '.';
    private const char Together = '+';
    private const char AnyLane = '?';

    private static readonly int[] DefenderUnlocks = { 0, 1, 2, 4, 6, 8 };
    private static readonly int[] EnemyIntros = { 1, 3, 5, 7, 11, 10 };
    private static readonly int[] EndlessCosts = { 1, 2, 4, 3, 4, 0 };
    private static readonly int[] EndlessUnlocks = { 1, 2, 4, 5, 7, int.MaxValue };

    public static readonly EnemyBudget EndlessBudget = new(EndlessCosts, EndlessUnlocks, 4, 3, 20f);

    private static readonly string[] Levels =
    {
        "7:w2 w1 w3|6:w0 w2 w4 w2|5:w1 w3 . w2 +w0 w4",
        "6.4:w?|5.7:w? w? w?|5:w? +w? w? w? w?",
        "6.3:r2|5.6:w? w? r?|4.9:r? +w? w? r? +r?",
        "6.2:r? w?|5.7:r? w? w?|5.3:r? +r? r? +w?|4.8:r? w? w? w? w? w?",
        "6.1:a2|5.6:r? a?|5.2:a? r? w?|4.7:w? +a? +a? w?",
        "6:a?|5.5:w? w? w? w?|5.1:w? w? w? +r? r?|4.6:r? w? +r? +w? w? r?",
        "5.9:f2 w?|5.4:w? w? w? w?|5:a? w? f? w?|4.5:f? r? r? +f? f?",
        "5.8:a?|5.3:r? f? w? w?|4.9:w? f? w? +f? w?|4.4:w? r? f? a? w? w?",
        "5.7:a?|5.2:w? w? w? w? r?|4.8:w? r? w? a? r?|4.3:r? w? +w? w? a? r? r?",
        "5.6:f? w?|5.2:w? r? +w? f?|4.9:a? f? f?|4.5:r? w? +w? r? w? +r? w?|4.2:b2 . a? +w? w?",
        "5.5:d2 r?|5.2:w? a? w?|4.8:f? d? w? f?|4.5:f? w? +f? r? f? +w?|4.1:r? r? w? w? +w? d? w? w? w?",
        "5.4:f? w? +w?|5:w? r? d? w?|4.7:f? a? w? w? w?|4.3:f? +w? a? +r? r? w?|4:a? +a? f? w? +a?",
        "5.3:r? +a?|5:r? +w? w? w? w?|4.6:r? +f? r? +f? r?|4.2:w? w? +a? +a? r? r?|3.9:w? r? w? r? f? +r? w? d? w?",
        "5.2:f? w? w?|4.8:f? r? d? w?|4.5:f? +w? +r? w? f? f?|4.1:w? f? w? f? a? +f?|3.8:f? f? w? +f? r? +w? r? +w? +f?",
        "5.1:w? w? w? r?|4.8:w? a? w? +f?|4.4:r? +w? f? +f? a?|4:w? r? d? f? +f? d? w?|3.7:a? +w? r? w? +r? +d? a? +w?",
        "5:d? r? w?|4.7:d? w? w? d?|4.3:r? w? a? w? +r? f?|4:a? r? f? +a? +a?|3.6:w? r? +a? +r? +a? d? +d? w?",
        "4.9:a? +w? w?|4.5:f? +r? w? +w? w? w?|4.2:a? a? w? a?|3.8:w? r? w? +r? r? +a? +w? f?|3.5:a? r? d? w? +w? +d? d? w? w?",
        "4.8:f? a?|4.5:a? a? r? w?|4.1:w? a? a? +a? w?|3.8:w? +a? +f? a? r? +a?|3.4:f? +w? d? w? f? +w? r? +w? +r? a?",
        "4.7:d? d? w?|4.3:r? f? f? a?|4:a? w? +w? r? a? r?|3.6:w? +r? r? w? a? a? +w? r?|3.3:r? +r? d? +w? w? +w? +w? d? +a? +d?",
        "4.6:w? r? r? r?|4.2:r? f? f? +w? +f?|3.9:r? f? f? +w? a? +w? w?|3.5:b1 . w? r? +a? f?|3.2:b3 . d? w? a? +r? w?",
        "4.5:f? +a? w?|4.2:w? r? w? d? r? f?|3.8:w? d? +a? +w? r? r? f?|3.5:w? +w? f? +f? f? +a? f? +w? w?|3.1:a? +a? +w? f? a? w? +a? r? r?",
        "4.4:w? w? r? r? w?|4:r? f? d? f? r?|3.7:r? +r? d? w? +r? f? f? +r?|3.3:r? r? r? +w? +w? r? w? +r? f? w? f?|3:w? f? f? r? r? a? +a? +f? +f? f?",
        "4.3:a? a?|3.9:f? a? +f? d?|3.6:r? +a? d? +w? r? r? +r? w?|3.2:d? a? +a? +d? a? r? f?|2.9:d? a? a? w? +d? +w? a? d? r? +w?",
        "4.2:r? +w? +a? w?|3.8:w? +w? r? +f? a? +w?|3.5:a? w? r? +a? w? +f? +f?|3.1:w? +r? +r? f? d? r? a? +d? +r? +w?|2.8:f? f? +f? +a? +d? f? +d? r? a? r?",
        "4.1:r? r? +f? f?|3.7:d? +f? a? +f? +r?|3.4:a? f? +a? +f? +f? +f?|3:f? f? f? r? a? +a? r? +f? w?|2.7:w? d? a? +r? f? f? +a? +r? +w? a? +w?",
        "4:a? d? w?|3.6:a? +a? +a? f?|3.3:f? a? a? a? +f? w?|2.9:a? a? +f? r? a? +a? +a?|2.6:a? a? +a? +a? +a? a? +a? w?",
        "3.9:d? a? r?|3.5:a? d? d? +w? d?|3.2:r? f? a? w? a? r? +f?|2.8:a? d? r? f? a? d? r? d?|2.5:a? w? +r? w? f? f? d? w? w? a? a? w?",
        "3.8:w? +f? +f? r?|3.4:a? a? a? f?|3.1:f? +f? +f? +d? a? +a?|2.7:r? +w? +f? a? +w? f? a? r? +a?|2.4:f? +w? w? f? +d? a? r? +f? d? +r? r? +r? +w?",
        "3.7:r? w? +a? +w?|3.3:w? +a? a? +f? +r?|3:a? +a? w? +r? a? a?|2.6:d? w? w? +a? f? +w? w? w? +a? a?|2.3:r? d? +a? w? d? +r? +r? r? r? w? d? r? +f?",
        "3.6:r? d? +f? r?|3.2:r? +a? a? a? w?|2.9:f? a? a? +r? d? a?|2.5:b2 . f? +f? +f? r? +f?|2.2:b1 . f? +f? w? +f? a? r?",
    };

    public static int UnlockLevel(DefenderKind kind) => DefenderUnlocks[(int)kind];

    public static int IntroLevel(EnemyKind kind) => EnemyIntros[(int)kind];

    public static bool IsBossLevel(int level) => level > 0 && level % BossEvery == 0;

    public static DefenderKind DefenderIntroducedAt(int level)
    {
        for (var kind = DefenderKind.Sprout; kind <= DefenderKind.Bombcap; kind++)
        {
            if (DefenderUnlocks[(int)kind] == level)
            {
                return kind;
            }
        }

        return DefenderKind.None;
    }

    public static bool IntroducesEnemy(int level, EnemyKind kind) => EnemyIntros[(int)kind] == level;

    public static string Source(int level) => Levels[Math.Clamp(level, 1, Count) - 1];

    public static int WaveCount(int level)
    {
        var text = Source(level);
        var count = 1;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == WaveSeparator)
            {
                count++;
            }
        }

        return count;
    }

    public static int BuildWave(int level, int wave, ref GameRandom random, Span<WaveEntry> buffer)
    {
        var text = WaveText(Source(level), wave);
        var colon = text.IndexOf(SpacingSeparator);
        if (colon <= 0)
        {
            return 0;
        }

        var spacing = float.Parse(text[..colon], NumberStyles.Float, CultureInfo.InvariantCulture);
        Span<int> laneLoad = stackalloc int[SiegeRules.Columns];
        laneLoad.Clear();
        var time = WaveLead - spacing;
        var count = 0;
        var spawned = false;
        var tokens = text[(colon + 1)..];
        var cursor = 0;
        while (cursor < tokens.Length)
        {
            if (tokens[cursor] == ' ')
            {
                cursor++;
                continue;
            }

            var end = cursor;
            while (end < tokens.Length && tokens[end] != ' ')
            {
                end++;
            }

            var token = tokens[cursor..end];
            cursor = end;
            if (token[0] == Rest)
            {
                time += spacing;
                continue;
            }

            var joined = token[0] == Together;
            var body = joined ? token[1..] : token;
            if (body.Length < 2 || !TryKind(body[0], out var kind))
            {
                continue;
            }

            if (!joined || !spawned)
            {
                time += spacing;
            }

            var lane = body[1] == AnyLane ? PickLane(ref random, laneLoad) : body[1] - '0';
            if ((uint)lane >= SiegeRules.Columns)
            {
                continue;
            }

            laneLoad[lane]++;
            spawned = true;
            if (count < buffer.Length)
            {
                buffer[count++] = new WaveEntry(time, (byte)lane, (byte)kind);
            }
        }

        return count;
    }

    public static bool TryKind(char letter, out EnemyKind kind)
    {
        kind = letter switch
        {
            'r' => EnemyKind.Runner,
            'a' => EnemyKind.Armoured,
            'f' => EnemyKind.Flyer,
            'd' => EnemyKind.Digger,
            'b' => EnemyKind.Boss,
            _ => EnemyKind.Walker,
        };
        return letter is 'w' or 'r' or 'a' or 'f' or 'd' or 'b';
    }

    private static ReadOnlySpan<char> WaveText(string source, int wave)
    {
        var start = 0;
        var current = 0;
        for (var index = 0; index <= source.Length; index++)
        {
            if (index < source.Length && source[index] != WaveSeparator)
            {
                continue;
            }

            if (current == wave)
            {
                return source.AsSpan(start, index - start);
            }

            current++;
            start = index + 1;
        }

        return ReadOnlySpan<char>.Empty;
    }

    private static int PickLane(ref GameRandom random, ReadOnlySpan<int> laneLoad)
    {
        var first = random.Next(SiegeRules.Columns);
        var second = random.Next(SiegeRules.Columns);
        return laneLoad[second] < laneLoad[first] ? second : first;
    }
}
