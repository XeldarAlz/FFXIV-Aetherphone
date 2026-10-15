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

    private static readonly int[] TileCounts = { 0, 1, 4 };

    private static CashierBlocks CashierText(float scale, float notice = 0f, int statusLines = 1, int noteLines = 0,
        int errorLines = 0, int hintLines = 2) =>
        new(notice * scale, Footnote * scale, Amount * scale, Footnote * scale, Footnote * scale, Heading * scale,
            CashierLayout.FieldHeightFor(Amount * 1.3f * scale, scale), statusLines * Footnote * scale,
            noteLines * Footnote * scale, errorLines * Footnote * scale, (AutoTitle + hintLines * Footnote) * scale,
            (AutoTitle + Footnote) * scale);

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
                for (var tileIndex = 0; tileIndex < TileCounts.Length; tileIndex++)
                {
                    var tiles = TileCounts[tileIndex];
                    AssertCashier(CashierLayout.Compute(Left, Top, width, CashierText(scale), true, tiles, scale),
                        width);
                    AssertCashier(CashierLayout.Compute(Left, Top, width, CashierText(scale, 60f, 2, 2, 3, 4), true,
                        tiles, scale), width);
                    AssertCashier(CashierLayout.Compute(Left, Top, width, CashierText(scale, 60f, 3), false, tiles,
                        scale), width);
                }
            }
        }
    }

    [Fact]
    public void ABlockedFloorDropsTheModeSegment()
    {
        const float scale = 1f;
        var layout = CashierLayout.Compute(Left, Top, 330f, CashierText(scale, 60f), false, 4, scale);

        Assert.True(layout.HasNotice);
        Assert.False(layout.HasSegment);
        Assert.True(layout.Balances.Card.Min.Y >= layout.Notice.Max.Y - Tolerance);
        Assert.True(layout.Amount.Card.Min.Y >= layout.Balances.Card.Max.Y - Tolerance);
    }

    [Fact]
    public void TheModeSegmentSitsBetweenTheBalancesAndTheAmount()
    {
        const float scale = 1f;
        var layout = CashierLayout.Compute(Left, Top, 330f, CashierText(scale), true, 4, scale);

        Assert.True(layout.HasSegment);
        Assert.True(layout.Segment.Min.Y >= layout.Balances.Card.Max.Y - Tolerance);
        Assert.True(layout.Amount.Card.Min.Y >= layout.Segment.Max.Y - Tolerance);
        Assert.True(layout.Extras.Card.Min.Y >= layout.Amount.Card.Max.Y - Tolerance);
        Assert.Equal(layout.Extras.Card.Max.Y, layout.Bottom, 3);
    }

    [Fact]
    public void QuickTilesShareOneRowEvenly()
    {
        const float scale = 1f;
        var layout = CashierLayout.Compute(Left, Top, 330f, CashierText(scale), true, 4, scale).Amount;

        Assert.Equal(4, layout.TileCount);
        Assert.Equal(layout.Tile(0).Width, layout.Tile(3).Width, 3);
        Assert.Equal(layout.TileRow.Max.X, layout.Tile(3).Max.X, 3);
        var single = CashierLayout.Compute(Left, Top, 330f, CashierText(scale), true, 1, scale).Amount;
        Assert.Equal(single.TileRow.Width, single.Tile(0).Width, 3);
    }

    [Fact]
    public void TheFieldGrowsWithTheTextSize()
    {
        Assert.Equal(CashierLayout.FieldHeight, CashierLayout.FieldHeightFor(10f, 1f), 3);
        Assert.True(CashierLayout.FieldHeightFor(60f, 1f) > CashierLayout.FieldHeight);
    }

    [Fact]
    public void EveryCashierControlIsATouchTarget()
    {
        for (var scaleIndex = 0; scaleIndex < Scales.Length; scaleIndex++)
        {
            var scale = Scales[scaleIndex];
            var layout = CashierLayout.Compute(Left, Top, 330f * scale, CashierText(scale), true, 4, scale);
            var touch = Button.LargeHeight * scale - Tolerance;
            Assert.True(layout.Segment.Height >= touch);
            Assert.True(layout.Amount.Field.Height >= touch);
            Assert.True(layout.Amount.Tile(0).Height >= touch);
            Assert.True(layout.Amount.Primary.Height >= touch);
            Assert.True(layout.Extras.Auto.Row.Height >= touch);
            Assert.True(layout.Extras.Limits.Row.Height >= touch);
        }
    }

    [Fact]
    public void ExtrasTextNeverRunsUnderItsTrailingControl()
    {
        for (var scaleIndex = 0; scaleIndex < Scales.Length; scaleIndex++)
        {
            var scale = Scales[scaleIndex];
            var width = 330f * scale;
            var extras = CashierLayout.Compute(Left, Top, width, CashierText(scale), true, 4, scale).Extras;
            Assert.True(extras.Auto.Text.Max.X <= extras.Auto.Trailing.Min.X - Tolerance);
            Assert.True(extras.Limits.Text.Max.X <= extras.Limits.Trailing.Min.X - Tolerance);
            Assert.Equal(CashierLayout.AutoTextWidth(width, scale), extras.Auto.Text.Width, 2);
            Assert.Equal(CashierLayout.LimitsTextWidth(width, scale), extras.Limits.Text.Width, 2);
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

    private static void AssertCashier(in CashierLayout layout, float width)
    {
        var cards = new List<Rect> { layout.Balances.Card, layout.Amount.Card, layout.Extras.Card };
        if (layout.HasNotice)
        {
            cards.Add(layout.Notice);
        }

        if (layout.HasSegment)
        {
            cards.Add(layout.Segment);
        }

        var bounds = new Rect(new Vector2(Left, Top), new Vector2(Left + width, layout.Bottom));
        for (var index = 0; index < cards.Count; index++)
        {
            AssertInside(bounds, cards[index]);
        }

        AssertDisjoint(cards);
        AssertBalances(layout.Balances);
        AssertAmount(layout.Amount);
        AssertExtras(layout.Extras);
        Assert.True(layout.Bottom > Top);
    }

    private static void AssertBalances(in CashierBalancesLayout balances)
    {
        AssertInside(balances.Card, balances.WalletColumn);
        AssertInside(balances.Card, balances.ChipsColumn);
        AssertInside(balances.Card, balances.Exchange);
        AssertInside(balances.Card, balances.RateRow);
        Assert.True(balances.WalletColumn.Width > 0f);
        Assert.True(balances.WalletColumn.Max.X <= balances.Exchange.Min.X + Tolerance);
        Assert.True(balances.Exchange.Max.X <= balances.ChipsColumn.Min.X + Tolerance);
        Assert.True(balances.RateRow.Min.Y >= balances.WalletColumn.Max.Y - Tolerance);
    }

    private static void AssertAmount(in CashierAmountLayout amount)
    {
        var regions = new List<Rect> { amount.Heading, amount.Field, amount.Status, amount.Primary };
        if (amount.HasNote)
        {
            regions.Add(amount.Note);
        }

        if (amount.HasError)
        {
            regions.Add(amount.Error);
        }

        for (var index = 0; index < amount.TileCount; index++)
        {
            var tile = amount.Tile(index);
            Assert.True(tile.Width > 0f);
            regions.Add(tile);
        }

        for (var index = 0; index < regions.Count; index++)
        {
            AssertInside(amount.Card, regions[index]);
        }

        AssertDisjoint(regions);
    }

    private static void AssertExtras(in CashierExtrasLayout extras)
    {
        AssertInside(extras.Card, extras.Auto.Row);
        AssertInside(extras.Card, extras.Limits.Row);
        AssertDisjoint(new List<Rect> { extras.Auto.Row, extras.Limits.Row });
        AssertRow(extras.Auto);
        AssertRow(extras.Limits);
        Assert.Equal(extras.Auto.Row.Max.Y, extras.Divider, 3);
    }

    private static void AssertRow(in CashierRowLayout row)
    {
        AssertInside(row.Row, row.Text);
        AssertInside(row.Row, row.Trailing);
        Assert.True(row.IconCenter.X < row.Text.Min.X);
        Assert.True(row.IconCenter.Y >= row.Row.Min.Y && row.IconCenter.Y <= row.Row.Max.Y);
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
