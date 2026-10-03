using Aetherphone.Core.Animation;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RollingValueTests
{
    private const float FrameSeconds = 1f / 60f;

    [Theory]
    [InlineData(123_456_789)]
    [InlineData(999_999_999)]
    [InlineData(16_777_217)]
    public void SnapShowsLargeValuesExactly(int value)
    {
        var roll = new RollingValue();
        roll.Snap(value);
        Assert.Equal(value, roll.Display);
    }

    [Fact]
    public void RollSettlesExactlyOnLargeValues()
    {
        var roll = new RollingValue();
        roll.Snap(123_000_000);
        for (var frame = 0; frame < 600; frame++)
        {
            roll.Update(123_456_789, FrameSeconds);
        }

        Assert.Equal(123_456_789, roll.Display);
    }
}
