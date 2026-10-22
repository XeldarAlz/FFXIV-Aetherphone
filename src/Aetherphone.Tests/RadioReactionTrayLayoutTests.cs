using Aetherphone.Apps.Music.Radio.Live;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RadioReactionTrayLayoutTests
{
    private const float Margin = 8f;
    private const float Padding = 8f;
    private const int Slots = 8;
    private const float PreferredSlot = 34f;

    [Fact]
    public void AWideBarKeepsTheFullSlotAndCentresTheTray()
    {
        var fit = RadioReactionTrayLayout.Fit(0f, 400f, Margin, Padding, Slots, PreferredSlot);

        Assert.Equal(PreferredSlot, fit.Slot);
        Assert.Equal(PreferredSlot * Slots + Padding * 2f, fit.Width);
        Assert.Equal(400f - (fit.Left + fit.Width), fit.Left, 3);
    }

    [Fact]
    public void ANarrowBarShrinksTheSlotsSoTheTrayStaysInside()
    {
        var fit = RadioReactionTrayLayout.Fit(20f, 270f, Margin, Padding, Slots, PreferredSlot);

        Assert.True(fit.Slot < PreferredSlot);
        Assert.True(fit.Left >= 20f + Margin - 0.001f);
        Assert.True(fit.Left + fit.Width <= 270f - Margin + 0.001f);
    }

    [Fact]
    public void ANarrowBarStillCentresTheTray()
    {
        var fit = RadioReactionTrayLayout.Fit(20f, 270f, Margin, Padding, Slots, PreferredSlot);

        Assert.Equal(270f - (fit.Left + fit.Width), fit.Left - 20f, 3);
    }

    [Fact]
    public void ABarTooNarrowForAnySlotCollapsesTheTrayAroundItsCentre()
    {
        var fit = RadioReactionTrayLayout.Fit(0f, 10f, Margin, Padding, Slots, PreferredSlot);

        Assert.Equal(0f, fit.Slot);
        Assert.Equal(5f, fit.Left + fit.Width * 0.5f, 3);
    }
}
