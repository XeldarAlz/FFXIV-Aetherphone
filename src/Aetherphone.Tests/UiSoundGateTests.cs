using Aetherphone.Core.Notifications;
using Xunit;

namespace Aetherphone.Tests;

public sealed class UiSoundGateTests
{
    [Fact]
    public void GameSoundsPlayWithInterfaceSoundsOff()
    {
        Assert.True(UiSoundGate.Allows(UiSoundChannel.Game, false, false, true));
    }

    [Fact]
    public void GameSoundsFollowTheirOwnToggle()
    {
        Assert.False(UiSoundGate.Allows(UiSoundChannel.Game, false, true, false));
    }

    [Theory]
    [InlineData((int)UiSoundChannel.Event)]
    [InlineData((int)UiSoundChannel.Transition)]
    [InlineData((int)UiSoundChannel.Tap)]
    [InlineData((int)UiSoundChannel.Toggle)]
    [InlineData((int)UiSoundChannel.Keyboard)]
    public void InterfaceChannelsNeedInterfaceSounds(int channel)
    {
        Assert.False(UiSoundGate.Allows((UiSoundChannel)channel, false, false, true));
        Assert.True(UiSoundGate.Allows((UiSoundChannel)channel, false, true, true));
    }

    [Theory]
    [InlineData((int)UiSoundChannel.Event)]
    [InlineData((int)UiSoundChannel.Tap)]
    [InlineData((int)UiSoundChannel.Game)]
    public void SilentModeMutesEveryChannel(int channel)
    {
        Assert.False(UiSoundGate.Allows((UiSoundChannel)channel, true, true, true));
    }

    [Fact]
    public void AnInterfaceChannelSwitchedOffStaysOff()
    {
        Assert.False(UiSoundGate.Allows(UiSoundChannel.Tap, false, true, false));
    }
}
