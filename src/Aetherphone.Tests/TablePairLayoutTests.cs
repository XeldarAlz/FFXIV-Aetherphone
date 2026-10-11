using Aetherphone.Apps.Casino.Tables;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TablePairLayoutTests
{
    private const float PageWidth = 328f;
    private const float Height = 44f;

    public static TheoryData<float> Scales => new() { 0.75f, 0.9f, 1f, 1.25f, 1.5f };

    [Theory]
    [MemberData(nameof(Scales))]
    public void PairsShareARowOnlyWhenBothLabelsFitTheirHalf(float scale)
    {
        var width = PageWidth * scale;
        var height = Height * scale;

        Assert.True(TablePairLayout.SideBySide(width, height, 80f * scale, 90f * scale, scale));
        Assert.False(TablePairLayout.SideBySide(width, height, 80f * scale, 130f * scale, scale));
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void CellsNeverOverlapAndStayInsideTheirArea(float scale)
    {
        var width = PageWidth * scale;
        var height = Height * scale;
        for (var arrangement = 0; arrangement < 2; arrangement++)
        {
            var sideBySide = arrangement == 0;
            var total = TablePairLayout.Height(sideBySide, height, scale);
            var first = TablePairLayout.Cell(10f, 20f, width, height, 0, sideBySide, scale);
            var second = TablePairLayout.Cell(10f, 20f, width, height, 1, sideBySide, scale);

            Assert.True(first.Max.X <= second.Min.X || first.Max.Y <= second.Min.Y);
            Assert.True(second.Max.X <= 10f + width + 0.01f);
            Assert.True(second.Max.Y <= 20f + total + 0.01f);
            Assert.Equal(height, first.Height, 2);
            Assert.Equal(height, second.Height, 2);
            Assert.True(first.Width >= height);
        }
    }
}
