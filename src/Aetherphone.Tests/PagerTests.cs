using Aetherphone.Core.Animation;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PagerTests
{
    private const float Width = 100f;
    private const int Pages = 3;
    private const float Frame = 1f / 60f;
    private const float GrabAt = 0.3f;

    [Fact]
    public void FlickAfterAMidSettleGrabMovesOnePageFromWhereThePageIs()
    {
        var pager = new Pager();
        pager.AnimateTo(1, Pages);
        while (pager.Value < GrabAt)
        {
            pager.Step(Frame, Pages);
        }

        pager.Begin(0f);
        pager.Drag(-Width * 0.15f, Width, Pages, Frame);
        pager.Release(Width, Pages);

        Assert.Equal(1, pager.Page);
    }
}
