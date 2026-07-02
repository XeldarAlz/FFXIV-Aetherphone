using Concentus;
using Concentus.Enums;

namespace Aetherphone.Core.Telephony.Audio;

internal static class OpusAudio
{
    public const int SampleRate = 48000;
    public const int Channels = 1;
    public const int FrameSamples = 960;
    public const int FrameBytes = FrameSamples * sizeof(short);
    public const int MaxPacketBytes = 1275;

    static OpusAudio()
    {
        OpusCodecFactory.AttemptToUseNativeLibrary = false;
    }

    public static IOpusEncoder CreateEncoder(int bitrate)
    {
        var encoder = OpusCodecFactory.CreateEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_VOIP);
        encoder.Bitrate = bitrate;
        encoder.Complexity = 5;
        encoder.UseVBR = true;
        encoder.SignalType = OpusSignal.OPUS_SIGNAL_VOICE;
        return encoder;
    }

    public static IOpusDecoder CreateDecoder()
    {
        return OpusCodecFactory.CreateDecoder(SampleRate, Channels);
    }
}
