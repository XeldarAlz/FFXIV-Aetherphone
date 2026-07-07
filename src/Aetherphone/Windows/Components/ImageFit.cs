using System.Numerics;

namespace Aetherphone.Windows.Components;

internal static class ImageFit
{
    public static (Vector2 Uv0, Vector2 Uv1) Cover(float imageWidth, float imageHeight, float targetWidth,
        float targetHeight)
    {
        if (imageWidth <= 0f || imageHeight <= 0f || targetWidth <= 0f || targetHeight <= 0f)
        {
            return (Vector2.Zero, Vector2.One);
        }

        var imageAspect = imageWidth / imageHeight;
        var targetAspect = targetWidth / targetHeight;
        if (imageAspect > targetAspect)
        {
            var span = targetAspect / imageAspect;
            var inset = (1f - span) * 0.5f;
            return (new Vector2(inset, 0f), new Vector2(1f - inset, 1f));
        }

        var verticalSpan = imageAspect / targetAspect;
        var verticalInset = (1f - verticalSpan) * 0.5f;
        return (new Vector2(0f, verticalInset), new Vector2(1f, 1f - verticalInset));
    }

    public static (Vector2 Uv0, Vector2 Uv1) CoverSquare(float imageWidth, float imageHeight) =>
        Cover(imageWidth, imageHeight, 1f, 1f);

    public static (Vector2 Uv0, Vector2 Uv1) CoverSquare(Vector2 imageSize) =>
        Cover(imageSize.X, imageSize.Y, 1f, 1f);
}
