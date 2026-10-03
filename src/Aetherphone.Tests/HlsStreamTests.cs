using System.Text;
using Aetherphone.Core.Radio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HlsStreamTests
{
    private static readonly Uri MasterUri = new("https://radio.example/master.m3u8");

    [Fact]
    public void FollowsTheMasterAndFlattensTransportSegments()
    {
        var aac = RadioStreamFixtures.ToneAac();
        var half = FrameBoundaryNear(aac, aac.Length / 2);
        var first = RadioStreamFixtures.TransportStream(aac[..half], 0x0F);
        var second = RadioStreamFixtures.TransportStream(aac[half..], 0x0F);
        var routes = new Dictionary<string, byte[]>
        {
            ["https://radio.example/audio/index.m3u8"] = Text(
                "#EXTM3U\n#EXT-X-TARGETDURATION:6\n#EXT-X-MEDIA-SEQUENCE:10\n#EXTINF:6,\nseg10.ts\n#EXTINF:6,\nseg11.ts\n#EXT-X-ENDLIST\n"),
            ["https://radio.example/audio/seg10.ts"] = first,
            ["https://radio.example/audio/seg11.ts"] = second,
        };
        const string master = "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=128000,CODECS=\"mp4a.40.2\"\naudio/index.m3u8\n";

        var stream = new HlsStream(MasterUri, master, Fetcher(routes, new List<string>()), CancellationToken.None);
        var flattened = ReadAll(stream);

        Assert.Equal(aac, flattened);
        Assert.Equal(StreamCodec.Aac, stream.Codec);
    }

    [Fact]
    public void ALivePlaylistStartsNearTheEdgeAndOnlyQueuesNewSequences()
    {
        var segment = RadioStreamFixtures.TransportStream(new byte[] { 1, 2, 3 }, 0x0F);
        var fetched = new List<string>();
        var reloads = 0;
        var playlists = new[]
        {
            Media(3, 5, false),
            Media(5, 4, false),
            Media(6, 4, true),
        };
        Func<Uri, CancellationToken, byte[]> fetch = (uri, _) =>
        {
            var path = uri.AbsolutePath;
            fetched.Add(path);
            if (path.EndsWith(".m3u8", StringComparison.Ordinal))
            {
                reloads++;
                return Text(playlists[Math.Min(reloads, playlists.Length - 1)]);
            }

            return segment;
        };

        var stream = new HlsStream(new Uri("https://radio.example/live/index.m3u8"), playlists[0], fetch,
            CancellationToken.None);
        ReadAll(stream);

        var segments = fetched.FindAll(path => path.EndsWith(".ts", StringComparison.Ordinal));
        Assert.Equal(new[] { "/live/s5.ts", "/live/s6.ts", "/live/s7.ts", "/live/s8.ts", "/live/s9.ts" }, segments);
    }

    [Fact]
    public void RawAacSegmentsLoseTheirId3TimestampTag()
    {
        var aac = RadioStreamFixtures.ToneAac();
        var id3 = new byte[] { (byte)'I', (byte)'D', (byte)'3', 4, 0, 0, 0, 0, 0, 5, 1, 2, 3, 4, 5 };
        var routes = new Dictionary<string, byte[]>
        {
            ["https://radio.example/a.aac"] = id3.Concat(aac).ToArray(),
        };
        const string media = "#EXTM3U\n#EXT-X-TARGETDURATION:6\n#EXTINF:6,\na.aac\n#EXT-X-ENDLIST\n";

        var stream = new HlsStream(MasterUri, media, Fetcher(routes, new List<string>()), CancellationToken.None);

        Assert.Equal(aac, ReadAll(stream));
    }

    [Fact]
    public void FragmentedMp4IsRefusedUpFront()
    {
        const string media = "#EXTM3U\n#EXT-X-TARGETDURATION:6\n#EXT-X-MAP:URI=\"init.mp4\"\n#EXTINF:6,\na.m4s\n";

        Assert.Throws<NotSupportedException>(() =>
            new HlsStream(MasterUri, media, Fetcher(new Dictionary<string, byte[]>(), new List<string>()),
                CancellationToken.None));
    }

    [Fact]
    public void AMissingSegmentIsSkippedRatherThanEndingTheStation()
    {
        var aac = RadioStreamFixtures.ToneAac();
        var routes = new Dictionary<string, byte[]>
        {
            ["https://radio.example/b.ts"] = RadioStreamFixtures.TransportStream(aac, 0x0F),
        };
        const string media = "#EXTM3U\n#EXT-X-TARGETDURATION:6\n#EXTINF:6,\na.ts\n#EXTINF:6,\nb.ts\n#EXT-X-ENDLIST\n";

        var stream = new HlsStream(MasterUri, media, Fetcher(routes, new List<string>()), CancellationToken.None);

        Assert.Equal(aac, ReadAll(stream));
    }

    private static string Media(long firstSequence, int count, bool endList)
    {
        var builder = new StringBuilder("#EXTM3U\n#EXT-X-TARGETDURATION:1\n");
        builder.Append("#EXT-X-MEDIA-SEQUENCE:").Append(firstSequence).Append('\n');
        for (var index = 0; index < count; index++)
        {
            builder.Append("#EXTINF:1.0,\ns").Append(firstSequence + index).Append(".ts\n");
        }

        if (endList)
        {
            builder.Append("#EXT-X-ENDLIST\n");
        }

        return builder.ToString();
    }

    private static Func<Uri, CancellationToken, byte[]> Fetcher(Dictionary<string, byte[]> routes, List<string> log)
    {
        return (uri, _) =>
        {
            log.Add(uri.ToString());
            if (!routes.TryGetValue(uri.ToString(), out var body))
            {
                throw new HttpRequestException("404");
            }

            return body;
        };
    }

    private static int FrameBoundaryNear(byte[] aac, int target)
    {
        var offset = 0;
        while (offset < aac.Length)
        {
            Assert.True(AdtsReader.TryParseHeader(aac.AsSpan(offset), out var frame));
            if (offset + frame.Length > target)
            {
                return offset;
            }

            offset += frame.Length;
        }

        return offset;
    }

    private static byte[] Text(string value) => Encoding.UTF8.GetBytes(value);

    private static byte[] ReadAll(Stream stream)
    {
        var output = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0)
            {
                return output.ToArray();
            }

            output.Write(buffer, 0, read);
        }
    }
}
