namespace Aetherphone.Core.Notifications;

internal static class VolumeCatalog
{
    public static readonly float[] Scales = { 0.25f, 0.5f, 0.75f, 1f };
    public static readonly string[] Labels = { "25%", "50%", "75%", "100%" };

    public static int IndexOf(float value)
    {
        var bestIndex = Scales.Length - 1;
        var bestDistance = float.MaxValue;
        for (var index = 0; index < Scales.Length; index++)
        {
            var distance = MathF.Abs(Scales[index] - value);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        return bestIndex;
    }
}
