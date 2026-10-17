using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BlackjackRailTextBudgetTests
{
    private const float Tolerance = 0.01f;
    private const int FullRail = BlackjackRules.SeatCount;
    private const int SeatedRail = BlackjackRules.SeatCount - 1;
    private const float WidePayoutFullRailFloor = 300f;
    private const string LongForename = "KiwiShadow";
    private const float LongForenameEndSeatFullRailFloor = 336f;

    private static readonly float[] Scales = { 0.75f, 1f, 1.25f, 1.5f };
    private static readonly float[] FeltWidths = { 272f, 300f, 312f, 336f };
    private static readonly float[] FeltHeights = { 520f, 330f };
    private static readonly string[] Stacks = { "1.8M", "12.5M", "999K" };
    private static readonly string[] Bets = { "250K", "1.8M", "12.5M" };
    private static readonly string[] Payouts = { "+500K", "+1.2M", "-999K", "±0" };
    private static readonly string[] WidePayouts = { "+12.5M", "-12.5M" };
    private static readonly LocString[] ShortTitles = { L.Strip.TitleVip, L.Strip.TitleShark, L.Strip.TitleWhale };

    public static TheoryData<float, float, float, int> Rails()
    {
        var data = new TheoryData<float, float, float, int>();
        for (var scaleIndex = 0; scaleIndex < Scales.Length; scaleIndex++)
        {
            for (var widthIndex = 0; widthIndex < FeltWidths.Length; widthIndex++)
            {
                for (var heightIndex = 0; heightIndex < FeltHeights.Length; heightIndex++)
                {
                    data.Add(Scales[scaleIndex], FeltWidths[widthIndex], FeltHeights[heightIndex], SeatedRail);
                    data.Add(Scales[scaleIndex], FeltWidths[widthIndex], FeltHeights[heightIndex], FullRail);
                }
            }
        }

        return data;
    }

    [Fact]
    public void RailCaptionsShrinkNoFurtherThanCaptionTwo()
    {
        var floor = StageText.Minimum(StageTextRole.Caption);
        Assert.Equal(TextStyles.Caption2.Scale, floor.Scale);
        Assert.True(floor.Scale < TextStyles.Footnote.Scale);
    }

    [Theory]
    [MemberData(nameof(Rails))]
    public void TheSeatPlateHoldsAForenameAndACompactStackAtTheFloor(float scale, float width, float height,
        int railCount)
    {
        var layout = Compute(scale, width, height, railCount);
        var inner = layout.RailPlateTextWidth;
        AssertHoldsText(inner, PitText.Forename("Akira Tanaka"), scale);
        for (var index = 0; index < Stacks.Length; index++)
        {
            AssertHoldsText(inner, Stacks[index], scale);
        }
    }

    [Theory]
    [MemberData(nameof(Rails))]
    public void AMiddleSeatBetweenEmptySeatsHoldsATenLetterForenameOnARegularFelt(float scale, float width,
        float height, int railCount)
    {
        var layout = Compute(scale, width, height, railCount);
        if (layout.Compact)
        {
            return;
        }

        var slot = railCount / 2;
        var plate = layout.RailPlateRect(slot, true, true);
        Assert.True(plate.Width > layout.RailPlateWidth);
        AssertHoldsText(layout.RailPlateTextWidthFor(plate.Width), LongForename, scale);
    }

    [Theory]
    [MemberData(nameof(Rails))]
    public void AnEndSeatBesideAnEmptyOneHoldsATenLetterForenameOnSixSeatRailsAndTheWidestSevenSeatRail(
        float scale, float width, float height, int railCount)
    {
        if (railCount == FullRail && width < LongForenameEndSeatFullRailFloor)
        {
            return;
        }

        var layout = Compute(scale, width, height, railCount);
        if (layout.Compact)
        {
            return;
        }

        var first = layout.RailPlateRect(0, false, true);
        var last = layout.RailPlateRect(railCount - 1, true, false);
        AssertHoldsText(layout.RailPlateTextWidthFor(first.Width), LongForename, scale);
        AssertHoldsText(layout.RailPlateTextWidthFor(last.Width), LongForename, scale);
    }

    [Theory]
    [MemberData(nameof(Rails))]
    public void TheBetPlateHoldsACompactBetAtTheFloor(float scale, float width, float height, int railCount)
    {
        var layout = Compute(scale, width, height, railCount);
        Assert.True(layout.RailBetPlateWidth < layout.ColumnWidth);
        for (var index = 0; index < Bets.Length; index++)
        {
            AssertHoldsCapsule(layout.RailBetPlateWidth, Bets[index], scale);
        }
    }

    [Theory]
    [MemberData(nameof(Rails))]
    public void TheOutcomeBadgeHoldsAFiveCharacterPayoutAtTheFloor(float scale, float width, float height,
        int railCount)
    {
        var layout = Compute(scale, width, height, railCount);
        Assert.Equal(layout.ColumnWidth, layout.RailHandWidth(1), 3);
        for (var index = 0; index < Payouts.Length; index++)
        {
            AssertHoldsCapsule(layout.RailHandWidth(1), Payouts[index], scale);
        }
    }

    [Theory]
    [MemberData(nameof(Rails))]
    public void ASixCharacterPayoutHoldsOnEverySixSeatRailAndOnWiderSevenSeatRails(float scale, float width,
        float height, int railCount)
    {
        if (railCount == FullRail && width < WidePayoutFullRailFloor)
        {
            return;
        }

        var layout = Compute(scale, width, height, railCount);
        for (var index = 0; index < WidePayouts.Length; index++)
        {
            AssertHoldsCapsule(layout.RailHandWidth(1), WidePayouts[index], scale);
        }
    }

    [Theory]
    [MemberData(nameof(Rails))]
    public void TheShortEnglishTitlesHoldOnEveryColumnSoOnlyLongerOnesHide(float scale, float width, float height,
        int railCount)
    {
        var layout = Compute(scale, width, height, railCount);
        for (var index = 0; index < ShortTitles.Length; index++)
        {
            AssertHoldsCapsule(layout.ColumnWidth, ShortTitles[index].Source, scale);
        }

        var vip = DeckLabelMeasure.Translations(L.Strip.TitleVip);
        for (var index = 0; index < vip.Length; index++)
        {
            AssertHoldsCapsule(layout.ColumnWidth, vip[index], scale);
        }
    }

    private static float FloorWidth(string text, float scale) =>
        DeckLabelMeasure.Width(text, scale) * StageText.Minimum(StageTextRole.Caption).Scale;

    private static void AssertHoldsText(float width, string label, float scale) =>
        Assert.True(width >= FloorWidth(label, scale) - Tolerance, label);

    private static void AssertHoldsCapsule(float width, string label, float scale) =>
        Assert.True(width >= FloorWidth(label, scale) + StageText.TightPadX * 2f * scale - Tolerance, label);

    private static BlackjackTableLayout Compute(float scale, float width, float height, int railCount) =>
        BlackjackTableLayoutTests.Compute(scale, width, height, railCount, railCount < FullRail);
}
