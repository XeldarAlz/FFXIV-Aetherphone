using System.Numerics;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LedgerEntryLayoutTests
{
    private const float PageWidth = 328f;
    private const float Epsilon = 0.01f;

    public static TheoryData<float> Scales => new() { 0.75f, 1f, 1.25f, 1.5f };

    [Theory]
    [MemberData(nameof(Scales))]
    public void ShortButtonsShareOneRowUnderTheStateLine(float scale)
    {
        var width = PageWidth * scale;
        var title = 17f * scale;
        var parties = 15f * scale;
        var state = 15f * scale;
        var confirm = 110f * scale;
        var dispute = 120f * scale;
        var rows = LedgerEntryLayout.ButtonRows(width - LedgerEntryLayout.Pad * 2f * scale, confirm, dispute, scale);
        Assert.Equal(1, rows);

        var card = new Rect(Vector2.Zero,
            new Vector2(width, LedgerEntryLayout.Height(title, parties, state, rows, scale)));
        var disputeRect = LedgerEntryLayout.RowButton(card, card.Max.X - LedgerEntryLayout.Pad * scale, dispute, scale);
        var confirmRect = LedgerEntryLayout.RowButton(card,
            disputeRect.Min.X - LedgerEntryLayout.ButtonGap * scale, confirm, scale);
        var stateBottom = LedgerEntryLayout.StateTop(card.Min.Y, title, parties, scale) + state;

        Assert.True(confirmRect.Min.X >= card.Min.X + LedgerEntryLayout.Pad * scale - Epsilon);
        Assert.True(confirmRect.Max.X <= disputeRect.Min.X + Epsilon);
        Assert.True(confirmRect.Min.Y >= stateBottom - Epsilon);
        Assert.True(disputeRect.Max.Y <= card.Max.Y - LedgerEntryLayout.Pad * scale + Epsilon);
        Assert.True(disputeRect.Height >= LedgerEntryLayout.Touch * scale - Epsilon);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void LongButtonsStackFullWidthAndStayInsideTheCard(float scale)
    {
        var width = PageWidth * scale;
        var title = 17f * scale;
        var parties = 15f * scale;
        var state = 15f * scale;
        var rows = LedgerEntryLayout.ButtonRows(width - LedgerEntryLayout.Pad * 2f * scale, 120f * scale,
            260f * scale, scale);
        Assert.Equal(2, rows);

        var card = new Rect(Vector2.Zero,
            new Vector2(width, LedgerEntryLayout.Height(title, parties, state, rows, scale)));
        var first = LedgerEntryLayout.StackedButton(card, 0, rows, scale);
        var second = LedgerEntryLayout.StackedButton(card, 1, rows, scale);
        var stateBottom = LedgerEntryLayout.StateTop(card.Min.Y, title, parties, scale) + state;

        Assert.True(first.Min.Y >= stateBottom - Epsilon);
        Assert.True(first.Max.Y <= second.Min.Y + Epsilon);
        Assert.True(second.Max.Y <= card.Max.Y + Epsilon);
        Assert.True(first.Width >= 260f * scale);
        Assert.Equal(0, LedgerEntryLayout.ButtonRows(width, 0f, 0f, scale));
        Assert.Equal(1, LedgerEntryLayout.ButtonRows(width, 0f, 300f * scale, scale));
    }
}
