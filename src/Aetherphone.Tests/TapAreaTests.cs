using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TapAreaTests
{
    private static readonly Rect Row = new(new Vector2(0f, 100f), new Vector2(300f, 164f));

    [Fact]
    public void SquareInsideTheContainerKeepsItsFullSize()
    {
        var hit = TapArea.Around(new Vector2(150f, 132f), 28f, Row);
        Assert.Equal(new Vector2(136f, 118f), hit.Min);
        Assert.Equal(new Vector2(164f, 146f), hit.Max);
    }

    [Fact]
    public void SquareNearTheRowBottomStopsAtTheRowEdge()
    {
        var hit = TapArea.Around(new Vector2(150f, 156f), 28f, Row);
        Assert.Equal(Row.Max.Y, hit.Max.Y);
        Assert.Equal(142f, hit.Min.Y);
    }

    [Fact]
    public void SquareNeverReachesPastTheTrailingTextEdge()
    {
        var zone = new Rect(Row.Min, new Vector2(200f, Row.Max.Y));
        var hit = TapArea.Around(new Vector2(190f, 132f), 28f, zone);
        Assert.Equal(200f, hit.Max.X);
        Assert.Equal(176f, hit.Min.X);
    }

    [Fact]
    public void CenterOutsideTheContainerCollapsesToAnEmptyArea()
    {
        var hit = TapArea.Around(new Vector2(150f, 300f), 28f, Row);
        Assert.Equal(0f, hit.Height);
    }
}
