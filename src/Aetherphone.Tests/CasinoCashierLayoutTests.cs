using System.Numerics;
using Aetherphone.Apps.Casino;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoCashierLayoutTests
{
    private const float Tolerance = 0.01f;
    private const float Left = 20f;
    private const float Top = 40f;
    private const float Caption = 16f;
    private const float Amount = 24f;
    private const float Footnote = 16f;
    private const float Heading = 19f;
    private const float Line = 18f;
    private const float AutoTitle = 19f;

    private static readonly float[] Scales = { 0.75f, 1f, 1.5f };
    private static readonly float[] DesignWidths = { 240f, 300f, 330f, 420f };

    private static CashierBlocks CashierText(float scale, float notice = 0f, float note = 0f) =>
        new(Caption * scale, Amount * scale, Footnote * scale, Footnote * scale, Heading * scale, Footnote * scale,
            Line * scale, notice * scale, note * scale);

    private static GetChipsBlocks SheetText(float scale, int needLines, int hintLines, float note = 0f) =>
        new(Line * scale, needLines * Footnote * scale, Footnote * scale, Amount * scale, Footnote * scale,
            AutoTitle * scale, hintLines * Footnote * scale, note * scale);

    [Fact]
    public void TheCashierStacksWithoutOverlapAtEveryScale()
    {
        for (var scaleIndex = 0; scaleIndex < Scales.Length; scaleIndex++)
        {
            var scale = Scales[scaleIndex];
            for (var widthIndex = 0; widthIndex < DesignWidths.Length; widthIndex++)
            {
                var width = DesignWidths[widthIndex] * scale;
                for (var tiles = 0; tiles <= 3; tiles++)
                {
                    AssertCashier(CashierLayout.Compute(Left, Top, width, CashierText(scale), true, true, tiles,
                        scale), width, scale);
                    AssertCashier(CashierLayout.Compute(Left, Top, width, CashierText(scale, 60f, 32f), true, true,
                        tiles, scale), width, scale);
                }
            }
        }
    }

    [Fact]
    public void ABlockedFloorKeepsOnlyTheBalancesAndCashOut()
    {
        const float scale = 1f;
        var layout = CashierLayout.Compute(Left, Top, 330f, CashierText(scale, 60f), false, true, 3, scale);

        Assert.True(layout.HasNotice);
        Assert.False(layout.HasBuy);
        Assert.False(layout.HasTiles);
        Assert.True(layout.HasCashOut);
        Assert.True(layout.Balances.Min.Y >= layout.Notice.Max.Y - Tolerance);
        Assert.True(layout.CashLine.Min.Y >= layout.Balances.Max.Y - Tolerance);
    }

    [Fact]
    public void WithoutABankrollTheCashierEndsAtBuyChips()
    {
        const float scale = 1f;
        var layout = CashierLayout.Compute(Left, Top, 330f, CashierText(scale), true, false, 3, scale);

        Assert.False(layout.HasCashOut);
        Assert.Equal(layout.Buy.Max.Y, layout.Bottom, 3);
    }

    [Fact]
    public void EveryCashierControlIsATouchTarget()
    {
        for (var scaleIndex = 0; scaleIndex < Scales.Length; scaleIndex++)
        {
            var scale = Scales[scaleIndex];
            var layout = CashierLayout.Compute(Left, Top, 330f * scale, CashierText(scale), true, true, 3, scale);
            var touch = Button.LargeHeight * scale - Tolerance;
            Assert.True(layout.Field.Height >= touch);
            Assert.True(layout.Tile(0).Height >= touch);
            Assert.True(layout.Buy.Height >= touch);
            Assert.True(layout.CashOut.Height >= touch);
        }
    }

    [Fact]
    public void TheGetChipsSheetStacksWithoutOverlapAtEveryScale()
    {
        for (var scaleIndex = 0; scaleIndex < Scales.Length; scaleIndex++)
        {
            var scale = Scales[scaleIndex];
            for (var widthIndex = 0; widthIndex < DesignWidths.Length; widthIndex++)
            {
                var width = DesignWidths[widthIndex] * scale;
                for (var tiles = 0; tiles <= 3; tiles++)
                {
                    AssertSheet(GetChipsLayout.Compute(Left, Top, width, SheetText(scale, 1, 2), tiles, scale), width,
                        scale);
                    AssertSheet(GetChipsLayout.Compute(Left, Top, width, SheetText(scale, 2, 4, 32f), tiles, scale),
                        width, scale);
                    AssertSheet(GetChipsLayout.Compute(Left, Top, width, SheetText(scale, 0, 1), tiles, scale), width,
                        scale);
                }
            }
        }
    }

    [Fact]
    public void TheAutoTopUpToggleSitsBesideItsText()
    {
        for (var scaleIndex = 0; scaleIndex < Scales.Length; scaleIndex++)
        {
            var scale = Scales[scaleIndex];
            var width = 330f * scale;
            var layout = GetChipsLayout.Compute(Left, Top, width, SheetText(scale, 1, 3), 3, scale);
            Assert.True(layout.AutoText.Max.X <= layout.AutoToggle.Min.X - Tolerance);
            Assert.Equal(width - Metrics.Size.ToggleWidth * scale - GetChipsLayout.Gap * 2f * scale,
                GetChipsLayout.AutoTextWidth(width, scale), 3);
            AssertInside(layout.AutoRow, layout.AutoText);
            AssertInside(layout.AutoRow, layout.AutoToggle);
        }
    }

    private static void AssertCashier(in CashierLayout layout, float width, float scale)
    {
        var regions = new List<Rect> { layout.Balances };
        if (layout.HasNotice)
        {
            regions.Add(layout.Notice);
        }

        if (layout.HasNote)
        {
            regions.Add(layout.Note);
        }

        if (layout.HasBuy)
        {
            regions.Add(layout.BuyHeading);
            regions.Add(layout.Field);
            regions.Add(layout.Status);
            regions.Add(layout.Buy);
        }

        for (var index = 0; index < layout.TileCount; index++)
        {
            var tile = layout.Tile(index);
            Assert.True(tile.Width > 0f);
            regions.Add(tile);
        }

        if (layout.HasCashOut)
        {
            regions.Add(layout.CashLine);
            regions.Add(layout.CashOut);
            Assert.True(layout.Divider > (layout.HasBuy ? layout.Buy.Max.Y : layout.Balances.Max.Y) - Tolerance);
            Assert.True(layout.Divider < layout.CashLine.Min.Y + Tolerance);
        }

        var bounds = new Rect(new Vector2(Left, Top), new Vector2(Left + width, layout.Bottom));
        for (var index = 0; index < regions.Count; index++)
        {
            AssertInside(bounds, regions[index]);
        }

        AssertDisjoint(regions);
        AssertInside(layout.Balances, layout.WalletColumn);
        AssertInside(layout.Balances, layout.ChipsColumn);
        AssertInside(layout.Balances, layout.RateLine);
        Assert.True(layout.WalletColumn.Max.X <= layout.ArrowSlot.Min.X + Tolerance);
        Assert.True(layout.ArrowSlot.Max.X <= layout.ChipsColumn.Min.X + Tolerance);
        Assert.True(layout.RateLine.Min.Y >= layout.WalletColumn.Max.Y - Tolerance);
        Assert.True(layout.Bottom > Top);
    }

    private static void AssertSheet(in GetChipsLayout layout, float width, float scale)
    {
        var regions = new List<Rect> { layout.WalletRow, layout.ChipsRow, layout.Field, layout.Buy, layout.AutoRow };
        if (layout.HasNeed)
        {
            regions.Add(layout.Need);
        }

        if (layout.HasNote)
        {
            regions.Add(layout.Note);
        }

        for (var index = 0; index < layout.TileCount; index++)
        {
            var tile = layout.Tile(index);
            Assert.True(tile.Height >= Button.LargeHeight * scale - Tolerance);
            regions.Add(tile);
        }

        var bounds = new Rect(new Vector2(Left, Top), new Vector2(Left + width, layout.Bottom));
        for (var index = 0; index < regions.Count; index++)
        {
            AssertInside(bounds, regions[index]);
        }

        AssertDisjoint(regions);
        Assert.True(layout.Field.Height >= Button.LargeHeight * scale - Tolerance);
        Assert.True(layout.Buy.Height >= Button.LargeHeight * scale - Tolerance);
        Assert.True(layout.AutoRow.Height >= Button.LargeHeight * scale - Tolerance);
    }

    private static void AssertInside(Rect outer, Rect inner)
    {
        Assert.True(inner.Min.X >= outer.Min.X - Tolerance, $"{inner} left of {outer}");
        Assert.True(inner.Max.X <= outer.Max.X + Tolerance, $"{inner} right of {outer}");
        Assert.True(inner.Min.Y >= outer.Min.Y - Tolerance, $"{inner} above {outer}");
        Assert.True(inner.Max.Y <= outer.Max.Y + Tolerance, $"{inner} below {outer}");
    }

    private static void AssertDisjoint(List<Rect> regions)
    {
        for (var first = 0; first < regions.Count; first++)
        {
            for (var second = first + 1; second < regions.Count; second++)
            {
                var earlier = regions[first];
                var later = regions[second];
                var overlapX = MathF.Min(earlier.Max.X, later.Max.X) - MathF.Max(earlier.Min.X, later.Min.X);
                var overlapY = MathF.Min(earlier.Max.Y, later.Max.Y) - MathF.Max(earlier.Min.Y, later.Min.Y);
                Assert.False(overlapX > Tolerance && overlapY > Tolerance, $"{earlier} overlaps {later}");
            }
        }
    }
}
