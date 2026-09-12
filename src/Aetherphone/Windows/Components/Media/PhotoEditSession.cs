using Aetherphone.Core.Media;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Windows.Components;

internal enum PhotoEditTool : byte
{
    Adjust,
    Looks,
    Crop,
}

internal enum PhotoCropAspect : byte
{
    Original,
    Square,
    FourByThree,
    ThreeByFour,
    SixteenByNine,
    NineBySixteen,
}

internal static class PhotoCropAspects
{
    public static readonly PhotoCropAspect[] All =
    {
        PhotoCropAspect.Original,
        PhotoCropAspect.Square,
        PhotoCropAspect.FourByThree,
        PhotoCropAspect.ThreeByFour,
        PhotoCropAspect.SixteenByNine,
        PhotoCropAspect.NineBySixteen,
    };

    public static float Ratio(PhotoCropAspect aspect, float originalAspect)
    {
        switch (aspect)
        {
            case PhotoCropAspect.Square:
                return 1f;
            case PhotoCropAspect.FourByThree:
                return 4f / 3f;
            case PhotoCropAspect.ThreeByFour:
                return 3f / 4f;
            case PhotoCropAspect.SixteenByNine:
                return 16f / 9f;
            case PhotoCropAspect.NineBySixteen:
                return 9f / 16f;
            default:
                return originalAspect > 0f ? originalAspect : 1f;
        }
    }
}

internal readonly struct PhotoSaveRequest
{
    public readonly string Path;
    public readonly PhotoEdit Edit;
    public readonly WallpaperCrop Crop;
    public readonly PhotoCropAspect Aspect;

    public PhotoSaveRequest(string path, PhotoEdit edit, WallpaperCrop crop, PhotoCropAspect aspect)
    {
        Path = path;
        Edit = edit;
        Crop = crop;
        Aspect = aspect;
    }
}

internal sealed class PhotoEditSession : IDisposable
{
    public static readonly PhotoAdjustment[] Adjustments =
    {
        PhotoAdjustment.Brightness,
        PhotoAdjustment.Contrast,
        PhotoAdjustment.Saturation,
        PhotoAdjustment.Warmth,
        PhotoAdjustment.Vignette,
        PhotoAdjustment.Straighten,
    };

    public readonly CropCanvas Crop = new();
    public readonly PhotoEditPreview Preview = new();
    public readonly ChipRail AdjustmentRail = new();
    public readonly ChipRail LookRail = new();
    public readonly ChipRail AspectRail = new();
    public readonly string[] AdjustmentLabels = new string[Adjustments.Length];
    public readonly bool[] AdjustmentActive = new bool[Adjustments.Length];
    public readonly string[] LookLabels = new string[PhotoLooks.All.Length];
    public readonly bool[] LookActive = new bool[PhotoLooks.All.Length];
    public readonly string[] AspectLabels = new string[PhotoCropAspects.All.Length];
    public readonly bool[] AspectActive = new bool[PhotoCropAspects.All.Length];

    private string valueLabel = string.Empty;
    private float valueLabelFor = float.NaN;
    private PhotoAdjustment valueLabelAdjustment;

    public string Path { get; private set; } = string.Empty;

    public PhotoEdit Edit { get; private set; } = PhotoEdit.None;

    public PhotoEditTool Tool { get; set; }

    public PhotoAdjustment Adjustment { get; set; }

    public PhotoCropAspect Aspect { get; set; }

    public bool Saving { get; set; }

    public string Notice { get; set; } = string.Empty;

    public Vector2 StageTextureSize { get; set; }

    public bool IsOpen => Path.Length > 0;

    public float OriginalAspect
    {
        get
        {
            var size = Preview.ProxySize;
            if (size.X <= 0f || size.Y <= 0f)
            {
                return 1f;
            }

            var aspect = size.X / size.Y;
            return (Edit.QuarterTurns & 1) == 1 ? 1f / aspect : aspect;
        }
    }

    public float CropRatio => PhotoCropAspects.Ratio(Aspect, OriginalAspect);

