using System.Numerics;

namespace Aetherphone.Core.Theme;

internal static class PhoneSizeCatalog
{
    public static readonly IReadOnlyList<Vector2> Sizes = new[]
    {
        new Vector2(280f, 606f),
        new Vector2(320f, 694f),
        new Vector2(360f, 780f),
        new Vector2(400f, 866f),
        new Vector2(450f, 975f),
        new Vector2(500f, 1084f),
    };

    public static readonly IReadOnlyList<float> Scales = new[] { 0.778f, 0.889f, 1.0f, 1.111f, 1.25f, 1.389f };

    public static readonly IReadOnlyList<string> Labels = new[] { "XS", "S", "M", "L", "XL", "XXL" };

    public const float DefaultScale = 1.0f;

    public static Vector2 SizeFor(float scale) => Sizes[IndexOf(scale)];

    public static int IndexOf(float scale)
    {
        var best = 0;
        var bestDelta = float.MaxValue;
        for (var index = 0; index < Scales.Count; index++)
        {
            var delta = MathF.Abs(Scales[index] - scale);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = index;
            }
        }

        return best;
    }
}
