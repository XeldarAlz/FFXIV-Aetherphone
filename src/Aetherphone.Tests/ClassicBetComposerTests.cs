using Aetherphone.Apps.Casino;
using Aetherphone.Apps.Casino.Stage;
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
    public void HalfHalvesTheAmountAndNeverDropsBelowTheMinimum()
    {
        Assert.Equal(200, ClassicBetComposer.Half(400, Minimum, Maximum, 1000));
        Assert.Equal(60, ClassicBetComposer.Half(900, Minimum, Maximum, 60));
        Assert.Equal(Minimum, ClassicBetComposer.Half(15, Minimum, Maximum, 1000));
        Assert.Equal(0, ClassicBetComposer.Half(400, Minimum, Maximum, 9));
    }

    [Fact]
    public void DoubleDoublesTheAmountUpToTheCeiling()
    {
        Assert.Equal(400, ClassicBetComposer.Double(200, Minimum, Maximum, 1000));
        Assert.Equal(Maximum, ClassicBetComposer.Double(400, Minimum, Maximum, 1000));
        Assert.Equal(120, ClassicBetComposer.Double(100, Minimum, Maximum, 120));
        Assert.Equal(Maximum, ClassicBetComposer.Double(long.MaxValue, Minimum, Maximum, long.MaxValue));
        Assert.Equal(Minimum, ClassicBetComposer.Double(0, Minimum, Maximum, 1000));
    }

    [Fact]
    public void NegativeEntriesCannotWalkPastTheMinimum()
    {
        Assert.Equal(Minimum, ClassicBetComposer.Clamp(-900, Minimum, Maximum, 1000));
    }

    [Fact]
    public void TheComposerHeightIsTheAmountRowAndThePrimaryRow()
    {
        var expected = (ClassicBetComposer.FieldHeight + DeckActions.Gap + ClassicBetComposer.ConfirmHeight) * 2f;
        Assert.Equal(expected, ClassicBetComposer.HeightFor(2f), 3);
        Assert.Equal(DeckActions.SecondaryHeight, ClassicBetComposer.FieldHeight);
        Assert.Equal(DeckActions.PrimaryHeight, ClassicBetComposer.ConfirmHeight);
    }
}
