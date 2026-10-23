using Aetherphone.Apps.Prism;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PrismTypeFitTests
{
    private const float Zoom = 1.5f;
    private static readonly float[] Tiers = { 10f, 12f, 14f, 16f, 20f, 24f, 28f, 32f, 40f };

    [Fact]
    public void NothingFittingAtLargeTextZoomStillStops()
    {
        var probe = new TieredProbe(float.MinValue);

        var pixels = PrismType.FitRequest(ref probe, 40f, 12f);

        Assert.True(probe.Snap(pixels) <= probe.Snap(12f));
        Assert.InRange(probe.Attempts, 1, 32);
    }

    [Fact]
    public void TextThatFitsKeepsTheLargestRequestedSize()
    {
        var probe = new TieredProbe(float.MaxValue);

        var pixels = PrismType.FitRequest(ref probe, 40f, 12f);

        Assert.Equal(40f, pixels);
        Assert.Equal(1, probe.Attempts);
    }

    [Fact]
    public void ShrinksUntilTheSnappedSizeFits()
    {
        var probe = new TieredProbe(24f * Zoom);

        var pixels = PrismType.FitRequest(ref probe, 40f, 12f);

        Assert.True(probe.Snap(pixels) <= 24f * Zoom);
        Assert.True(probe.Snap(pixels / 0.92f) > 24f * Zoom);
    }

    [Fact]
    public void TheFloorStopsShrinkingEvenWhenNothingFits()
    {
        var probe = new TieredProbe(float.MinValue);

        var pixels = PrismType.FitRequest(ref probe, 40f, 20f);

        Assert.True(probe.Snap(pixels) <= probe.Snap(20f));
        Assert.True(pixels > 12f);
    }

    private struct TieredProbe : PrismType.IFitProbe
    {
        private readonly float widestFittingSnap;

        public TieredProbe(float widestFittingSnap)
        {
            this.widestFittingSnap = widestFittingSnap;
            Attempts = 0;
        }

        public int Attempts { get; private set; }

        public readonly float Snap(float pixels)
        {
            var tier = Tiers[0];
            for (var index = 0; index < Tiers.Length; index++)
            {
                if (Tiers[index] <= pixels)
                {
                    tier = Tiers[index];
                }
            }

            return tier * Zoom;
        }

        public bool Fits(float pixels)
        {
            Attempts++;
            return Snap(pixels) <= widestFittingSnap;
        }
    }
}
