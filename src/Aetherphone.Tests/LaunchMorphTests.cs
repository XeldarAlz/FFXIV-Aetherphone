using System.Numerics;
using Aetherphone.Apps.Games.Hub;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LaunchMorphTests
{
    private const float SixtyHertz = 1f / 60f;
    private const int MaxFrames = 120;
    private const float Tolerance = 0.01f;

    private static readonly Rect Area = new(new Vector2(0f, 60f), new Vector2(360f, 700f));
    private static readonly Rect Tile = new(new Vector2(40f, 420f), new Vector2(111.5f, 491.5f));
    private static readonly Rect Poster = new(new Vector2(16f, 300f), new Vector2(172f, 400f));

    private static void AssertNear(Vector2 expected, Vector2 actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
    }

    private static void AssertNear(Rect expected, Rect actual)
    {
        AssertNear(expected.Min, actual.Min);
        AssertNear(expected.Max, actual.Max);
    }

    [Fact]
    public void TheCardRunsFromTheSourceToTheArea()
    {
        AssertNear(Tile, LaunchMorph.Card(Tile, Area, 0f));
        AssertNear(Area, LaunchMorph.Card(Tile, Area, 1f));
    }

    [Fact]
    public void ARestingMorphIsTheIdentity()
    {
        var transform = LaunchMorph.Transform(Tile, Area, 1f, true);

        Assert.Equal(1f, transform.Scale, 4);
        AssertNear(Area.Center, transform.Map(Area.Center));
        AssertNear(Area.Min, transform.Map(Area.Min));
        AssertNear(Area, transform.Clip);
        Assert.Equal(1f, transform.Alpha);
    }

    [Fact]
    public void TheFirstFrameSitsInsideTheTappedIcon()
    {
        var transform = LaunchMorph.Transform(Tile, Area, 0f, true);

        AssertNear(Tile.Center, transform.Map(Area.Center));
        Assert.Equal(Tile.Width, transform.Map(Area).Width, 3);
        Assert.True(transform.Clip.Min.X > Tile.Min.X && transform.Clip.Min.Y > Tile.Min.Y);
        Assert.True(transform.Clip.Max.X < Tile.Max.X && transform.Clip.Max.Y < Tile.Max.Y);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheScaledScreenAlwaysCoversTheClip(bool icon)
    {
        var source = icon ? Tile : Poster;
        for (var step = 0; step <= 20; step++)
        {
            var progress = step / 20f;
            var transform = LaunchMorph.Transform(source, Area, progress, icon);
            var screen = transform.Map(Area);
            var clip = transform.Clip;
            Assert.True(screen.Min.X <= clip.Min.X + Tolerance, $"left gap at {progress}");
            Assert.True(screen.Min.Y <= clip.Min.Y + Tolerance, $"top gap at {progress}");
            Assert.True(screen.Max.X >= clip.Max.X - Tolerance, $"right gap at {progress}");
            Assert.True(screen.Max.Y >= clip.Max.Y - Tolerance, $"bottom gap at {progress}");
        }
    }

    [Fact]
    public void OnlySquareSourcesCountAsIcons()
    {
        Assert.True(LaunchMorph.IsIcon(Tile));
        Assert.False(LaunchMorph.IsIcon(Poster));
        Assert.False(LaunchMorph.IsIcon(default));
    }

    [Fact]
    public void ACardSourceFadesTheGameInWhileAnIconSourceFadesTheIconOut()
    {
        Assert.Equal(0f, LaunchMorph.ContentAlpha(0f, false));
        Assert.Equal(1f, LaunchMorph.ContentAlpha(0.5f, false));
        Assert.Equal(1f, LaunchMorph.ContentAlpha(0f, true));
        Assert.Equal(1f, LaunchMorph.IconAlpha(0f));
        Assert.Equal(0f, LaunchMorph.IconAlpha(0.4f));
        Assert.Equal(0f, LaunchMorph.Veil(0f));
        Assert.Equal(LaunchMorph.VeilDim, LaunchMorph.Veil(1f));
    }

    [Fact]
    public void APresentMovesForwardAndSettlesKeepingItsSource()
    {
        var morph = new LaunchMorph();
        morph.Begin(Tile);
        var previous = 0f;
        var step = LaunchStep.Moving;
        var frames = 0;
        while (step == LaunchStep.Moving && frames < MaxFrames)
        {
            step = morph.Advance(SixtyHertz);
            Assert.True(morph.Progress >= previous - 1e-6f);
            Assert.True(morph.Progress <= 1f);
            previous = morph.Progress;
            frames++;
        }

        Assert.Equal(LaunchStep.Presented, step);
        Assert.False(morph.Active);
        Assert.True(morph.HasSource);
        AssertNear(Tile, morph.Source);
        Assert.Equal(LaunchStep.Idle, morph.Advance(SixtyHertz));
    }

    [Fact]
    public void ADismissRunsBackToTheSourceAndForgetsIt()
    {
        var morph = new LaunchMorph();
        morph.Begin(Tile);
        var settle = 0;
        while (morph.Advance(SixtyHertz) == LaunchStep.Moving && settle < MaxFrames)
        {
            settle++;
        }

        Assert.True(morph.BeginDismiss());
        Assert.True(morph.Dismissing);
        var previous = 1f;
        var step = LaunchStep.Moving;
        var frames = 0;
        while (step == LaunchStep.Moving && frames < MaxFrames)
        {
            step = morph.Advance(SixtyHertz);
            if (step == LaunchStep.Moving)
            {
                Assert.True(morph.Progress <= previous + 1e-6f);
                Assert.True(morph.Progress >= 0f);
                previous = morph.Progress;
            }

            frames++;
        }

        Assert.Equal(LaunchStep.Dismissed, step);
        Assert.False(morph.Active);
        Assert.False(morph.HasSource);
        Assert.False(morph.BeginDismiss());
    }

    [Fact]
    public void ADismissDuringThePresentTurnsAround()
    {
        var morph = new LaunchMorph();
        morph.Begin(Tile);
        for (var frame = 0; frame < 6; frame++)
        {
            morph.Advance(SixtyHertz);
        }

        var midway = morph.Progress;
        Assert.True(morph.BeginDismiss());
        morph.Advance(SixtyHertz);
        Assert.True(morph.Progress < midway);
    }

    [Fact]
    public void ClearDropsTheSource()
    {
        var morph = new LaunchMorph();
        morph.Begin(Tile);
        morph.Clear();

        Assert.False(morph.Active);
        Assert.False(morph.HasSource);
        Assert.Equal(0f, morph.Progress);
    }
}
