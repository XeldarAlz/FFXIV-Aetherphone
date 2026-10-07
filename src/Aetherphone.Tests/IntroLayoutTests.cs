using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class IntroLayoutTests
{
    private const float LandscapeWidth = 780f;
    private const float LandscapeHeight = 360f;
    private const float PortraitWidth = 390f;
    private const float PortraitHeight = 720f;
    private const float TitleHeight = 42f;
    private const float HookLineHeight = 19f;
    private const float PillHeight = 28f;
    private const float StripHeight = 30f;
    private const float CaptionHeight = 16f;
    private const float LevelHeight = 25f;
    private const float PlayHeight = 52f;
    private const float LinksHeight = 28f;

    private static IntroBlocks Blocks(float scale, bool daily = true, int hookLines = 3, bool modes = true,
        bool seats = true, bool levels = true) =>
        new(daily ? PillHeight * scale : 0f, TitleHeight * scale, hookLines * HookLineHeight * scale,
            PillHeight * scale, modes ? StripHeight * scale : 0f, seats ? CaptionHeight * scale : 0f,
            seats ? StripHeight * scale : 0f, levels ? LevelHeight * scale : 0f, PlayHeight * scale,
            LinksHeight * scale);

    private static Rect Full(float width, float height, float scale) =>
        new(new Vector2(100f, 40f), new Vector2(100f + width * scale, 40f + height * scale));

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void EveryLandscapeIntroElementFitsTheFullRect(float scale)
    {
        var full = Full(LandscapeWidth, LandscapeHeight, scale);
        var safe = StageLayout.Safe(full, HudStyle.Compact, scale);
        var layout = IntroLayout.Compute(full, safe, true, Blocks(scale), scale);
        var content = IntroLayout.Content(full, scale);

        Assert.True(layout.Columns);
        Assert.True(layout.TextColumn.Max.X < layout.ActionColumn.Min.X);
        AssertStack(content, layout.Daily, layout.Title, layout.Hook, layout.Pills);
        AssertStack(content, layout.Modes, layout.SeatsCaption, layout.Seats, layout.Level, layout.Play, layout.Links);
        AssertWithin(layout.TextColumn, layout.Daily, layout.Title, layout.Hook, layout.Pills);
        AssertWithin(layout.ActionColumn, layout.Modes, layout.Seats, layout.Level, layout.Play, layout.Links);
    }

    [Fact]
    public void ALandscapeIntroWithoutStripsCentresItsActions()
    {
        const float scale = 1f;
        var full = Full(LandscapeWidth, LandscapeHeight, scale);
        var safe = StageLayout.Safe(full, HudStyle.Standard, scale);
        var layout = IntroLayout.Compute(full, safe, true,
            Blocks(scale, daily: false, hookLines: 2, modes: false, seats: false, levels: false), scale);
        var content = IntroLayout.Content(full, scale);

        AssertStack(content, layout.Title, layout.Hook, layout.Pills);
        AssertStack(content, layout.Play, layout.Links);
        Assert.Equal(0f, layout.Modes.Height);
        Assert.Equal(0f, layout.Seats.Height);
        var actions = layout.Links.Max.Y - layout.Play.Min.Y;
        Assert.Equal(content.Center.Y, layout.Play.Min.Y + actions * 0.5f, 3);
        Assert.Equal(layout.ActionColumn.Center.X, layout.Play.Center.X, 3);
    }

    [Fact]
    public void ThePortraitIntroKeepsOneCentredColumnUnderTheHud()
    {
        const float scale = 1f;
        var full = Full(PortraitWidth, PortraitHeight, scale);
        var safe = StageLayout.Safe(full, HudStyle.Standard, scale);
        var layout = IntroLayout.Compute(full, safe, false, Blocks(scale), scale);

        Assert.False(layout.Columns);
        AssertStack(safe, layout.Daily, layout.Title, layout.Hook, layout.Pills, layout.Modes, layout.SeatsCaption,
            layout.Seats, layout.Level, layout.Play, layout.Links);
        Assert.Equal(full.Center.X, layout.Play.Center.X, 3);
        Assert.Equal(full.Center.X, layout.Hook.Center.X, 3);
        Assert.Equal(IntroLayout.HookMaxWidth * scale, layout.Hook.Width, 3);
        Assert.Equal(IntroLayout.PlayWidth * scale, layout.Play.Width, 3);
        Assert.Equal(IntroLayout.StripMaxWidth * scale, layout.Modes.Width, 3);
        var stack = layout.Links.Max.Y - layout.Daily.Min.Y;
        Assert.Equal(full.Center.Y, layout.Daily.Min.Y + stack * 0.5f, 3);
    }

    [Fact]
    public void ATallPortraitStackNeverRisesAboveTheSafeTop()
    {
        const float scale = 1f;
        var full = Full(PortraitWidth, 520f, scale);
        var safe = StageLayout.Safe(full, HudStyle.Standard, scale);
        var layout = IntroLayout.Compute(full, safe, false, Blocks(scale, hookLines: 6), scale);

        Assert.Equal(safe.Min.Y, layout.Daily.Min.Y, 3);
    }

    [Fact]
    public void TheTextColumnIsTheSafeRectInPortraitAndTheLeftShareInLandscape()
    {
        const float scale = 1f;
        var portrait = Full(PortraitWidth, PortraitHeight, scale);
        var portraitSafe = StageLayout.Safe(portrait, HudStyle.Standard, scale);
        Assert.Equal(portraitSafe, IntroLayout.TextColumnOf(portrait, portraitSafe, false, scale));

        var landscape = Full(LandscapeWidth, LandscapeHeight, scale);
        var column = IntroLayout.TextColumnOf(landscape, StageLayout.Safe(landscape, HudStyle.Standard, scale), true,
            scale);
        var content = IntroLayout.Content(landscape, scale);
        Assert.Equal(content.Min, column.Min);
        Assert.True(column.Width > content.Width * 0.5f);
        Assert.True(column.Width < content.Width * 0.6f);
        Assert.Equal(IntroLayout.HookMaxWidth * scale, IntroLayout.HookWidth(column, scale), 3);
    }

    private static void AssertStack(Rect bounds, params Rect[] items)
    {
        var previousBottom = float.MinValue;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (item.Height <= 0f)
            {
                continue;
            }

            Assert.True(item.Min.Y >= bounds.Min.Y - 0.01f, $"item {index} starts above the bounds");
            Assert.True(item.Max.Y <= bounds.Max.Y + 0.01f, $"item {index} ends below the bounds");
            Assert.True(item.Min.Y >= previousBottom - 0.01f, $"item {index} overlaps the one before it");
            previousBottom = item.Max.Y;
        }
    }

    private static void AssertWithin(Rect column, params Rect[] items)
    {
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (item.Height <= 0f)
            {
                continue;
            }

            Assert.True(item.Min.X >= column.Min.X - 0.01f, $"item {index} spills left of its column");
            Assert.True(item.Max.X <= column.Max.X + 0.01f, $"item {index} spills right of its column");
        }
    }
}
