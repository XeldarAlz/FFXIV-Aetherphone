using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ChipRailLayoutTests
{
    [Theory]
    [InlineData(ChipRail.DefaultLabelPadding, 0.75f)]
    [InlineData(ChipRail.DefaultLabelPadding, 1f)]
    [InlineData(ChipRail.DefaultLabelPadding, 2.2f)]
    [InlineData(ChipRail.CompactLabelPadding, 0.75f)]
    [InlineData(ChipRail.CompactLabelPadding, 1f)]
    [InlineData(ChipRail.CompactLabelPadding, 2.2f)]
    public void ARailChipAlwaysFitsItsOwnLabel(float labelPadding, float scale)
    {
        var chipHeight = ChipRail.ChipHeight * scale;
        for (var labelWidth = 10f; labelWidth <= 160f; labelWidth += 10f)
        {
            var width = ChipRail.WidthFor(labelWidth * scale, labelPadding, scale);
            var chip = new Rect(Vector2.Zero, new Vector2(width, chipHeight));
            Assert.True(ChipRail.LabelRoom(chip) > labelWidth * scale);
        }
    }
}
