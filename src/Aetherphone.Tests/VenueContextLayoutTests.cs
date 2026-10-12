using System.Numerics;
using Aetherphone.Apps.Casino.Venue;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class VenueContextLayoutTests
{
    private const float PhoneWidth = 330f;
    private const float HeadlineUnits = 22f;
    private const float FootnoteUnits = 18f;
    private const float Epsilon = 0.01f;

    public static TheoryData<float> Scales => new() { 0.75f, 1f, 1.5f };

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheTableCardStacksArtTextCodeAndButtonsWithoutOverlap(float scale)
    {
        var width = PhoneWidth * scale;
        var origin = new Vector2(40f, 120f);
        var headline = HeadlineUnits * scale;
        var footnote = FootnoteUnits * scale;
        var layout = VenueContextLayout.Table(origin, width, scale, headline, footnote, footnote, false);
        var card = layout.Card;
        var pad = VenueContextLayout.Pad * scale;
        var touch = VenueContextLayout.Touch * scale;

        Assert.Equal(origin, card.Min);
        Assert.Equal(width, card.Width, 2);
        Assert.True(layout.Art.Min.X >= card.Min.X + pad - Epsilon);
        Assert.True(layout.Art.Min.Y >= card.Min.Y + pad - Epsilon);
        Assert.True(layout.TextLeft > layout.Art.Max.X);
        Assert.True(layout.TextLeft + layout.TextWidth <= card.Max.X - pad + Epsilon);
        Assert.True(layout.NameTop >= card.Min.Y + pad - Epsilon);
        Assert.True(layout.SeatsTop >= layout.NameTop + headline);
        Assert.True(layout.CurrencyTop >= layout.SeatsTop + footnote);

        var textBottom = layout.CurrencyTop + footnote;
        Assert.True(layout.CodeTop >= textBottom);
        Assert.True(layout.CodeTop >= layout.Art.Max.Y);
        for (var index = 0; index < CasinoRoomCodes.Length; index++)
        {
            var cell = layout.Cell(index);
            Assert.True(cell.Width > 0f && cell.Height > 0f);
            Assert.True(cell.Min.X >= card.Min.X + pad - Epsilon);
            Assert.True(cell.Max.X <= card.Max.X - pad + Epsilon);
            if (index > 0)
            {
                Assert.True(cell.Min.X >= layout.Cell(index - 1).Max.X);
            }
        }

        var cellsBottom = layout.Cell(0).Max.Y;
        Assert.True(layout.Door.Min.Y >= cellsBottom);
        Assert.True(layout.Ledger.Min.Y >= cellsBottom);
        Assert.True(layout.Door.Height >= touch - Epsilon);
        Assert.True(layout.Ledger.Height >= touch - Epsilon);
        Assert.True(layout.Door.Width >= touch);
        Assert.True(layout.Ledger.Width >= touch);
        Assert.True(layout.Door.Max.X < layout.Ledger.Min.X);
        Assert.True(layout.Door.Min.X >= card.Min.X + pad - Epsilon);
        Assert.True(layout.Ledger.Max.X <= card.Max.X - pad + Epsilon);
        Assert.True(layout.Door.Max.Y <= card.Max.Y - pad + Epsilon);
        Assert.True(layout.Ledger.Max.Y <= card.Max.Y - pad + Epsilon);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void ABookCardSpansOneButtonAcrossTheCard(float scale)
    {
        var width = PhoneWidth * scale;
        var origin = new Vector2(40f, 120f);
        var layout = VenueContextLayout.Table(origin, width, scale, HeadlineUnits * scale, FootnoteUnits * scale,
            FootnoteUnits * scale, true);
        var card = layout.Card;
        var pad = VenueContextLayout.Pad * scale;

        Assert.Equal(card.Min.X + pad, layout.Door.Min.X, 2);
        Assert.Equal(card.Max.X - pad, layout.Door.Max.X, 2);
        Assert.True(layout.Door.Height >= VenueContextLayout.Touch * scale - Epsilon);
        Assert.True(layout.Door.Min.Y >= layout.Cell(0).Max.Y);
        Assert.Equal(0f, layout.Ledger.Width);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void WrappedTextGrowsTheCardAndPushesTheCodeDown(float scale)
    {
        var width = PhoneWidth * scale;
        var origin = new Vector2(40f, 120f);
        var headline = HeadlineUnits * scale;
        var footnote = FootnoteUnits * scale;
        var single = VenueContextLayout.Table(origin, width, scale, headline, footnote, footnote, false);
        var wrapped = VenueContextLayout.Table(origin, width, scale, headline * 2f, footnote, footnote * 3f, false);

        Assert.Equal(single.TextWidth, VenueContextLayout.TextWidth(width, scale), 2);
        Assert.True(wrapped.SeatsTop >= wrapped.NameTop + headline * 2f);
        Assert.True(wrapped.CurrencyTop >= wrapped.SeatsTop + footnote);
        Assert.True(wrapped.CodeTop >= wrapped.CurrencyTop + footnote * 3f);
        Assert.True(wrapped.Card.Height > single.Card.Height);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheCodeCellsNeverOutgrowTheirCap(float scale)
    {
        var width = VenueContextLayout.CellWidth(PhoneWidth * scale, scale);
        Assert.True(width <= VenueContextLayout.CellMax * scale + Epsilon);
        Assert.True(width > VenueContextLayout.Touch * scale * 0.5f);
        Assert.True(VenueContextLayout.CellWidth(120f * scale, scale) < width);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheHostCardKeepsItsButtonsUnderTheSummary(float scale)
    {
        var width = PhoneWidth * scale;
        var origin = new Vector2(12f, 300f);
        var title = HeadlineUnits * scale;
        var summary = FootnoteUnits * scale * 2f;
        var pad = VenueContextLayout.Pad * scale;
        var touch = VenueContextLayout.Touch * scale;
        for (var buttons = 1; buttons <= 2; buttons++)
        {
            var layout = VenueContextLayout.Host(origin, width, scale, title, summary, buttons);
            var card = layout.Card;
            Assert.Equal(width, card.Width, 2);
            Assert.True(layout.Title.Y >= card.Min.Y + pad - Epsilon);
            Assert.True(layout.Summary.Y >= layout.Title.Y + title);
            Assert.True(layout.TextWidth <= width - pad * 2f + Epsilon);
            Assert.True(layout.Primary.Min.Y >= layout.Summary.Y + summary);
            Assert.True(layout.Primary.Height >= touch - Epsilon);
            Assert.True(layout.Primary.Width >= touch);
            Assert.True(layout.Primary.Max.Y <= card.Max.Y - pad + Epsilon);
            if (buttons == 1)
            {
                Assert.Equal(card.Max.X - pad, layout.Primary.Max.X, 2);
                continue;
            }

            Assert.True(layout.Secondary.Min.X > layout.Primary.Max.X);
            Assert.True(layout.Secondary.Width >= touch);
            Assert.True(layout.Secondary.Height >= touch - Epsilon);
            Assert.Equal(card.Max.X - pad, layout.Secondary.Max.X, 2);
            Assert.Equal(layout.Primary.Width, layout.Secondary.Width, 2);
        }
    }
}
