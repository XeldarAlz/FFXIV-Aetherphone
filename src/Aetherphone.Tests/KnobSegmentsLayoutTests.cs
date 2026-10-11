using System;
using Aetherphone.Apps.Casino.Stage;
using Xunit;

namespace Aetherphone.Tests;

public sealed class KnobSegmentsLayoutTests
{
    public static TheoryData<float> Scales => new() { 0.75f, 1f, 1.25f, 1.5f };

    [Theory]
    [MemberData(nameof(Scales))]
    public void SegmentsThatFitFillTheRowAndKeepEveryLabelWhole(float scale)
    {
        ReadOnlySpan<float> labels = stackalloc float[] { 20f * scale, 70f * scale, 38f * scale };
        Span<float> widths = stackalloc float[3];
        var row = 336f * scale;

        Assert.True(KnobSegmentsLayout.Distribute(labels, row, scale, widths));

        var total = 0f;
        for (var index = 0; index < labels.Length; index++)
        {
            Assert.True(widths[index] >= labels[index] + KnobSegmentsLayout.LabelPad * 2f * scale - 0.01f);
            Assert.True(widths[index] >= KnobSegmentsLayout.MinimumSegment * scale - 0.01f);
            total += widths[index];
        }

        Assert.Equal(row, total, 2);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void ALongerLabelGetsAWiderSegment(float scale)
    {
        ReadOnlySpan<float> labels = stackalloc float[] { 30f * scale, 90f * scale };
        Span<float> widths = stackalloc float[2];

        KnobSegmentsLayout.Distribute(labels, 300f * scale, scale, widths);

        Assert.True(widths[1] > widths[0]);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void SegmentsThatCannotFitReportItAndStayInsideTheRow(float scale)
    {
        ReadOnlySpan<float> labels = stackalloc float[] { 120f * scale, 120f * scale, 120f * scale };
        Span<float> widths = stackalloc float[3];
        var row = 300f * scale;

        Assert.False(KnobSegmentsLayout.Fits(labels, row, scale));
        Assert.False(KnobSegmentsLayout.Distribute(labels, row, scale, widths));

        var total = 0f;
        for (var index = 0; index < labels.Length; index++)
        {
            total += widths[index];
        }

        Assert.True(total <= row + 0.01f);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void ShortLabelsStillMeetTheTouchMinimum(float scale)
    {
        ReadOnlySpan<float> labels = stackalloc float[] { 4f * scale, 6f * scale };

        Assert.Equal(KnobSegmentsLayout.MinimumSegment * scale * 2f, KnobSegmentsLayout.Total(labels, scale), 2);
    }
}
