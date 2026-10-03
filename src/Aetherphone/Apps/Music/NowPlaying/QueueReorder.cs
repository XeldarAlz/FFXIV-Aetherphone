namespace Aetherphone.Apps.Music.NowPlaying;

internal static class QueueReorder
{
    public static int TargetIndex(ReadOnlySpan<float> restCenters, int sourceIndex, float dragCenter)
    {
        var target = 0;
        for (var rowIndex = 0; rowIndex < restCenters.Length; rowIndex++)
        {
            if (rowIndex == sourceIndex)
            {
                continue;
            }

            if (restCenters[rowIndex] < dragCenter)
            {
                target++;
            }
        }

        return target;
    }

    public static int Shift(int rowIndex, int sourceIndex, int targetIndex)
    {
        if (rowIndex == sourceIndex)
        {
            return 0;
        }

        if (rowIndex < sourceIndex && rowIndex >= targetIndex)
        {
            return 1;
        }

        if (rowIndex > sourceIndex && rowIndex <= targetIndex)
        {
            return -1;
        }

        return 0;
    }

    public static float AutoscrollSpeed(float pointerY, float top, float bottom, float edge, float maximumSpeed)
    {
        if (edge <= 0f)
        {
            return 0f;
        }

        if (pointerY < top + edge)
        {
            return -maximumSpeed * Math.Clamp((top + edge - pointerY) / edge, 0f, 1f);
        }

        if (pointerY > bottom - edge)
        {
            return maximumSpeed * Math.Clamp((pointerY - (bottom - edge)) / edge, 0f, 1f);
        }

        return 0f;
    }
}
