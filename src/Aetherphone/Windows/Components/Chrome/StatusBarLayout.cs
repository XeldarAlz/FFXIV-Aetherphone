namespace Aetherphone.Windows.Components;

internal static class StatusBarLayout
{
    public static float ClockLeft(float screenLeft, float edgeInset, float earRight, float clockWidth) =>
        MathF.Min(screenLeft + edgeInset, earRight - clockWidth);

    public static float ClusterRight(float screenRight, float edgeInset, float minClusterLeft, float clusterWidth,
        float minRightPadding)
    {
        var right = screenRight - edgeInset;
        if (right - clusterWidth >= minClusterLeft)
        {
            return right;
        }

        return MathF.Min(screenRight - minRightPadding, minClusterLeft + clusterWidth);
    }
}
