using System;
using System.Numerics;
using Aetherphone.Apps.Casino.Originals;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HiLoCallLayoutTests
{
    public static TheoryData<float> Scales => new() { 0.75f, 1f, 1.25f, 1.5f };

    [Theory]
    [MemberData(nameof(Scales))]
    public void ThreeCallsKeepTheirNaturalWidthAndNeverOverlap(float scale)
    {
        var row = new Rect(new Vector2(12f * scale, 300f * scale), new Vector2(348f * scale, 400f * scale));
        ReadOnlySpan<float> naturals = stackalloc float[] { 123f * scale, 77f * scale, 90f * scale };
        Span<float> widths = stackalloc float[3];

        Assert.True(HiLoCallLayout.Distribute(naturals, row.Width, scale, widths));

        for (var index = 0; index < naturals.Length; index++)
        {
            var call = HiLoCallLayout.Call(row, widths, index, scale);
            Assert.True(call.Width >= naturals[index] - 0.01f);
            Assert.True(call.Height >= 44f * scale);
            Assert.True(call.Min.X >= row.Min.X - 0.01f && call.Max.X <= row.Max.X + 0.01f);
            if (index > 0)
            {
                Assert.True(HiLoCallLayout.Call(row, widths, index - 1, scale).Max.X <= call.Min.X + 0.01f);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void ATinyLabelStillGetsATouchSizedCall(float scale)
    {
        ReadOnlySpan<float> naturals = stackalloc float[] { 10f * scale, 10f * scale };
        Span<float> widths = stackalloc float[2];

        HiLoCallLayout.Distribute(naturals, 400f * scale, scale, widths);

        Assert.True(widths[0] >= HiLoCallLayout.MinimumWidth * scale);
        Assert.True(widths[1] >= HiLoCallLayout.MinimumWidth * scale);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheCardAndCallsAreCentredInTheStage(float scale)
    {
        var stage = new Rect(new Vector2(0f, 100f * scale), new Vector2(336f * scale, 500f * scale));
        var group = HiLoCallLayout.GroupHeight(160f * scale, 92f * scale, scale);
        var top = HiLoCallLayout.GroupTop(stage, group);

        Assert.True(top >= stage.Min.Y);
        Assert.True(top + group <= stage.Max.Y + 0.01f);
        Assert.Equal(stage.Max.Y - (top + group), top - stage.Min.Y, 2);
    }
}
