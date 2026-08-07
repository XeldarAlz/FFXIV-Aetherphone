using Aetherphone.Core.Radio;
using NAudio.Wave;
using Xunit;

namespace Aetherphone.Tests;

/// The player stops the moment a decoder returns 0, so the difference between "this frame produced
/// no audio yet" and "the stream is over" is the difference between playing and silence.
public sealed class StreamDecoderContractTests
{
    [Fact]
    public void ZeroDecodedBytesDoesNotEndTheStream()
    {
        var decoder = new PrimingDecoder(silentReads: 3, thenBytes: 512);
        var buffer = new byte[4096];

        var produced = decoder.Read(buffer);

        Assert.Equal(512, produced);
        Assert.Equal(4, decoder.Attempts);
    }

    [Fact]
    public void ZeroIsReservedForTheEndOfTheStream()
    {
        var decoder = new PrimingDecoder(silentReads: 0, thenBytes: 0);

        Assert.Equal(0, decoder.Read(new byte[4096]));
    }

    /// Stands in for the ACM decoder priming: a few frames yield nothing, then audio arrives.
    private sealed class PrimingDecoder : IStreamDecoder
    {
        private readonly int silentReads;
        private readonly int thenBytes;

        public PrimingDecoder(int silentReads, int thenBytes)
        {
            this.silentReads = silentReads;
            this.thenBytes = thenBytes;
        }

        public int Attempts { get; private set; }

        public WaveFormat? WaveFormat => new(44100, 16, 2);

        public int Read(byte[] buffer)
        {
            while (true)
            {
                Attempts++;
                if (Attempts <= silentReads)
                {
                    continue;
                }

                return thenBytes;
            }
        }

        public void Dispose()
        {
        }
    }
}