    public bool IsDirty
    {
        get
        {
            if (!Edit.IsIdentity || Aspect != PhotoCropAspect.Original)
            {
                return true;
            }

            return StageTextureSize.X > 0f && !Crop.IsAtRest(StageTextureSize, CropRatio, false);
        }
    }

    public void Open(string path)
    {
        Path = path;
        Edit = PhotoEdit.None;
        Tool = PhotoEditTool.Adjust;
        Adjustment = PhotoAdjustment.Brightness;
        Aspect = PhotoCropAspect.Original;
        Saving = false;
        Notice = string.Empty;
        StageTextureSize = Vector2.Zero;
        valueLabelFor = float.NaN;
        Crop.Reset();
        Preview.Open(path);
    }

    public void Close()
    {
        Path = string.Empty;
        Saving = false;
        Preview.Close();
    }

    public void Adjust(PhotoAdjustment adjustment, float value)
    {
        Edit = Edit.With(adjustment, value);
    }

    public void SetLook(PhotoLook look)
    {
        Edit = Edit.WithLook(look, Edit.LookStrength);
    }

    public void SetLookStrength(float strength)
    {
        Edit = Edit.WithLook(Edit.Look, strength);
    }

    public void Rotate()
    {
        Edit = Edit.RotatedClockwise();
    }

    public void Flip()
    {
        Edit = Edit.Flipped();
    }

    public void Reset()
    {
        Edit = PhotoEdit.None;
        Aspect = PhotoCropAspect.Original;
        Notice = string.Empty;
        Crop.Reset();
    }

    public string ValueLabel(PhotoAdjustment adjustment, float value, IFormatProvider culture)
    {
        if (adjustment == valueLabelAdjustment && value == valueLabelFor)
        {
            return valueLabel;
        }

        valueLabelAdjustment = adjustment;
        valueLabelFor = value;
        valueLabel = adjustment == PhotoAdjustment.Straighten
            ? value.ToString("+0.0;-0.0;0.0", culture) + "°"
            : MathF.Round(value * 100f).ToString("+0;-0;0", culture);
        return valueLabel;
    }

    public PhotoSaveRequest Snapshot()
    {
        return new PhotoSaveRequest(Path, Edit, Crop.Target, Aspect);
    }

    public static PixelImage Render(in PhotoSaveRequest request)
    {
        var source = ImageProcessor.DecodeLocalRgba32(request.Path, 0);
        var edited = PhotoEditor.Apply(source, request.Edit);
        var ratio = PhotoCropAspects.Ratio(request.Aspect, edited.Aspect);
        return CropTo(edited, request.Crop, ratio);
    }

    private static PixelImage CropTo(in PixelImage image, WallpaperCrop crop, float aspect)
    {
        var size = image.Size;
        var clamped = crop.Clamped(size, aspect, WallpaperCrop.MinZoom);
        var (uv0, uv1) = clamped.ComputeUv(size, aspect, WallpaperCrop.MinZoom);
        var x = Math.Clamp((int)MathF.Round(uv0.X * image.Width), 0, Math.Max(0, image.Width - 1));
        var y = Math.Clamp((int)MathF.Round(uv0.Y * image.Height), 0, Math.Max(0, image.Height - 1));
        var width = Math.Clamp((int)MathF.Round((uv1.X - uv0.X) * image.Width), 1, image.Width - x);
        var height = Math.Clamp((int)MathF.Round((uv1.Y - uv0.Y) * image.Height), 1, image.Height - y);
        if (x == 0 && y == 0 && width == image.Width && height == image.Height)
        {
            return image;
        }

        var stride = width * PixelImage.BytesPerPixel;
        var pixels = new byte[height * stride];
        for (var row = 0; row < height; row++)
        {
            var sourceOffset = (((y + row) * image.Width) + x) * PixelImage.BytesPerPixel;
            Array.Copy(image.Pixels, sourceOffset, pixels, row * stride, stride);
        }

        return new PixelImage(pixels, width, height);
    }

    public void Dispose()
    {
        Preview.Dispose();
    }
}
