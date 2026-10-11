using Aetherphone.Apps.Casino.Cabinets;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ScratchStatusLayoutTests
{
    public static TheoryData<float> Scales => new() { 0.75f, 1f, 1.25f, 1.5f };

    [Theory]
    [MemberData(nameof(Scales))]
    public void AWrappedHintAndTheRunButtonBothFitTheReservedArea(float scale)
    {
        var line = 22f * scale;
        var button = 44f * scale;
        var gap = 5f * scale;
        var twoLines = line * 2f;

        var reserve = ScratchCabinet.StatusReserve(twoLines, true, line, button, gap);

        Assert.True(reserve >= twoLines + gap + button - 0.01f);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void ATallNoticeIsNeverClippedByTheRestingReserve(float scale)
    {
        var notice = 74f * scale;

        var reserve = ScratchCabinet.StatusReserve(notice, false, 22f * scale, 44f * scale, 5f * scale);

        Assert.True(reserve >= notice - 0.01f);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheReserveNeverDropsBelowOneLineAndAButton(float scale)
    {
        var line = 22f * scale;
        var button = 44f * scale;
        var gap = 5f * scale;

        Assert.Equal(line + gap + button, ScratchCabinet.StatusReserve(0f, false, line, button, gap), 3);
        Assert.Equal(line + gap + button, ScratchCabinet.StatusReserve(button, false, line, button, gap), 3);
    }

    [Fact]
    public void TheRunButtonOnlyShowsBesideAnIdleOrLosingTicket()
    {
        Assert.True(ScratchCabinet.ShowsRun(ScratchStatusLine.None, true));
        Assert.True(ScratchCabinet.ShowsRun(ScratchStatusLine.NoWin, true));
        Assert.False(ScratchCabinet.ShowsRun(ScratchStatusLine.Hint, true));
        Assert.False(ScratchCabinet.ShowsRun(ScratchStatusLine.Reason, true));
        Assert.False(ScratchCabinet.ShowsRun(ScratchStatusLine.None, false));
    }
}
