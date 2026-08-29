using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PhotoZoomViewTests
{
    private static readonly Rect Stage = new(Vector2.Zero, new Vector2(260f, 260f));
    private static readonly Vector2 TextureSize = new(2048f, 2048f);

    [Fact]
    public void FocusOnATightClusterClampsToMaxZoom()
    {
        var view = new PhotoZoomView();
        var bounds = new Rect(new Vector2(0.45f, 0.45f), new Vector2(0.55f, 0.55f));

        view.FocusOn(Stage, TextureSize, bounds);

        Assert.Equal(4f, view.Zoom, 3);
    }

    [Fact]
    public void FocusOnTheFullCanvasStaysAtMinZoomWithNoPan()
    {
        var view = new PhotoZoomView();
        var bounds = new Rect(Vector2.Zero, Vector2.One);

        view.FocusOn(Stage, TextureSize, bounds, paddingFraction: 0f);

        Assert.Equal(1f, view.Zoom, 3);
        Assert.Equal(0f, view.Pan.X, 3);
        Assert.Equal(0f, view.Pan.Y, 3);
    }

    [Fact]
    public void FocusOnAnOffCenterClusterPansTowardItWithoutExceedingTheClamp()
    {
        var view = new PhotoZoomView();
        var bounds = new Rect(new Vector2(0.05f, 0.4f), new Vector2(0.25f, 0.6f));

        view.FocusOn(Stage, TextureSize, bounds);

        Assert.True(view.Zoom > 1f);
        var fit = PhotoZoomView.FitScale(Stage, TextureSize);
        var drawn = TextureSize * fit * view.Zoom;
        var maxPanX = MathF.Max(0f, (drawn.X - Stage.Width) * 0.5f);
        Assert.InRange(view.Pan.X, -maxPanX, maxPanX);
    }

    [Fact]
    public void SnapToClampsZoomToTheConfiguredRange()
    {
        var view = new PhotoZoomView();

        view.SnapTo(Stage, TextureSize, 50f, Vector2.Zero);

        Assert.Equal(4f, view.Zoom, 3);
    }
}
