using System.Numerics;
using Aetherphone.Apps.Casino.Originals;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class OriginalsLayoutTests
{
    public static TheoryData<float> Scales => new() { 0.75f, 1f, 1.25f, 1.5f };

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheSignStripWorldAndStatusStackInsideSafeWithoutOverlap(float scale)
    {
        var safe = new Rect(new Vector2(12f * scale, 64f * scale), new Vector2(348f * scale, 520f * scale));
        var layout = OriginalsLayout.Compute(safe, true, 48f * scale, scale);

        Assert.True(layout.HasLadder);
        Assert.True(layout.Sign.Max.Y <= layout.Ladder.Min.Y);
        Assert.True(layout.Ladder.Max.Y <= layout.World.Min.Y);
        Assert.True(layout.World.Max.Y <= layout.Status.Min.Y + 0.01f);
        Assert.Equal(safe.Max.Y, layout.Status.Max.Y, 2);
        Assert.Equal(48f * scale, layout.Status.Height, 2);
        Assert.True(layout.World.Height > 0f);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void AGameWithoutAStripGivesItsBandToTheWorld(float scale)
    {
        var safe = new Rect(new Vector2(12f * scale, 64f * scale), new Vector2(348f * scale, 520f * scale));
        var withStrip = OriginalsLayout.Compute(safe, true, 48f * scale, scale);
        var without = OriginalsLayout.Compute(safe, false, 48f * scale, scale);

        Assert.False(without.HasLadder);
        Assert.True(without.World.Height > withStrip.World.Height);
        Assert.True(without.World.Min.Y >= without.Sign.Max.Y);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void ATallStatusShrinksTheWorldButNeverLeavesSafe(float scale)
    {
        var safe = new Rect(new Vector2(0f, 0f), new Vector2(336f * scale, 300f * scale));
        var layout = OriginalsLayout.Compute(safe, true, 900f * scale, scale);

        Assert.True(layout.World.Height >= 0f);
        Assert.True(layout.Status.Min.Y >= layout.World.Min.Y);
        Assert.True(layout.Status.Max.Y <= safe.Max.Y + 0.01f);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void StripChipsSitAfterTheCaptionAndInsideTheRowWithoutOverlap(float scale)
    {
        var rowLeft = 12f * scale;
        var rowRight = 348f * scale;
        var caption = 64f * scale;
        for (var count = 1; count <= OriginalsLadder.Capacity; count++)
        {
            var layout = LadderLayout.Compute(rowLeft, rowRight, caption, count, count - 1, scale);
            Assert.True(layout.Visible >= 1);
            Assert.True(layout.ChipsLeft >= rowLeft + caption - 0.01f);
            Assert.True(layout.First >= 0 && layout.First + layout.Visible <= count);
            for (var slot = 0; slot < layout.Visible; slot++)
            {
                var left = layout.ChipLeft(slot);
                Assert.True(left + layout.ChipWidth <= rowRight + 0.01f);
                if (slot > 0)
                {
                    Assert.True(layout.ChipLeft(slot - 1) + layout.ChipWidth <= left + 0.01f);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void KenoFooterKeepsTheStatusOnItsOwnLineAboveTouchSizedButtons(float scale)
    {
        var status = 18f * scale;
        var top = 400f * scale;
        var footer = new Rect(new Vector2(12f * scale, top),
            new Vector2(348f * scale, top + KenoFooterLayout.Height(status, scale)));
        KenoFooterLayout.Buttons(footer, status, 150f * scale, 100f * scale, scale, out var first, out var second);
        var statusRow = KenoFooterLayout.Status(footer, status);

        Assert.True(first.Height >= 44f * scale - 0.01f);
        Assert.Equal(first.Height, second.Height, 2);
        Assert.True(statusRow.Max.Y <= first.Min.Y);
        Assert.True(first.Max.X <= second.Min.X);
        Assert.True(first.Min.X >= footer.Min.X - 0.01f && second.Max.X <= footer.Max.X + 0.01f);
        Assert.True(second.Max.Y <= footer.Max.Y + 0.01f);
        Assert.Equal(150f * scale, first.Width, 2);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void KenoButtonsThatCannotFitShrinkInsideTheFooter(float scale)
    {
        var footer = new Rect(Vector2.Zero, new Vector2(200f * scale, 100f * scale));
        KenoFooterLayout.Buttons(footer, 18f * scale, 180f * scale, 120f * scale, scale, out var first, out var second);

        Assert.True(first.Min.X >= footer.Min.X - 0.01f);
        Assert.True(second.Max.X <= footer.Max.X + 0.01f);
        Assert.True(first.Max.X <= second.Min.X);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheFocusedStepIsAlwaysVisible(float scale)
    {
        for (var focus = 0; focus < 20; focus++)
        {
            var layout = LadderLayout.Compute(0f, 336f * scale, 60f * scale, 20, focus, scale);
            Assert.InRange(focus, layout.First, layout.First + layout.Visible - 1);
        }
    }
}
