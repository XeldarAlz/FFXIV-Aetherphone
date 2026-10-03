using System.Text;
using Aetherphone.Core.Radio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RadioStreamOpenerTests
{
    [Fact]
    public void ADirectAacMountIsSniffedEvenWhenLabelledMp3()
    {
        var handler = new FakeRadioHandler();
        handler.Add("http://radio.example/live", "audio/mpeg", RadioStreamFixtures.ToneAac());

        using var connection = Open(handler, "http://radio.example/live", "MP3");

        Assert.Equal(StreamCodec.Aac, connection.Codec);
        Assert.Equal(RadioStreamFixtures.ToneAac(), ReadAll(connection.Audio));
    }

    [Fact]
    public void APlsIsFollowedToItsStream()
    {
        var handler = new FakeRadioHandler();
        handler.Add("http://radio.example/listen.pls", "audio/x-scpls",
            "[playlist]\nNumberOfEntries=1\nFile1=http://radio.example/mount\n");
        handler.Add("http://radio.example/mount", "audio/aac", RadioStreamFixtures.ToneAac());

        using var connection = Open(handler, "http://radio.example/listen.pls", string.Empty);

        Assert.Equal(StreamCodec.Aac, connection.Codec);
        Assert.Equal(RadioStreamFixtures.ToneAac().Length, ReadAll(connection.Audio).Length);
    }

    [Fact]
    public void AnHlsStationBecomesOneFlatStream()
    {
        var aac = RadioStreamFixtures.ToneAac();
        var handler = new FakeRadioHandler();
        handler.Add("https://radio.example/hls/master.m3u8", "application/vnd.apple.mpegurl",
            "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=96000,CODECS=\"mp4a.40.2\"\nchunks.m3u8\n");
        handler.Add("https://radio.example/hls/chunks.m3u8", "application/vnd.apple.mpegurl",
            "#EXTM3U\n#EXT-X-TARGETDURATION:6\n#EXT-X-MEDIA-SEQUENCE:1\n#EXTINF:6,\n1.ts\n#EXT-X-ENDLIST\n");
        handler.Add("https://radio.example/hls/1.ts", "video/mp2t", RadioStreamFixtures.TransportStream(aac, 0x0F));

        using var connection = Open(handler, "https://radio.example/hls/master.m3u8", "MP3");

        Assert.Equal(StreamCodec.Aac, connection.Codec);
        Assert.Equal(aac, ReadAll(connection.Audio));
    }

    [Fact]
    public void IcyMetadataStillReachesThePlayerOnAnAacMount()
    {
        var aac = RadioStreamFixtures.ToneAac();
        const int interval = 1000;
        var block = Encoding.UTF8.GetBytes("StreamTitle='Eorzea Cafe - Night Shift';");
        var padded = new byte[(block.Length + 15) / 16 * 16];
        Array.Copy(block, padded, block.Length);
        var wire = new MemoryStream();
        for (var offset = 0; offset < aac.Length; offset += interval)
        {
            wire.Write(aac, offset, Math.Min(interval, aac.Length - offset));
            if (offset + interval < aac.Length)
            {
                wire.WriteByte(offset == 0 ? (byte)(padded.Length / 16) : (byte)0);
                if (offset == 0)
                {
                    wire.Write(padded);
                }
            }
        }

        var handler = new FakeRadioHandler();
        handler.Add("http://radio.example/icy", "audio/aacp", wire.ToArray(), interval.ToString());
        var titles = new List<string>();

        using var connection = RadioStreamOpener.Open(new HttpClient(handler), "http://radio.example/icy", "AAC",
            _ => true, titles.Add, CancellationToken.None)!;
        var audio = ReadAll(connection.Audio);

        Assert.Equal(StreamCodec.Aac, connection.Codec);
        Assert.Equal(aac, audio);
        Assert.Equal(new[] { "Eorzea Cafe - Night Shift" }, titles);
    }

    [Fact]
    public void AnEndedSessionGetsNoConnection()
    {
        var handler = new FakeRadioHandler();
        handler.Add("http://radio.example/live", "audio/mpeg", RadioStreamFixtures.ToneMp3());

        var connection = RadioStreamOpener.Open(new HttpClient(handler), "http://radio.example/live", "MP3",
            _ => false, _ => { }, CancellationToken.None);

        Assert.Null(connection);
    }

    [Fact]
    public void APlaylistThatPointsAtItselfGivesUp()
    {
        var handler = new FakeRadioHandler();
        handler.Add("http://radio.example/loop.m3u", "audio/x-mpegurl", "http://radio.example/loop.m3u\n");

        Assert.Throws<InvalidDataException>(() => Open(handler, "http://radio.example/loop.m3u", string.Empty));
    }

    [Fact]
    public void AnOversizedHlsResourceIsRefusedBeforeItIsBuffered()
    {
        var handler = new FakeRadioHandler();
        handler.Add("https://radio.example/hls/huge.ts", "video/mp2t", new byte[32 * 1024 * 1024]);

        Assert.Throws<InvalidDataException>(() => RadioStreamOpener.Fetch(new HttpClient(handler),
            new Uri("https://radio.example/hls/huge.ts"), CancellationToken.None));
    }

    [Fact]
    public void AnEndlessHlsSegmentStopsAtTheCapInsteadOfFillingMemory()
    {
        var handler = new EndlessBodyHandler();

        Assert.Throws<InvalidDataException>(() => RadioStreamOpener.Fetch(new HttpClient(handler),
            new Uri("https://radio.example/hls/endless.ts"), CancellationToken.None));
        Assert.True(handler.Body.Served < 64L * 1024 * 1024);
    }

    private sealed class EndlessBodyHandler : HttpMessageHandler
    {
        public readonly EndlessStream Body = new();

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                RequestMessage = request, Content = new StreamContent(Body),
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Send(request, cancellationToken));
        }
    }

    private sealed class EndlessStream : Stream
    {
        public long Served { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => Served;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Array.Clear(buffer, offset, count);
            Served += count;
            return count;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static RadioConnection Open(FakeRadioHandler handler, string url, string declaredCodec)
    {
        return RadioStreamOpener.Open(new HttpClient(handler), url, declaredCodec, _ => true, _ => { },
            CancellationToken.None)!;
    }

    private static byte[] ReadAll(Stream stream)
    {
        var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}
