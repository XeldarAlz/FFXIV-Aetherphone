using System.Net;
using System.Net.Http.Headers;
using Aetherphone.Core.Radio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RadioStallWatchdogTests
{
    private static readonly TimeSpan ShortStall = TimeSpan.FromMilliseconds(250);

    [Fact]
    public void A_read_that_never_returns_is_aborted_as_a_stall()
    {
        var source = new StallingStream(Array.Empty<byte>());
        var aborted = false;
        using var watchdog = new StallWatchdogStream(source, () => aborted = true, ShortStall);
        var buffer = new byte[64];

        Assert.Throws<RadioStallException>(() => watchdog.Read(buffer, 0, buffer.Length));

        Assert.True(watchdog.Stalled);
        Assert.True(aborted);
    }

    [Fact]
    public void A_slow_stream_that_keeps_delivering_bytes_is_not_a_stall()
    {
        var source = new TricklingStream(TimeSpan.FromMilliseconds(60), 16);
        using var watchdog = new StallWatchdogStream(source, () => { }, ShortStall);
        var buffer = new byte[8];
        var total = 0;

        for (var readIndex = 0; readIndex < 16; readIndex++)
        {
            total += watchdog.Read(buffer, 0, buffer.Length);
        }

        Assert.False(watchdog.Stalled);
        Assert.Equal(16 * buffer.Length, total);
    }

    [Fact]
    public void Time_spent_between_reads_never_counts_as_a_stall()
    {
        var source = new MemoryStream(new byte[32]);
        using var watchdog = new StallWatchdogStream(source, () => { }, ShortStall);
        var buffer = new byte[8];

        var first = watchdog.Read(buffer, 0, buffer.Length);
        Thread.Sleep(ShortStall * 3);
        var read = watchdog.Read(buffer, 0, buffer.Length);

        Assert.Equal(8, first);
        Assert.Equal(8, read);
        Assert.False(watchdog.Stalled);
    }

    [Fact]
    public void A_direct_station_that_goes_silent_after_connecting_surfaces_a_stall()
    {
        var handler = new StallingHandler(RadioStreamFixtures.ToneAac());

        using var connection = RadioStreamOpener.Open(new HttpClient(handler), "http://radio.example/live", "AAC",
            _ => true, _ => { }, CancellationToken.None, ShortStall)!;
        var buffer = new byte[4096];

        var exception = Record.Exception(() =>
        {
            while (connection.Audio.Read(buffer, 0, buffer.Length) > 0)
            {
            }
        });

        Assert.IsType<RadioStallException>(exception);
        Assert.True(connection.Stalled);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void Directory_stations_reconnect_only_after_a_stall(bool community, bool stalled, bool expected)
    {
        Assert.Equal(expected, RadioPlayer.ShouldReconnect(community, stalled));
    }

    private sealed class StallingHandler : HttpMessageHandler
    {
        private readonly byte[] head;

        public StallingHandler(byte[] head) => this.head = head;

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request, Content = new StreamContent(new StallingStream(head)),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("audio/aac");
            return response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Send(request, cancellationToken));
        }
    }

    private sealed class StallingStream : Stream
    {
        private readonly byte[] head;
        private readonly ManualResetEventSlim released = new();
        private int offset;

        public StallingStream(byte[] head) => this.head = head;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => offset;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int bufferOffset, int count)
        {
            if (offset < head.Length)
            {
                var take = Math.Min(count, head.Length - offset);
                Array.Copy(head, offset, buffer, bufferOffset, take);
                offset += take;
                return take;
            }

            released.Wait(TimeSpan.FromSeconds(10));
            throw new IOException("connection aborted");
        }

        protected override void Dispose(bool disposing)
        {
            released.Set();
            base.Dispose(disposing);
        }

        public override void Flush()
        {
        }

        public override long Seek(long seekOffset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int bufferOffset, int count) => throw new NotSupportedException();
    }

    private sealed class TricklingStream : Stream
    {
        private readonly TimeSpan delay;
        private int remaining;

        public TricklingStream(TimeSpan delay, int reads)
        {
            this.delay = delay;
            remaining = reads;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => 0;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (remaining-- <= 0)
            {
                return 0;
            }

            Thread.Sleep(delay);
            return count;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
