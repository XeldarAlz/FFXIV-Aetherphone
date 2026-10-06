using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class StageLayoutTests
{
    private static readonly Rect Full = new(new Vector2(20f, 40f), new Vector2(380f, 740f));

    [Fact]
    public void StandardSafeInsetsTwelveNinetySixAndSixteen()
    {
        var safe = StageLayout.Safe(Full, HudStyle.Standard, 1f);

        Assert.Equal(new Vector2(32f, 136f), safe.Min);
        Assert.Equal(new Vector2(368f, 724f), safe.Max);
    }

    [Fact]
    public void CompactSafeKeepsFortyMorePixelsOfHeight()
    {
        var standard = StageLayout.Safe(Full, HudStyle.Standard, 1f);
        var compact = StageLayout.Safe(Full, HudStyle.Compact, 1f);

        Assert.Equal(96f, compact.Min.Y);
        Assert.Equal(standard.Height + 40f, compact.Height);
        Assert.Equal(standard.Width, compact.Width);
    }

    [Fact]
    public void SafeScalesWithTheUiScale()
    {
        var safe = StageLayout.Safe(Full, HudStyle.Standard, 2f);

        Assert.Equal(new Vector2(44f, 232f), safe.Min);
        Assert.Equal(new Vector2(356f, 708f), safe.Max);
    }

    [Fact]
    public void SafeNeverInvertsOnATinyRect()
    {
        var tiny = new Rect(new Vector2(0f, 0f), new Vector2(20f, 50f));

        var safe = StageLayout.Safe(tiny, HudStyle.Standard, 1f);

        Assert.True(safe.Width >= 0f);
        Assert.True(safe.Height >= 0f);
    }

    [Fact]
    public void ChromeChipsSitOnTheTopRowAtTwentyEightIn()
    {
        Assert.Equal(new Vector2(48f, 66f), StageLayout.BackChipCenter(Full, 1f));
        Assert.Equal(new Vector2(352f, 66f), StageLayout.PauseChipCenter(Full, 1f));
        Assert.Equal(new Vector2(200f, 66f), StageLayout.PrimaryCenter(Full, 1f));
        Assert.Equal(111f, StageLayout.SecondaryRowY(Full, 1f));
    }

    [Fact]
    public void ThePrimaryPillLeavesRoomForBothChips()
    {
        Assert.Equal(360f - 144f, StageLayout.PrimaryMaxWidth(Full, 1f));
        Assert.Equal(0f, StageLayout.PrimaryMaxWidth(new Rect(Vector2.Zero, new Vector2(100f, 100f)), 1f));
    }

    [Fact]
    public void PadBandHugsTheBottom()
    {
        var band = StageLayout.PadBand(Full, StageLayout.DPadBand, 1f);

        Assert.Equal(600f, band.Min.Y);
        Assert.Equal(Full.Max.Y, band.Max.Y);
    }

    [Fact]
    public void PunchedGrowsTheSafeRectAroundItsCentre()
    {
        var safe = StageLayout.Safe(Full, HudStyle.Standard, 1f);
        var punched = StageLayout.Punched(safe, 1.1f);

        Assert.Equal(safe, StageLayout.Punched(safe, 1f));
        Assert.Equal(safe.Center, punched.Center);
        Assert.InRange(punched.Width, safe.Width * 1.1f - 0.01f, safe.Width * 1.1f + 0.01f);
    }
}
