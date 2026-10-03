using System.Runtime.InteropServices;
using NAudio.MediaFoundation;
using NAudio.Wave;

namespace Aetherphone.Core.Radio;

/// AAC through the decoder Windows already ships. The pump is written out by hand because NAudio's
/// MediaFoundationTransform is shaped for encoders and rejects the first frame with
/// MF_E_NOTACCEPTING: a decoder has to be drained before it will take more input.
internal sealed class AacStreamDecoder : IStreamDecoder
{
    private const int NeedMoreInput = unchecked((int)0xC00D6D72);
    private const int StreamChange = unchecked((int)0xC00D6D61);
    private const int PcmBits = 16;
    private const int MaxConsecutiveBadFrames = 64;
    private const int MaxOutputTypes = 64;
    private static readonly Guid AacDecoderClsid = new("32D186A7-218F-4C75-8876-DD77273A8999");

    private readonly Stream source;
    private readonly byte[] frame = new byte[AdtsReader.MaxFrameLength];
    private IMFTransform? transform;
    private IMFSample? outputSample;
    private IMFMediaBuffer? outputBuffer;
    private int outputBufferSize;
    private WaveFormat? waveFormat;
    private int badFrames;
    private bool ended;

    public AacStreamDecoder(Stream source)
    {
        this.source = source;
    }

    public WaveFormat? WaveFormat => waveFormat;

    public int Read(byte[] buffer)
    {
        if (ended)
        {
            return 0;
        }

        if (transform is null && !TryStart())
        {
            return 0;
        }

        while (true)
        {
            var produced = Drain(buffer);
            if (produced > 0)
            {
                return produced;
            }

            if (!PushFrame())
            {
                ended = true;
                return 0;
            }
        }
    }

    private bool TryStart()
    {
        if (!AdtsReader.TryRead(source, frame, out var first))
        {
            ended = true;
            return false;
        }

        waveFormat = new WaveFormat(first.SampleRate, PcmBits, first.Channels);
        var comType = Type.GetTypeFromCLSID(AacDecoderClsid);
        if (comType is null || Activator.CreateInstance(comType) is not IMFTransform created)
        {
            ended = true;
            return false;
        }

        transform = created;
        var aacFormat = new AacWaveFormat(first.SampleRate, first.Channels);
        transform.SetInputType(0, MediaFoundationApi.CreateMediaTypeFromWaveFormat(aacFormat), 0);
        transform.SetOutputType(0, MediaFoundationApi.CreateMediaTypeFromWaveFormat(waveFormat), 0);
        transform.ProcessMessage(MFT_MESSAGE_TYPE.MFT_MESSAGE_COMMAND_FLUSH, IntPtr.Zero);
        transform.ProcessMessage(MFT_MESSAGE_TYPE.MFT_MESSAGE_NOTIFY_BEGIN_STREAMING, IntPtr.Zero);
        transform.ProcessMessage(MFT_MESSAGE_TYPE.MFT_MESSAGE_NOTIFY_START_OF_STREAM, IntPtr.Zero);
        EnsureOutputBuffer();
        Feed(frame, first.Length);
        return true;
    }

    private void EnsureOutputBuffer()
    {
        transform!.GetOutputStreamInfo(0, out var streamInfo);
        var size = Math.Max(streamInfo.cbSize, waveFormat!.AverageBytesPerSecond);
        if (outputSample is not null && size <= outputBufferSize)
        {
            return;
        }

        ReleaseOutput();
        outputBuffer = MediaFoundationApi.CreateMemoryBuffer(size);
        outputSample = MediaFoundationApi.CreateSample();
        outputSample.AddBuffer(outputBuffer);
        outputBufferSize = size;
    }

    private bool PushFrame()
    {
        while (AdtsReader.TryRead(source, frame, out var next))
        {
            if (Feed(frame, next.Length))
            {
                badFrames = 0;
                return true;
            }

            badFrames++;
            if (badFrames >= MaxConsecutiveBadFrames)
            {
                return false;
            }
        }

        return false;
    }

