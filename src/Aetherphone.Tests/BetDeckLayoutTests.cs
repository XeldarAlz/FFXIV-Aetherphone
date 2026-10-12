using System.Numerics;
using Aetherphone.Apps.Casino.Machines;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BetDeckLayoutTests
{
    private const float Tolerance = 0.01f;

    private const float SpecFieldWidth = 120f;

    private static readonly float[] Scales = { 0.75f, 1f, 1.5f };
    private static readonly float[] PhoneWidths = { ScreenWidth(PhoneCaseKind.Art), ScreenWidth(PhoneCaseKind.Color) };
    private static readonly float[] DesignWidths = { 240f, 280f, 320f, 361f, 420f };

    public static TheoryData<bool, bool, bool> Configurations()
    {
        var data = new TheoryData<bool, bool, bool>();
        for (var mask = 0; mask < 8; mask++)
        {
            data.Add((mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void NoComposerRectOverlapsAnotherAtAnyScaleOrWidth(bool knob, bool fixedAmount, bool auto)
    {
        for (var scaleIndex = 0; scaleIndex < Scales.Length; scaleIndex++)
        {
            var scale = Scales[scaleIndex];
            for (var widthIndex = 0; widthIndex < DesignWidths.Length; widthIndex++)
            {
                AssertClean(DeckOf(DesignWidths[widthIndex] * scale, knob, fixedAmount, scale), knob, fixedAmount,
                    auto, scale);
                AssertClean(DeckOf(DesignWidths[widthIndex], knob, fixedAmount, scale), knob, fixedAmount, auto,
                    scale);
            }
        }
    }

    [Fact]
    public void QuickChipsHoldTheirWidestLabelInEveryLanguageOnEveryPhone()
    {
        for (var scaleIndex = 0; scaleIndex < Scales.Length; scaleIndex++)
        {
            var scale = Scales[scaleIndex];
            var max = DeckLabelMeasure.Widest(L.Casino.BetMax, scale);
            var quick = Quick(max, scale);
            for (var widthIndex = 0; widthIndex < PhoneWidths.Length; widthIndex++)
            {
                var deck = DeckOf(PhoneWidths[widthIndex] * scale, true, false, scale);
                var layout = BetDeckLayout.Compute(deck, true, false, true, quick, AutoLabel(scale), scale);
                var amount = layout.Amount;
                Assert.True(amount.HasHalf && amount.HasDouble && amount.HasMax, max);
                AssertHolds(amount.Half, quick.Half, scale);
                AssertHolds(amount.Double, quick.Double, scale);
                AssertHolds(amount.Max, quick.Max, scale);
                Assert.True(amount.Field.Width >= BetDeckLayout.FieldMinWidth * scale - Tolerance,
                    amount.Field.Width.ToString());
                Assert.Equal(amount.Field.Max.X + BetDeckLayout.Gap * scale, amount.Half.Min.X, 2);
                Assert.Equal(deck.Max.X - BetDeckLayout.Pad * scale, amount.Max.Max.X, 2);
            }
        }
    }

    [Fact]
    public void TheDefaultPhoneGivesTheFieldTheSpecWidthInEnglish()
    {
        var deck = DeckOf(ScreenWidth(PhoneCaseKind.Color), true, false, 1f);
        var layout = BetDeckLayout.Compute(deck, true, false, true, Quick("Max", 1f), AutoLabel(1f), 1f);
        Assert.True(layout.Amount.HasHalf && layout.Amount.HasDouble && layout.Amount.HasMax);
        Assert.True(layout.Field.Width >= SpecFieldWidth, layout.Field.Width.ToString());
    }

    [Fact]
    public void QuickChipsAreSymbolsAndNeverTheOldHalfWord()
    {
        Assert.Equal("½", DeckAmountRow.HalfLabel);
        Assert.Equal("2×", DeckAmountRow.DoubleLabel);
        Assert.True(DeckLabelMeasure.Width(DeckAmountRow.HalfLabel, 1f) <= DeckLabelMeasure.LatinWidth);
    }

    [Fact]
    public void ANarrowRowDropsHalfThenDoubleBeforeTheFieldGoesUnderItsMinimum()
    {
        var quick = Quick("Max", 1f);
        var chips = DeckActions.PillWidth(quick.Half, 1f) + DeckActions.PillWidth(quick.Double, 1f)
            + DeckActions.PillWidth(quick.Max, 1f) + BetDeckLayout.Gap * 3f;
        var roomy = Row(chips + BetDeckLayout.FieldMinWidth);
        var full = DeckAmountRow.Compute(roomy, quick, 1f);
        Assert.True(full.HasHalf && full.HasDouble && full.HasMax);

        var tight = DeckAmountRow.Compute(Row(chips + BetDeckLayout.FieldMinWidth - 1f), quick, 1f);
        Assert.False(tight.HasHalf);
        Assert.True(tight.HasDouble && tight.HasMax);
        Assert.True(tight.Field.Width >= BetDeckLayout.FieldMinWidth - Tolerance);

        var tighter = DeckAmountRow.Compute(Row(DeckActions.PillWidth(quick.Max, 1f) + BetDeckLayout.Gap
            + BetDeckLayout.FieldMinWidth + 4f), quick, 1f);
        Assert.False(tighter.HasHalf);
        Assert.False(tighter.HasDouble);
        Assert.True(tighter.HasMax);
        AssertDisjoint(new[] { tighter.Field, tighter.Half, tighter.Double, tighter.Max });
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void TheDeckIsExactlyTheRowsItHas(bool knob, bool fixedAmount, bool auto)
    {
        var scale = 1.25f;
        var deck = DeckOf(361f * scale, knob, fixedAmount, scale);
        var expected = BetDeckLayout.Pad * 2f + BetDeckLayout.ActionHeight
            + (fixedAmount ? 0f : BetDeckLayout.AmountHeight + BetDeckLayout.Gap)
            + (knob ? BetDeckLayout.KnobHeight + BetDeckLayout.Gap : 0f);
        Assert.Equal(expected, BetDeckLayout.DeckHeightFor(knob, fixedAmount), 3);
        var layout = Layout(deck, knob, fixedAmount, auto, scale);
        var firstTop = knob ? layout.Knob.Min.Y : fixedAmount ? layout.Action.Min.Y : layout.Field.Min.Y;
        Assert.Equal(deck.Min.Y + BetDeckLayout.Pad * scale, firstTop, 2);
        Assert.Equal(deck.Max.Y - BetDeckLayout.Pad * scale, layout.Action.Max.Y, 2);
        if (knob && !fixedAmount)
        {
            Assert.Equal(layout.Knob.Max.Y + BetDeckLayout.Gap * scale, layout.Field.Min.Y, 2);
        }

        if (!fixedAmount)
        {
            Assert.Equal(layout.Field.Max.Y + BetDeckLayout.Gap * scale, layout.Action.Min.Y, 2);
        }
    }

    [Fact]
    public void EveryRowSharesOneControlHeight()
    {
        var scale = 1.5f;
        var deck = DeckOf(361f * scale, true, false, scale);
        var layout = Layout(deck, true, false, true, scale);
        Assert.Equal(DeckActions.SecondaryHeight * scale, layout.Knob.Height, 3);
        Assert.Equal(DeckActions.SecondaryHeight * scale, layout.Field.Height, 3);
        Assert.Equal(DeckActions.SecondaryHeight * scale, layout.Half.Height, 3);
        Assert.Equal(DeckActions.SecondaryHeight * scale, layout.Double.Height, 3);
        Assert.Equal(DeckActions.SecondaryHeight * scale, layout.Max.Height, 3);
        Assert.Equal(DeckActions.PrimaryHeight * scale, layout.Action.Height, 3);
        Assert.Equal(DeckActions.PrimaryHeight * scale, layout.Auto.Height, 3);
        Assert.Equal(44f, DeckActions.SecondaryHeight);
        Assert.Equal(56f, DeckActions.PrimaryHeight);
    }

    [Fact]
    public void WithoutAutoThePrimaryTakesTheWholeRow()
    {
        var deck = DeckOf(320f, false, false, 1f);
        var layout = Layout(deck, false, false, false, 1f);
        Assert.False(layout.HasAuto);
        Assert.Equal(deck.Width - BetDeckLayout.Pad * 2f, layout.Action.Width, 3);
        Assert.Equal(deck.Max.Y - BetDeckLayout.Pad, layout.Action.Max.Y, 3);
    }

    [Fact]
    public void TheAutoPillHoldsIconAndLabelInEveryLanguageAndLeavesThePrimaryItsRoom()
    {
        for (var scaleIndex = 0; scaleIndex < Scales.Length; scaleIndex++)
        {
            var scale = Scales[scaleIndex];
            var label = DeckLabelMeasure.Width(DeckLabelMeasure.Widest(L.Strip.Auto, scale), scale);
            for (var widthIndex = 0; widthIndex < PhoneWidths.Length; widthIndex++)
            {
                var deck = DeckOf(PhoneWidths[widthIndex] * scale, true, false, scale);
                var layout = BetDeckLayout.Compute(deck, true, false, true, Quick("Max", scale), label, scale);
                Assert.True(layout.HasAuto);
                Assert.False(layout.AutoCompact);
                Assert.Equal(BetDeckLayout.AutoWidth(label, scale), layout.Auto.Width, 2);
                Assert.True(layout.Auto.Width >= label + (DeckActions.PillPadX * 2f + DeckActions.IconSize
                    + DeckActions.IconGap) * scale - Tolerance);
                Assert.Equal(deck.Min.X + BetDeckLayout.Pad * scale, layout.Auto.Min.X, 2);
                Assert.Equal(layout.Auto.Max.X + BetDeckLayout.Gap * scale, layout.Action.Min.X, 2);
                Assert.True(layout.Action.Width >= BetDeckLayout.PrimaryMinWidth * scale - Tolerance);
                AssertDisjoint(new[] { layout.Auto, layout.Action });
            }
        }
    }

    [Fact]
    public void AnAutoPillThatCannotFitShrinksToItsIconBeforeThePrimaryLosesItsMinimum()
    {
        var deck = DeckOf(240f, false, true, 1f);
        var layout = BetDeckLayout.Compute(deck, false, true, true, default, 160f, 1f);
        Assert.True(layout.HasAuto);
        Assert.True(layout.AutoCompact);
        Assert.Equal(BetDeckLayout.ActionHeight, layout.Auto.Width, 3);
        Assert.True(layout.Action.Width >= BetDeckLayout.PrimaryMinWidth - Tolerance);
        AssertDisjoint(new[] { layout.Auto, layout.Action });
    }

    [Fact]
    public void StartAutoStartsCoveredRoundsAndRoutesShortStacksToChips()
    {
        Assert.Equal(AutoStartRoute.Start, AutoBetPlan.RouteStart(true, 500, 1_000, true, false, false));
        Assert.Equal(AutoStartRoute.Start, AutoBetPlan.RouteStart(true, 1_000, 1_000, true, true, false));
        Assert.Equal(AutoStartRoute.Ignore, AutoBetPlan.RouteStart(false, 500, 1_000, true, false, false));
        Assert.Equal(AutoStartRoute.TopUp, AutoBetPlan.RouteStart(true, 5_000, 1_000, true, false, true));
        Assert.Equal(AutoStartRoute.GetChips, AutoBetPlan.RouteStart(true, 5_000, 1_000, true, false, false));
        Assert.Equal(AutoStartRoute.Ignore, AutoBetPlan.RouteStart(true, 5_000, 1_000, false, false, true));
        Assert.Equal(AutoStartRoute.Ignore, AutoBetPlan.RouteStart(true, 5_000, 1_000, true, true, true));
    }

    [Fact]
    public void StartingAndStoppingAutoKeepsTheCountAndTheReason()
    {
        var plan = new AutoBetPlan { Rounds = 3 };
        plan.Start(1_000);
        Assert.True(plan.Running);
        Assert.Equal(3, plan.Remaining);
        Assert.Equal(AutoStop.None, plan.Settle(1_000, 0, false, 100, 10_000, 100_000));
        Assert.Equal(2, plan.Remaining);
        plan.Stop(AutoStop.Manual);
        Assert.False(plan.Running);
        Assert.Equal(AutoStop.Manual, plan.Stopped);
        plan.Acknowledge();
        Assert.Equal(AutoStop.None, plan.Stopped);
        plan.Start(0);
        Assert.False(plan.Running);
        plan.Start(2_500);
        Assert.True(plan.Running);
        Assert.Equal(3, plan.Remaining);
        Assert.Equal(2_500, plan.Next);
    }

    [Fact]
    public void AutoWaitsOutAPacingRefusalAndStopsForAnyOther()
    {
        var plan = new AutoBetPlan();
        Assert.False(plan.WaitsOut(CasinoReasons.Cooldown));
        plan.Start(1_000);
        Assert.True(plan.WaitsOut(CasinoReasons.Cooldown));
        Assert.True(plan.WaitsOut(L.Casino.ReasonCooldown));
        Assert.False(plan.WaitsOut(CasinoReasons.Unreachable));
        Assert.False(plan.WaitsOut(string.Empty));
        Assert.False(plan.WaitsOut(L.Originals.Resumed));
        Assert.True(plan.Running);
    }

    internal static DeckQuickLabels Quick(string max, float scale) =>
        new(DeckLabelMeasure.Width(DeckAmountRow.HalfLabel, scale),
            DeckLabelMeasure.Width(DeckAmountRow.DoubleLabel, scale), DeckLabelMeasure.Width(max, scale));

    private static float AutoLabel(float scale) => DeckLabelMeasure.Width("Auto", scale);

    private static BetDeckLayout Layout(Rect deck, bool knob, bool fixedAmount, bool auto, float scale) =>
        BetDeckLayout.Compute(deck, knob, fixedAmount, auto, Quick("Max", scale), AutoLabel(scale), scale);

    private static Rect Row(float width) => new(new Vector2(10f, 100f), new Vector2(10f + width, 144f));

    internal static float ScreenWidth(PhoneCaseKind kind)
    {
        var chassis = ChassisMetrics.For(kind, PhoneSizeCatalog.DesignWidth);
        return PhoneSizeCatalog.DesignWidth - (chassis.RailWidth + chassis.MetalWidth + chassis.GlassWidth) * 2f;
    }

    private static void AssertHolds(Rect chip, float labelWidth, float scale)
    {
        Assert.True(chip.Width >= labelWidth + DeckActions.PillPadX * 2f * scale - Tolerance, chip.Width.ToString());
        Assert.True(chip.Width >= DeckActions.PillMinWidth * scale - Tolerance);
    }

    private static Rect DeckOf(float width, bool knob, bool fixedAmount, float scale)
    {
        var height = BetDeckLayout.DeckHeightFor(knob, fixedAmount) * scale;
        return new Rect(new Vector2(16f, 600f), new Vector2(16f + width, 600f + height));
    }

    private static void AssertClean(Rect deck, bool knob, bool fixedAmount, bool auto, float scale)
    {
        var layout = Layout(deck, knob, fixedAmount, auto, scale);
        Assert.Equal(auto, layout.HasAuto);
        var rects = new List<Rect> { layout.Action };
        if (layout.HasKnob)
        {
            rects.Add(layout.Knob);
        }

        if (layout.HasAmount)
        {
            rects.Add(layout.Field);
            rects.Add(layout.Half);
            rects.Add(layout.Double);
            rects.Add(layout.Max);
        }

        if (layout.HasAuto)
        {
            rects.Add(layout.Auto);
            Assert.Equal(layout.Action.Height, layout.Auto.Height, 3);
        }

        var all = rects.ToArray();
        AssertInside(deck, all);
        AssertDisjoint(all);
        Assert.Equal(DeckActions.PrimaryHeight * scale, layout.Action.Height, 3);
    }

    private static void AssertInside(Rect outer, Rect[] rects)
    {
        for (var index = 0; index < rects.Length; index++)
        {
            var rect = rects[index];
            Assert.True(rect.Width >= 0f && rect.Height >= 0f);
            Assert.True(rect.Min.X >= outer.Min.X - Tolerance && rect.Max.X <= outer.Max.X + Tolerance
                && rect.Min.Y >= outer.Min.Y - Tolerance && rect.Max.Y <= outer.Max.Y + Tolerance,
                index.ToString());
        }
    }

    internal static void AssertDisjoint(Rect[] rects)
    {
        for (var first = 0; first < rects.Length; first++)
        {
            for (var second = first + 1; second < rects.Length; second++)
            {
                Assert.False(Overlaps(rects[first], rects[second]), first + " overlaps " + second);
            }
        }
    }

    internal static bool Overlaps(Rect first, Rect second) =>
        first.Min.X < second.Max.X - Tolerance && second.Min.X < first.Max.X - Tolerance
        && first.Min.Y < second.Max.Y - Tolerance && second.Min.Y < first.Max.Y - Tolerance;
}
