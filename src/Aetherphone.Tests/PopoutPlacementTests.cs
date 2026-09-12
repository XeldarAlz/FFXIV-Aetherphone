using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.GameChat;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PopoutPlacementTests
{
    private static readonly Rect Viewport = new(Vector2.Zero, new Vector2(1920f, 1080f));
    private static readonly Vector2 Size = new(336f, 430f);

    [Fact]
    public void BesideThePhoneLandsOnItsLeftWhenThereIsRoom()
    {
        var phone = new Rect(new Vector2(1200f, 200f), new Vector2(1600f, 1000f));

        var origin = PopoutPlacements.Resolve(PopoutPlacement.BesidePhone, Viewport, phone, Size, 24f, 0f);

        Assert.Equal(1200f - 24f - Size.X, origin.X);
        Assert.Equal(200f, origin.Y);
    }

    [Fact]
    public void BesideThePhoneFallsBackToTheRightSide()
    {
        var phone = new Rect(new Vector2(40f, 200f), new Vector2(440f, 1000f));

        var origin = PopoutPlacements.Resolve(PopoutPlacement.BesidePhone, Viewport, phone, Size, 24f, 0f);

        Assert.Equal(440f + 24f, origin.X);
        Assert.Equal(200f, origin.Y);
    }

    [Fact]
    public void WithoutAPhoneTheWindowCentres()
    {
        var origin = PopoutPlacements.Resolve(PopoutPlacement.BesidePhone, Viewport, null, Size, 24f, 0f);

        Assert.Equal((1920f - Size.X) * 0.5f, origin.X);
        Assert.Equal((1080f - Size.Y) * 0.5f, origin.Y);
    }

    [Fact]
    public void CornersRespectTheMarginAndStayInsideTheViewport()
    {
        var bottomRight = PopoutPlacements.Resolve(PopoutPlacement.BottomRight, Viewport, null, Size, 24f, 0f);
        var topLeft = PopoutPlacements.Resolve(PopoutPlacement.TopLeft, Viewport, null, Size, 24f, 5000f);

        Assert.Equal(1920f - 24f - Size.X, bottomRight.X);
        Assert.Equal(1080f - 24f - Size.Y, bottomRight.Y);
        Assert.Equal(1920f - Size.X, topLeft.X);
        Assert.Equal(1080f - Size.Y, topLeft.Y);
    }
}
