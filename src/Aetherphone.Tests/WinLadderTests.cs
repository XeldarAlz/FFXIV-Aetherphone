using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core.Notifications;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WinLadderTests
{
    private const long Stake = 1000;

    [Fact]
    public void APayoutAtOrBelowTheStakeIsNeverAWin()
    {
        Assert.Equal(WinTier.None, WinLadder.TierFor(Stake, 0));
        Assert.Equal(WinTier.None, WinLadder.TierFor(Stake, 400));
        Assert.Equal(WinTier.None, WinLadder.TierFor(Stake, Stake));
    }

    [Fact]
    public void TiersClimbOnTheNetWinOverTheStake()
    {
        Assert.Equal(WinTier.Win, WinLadder.TierFor(Stake, Stake + 1));
        Assert.Equal(WinTier.Win, WinLadder.TierFor(Stake, Stake + 2999));
        Assert.Equal(WinTier.Nice, WinLadder.TierFor(Stake, Stake + 3000));
        Assert.Equal(WinTier.Big, WinLadder.TierFor(Stake, Stake + 10_000));
        Assert.Equal(WinTier.Mega, WinLadder.TierFor(Stake, Stake + 25_000));
        Assert.Equal(WinTier.Epic, WinLadder.TierFor(Stake, Stake + 50_000));
        Assert.Equal(WinTier.Legendary, WinLadder.TierFor(Stake, Stake + 100_000));
        Assert.Equal(WinTier.Legendary, WinLadder.TierFor(Stake, Stake * 5000));
    }

    [Fact]
    public void AJackpotIsAlwaysLegendary()
    {
        Assert.Equal(WinTier.Legendary, WinLadder.TierFor(Stake, Stake / 2, jackpot: true));
        Assert.Equal(WinTier.None, WinLadder.TierFor(Stake, 0, jackpot: true));
    }

    [Fact]
    public void BillionChipStakesDoNotOverflow()
    {
        const long whale = 50_000_000_000;
        Assert.Equal(WinTier.Legendary, WinLadder.TierFor(whale, whale * 101));
    }

    [Fact]
    public void TheLadderMatchesTheStandardTable()
    {
        Assert.Equal(12, WinLadder.Spec(WinTier.Win).Sparkles);
        Assert.Equal(0.4f, WinLadder.Spec(WinTier.Win).CountUpSeconds);
        Assert.Equal(36, WinLadder.Spec(WinTier.Nice).Confetti);
        Assert.Equal(90, WinLadder.Spec(WinTier.Big).Confetti);
        Assert.Equal(1f, WinLadder.Spec(WinTier.Big).ShowerSeconds);
        Assert.Equal(UiSound.WinBig, WinLadder.Spec(WinTier.Big).Sound);
        Assert.True(WinLadder.Spec(WinTier.Mega).Fanfare);
        Assert.Equal(160, WinLadder.Spec(WinTier.Mega).Confetti);
        Assert.True(WinLadder.Spec(WinTier.Epic).FullCard);
        Assert.Equal(UiSound.WinEpic, WinLadder.Spec(WinTier.Epic).Sound);
        Assert.Equal(320, WinLadder.Spec(WinTier.Legendary).Confetti);
        Assert.Equal(4f, WinLadder.Spec(WinTier.Legendary).CountUpSeconds);
        Assert.False(WinLadder.Spec(WinTier.Win).Banner);
        Assert.True(WinLadder.Spec(WinTier.Nice).Banner);
    }

    [Fact]
    public void BigWinSoundsShareOneTenSecondSlot()
    {
        CasinoSfx.ResetThrottle();
        Assert.True(CasinoSfx.TryTakeBigWinSlot(1_000));
        Assert.False(CasinoSfx.TryTakeBigWinSlot(5_000));
        Assert.False(CasinoSfx.TryTakeBigWinSlot(10_999));
        Assert.True(CasinoSfx.TryTakeBigWinSlot(11_000));
        Assert.True(CasinoSfx.IsBigWin(UiSound.Fanfare));
        Assert.False(CasinoSfx.IsBigWin(UiSound.WinSmall));
        CasinoSfx.ResetThrottle();
    }

    [Fact]
    public void ClimbingCuesRiseAndCap()
    {
        Assert.Equal(1f, CasinoSfx.PitchFor(0));
        Assert.True(CasinoSfx.PitchFor(5) > CasinoSfx.PitchFor(4));
        Assert.Equal(CasinoSfx.MaxPitch, CasinoSfx.PitchFor(500));
    }
}
