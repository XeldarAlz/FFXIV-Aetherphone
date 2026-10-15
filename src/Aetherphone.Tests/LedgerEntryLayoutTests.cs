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
    public void ARowHoldsItsTallestColumnInsideThePadding(float scale)
    {
        var name = 17f * scale;
        var meta = 15f * scale;
        var pill = LedgerEntryLayout.PillHeight(15f * scale, scale);
        var height = LedgerEntryLayout.Height(name, meta, name, pill, scale);
        var pad = LedgerEntryLayout.PadY * scale * 2f;

        Assert.True(height - pad >= LedgerEntryLayout.Icon * scale - Epsilon);
        Assert.True(height - pad >= name + LedgerEntryLayout.LineGap * scale + meta - Epsilon);
        Assert.True(height - pad >= name + LedgerEntryLayout.PillGap * scale + pill - Epsilon);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheNameColumnStopsShortOfTheAmountColumn(float scale)
    {
        var row = new Rect(Vector2.Zero, new Vector2(PageWidth * scale, 60f * scale));
        var valueWidth = 96f * scale;
        var icon = LedgerEntryLayout.IconCenter(row, scale);
        var left = LedgerEntryLayout.TextLeft(row, scale);
        var right = left + LedgerEntryLayout.TextWidth(row, valueWidth, scale);

        Assert.True(icon.X + LedgerEntryLayout.Icon * 0.5f * scale < left);
        Assert.True(right + LedgerEntryLayout.Gap * scale <= LedgerEntryLayout.ValueRight(row, scale) - valueWidth
            + Epsilon);
        Assert.Equal(row.Max.X - LedgerEntryLayout.PadX * scale, LedgerEntryLayout.ValueRight(row, scale), 3);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void AnOverlongAmountNeverLeavesANegativeNameColumn(float scale)
    {
        var row = new Rect(Vector2.Zero, new Vector2(PageWidth * scale, 60f * scale));

        Assert.True(LedgerEntryLayout.TextWidth(row, PageWidth * 2f * scale, scale) >= 1f);
    }
}
