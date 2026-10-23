namespace Aetherphone.Core.Notifications;

internal static class UiSoundGate
{
    public static bool Allows(UiSoundChannel channel, bool silentMode, bool interfaceSounds, bool channelEnabled)
    {
        if (silentMode || !channelEnabled)
        {
            return false;
        }

        return channel == UiSoundChannel.Game || interfaceSounds;
    }
}
