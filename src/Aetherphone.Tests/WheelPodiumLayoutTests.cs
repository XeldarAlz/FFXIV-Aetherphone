using System.Numerics;
using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WheelPodiumLayoutTests
{
    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void PodiumRowsStackTopDownWithoutOverlap(float scale)
    {
        var podium = new Rect(new Vector2(20f, 400f),
            new Vector2(20f + 62f * scale, 400f + WheelPodiumLayout.Height(scale)));
        var rows = WheelPodiumLayout.Compute(podium, scale);
        var ordered = new[] { rows.Header, rows.Bet, rows.Crowd, rows.Back };
        BetDeckLayoutTests.AssertDisjoint(ordered);
        for (var index = 0; index < ordered.Length; index++)
        {
            var row = ordered[index];
            Assert.True(row.Height > 0f);
            Assert.True(row.Min.Y >= podium.Min.Y - 0.01f && row.Max.Y <= podium.Max.Y + 0.01f);
            Assert.True(row.Min.X >= podium.Min.X && row.Max.X <= podium.Max.X);
            if (index > 0)
            {
                Assert.True(row.Min.Y >= ordered[index - 1].Max.Y);
            }
        }
    }
}
