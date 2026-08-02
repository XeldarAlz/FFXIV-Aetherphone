using Aetherphone.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Components;

internal static class ImageFit
{
    public static readonly Vector4 LetterboxFill = new(0f, 0f, 0f, 1f);

    // Draws a photo contain-fit inside frame (using the uv0/uv1 window's own aspect, not frame's),
    // backed by a solid black fill so a mismatched aspect never shows raw frame background through
    // the gap. Returns the actual drawn image rect (frame is what's requested; this is what
    // actually holds the sharp image) since callers that overlay content on the photo itself (tag
    // pins, a hit-test) need it, not the wider frame.
    public static Rect DrawLetterboxed(ImDrawListPtr drawList, IDalamudTextureWrap texture, Rect frame, Vector2 uv0,
        Vector2 uv1, float rounding)
    {
        var size = texture.Size;
        drawList.AddRectFilled(frame.Min, frame.Max, ImGui.GetColorU32(LetterboxFill), rounding,
            ImDrawFlags.RoundCornersAll);
        var imageRect = CenteredRect(frame, VisibleAspect(uv0, uv1, size));
        drawList.AddImageRounded(texture.Handle, imageRect.Min, imageRect.Max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        return imageRect;
    }

    public static Rect CenteredRect(Rect region, float aspect)
    {
        var width = MathF.Min(region.Width, region.Height * aspect);
        var half = new Vector2(width, width / aspect) * 0.5f;
        return new Rect(region.Center - half, region.Center + half);
    }

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

    // The aspect ratio of a uv sub-window (Cover's output, or WallpaperCrop.ComputeUv's) once
    // mapped back onto real image pixels - used to contain-fit that window into a frame with
    // CenteredRect instead of stretching it to fill the frame outright.
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
