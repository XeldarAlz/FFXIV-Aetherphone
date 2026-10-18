using Aetherphone.Core.Message;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ReadWatermarkTests
{
    private const string ReadMessage = "message-8";
    private static readonly ReadThrough ReadAtEight = new(LastMessageAt: 1_000, Unread: 8);
    private static readonly ReadThrough ReadAtEightById = new(LastMessageAt: 1_000, Unread: 8, LastMessageId: ReadMessage);

    [Fact]
    public void HoldsZeroWhileTheServerRepeatsTheCountItHadWhenRead()
    {
        Assert.Equal(ReadWatermarkVerdict.Hold, ReadWatermark.Judge(ReadAtEight, null, 1_000, 8));
    }

    [Fact]
    public void HoldsZeroWhileTheServerStillReportsSomeOfThatCount()
    {
        Assert.Equal(ReadWatermarkVerdict.Hold, ReadWatermark.Judge(ReadAtEight, null, 1_000, 3));
    }

    [Fact]
    public void SettlesOnceTheServerAgrees()
    {
        Assert.Equal(ReadWatermarkVerdict.Settled, ReadWatermark.Judge(ReadAtEight, null, 1_000, 0));
    }

    [Fact]
    public void ANewerMessageSupersedesTheMark()
    {
        Assert.Equal(ReadWatermarkVerdict.Superseded, ReadWatermark.Judge(ReadAtEight, null, 1_001, 1));
    }

    [Fact]
    public void AnExtraMessageInTheSameSecondSupersedesTheMark()
    {
        Assert.Equal(ReadWatermarkVerdict.Superseded, ReadWatermark.Judge(ReadAtEight, null, 1_000, 9));
    }

    [Fact]
    public void TheSameLastMessageKeepsTheHold()
    {
        Assert.Equal(ReadWatermarkVerdict.Hold, ReadWatermark.Judge(ReadAtEightById, ReadMessage, 1_000, 8));
    }

    [Fact]
    public void ADifferentLastMessageInTheSameSecondSupersedesTheMark()
    {
        Assert.Equal(ReadWatermarkVerdict.Superseded,
            ReadWatermark.Judge(ReadAtEightById, "message-9", 1_000, 8));
    }

    [Fact]
    public void TheSameLastMessageSettlesOnceTheServerAgrees()
    {
        Assert.Equal(ReadWatermarkVerdict.Settled, ReadWatermark.Judge(ReadAtEightById, ReadMessage, 1_000, 0));
    }

    [Fact]
    public void ARowWithoutAnIdFallsBackToTheTimestamp()
    {
        Assert.Equal(ReadWatermarkVerdict.Hold, ReadWatermark.Judge(ReadAtEightById, string.Empty, 1_000, 8));
        Assert.Equal(ReadWatermarkVerdict.Superseded, ReadWatermark.Judge(ReadAtEightById, string.Empty, 1_001, 1));
    }
}
