using Aetherphone.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Components;

internal static class ImageFit
{
    private const uint LetterboxFill = 0xFF000000u;
    private const float FrameMatchEpsilon = 0.5f;

    // Draws a photo contain-fit inside frame, using the uv window's own aspect rather than the
    // frame's, so a photo whose aspect differs from the frame is bordered instead of stretched.
    public static void DrawLetterboxed(ImDrawListPtr drawList, IDalamudTextureWrap texture, Rect frame, Vector2 uv0,
        Vector2 uv1, float rounding)
    {
        var imageRect = CenteredRect(frame, VisibleAspect(uv0, uv1, texture.Size));
        if (MathF.Abs(imageRect.Width - frame.Width) <= FrameMatchEpsilon
            && MathF.Abs(imageRect.Height - frame.Height) <= FrameMatchEpsilon)
        {
            drawList.AddImageRounded(texture.Handle, frame.Min, frame.Max, uv0, uv1, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
            return;
        }

        drawList.AddRectFilled(frame.Min, frame.Max, LetterboxFill, rounding, ImDrawFlags.RoundCornersAll);
        drawList.AddImage(texture.Handle, imageRect.Min, imageRect.Max, uv0, uv1, 0xFFFFFFFFu);
    }

    public static Rect CenteredRect(Rect region, float aspect)
    {
        var width = MathF.Min(region.Width, region.Height * aspect);
        var half = new Vector2(width, width / aspect) * 0.5f;
        return new Rect(region.Center - half, region.Center + half);
    }

    public const float CenterFocus = 0.5f;

    public static (Vector2 Uv0, Vector2 Uv1) Cover(float imageWidth, float imageHeight, float targetWidth,
        float targetHeight) =>
        Cover(imageWidth, imageHeight, targetWidth, targetHeight, CenterFocus);

    public static (Vector2 Uv0, Vector2 Uv1) Cover(float imageWidth, float imageHeight, float targetWidth,
        float targetHeight, float focusY)
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
        var verticalTop = (1f - verticalSpan) * Math.Clamp(focusY, 0f, 1f);
        return (new Vector2(0f, verticalTop), new Vector2(1f, verticalTop + verticalSpan));
    }

    public static (Vector2 Uv0, Vector2 Uv1) CoverSquare(float imageWidth, float imageHeight) =>
        Cover(imageWidth, imageHeight, 1f, 1f);

    public static (Vector2 Uv0, Vector2 Uv1) CoverSquare(Vector2 imageSize) =>
        Cover(imageSize.X, imageSize.Y, 1f, 1f);

    public static float VisibleAspect(Vector2 uv0, Vector2 uv1, Vector2 imageSize)
    {
        if (imageSize.X <= 0f || imageSize.Y <= 0f)
        {
            return 1f;
        }

        var width = (uv1.X - uv0.X) * imageSize.X;
        var height = (uv1.Y - uv0.Y) * imageSize.Y;
        return height > 0f ? width / height : 1f;
    }
}