    // A live stream that resyncs mid-frame hands over the odd frame the decoder rejects; dropping
    // that one frame is inaudible, ending the stream for it is not.
    private bool Feed(byte[] data, int length)
    {
        var mediaBuffer = MediaFoundationApi.CreateMemoryBuffer(length);
        mediaBuffer.Lock(out var destination, out _, out _);
        Marshal.Copy(data, 0, destination, length);
        mediaBuffer.Unlock();
        mediaBuffer.SetCurrentLength(length);

        var sample = MediaFoundationApi.CreateSample();
        sample.AddBuffer(mediaBuffer);
        try
        {
            transform!.ProcessInput(0, sample, 0);
            return true;
        }
        catch (COMException exception)
        {
            AepLog.Debug(exception, "[Radio] the aac decoder rejected a frame");
            return false;
        }
        finally
        {
            Marshal.ReleaseComObject(sample);
            Marshal.ReleaseComObject(mediaBuffer);
        }
    }

    private int Drain(byte[] buffer)
    {
        while (true)
        {
            outputBuffer!.SetCurrentLength(0);
            var buffers = new MFT_OUTPUT_DATA_BUFFER[1];
            buffers[0].pSample = outputSample;
            buffers[0].dwStreamID = 0;

            var result = transform!.ProcessOutput(_MFT_PROCESS_OUTPUT_FLAGS.None, 1, buffers, out _);
            if (result == NeedMoreInput)
            {
                return 0;
            }

            if (result == StreamChange)
            {
                Renegotiate();
                continue;
            }

            if (result != 0)
            {
                Marshal.ThrowExceptionForHR(result);
            }

            outputBuffer.Lock(out var decoded, out _, out var length);
            var copied = Math.Min(length, buffer.Length);
            Marshal.Copy(decoded, buffer, 0, copied);
            outputBuffer.Unlock();
            if (copied > 0)
            {
                return copied;
            }
        }
    }

    // HE-AAC signals its doubled output rate only inside the bitstream, so the format guessed from
    // the ADTS header is wrong for it and the decoder asks for a new output type after the first
    // frame. Taking the first 16-bit PCM type it offers is what makes AAC+ stations play.
    private void Renegotiate()
    {
        for (var typeIndex = 0; typeIndex < MaxOutputTypes; typeIndex++)
        {
            IMFMediaType candidate;
            try
            {
                transform!.GetOutputAvailableType(0, typeIndex, out candidate);
            }
            catch (COMException)
            {
                break;
            }

            var mediaType = new MediaType(candidate);
            if (mediaType.SubType != AudioSubtypes.MFAudioFormat_PCM || mediaType.BitsPerSample != PcmBits)
            {
                Marshal.ReleaseComObject(candidate);
                continue;
            }

            transform.SetOutputType(0, candidate, 0);
            waveFormat = new WaveFormat(mediaType.SampleRate, PcmBits, mediaType.ChannelCount);
            Marshal.ReleaseComObject(candidate);
            EnsureOutputBuffer();
            return;
        }

        throw new InvalidOperationException("The AAC decoder offered no 16-bit PCM output");
    }

    private void ReleaseOutput()
    {
        if (outputSample is not null)
        {
            Marshal.ReleaseComObject(outputSample);
            outputSample = null;
        }

        if (outputBuffer is not null)
        {
            Marshal.ReleaseComObject(outputBuffer);
            outputBuffer = null;
        }

        outputBufferSize = 0;
    }

    public void Dispose()
    {
        ReleaseOutput();
        if (transform is not null)
        {
            Marshal.ReleaseComObject(transform);
            transform = null;
        }
    }
}

/// WAVE_FORMAT_MPEG_HEAAC with its HEAACWAVEINFO tail. The tail has to be real fields with a
/// sequential layout: Media Foundation receives this as an unmanaged struct, so anything written
/// only in Serialize never crosses over. Payload type 1 means we hand over whole ADTS frames and
/// the decoder does its own framing, which saves building an AudioSpecificConfig by hand.
[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal sealed class AacWaveFormat : WaveFormat
{
    private const int HeAacFormatTag = 0x1610;
    private const int TailBytes = 12;

    private readonly short payloadType = 1;
    private readonly short profileLevelIndication = 0xFE;
    private readonly short structType;
    private readonly short reserved1;
    private readonly int reserved2;

    public AacWaveFormat(int sampleRate, int channels)
    {
        waveFormatTag = (WaveFormatEncoding)HeAacFormatTag;
        this.channels = (short)channels;
        this.sampleRate = sampleRate;
        averageBytesPerSecond = 0;
        blockAlign = 1;
        bitsPerSample = 0;
        extraSize = TailBytes;
    }

    public override void Serialize(BinaryWriter writer)
    {
        base.Serialize(writer);
        writer.Write(payloadType);
        writer.Write(profileLevelIndication);
        writer.Write(structType);
        writer.Write(reserved1);
        writer.Write(reserved2);
    }
}
