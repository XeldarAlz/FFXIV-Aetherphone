using System.Globalization;

namespace Aetherphone.Apps.Games.Snip;

internal readonly struct SnipRope
{
    public readonly Vector2 Anchor;
    public readonly float Length;

    public SnipRope(Vector2 anchor, float length)
    {
        Anchor = anchor;
        Length = length;
    }
}

internal readonly struct SnipCushion
{
    public readonly Vector2 Position;
    public readonly Vector2 Direction;

    public SnipCushion(Vector2 position, Vector2 direction)
    {
        Position = position;
        Direction = direction;
    }
}

internal readonly struct SnipSpike
{
    public readonly Vector2 From;
    public readonly Vector2 To;

    public SnipSpike(Vector2 from, Vector2 to)
    {
        From = from;
        To = to;
    }
}

internal sealed class SnipLevel
{
    public const int StarCount = 3;
    public const int MaxRopes = 6;
    public const int MaxBubbles = 4;
    public const int MaxCushions = 4;
    public const int MaxSpikes = 8;
    private const char EntrySeparator = ';';
    private const char TokenSeparator = ' ';
    private static readonly string[] CompassNames = { "e", "se", "s", "sw", "w", "nw", "n", "ne" };

    private static readonly SnipLevel Empty = new(Vector2.Zero, Vector2.Zero, Array.Empty<SnipRope>(),
        Array.Empty<Vector2>(), Array.Empty<SnipCushion>(), Array.Empty<SnipSpike>(), Array.Empty<Vector2>());

    public readonly Vector2 Candy;
    public readonly Vector2 Moogle;
    public readonly SnipRope[] Ropes;
    public readonly Vector2[] Bubbles;
    public readonly SnipCushion[] Cushions;
    public readonly SnipSpike[] Spikes;
    public readonly Vector2[] Stars;

    private SnipLevel(Vector2 candy, Vector2 moogle, SnipRope[] ropes, Vector2[] bubbles, SnipCushion[] cushions,
        SnipSpike[] spikes, Vector2[] stars)
    {
        Candy = candy;
        Moogle = moogle;
        Ropes = ropes;
        Bubbles = bubbles;
        Cushions = cushions;
        Spikes = spikes;
        Stars = stars;
    }

    public static bool TryParse(string source, out SnipLevel level, out string error)
    {
        level = Empty;
        var candy = new Vector2(float.NaN);
        var moogle = new Vector2(float.NaN);
        var ropes = new List<SnipRope>(MaxRopes);
        var bubbles = new List<Vector2>(MaxBubbles);
        var cushions = new List<SnipCushion>(MaxCushions);
        var spikes = new List<SnipSpike>(MaxSpikes);
        var stars = new List<Vector2>(StarCount);
        var entries = source.Split(EntrySeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
        {
            var tokens = entries[entryIndex].Split(TokenSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (!TryReadEntry(tokens, ref candy, ref moogle, ropes, bubbles, cushions, spikes, stars, out error))
            {
                error = string.Concat(entries[entryIndex], ": ", error);
                return false;
            }
        }

        if (float.IsNaN(candy.X) || float.IsNaN(moogle.X))
        {
            error = "a level needs a candy and a moogle";
            return false;
        }

        if (stars.Count != StarCount)
        {
            error = "a level needs exactly three stars";
            return false;
        }

        if (ropes.Count == 0 && bubbles.Count == 0)
        {
            error = "a level needs a rope or a bubble";
            return false;
        }

        if (ropes.Count > MaxRopes || bubbles.Count > MaxBubbles || cushions.Count > MaxCushions ||
            spikes.Count > MaxSpikes)
        {
            error = "a level holds too many pieces";
            return false;
        }

        level = new SnipLevel(candy, moogle, ropes.ToArray(), bubbles.ToArray(), cushions.ToArray(), spikes.ToArray(),
            stars.ToArray());
        error = string.Empty;
        return true;
    }

    private static bool TryReadEntry(string[] tokens, ref Vector2 candy, ref Vector2 moogle, List<SnipRope> ropes,
        List<Vector2> bubbles, List<SnipCushion> cushions, List<SnipSpike> spikes, List<Vector2> stars,
        out string error)
    {
        error = "unknown piece";
        if (tokens.Length == 0)
        {
            return false;
        }

        switch (tokens[0])
        {
            case "c" when tokens.Length == 3 && TryPoint(tokens, 1, out var candyPoint):
                candy = candyPoint;
                return true;
            case "m" when tokens.Length == 3 && TryPoint(tokens, 1, out var mooglePoint):
                moogle = mooglePoint;
                return true;
            case "r" when tokens.Length == 4 && TryPoint(tokens, 1, out var anchor) &&
                          TryNumber(tokens[3], out var length) && length > 0f:
                ropes.Add(new SnipRope(anchor, length));
                return true;
            case "b" when tokens.Length == 3 && TryPoint(tokens, 1, out var bubble):
                bubbles.Add(bubble);
                return true;
            case "a" when tokens.Length == 4 && TryPoint(tokens, 1, out var cushion) &&
                          TryCompass(tokens[3], out var direction):
                cushions.Add(new SnipCushion(cushion, direction));
                return true;
            case "k" when tokens.Length == 5 && TryPoint(tokens, 1, out var from) && TryPoint(tokens, 3, out var to):
                spikes.Add(new SnipSpike(from, to));
                return true;
            case "s" when tokens.Length == 3 && TryPoint(tokens, 1, out var star):
                stars.Add(star);
                return true;
            default:
                return false;
        }
    }

    private static bool TryPoint(string[] tokens, int start, out Vector2 point)
    {
        point = Vector2.Zero;
        if (!TryNumber(tokens[start], out var x) || !TryNumber(tokens[start + 1], out var y))
        {
            return false;
        }

        point = new Vector2(x, y);
        return true;
    }

    private static bool TryNumber(string token, out float value) =>
        float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value);

    private static bool TryCompass(string token, out Vector2 direction)
    {
        for (var index = 0; index < CompassNames.Length; index++)
        {
            if (!string.Equals(CompassNames[index], token, StringComparison.Ordinal))
            {
                continue;
            }

            var angle = index * MathF.PI * 0.25f;
            direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            return true;
        }

        direction = Vector2.Zero;
        return false;
    }
}
