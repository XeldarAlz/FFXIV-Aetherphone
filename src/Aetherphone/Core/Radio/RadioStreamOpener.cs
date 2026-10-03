using System.Text;

namespace Aetherphone.Core.Radio;

internal sealed class RadioConnection : IDisposable
{
    private readonly HttpResponseMessage? response;
    private readonly StallWatchdogStream? watchdog;

    public RadioConnection(Stream audio, StreamCodec codec, HttpResponseMessage? response,
        StallWatchdogStream? watchdog = null)
    {
        Audio = audio;
        Codec = codec;
        this.response = response;
        this.watchdog = watchdog;
    }

    public Stream Audio { get; }

    public StreamCodec Codec { get; }

    public bool Stalled => watchdog?.Stalled ?? false;

    public void Dispose()
    {
        Audio.Dispose();
        watchdog?.Dispose();
        response?.Dispose();
    }
}

internal static class RadioStreamOpener
{
    private const int MaxPlaylistHops = 3;
    private const int MaxFetchBytes = 16 * 1024 * 1024;
    private const int FetchChunkBytes = 64 * 1024;
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan SegmentTimeout = TimeSpan.FromSeconds(20);

    // Returns null when the session ended while connecting; the publish callback is how the player
    // keeps a handle on the live response so Stop can abort a blocked read.
    public static RadioConnection? Open(HttpClient client, string url, string declaredCodec,
        Func<HttpResponseMessage, bool> publish, Action<string> onTitle, CancellationToken token,
        TimeSpan? stallTimeout = null)
    {
        var uri = new Uri(url);
        var stallAfter = stallTimeout ?? StallWatchdogStream.DefaultTimeout;
        for (var hop = 0; hop <= MaxPlaylistHops; hop++)
        {
            var response = Connect(client, uri, token);
            if (!publish(response))
            {
                response.Dispose();
                return null;
            }

            var handedOver = false;
            StallWatchdogStream? watchdog = null;
            try
            {
                response.EnsureSuccessStatusCode();
                var finalUri = response.RequestMessage?.RequestUri ?? uri;
                var contentType = response.Content.Headers.ContentType?.MediaType;
                watchdog = new StallWatchdogStream(response.Content.ReadAsStream(token), response.Dispose,
                    stallAfter);
                var audio = WrapMetadata(watchdog, response, onTitle);
                var head = new byte[StreamSniffer.HeadLength];
                var headLength = PrefixedStream.ReadHead(audio, head);
                var headSpan = head.AsSpan(0, headLength);
                if (!StationPlaylist.LooksLikePlaylist(contentType, finalUri, headSpan))
                {
                    var codec = StreamSniffer.Detect(contentType, declaredCodec, headSpan);
                    handedOver = true;
                    return new RadioConnection(new PrefixedStream(head, headLength, audio), codec, response,
                        watchdog);
                }

                var text = ReadText(head, headLength, audio);
                if (HlsPlaylist.IsHls(text))
                {
                    return OpenHls(client, finalUri, text, declaredCodec, token);
                }

                if (!StationPlaylist.TryFirstEntry(text, finalUri, out uri))
                {
                    throw new InvalidDataException("The station playlist lists no stream");
                }
            }
            finally
            {
                if (!handedOver)
                {
                    watchdog?.Dispose();
                    response.Dispose();
                }
            }
        }

        throw new InvalidDataException("The station playlist nests too deeply");
    }

    private static HttpResponseMessage Connect(HttpClient client, Uri uri, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation(IcyMetadataStream.RequestHeader, "1");
        using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        connectTimeout.CancelAfter(ConnectTimeout);
        return client.Send(request, HttpCompletionOption.ResponseHeadersRead, connectTimeout.Token);
    }

    private static RadioConnection OpenHls(HttpClient client, Uri playlistUri, string text, string declaredCodec,
        CancellationToken token)
    {
        var hls = new HlsStream(playlistUri, text, (segmentUri, fetchToken) => Fetch(client, segmentUri, fetchToken),
            token);
        var head = new byte[StreamSniffer.HeadLength];
        var headLength = PrefixedStream.ReadHead(hls, head);
        var codec = hls.Codec != StreamCodec.Unknown
            ? hls.Codec
            : StreamSniffer.Detect(null, declaredCodec, head.AsSpan(0, headLength));
        return new RadioConnection(new PrefixedStream(head, headLength, hls), codec, null);
    }

    public static byte[] Fetch(HttpClient client, Uri uri, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(SegmentTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = client.Send(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxFetchBytes)
        {
            throw new InvalidDataException("The HLS resource is larger than a segment can be");
        }

        using var abort = timeout.Token.Register(response.Dispose);
        using var body = response.Content.ReadAsStream(timeout.Token);
        using var memory = new MemoryStream();
        var chunk = new byte[FetchChunkBytes];
        while (true)
        {
            var read = body.Read(chunk, 0, chunk.Length);
            if (read <= 0)
            {
                return memory.ToArray();
            }

            if (memory.Length + read > MaxFetchBytes)
            {
                throw new InvalidDataException("The HLS resource is larger than a segment can be");
            }

            memory.Write(chunk, 0, read);
        }
    }

    private static Stream WrapMetadata(Stream network, HttpResponseMessage response, Action<string> onTitle)
    {
        if (!response.Headers.TryGetValues(IcyMetadataStream.IntervalHeader, out var values))
        {
            return network;
        }

        string? first = null;
        foreach (var value in values)
        {
            first = value;
            break;
        }

        var interval = IcyMetadataStream.ParseInterval(first);
        return interval > 0 ? new IcyMetadataStream(network, interval, onTitle) : network;
    }

    private static string ReadText(byte[] head, int headLength, Stream source)
    {
        using var memory = new MemoryStream(headLength * 2);
        memory.Write(head, 0, headLength);
        var chunk = new byte[8192];
        while (memory.Length < StationPlaylist.MaxTextBytes)
        {
            var read = source.Read(chunk, 0, chunk.Length);
            if (read <= 0)
            {
                break;
            }

            memory.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(memory.GetBuffer(), 0, (int)memory.Length);
    }
}
