namespace Aetherphone.Apps.Music.Radio.Live;

internal static class RadioStageLayout
{
    public static float FollowTop(float artBottom, float followHeight, float hostBottom, float gap)
    {
        return MathF.Max(artBottom - followHeight, hostBottom + gap);
    }

    public static float ColumnBottom(float artBottom, float hostBottom, float followBottom)
    {
        return MathF.Max(artBottom, MathF.Max(hostBottom, followBottom));
    }
}
