using Aetherphone.Apps.Casino;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ClassicBetComposerTests
{
    private const long Minimum = 10;
    private const long Maximum = 500;

    [Fact]
    public void TheStackIsAHardCeilingEvenUnderTheTableMaximum()
    {
        Assert.Equal(120, ClassicBetComposer.Ceiling(Maximum, 120));
        Assert.Equal(Maximum, ClassicBetComposer.Ceiling(Maximum, 900));
        Assert.Equal(0, ClassicBetComposer.Ceiling(Maximum, -5));
    }

    [Fact]
    public void AmountsClimbToTheMinimumAndStopAtTheCeiling()
    {
        Assert.Equal(Minimum, ClassicBetComposer.Clamp(0, Minimum, Maximum, 1000));
        Assert.Equal(Minimum, ClassicBetComposer.Clamp(3, Minimum, Maximum, 1000));
        Assert.Equal(250, ClassicBetComposer.Clamp(250, Minimum, Maximum, 1000));
        Assert.Equal(Maximum, ClassicBetComposer.Clamp(9000, Minimum, Maximum, 1000));
        Assert.Equal(120, ClassicBetComposer.Clamp(9000, Minimum, Maximum, 120));
    }

    [Fact]
    public void AStackTooThinForTheMinimumCanComposeNothingAtAll()
    {
        Assert.Equal(0, ClassicBetComposer.Clamp(50, Minimum, Maximum, 5));
        Assert.Equal(0, ClassicBetComposer.Clamp(Minimum, Minimum, Maximum, 0));
        Assert.Equal(0, ClassicBetComposer.Clamp(Minimum, Minimum, Maximum, -40));
    }

    [Fact]
    public void HalfIsHalfOfWhatCouldBeStakedAndNeverBelowTheMinimum()
    {
        Assert.Equal(250, ClassicBetComposer.Half(Minimum, Maximum, 1000));
        Assert.Equal(60, ClassicBetComposer.Half(Minimum, Maximum, 120));
        Assert.Equal(Minimum, ClassicBetComposer.Half(Minimum, Maximum, 15));
        Assert.Equal(0, ClassicBetComposer.Half(Minimum, Maximum, 9));
    }

    [Fact]
    public void NegativeEntriesCannotWalkPastTheMinimum()
    {
        Assert.Equal(Minimum, ClassicBetComposer.Clamp(-900, Minimum, Maximum, 1000));
    }

    [Fact]
    public void TheComposerHeightIsTheThreeRowsItActuallyDraws()
    {
        var expected = (ClassicBetComposer.FieldHeight + ClassicBetComposer.QuickHeight + ClassicBetComposer.ConfirmHeight + 16f) * 2f;
        Assert.Equal(expected, ClassicBetComposer.HeightFor(2f), 3);
    }
}
