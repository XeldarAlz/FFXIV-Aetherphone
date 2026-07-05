namespace Aetherphone.Core.Theme;

internal static class TextZoomCatalog
{
    public static readonly IReadOnlyList<float> Scales = new[] { 1.0f, 1.15f, 1.3f, 1.5f };
    public static readonly IReadOnlyList<string> Labels = new[] { "100%", "115%", "130%", "150%" };

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
