namespace Aetherphone.Core.Media;

internal enum PostAspect
{
    Square,
    Portrait,
    Landscape,
}

internal static class PostAspects
{
    public const float SquareRatio = 1f;
    public const float PortraitRatio = 3f / 4f;
    public const float LandscapeRatio = 16f / 9f;

    public static readonly PostAspect[] All = { PostAspect.Square, PostAspect.Portrait, PostAspect.Landscape };

    public static float Ratio(PostAspect aspect) => aspect switch
    {
        PostAspect.Portrait => PortraitRatio,
        PostAspect.Landscape => LandscapeRatio,
        _ => SquareRatio,
    };

    public static (int Width, int Height) Size(PostAspect aspect, int baseSize)
    {
        var ratio = Ratio(aspect);
        if (ratio >= 1f)
        {
            return (baseSize, Math.Max(1, (int)MathF.Round(baseSize / ratio)));
        }

        return (Math.Max(1, (int)MathF.Round(baseSize * ratio)), baseSize);
    }

    public static float DisplayRatio(int mediaWidth, int mediaHeight)
    {
        if (mediaWidth <= 0 || mediaHeight <= 0)
        {
            return SquareRatio;
        }

        return Math.Clamp((float)mediaWidth / mediaHeight, PortraitRatio, LandscapeRatio);
    }

    public static float DisplayHeight(float width, int mediaWidth, int mediaHeight) =>
        width / DisplayRatio(mediaWidth, mediaHeight);
}
