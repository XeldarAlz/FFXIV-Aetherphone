using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CaseArtSliceTests
{
    private const float Tolerance = 1e-3f;
    private const float CanvasBodyWidth = 1000f;
    private const float CanvasBodyHeight = 2255f;
    private const float CanvasMargin = 250f;
    private const float CanvasCorner = 161.08f;

    [Fact]
    public void TemplateAspectBodyMapsEveryRunAtOneScale()
    {
        Span<float> columns = stackalloc float[4];
        Span<float> rows = stackalloc float[4];
        CaseArt.SliceEdges(0f, CanvasBodyWidth, 1f, columns);
        CaseArt.SliceEdges(0f, CanvasBodyHeight, 1f, rows);
        Assert.Equal(-CanvasMargin, columns[0], Tolerance);
        Assert.Equal(CanvasCorner, columns[1], Tolerance);
        Assert.Equal(CanvasBodyWidth - CanvasCorner, columns[2], Tolerance);
        Assert.Equal(CanvasBodyWidth + CanvasMargin, columns[3], Tolerance);
        Assert.Equal(CanvasBodyHeight - 2f * CanvasCorner, rows[2] - rows[1], Tolerance);
        Assert.Equal(CanvasBodyWidth - 2f * CanvasCorner, columns[2] - columns[1], Tolerance);
    }

    [Theory]
    [InlineData(82f, 156f)]
    [InlineData(148f, 148f)]
    [InlineData(61.5f, 117f)]
    public void OffTemplateBodyKeepsCornersAndMarginsAtBodyScale(float width, float height)
    {
        var scale = width / CanvasBodyWidth;
        Span<float> columns = stackalloc float[4];
        Span<float> rows = stackalloc float[4];
        CaseArt.SliceEdges(0f, width, scale, columns);
        CaseArt.SliceEdges(0f, height, scale, rows);
        Assert.Equal((CanvasMargin + CanvasCorner) * scale, rows[1] - rows[0], Tolerance);
        Assert.Equal((CanvasMargin + CanvasCorner) * scale, rows[3] - rows[2], Tolerance);
        Assert.Equal((CanvasMargin + CanvasCorner) * scale, columns[1] - columns[0], Tolerance);
        Assert.Equal(height - 2f * CanvasCorner * scale, rows[2] - rows[1], Tolerance);
        Assert.True(rows[2] > rows[1]);
        Assert.True(columns[2] > columns[1]);
    }

    [Fact]
    public void RunShorterThanTwoCornersSplitsAtTheMiddle()
    {
        Span<float> edges = stackalloc float[4];
        CaseArt.SliceEdges(10f, 30f, 1f, edges);
        Assert.Equal(20f, edges[1], Tolerance);
        Assert.Equal(20f, edges[2], Tolerance);
        Assert.Equal(10f - CanvasMargin, edges[0], Tolerance);
        Assert.Equal(30f + CanvasMargin, edges[3], Tolerance);
    }
}
