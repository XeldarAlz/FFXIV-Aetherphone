using System.Numerics;
using Aetherphone.Apps.Games.Framework.World;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LaneProjectionTests
{
    private static readonly Rect View = new(new Vector2(20f, 40f), new Vector2(380f, 760f));

    [Fact]
    public void ScaleShrinksMonotonicallyWithDepth()
    {
        var projection = Runner();

        var previous = float.MaxValue;
        for (var depth = projection.NearZ; depth <= projection.FarZ; depth += 0.5f)
        {
            var scale = projection.Scale(depth);
            Assert.True(scale < previous, $"scale at {depth} should be smaller than nearer");
            Assert.True(scale > 0f);
            previous = scale;
        }

        Assert.Equal(2f, projection.Scale(2f) / projection.Scale(4f), 0.0001f);
    }

    [Fact]
    public void TheNearPlaneGroundMapsToTheBottom()
    {
        var projection = Runner();

        var centre = projection.ToScreen(0f, 0f, projection.NearZ);

        Assert.Equal(View.Max.Y, centre.Y, 0.01f);
        Assert.Equal(View.Center.X, centre.X, 0.01f);
        Assert.Equal(View.Max.Y, projection.LaneEdge(0, projection.NearZ).Y, 0.01f);
    }

    [Fact]
    public void LanesConvergeAtTheVanishingPoint()
    {
        var projection = Runner();
        var vanishing = new Vector2(projection.VanishingX, projection.HorizonY);

        for (var edge = 0; edge <= projection.LaneCount; edge++)
        {
            var far = projection.LaneEdge(edge, 1e6f);
            Assert.Equal(vanishing.X, far.X, 0.05f);
            Assert.Equal(vanishing.Y, far.Y, 0.05f);
        }

        var nearGap = projection.LaneEdge(1, projection.NearZ).X - projection.LaneEdge(0, projection.NearZ).X;
        var farGap = projection.LaneEdge(1, projection.FarZ).X - projection.LaneEdge(0, projection.FarZ).X;
        Assert.True(nearGap > farGap * 4f);
    }

    [Fact]
    public void LaneEdgesAreOrderedAndEvenlySpaced()
    {
        var projection = Runner();
        var depth = projection.NearZ * 2f;

        var gap = projection.LaneEdge(1, depth).X - projection.LaneEdge(0, depth).X;
        for (var edge = 1; edge <= projection.LaneCount; edge++)
        {
            var step = projection.LaneEdge(edge, depth).X - projection.LaneEdge(edge - 1, depth).X;
            Assert.Equal(gap, step, 0.01f);
        }

        Assert.Equal(projection.VanishingX, (projection.LaneEdge(0, depth).X + projection.LaneEdge(3, depth).X) * 0.5f, 0.01f);
    }

    [Fact]
    public void LaneOffsetsCentreTheMiddleLane()
    {
        var projection = Runner();

        Assert.Equal(-1f, projection.LaneOffset(0));
        Assert.Equal(0f, projection.LaneOffset(1));
        Assert.Equal(1f, projection.LaneOffset(2));
        var left = projection.ToScreen(projection.LaneOffset(0), 0f, 5f).X;
        var middle = projection.ToScreen(projection.LaneOffset(1), 0f, 5f).X;
        var right = projection.ToScreen(projection.LaneOffset(2), 0f, 5f).X;
        Assert.True(left < middle && middle < right);
    }

    [Fact]
    public void HeightLiftsAPointOnScreen()
    {
        var projection = Runner();

        var ground = projection.ToScreen(0f, 0f, 6f);
        var lifted = projection.ToScreen(0f, 1f, 6f);

        Assert.True(lifted.Y < ground.Y);
        Assert.Equal(projection.Scale(6f), ground.Y - lifted.Y, 0.01f);
        Assert.Equal(projection.HorizonY, projection.ToScreen(0f, projection.CameraHeight, 6f).Y, 0.01f);
    }

    [Fact]
    public void FitSpansTheRoadAcrossTheRequestedWidth()
    {
        var projection = Runner();

        var left = projection.LaneEdge(0, projection.NearZ).X;
        var right = projection.LaneEdge(projection.LaneCount, projection.NearZ).X;

        Assert.Equal(View.Width * 0.9f, right - left, 0.01f);
        Assert.Equal(View.Min.Y + View.Height * 0.35f, projection.HorizonY, 0.01f);
    }

    [Fact]
    public void FogRisesFromClearAtTheNearPlaneToFullAtTheFarPlane()
    {
        var projection = Runner();

        Assert.Equal(0f, projection.Fog(projection.NearZ));
        Assert.Equal(0f, projection.Fog(0f));
        Assert.Equal(1f, projection.Fog(projection.FarZ));
        Assert.Equal(1f, projection.Fog(projection.FarZ * 3f));
        var previous = 0f;
        for (var depth = projection.NearZ; depth <= projection.FarZ; depth += 1f)
        {
            var fog = projection.Fog(depth);
            Assert.True(fog >= previous);
            previous = fog;
        }
    }

    [Fact]
    public void PointsBehindTheCameraStayFinite()
    {
        var projection = Runner();

        var behind = projection.ToScreen(1f, 0f, -3f);

        Assert.True(float.IsFinite(behind.X));
        Assert.True(float.IsFinite(behind.Y));
        Assert.True(behind.Y > View.Max.Y);
    }

    private static LaneProjection Runner() => LaneProjection.Fit(View, 3, 2f, 0.35f, 0.9f, 2f, 40f);
}
