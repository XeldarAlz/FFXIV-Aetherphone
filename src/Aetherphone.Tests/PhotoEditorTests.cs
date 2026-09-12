using Aetherphone.Core.Media;
using Aetherphone.Core.Photos;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PhotoEditorTests
{
    [Fact]
    public void OneQuarterTurnMovesTheTopLeftPixelToTheTopRight()
    {
        var source = BuildUnique(3, 2);

        var turned = PhotoEditor.Orient(source, 1, false);

        Assert.Equal(2, turned.Width);
        Assert.Equal(3, turned.Height);
        AssertPixel(turned, 1, 0, PixelAt(source, 0, 0));
        AssertPixel(turned, 0, 0, PixelAt(source, 0, 1));
        AssertPixel(turned, 1, 2, PixelAt(source, 2, 0));
    }

    [Fact]
    public void TwoQuarterTurnsReflectThroughTheCenter()
    {
        var source = BuildUnique(3, 2);

        var turned = PhotoEditor.Orient(source, 2, false);

        Assert.Equal(3, turned.Width);
        Assert.Equal(2, turned.Height);
        AssertPixel(turned, 2, 1, PixelAt(source, 0, 0));
        AssertPixel(turned, 0, 0, PixelAt(source, 2, 1));
    }

    [Fact]
    public void ThreeQuarterTurnsMoveTheTopLeftPixelToTheBottomLeft()
    {
        var source = BuildUnique(3, 2);

        var turned = PhotoEditor.Orient(source, 3, false);

        Assert.Equal(2, turned.Width);
        Assert.Equal(3, turned.Height);
        AssertPixel(turned, 0, 2, PixelAt(source, 0, 0));
        AssertPixel(turned, 1, 0, PixelAt(source, 2, 1));
    }

    [Fact]
    public void MirroringSwapsTheColumns()
    {
        var source = BuildUnique(3, 2);

        var mirrored = PhotoEditor.Orient(source, 0, true);

        AssertPixel(mirrored, 2, 0, PixelAt(source, 0, 0));
        AssertPixel(mirrored, 0, 1, PixelAt(source, 2, 1));
        AssertPixel(mirrored, 1, 1, PixelAt(source, 1, 1));
    }

    [Fact]
    public void NoOrientationChangeReturnsTheSourceBuffer()
    {
        var source = BuildUnique(3, 2);

        var same = PhotoEditor.Orient(source, 4, false);

        Assert.Same(source.Pixels, same.Pixels);
    }

    [Fact]
    public void RotateAndFlipStateMatchesApplyingTheStepsOneByOne()
    {
        var source = BuildUnique(4, 3);
        var edit = PhotoEdit.None.RotatedClockwise().Flipped().RotatedClockwise().RotatedClockwise().Flipped()
            .RotatedClockwise();

        var fromState = PhotoEditor.Apply(source, edit);
        var stepwise = PhotoEditor.Orient(source, 1, false);
        stepwise = PhotoEditor.Orient(stepwise, 0, true);
        stepwise = PhotoEditor.Orient(stepwise, 1, false);
        stepwise = PhotoEditor.Orient(stepwise, 1, false);
        stepwise = PhotoEditor.Orient(stepwise, 0, true);
        stepwise = PhotoEditor.Orient(stepwise, 1, false);

        Assert.Equal(stepwise.Width, fromState.Width);
        Assert.Equal(stepwise.Height, fromState.Height);
        Assert.Equal(stepwise.Pixels, fromState.Pixels);
    }

    [Fact]
    public void FourClockwiseTurnsReturnToTheStart()
    {
        var edit = PhotoEdit.None.RotatedClockwise().RotatedClockwise().RotatedClockwise().RotatedClockwise();

        Assert.Equal(0, edit.QuarterTurns);
        Assert.True(edit.IsIdentity);
    }

    [Fact]
    public void InscribedScaleIsOneAtZeroAndShrinksWithAngle()
    {
        Assert.Equal(1f, PhotoEditor.InscribedScale(1920, 1080, 0f));
        var mild = PhotoEditor.InscribedScale(1920, 1080, 3f);
        var steep = PhotoEditor.InscribedScale(1920, 1080, 12f);

        Assert.True(mild < 1f);
        Assert.True(steep < mild);
        Assert.True(steep > 0.5f);
    }

    [Fact]
    public void StraightenKeepsAFlatImageFlatAndShrinksIt()
    {
        var source = BuildFlat(64, 40, 90, 140, 200);

        var straightened = PhotoEditor.Straighten(source, 6f);

        Assert.True(straightened.Width < 64);
        Assert.True(straightened.Height < 40);
        for (var index = 0; index < straightened.Length; index += 4)
        {
            Assert.Equal(90, straightened.Pixels[index]);
            Assert.Equal(140, straightened.Pixels[index + 1]);
            Assert.Equal(200, straightened.Pixels[index + 2]);
            Assert.Equal(255, straightened.Pixels[index + 3]);
        }
    }

    [Fact]
    public void IdentityEditReturnsTheSourceBuffer()
    {
        var source = BuildUnique(5, 4);

        var result = PhotoEditor.Apply(source, PhotoEdit.None);

        Assert.Same(source.Pixels, result.Pixels);
    }

    [Fact]
    public void BrightnessRaisesMidGray()
    {
        var source = BuildFlat(8, 8, 128, 128, 128);

        var brighter = PhotoEditor.Apply(source, PhotoEdit.None.With(PhotoAdjustment.Brightness, 0.5f));
        var darker = PhotoEditor.Apply(source, PhotoEdit.None.With(PhotoAdjustment.Brightness, -0.5f));

        Assert.True(brighter.Pixels[0] > 128);
        Assert.True(darker.Pixels[0] < 128);
        Assert.Equal(255, brighter.Pixels[3]);
    }

    [Fact]
    public void ContrastPushesValuesAwayFromMidGray()
    {
        var source = BuildFlat(4, 4, 200, 200, 200);

        var punchier = PhotoEditor.Apply(source, PhotoEdit.None.With(PhotoAdjustment.Contrast, 0.8f));
        var flatter = PhotoEditor.Apply(source, PhotoEdit.None.With(PhotoAdjustment.Contrast, -0.8f));

        Assert.True(punchier.Pixels[0] > 200);
        Assert.True(flatter.Pixels[0] < 200);
    }

    [Fact]
    public void FullDesaturationYieldsGray()
    {
        var source = BuildFlat(4, 4, 220, 40, 90);

        var gray = PhotoEditor.Apply(source, PhotoEdit.None.With(PhotoAdjustment.Saturation, -1f));

        Assert.Equal(gray.Pixels[0], gray.Pixels[1]);
        Assert.Equal(gray.Pixels[1], gray.Pixels[2]);
    }

    [Fact]
    public void WarmthShiftsRedAndBlueInOppositeDirections()
    {
        var source = BuildFlat(4, 4, 128, 128, 128);

        var warm = PhotoEditor.Apply(source, PhotoEdit.None.With(PhotoAdjustment.Warmth, 1f));
        var cool = PhotoEditor.Apply(source, PhotoEdit.None.With(PhotoAdjustment.Warmth, -1f));

        Assert.True(warm.Pixels[0] > warm.Pixels[2]);
        Assert.True(cool.Pixels[0] < cool.Pixels[2]);
    }

    [Fact]
    public void VignetteDarkensCornersMoreThanTheCenter()
    {
        var source = BuildFlat(64, 64, 180, 180, 180);

        var shaded = PhotoEditor.Apply(source, PhotoEdit.None.With(PhotoAdjustment.Vignette, 1f));

        var center = PixelAt(shaded, 32, 32);
        var corner = PixelAt(shaded, 0, 0);
        Assert.True(corner.Red < center.Red);
        Assert.Equal(180, center.Red);
    }

    [Fact]
    public void ALookAtZeroStrengthIsAnIdentity()
    {
        var edit = PhotoEdit.None.WithLook(PhotoLook.Noir, 0f);

        Assert.True(edit.IsIdentity);
    }

    [Fact]
    public void MonoLookRemovesColor()
    {
        var source = BuildFlat(4, 4, 250, 30, 60);

        var mono = PhotoEditor.Apply(source, PhotoEdit.None.WithLook(PhotoLook.Mono, 1f));

        Assert.Equal(mono.Pixels[0], mono.Pixels[1]);
        Assert.Equal(mono.Pixels[1], mono.Pixels[2]);
    }

    [Fact]
    public void AdjustmentsClampToTheirRanges()
    {
        var edit = PhotoEdit.None
            .With(PhotoAdjustment.Brightness, 4f)
            .With(PhotoAdjustment.Vignette, -2f)
            .With(PhotoAdjustment.Straighten, 90f);

        Assert.Equal(1f, edit.Brightness);
        Assert.Equal(0f, edit.Vignette);
        Assert.Equal(PhotoEdit.MaxStraightenDegrees, edit.Straighten);
        Assert.Equal(1f, edit.ValueOf(PhotoAdjustment.Brightness));
    }

    private readonly record struct Rgba(byte Red, byte Green, byte Blue, byte Alpha);

    private static Rgba PixelAt(in PixelImage image, int x, int y)
    {
        var index = ((y * image.Width) + x) * PixelImage.BytesPerPixel;
        return new Rgba(image.Pixels[index], image.Pixels[index + 1], image.Pixels[index + 2],
            image.Pixels[index + 3]);
    }

    private static void AssertPixel(in PixelImage image, int x, int y, Rgba expected)
    {
        Assert.Equal(expected, PixelAt(image, x, y));
    }

    private static PixelImage BuildUnique(int width, int height)
    {
        var pixels = new byte[width * height * PixelImage.BytesPerPixel];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = ((y * width) + x) * PixelImage.BytesPerPixel;
                pixels[index] = (byte)(x * 40);
                pixels[index + 1] = (byte)(y * 60);
                pixels[index + 2] = (byte)(((y * width) + x) * 7);
                pixels[index + 3] = 255;
            }
        }

        return new PixelImage(pixels, width, height);
    }

    private static PixelImage BuildFlat(int width, int height, byte red, byte green, byte blue)
    {
        var pixels = new byte[width * height * PixelImage.BytesPerPixel];
        for (var index = 0; index < pixels.Length; index += PixelImage.BytesPerPixel)
        {
            pixels[index] = red;
            pixels[index + 1] = green;
            pixels[index + 2] = blue;
            pixels[index + 3] = 255;
        }

        return new PixelImage(pixels, width, height);
    }
}
