using System.Numerics;

namespace Aetherphone.Core.Wallpapers;

internal readonly struct WallpaperCrop
{
    public const float MinZoom = 1f;

    public const float MaxZoom = 5f;

    public readonly float Zoom;

    public readonly float CenterX;

    public readonly float CenterY;

    public WallpaperCrop(float zoom, float centerX, float centerY)
    {
        Zoom = zoom;
        CenterX = centerX;
        CenterY = centerY;
    }

    public static WallpaperCrop Cover => new(1f, 0.5f, 0.5f);

    public WallpaperCrop With(float zoom, float centerX, float centerY) => new(zoom, centerX, centerY);

    public (Vector2 Uv0, Vector2 Uv1) ComputeUv(Vector2 imageSize, float targetAspect)
    {
        var (visibleWidth, visibleHeight) = VisibleSize(imageSize, targetAspect, Zoom);
        var center = new Vector2(ClampCenter(CenterX, visibleWidth), ClampCenter(CenterY, visibleHeight));
        var half = new Vector2(visibleWidth * 0.5f, visibleHeight * 0.5f);
        return (center - half, center + half);
    }

    public WallpaperCrop Clamped(Vector2 imageSize, float targetAspect)
    {
        var zoom = Math.Clamp(Zoom, MinZoom, MaxZoom);
        var (visibleWidth, visibleHeight) = VisibleSize(imageSize, targetAspect, zoom);
        return new WallpaperCrop(zoom, ClampCenter(CenterX, visibleWidth), ClampCenter(CenterY, visibleHeight));
    }

    private static (float Width, float Height) VisibleSize(Vector2 imageSize, float targetAspect, float zoom)
    {
        if (imageSize.X <= 0f || imageSize.Y <= 0f || targetAspect <= 0f)
        {
            return (1f, 1f);
        }

        var imageAspect = imageSize.X / imageSize.Y;
        float visibleWidthPixels;
        float visibleHeightPixels;
        if (imageAspect >= targetAspect)
        {
            visibleHeightPixels = imageSize.Y;
            visibleWidthPixels = imageSize.Y * targetAspect;
        }
        else
        {
            visibleWidthPixels = imageSize.X;
            visibleHeightPixels = imageSize.X / targetAspect;
        }

        var clampedZoom = MathF.Max(MinZoom, zoom);
        var width = MathF.Min(1f, visibleWidthPixels / imageSize.X / clampedZoom);
        var height = MathF.Min(1f, visibleHeightPixels / imageSize.Y / clampedZoom);
        return (width, height);
    }

    private static float ClampCenter(float center, float extent)
    {
        var half = extent * 0.5f;
        if (half >= 0.5f)
        {
            return 0.5f;
        }

        return Math.Clamp(center, half, 1f - half);
    }
}
