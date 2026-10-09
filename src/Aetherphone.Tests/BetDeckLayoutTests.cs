using System.Numerics;
using Aetherphone.Apps.Casino.Machines;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BetDeckLayoutTests
{
    private const float Tolerance = 0.01f;

    private static readonly float[] Scales = { 0.75f, 1f, 1.5f };
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
    public void TheGearHasItsOwnSlotBesideTheModePill()
    {
        var deck = DeckOf(298f, true, false, 1f);
        var layout = BetDeckLayout.Compute(deck, true, false, true, 1f);
        Assert.True(layout.HasMode);
        Assert.True(layout.Gear.Min.X >= layout.Mode.Max.X + BetDeckLayout.Gap - Tolerance);
        Assert.True(layout.Action.Min.X >= layout.Gear.Max.X + BetDeckLayout.Gap - Tolerance);
        Assert.True(layout.Gear.Width >= BetDeckLayout.GearSize - Tolerance);
        Assert.True(layout.Action.Width >= BetDeckLayout.PrimaryMinWidth - Tolerance);
    }

    [Fact]
    public void WithoutAutoThePrimaryTakesTheWholeRow()
    {
        var deck = DeckOf(320f, false, false, 1f);
        var layout = BetDeckLayout.Compute(deck, false, false, false, 1f);
        Assert.False(layout.HasMode);
        Assert.Equal(deck.Width - BetDeckLayout.Pad * 2f, layout.Action.Width, 3);
        Assert.Equal(deck.Max.Y - BetDeckLayout.Pad, layout.Action.Max.Y, 3);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void TheBonusRowNeverOverlaps(float scale)
    {
        for (var widthIndex = 0; widthIndex < DesignWidths.Length; widthIndex++)
        {
            var deck = DeckOf(DesignWidths[widthIndex] * scale, true, false, scale);
            var knob = BetDeckLayout.Compute(deck, true, false, true, scale).Knob;
            var row = MachineBonusRow.Compute(knob, 8f * scale);
            var rects = new[] { row.Turbo, row.Ante, row.Buy };
            AssertInside(knob, rects);
            AssertDisjoint(rects);
            Assert.True(row.Info.Min.X >= row.Turbo.Max.X - Tolerance);
        }
    }

    private static Rect DeckOf(float width, bool knob, bool fixedAmount, float scale)
    {
        var height = BetDeckLayout.DeckHeightFor(knob, fixedAmount) * scale;
        return new Rect(new Vector2(16f, 600f), new Vector2(16f + width, 600f + height));
    }

    private static void AssertClean(Rect deck, bool knob, bool fixedAmount, bool auto, float scale)
    {
        var layout = BetDeckLayout.Compute(deck, knob, fixedAmount, auto, scale);
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

        if (layout.HasMode)
        {
            rects.Add(layout.Mode);
            rects.Add(layout.Gear);
            Assert.True(layout.Gear.Width > 0f);
            Assert.True(layout.Mode.Width > 0f);
        }

        var all = rects.ToArray();
        AssertInside(deck, all);
        AssertDisjoint(all);
        Assert.True(layout.Action.Width > deck.Width * 0.25f, layout.Action.Width.ToString());
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
