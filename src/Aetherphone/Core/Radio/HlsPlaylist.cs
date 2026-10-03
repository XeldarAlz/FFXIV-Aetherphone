using System.Globalization;

namespace Aetherphone.Core.Radio;

internal readonly struct HlsVariant
{
    public readonly Uri Uri;
    public readonly int Bandwidth;
    public readonly bool HasVideo;

    public HlsVariant(Uri uri, int bandwidth, bool hasVideo)
    {
        Uri = uri;
        Bandwidth = bandwidth;
        HasVideo = hasVideo;
    }
}

internal readonly struct HlsSegment
{
    public readonly Uri Uri;
    public readonly long Sequence;
    public readonly double Duration;
    public readonly bool Discontinuity;

    public HlsSegment(Uri uri, long sequence, double duration, bool discontinuity)
    {
        Uri = uri;
        Sequence = sequence;
        Duration = duration;
        Discontinuity = discontinuity;
    }
}

internal sealed class HlsMasterPlaylist
{
    public HlsMasterPlaylist(HlsVariant[] variants, Uri[] audioRenditions)
    {
        Variants = variants;
        AudioRenditions = audioRenditions;
    }

    public HlsVariant[] Variants { get; }

    public Uri[] AudioRenditions { get; }
}

internal sealed class HlsMediaPlaylist
{
    public HlsMediaPlaylist(HlsSegment[] segments, double targetDuration, bool endList, bool encrypted,
        bool fragmented)
    {
        Segments = segments;
        TargetDuration = targetDuration;
        EndList = endList;
        Encrypted = encrypted;
        Fragmented = fragmented;
    }

    public HlsSegment[] Segments { get; }

    public double TargetDuration { get; }

    public bool EndList { get; }

    public bool Encrypted { get; }

    public bool Fragmented { get; }
}

internal static class HlsPlaylist
{
    public const int DefaultBitrateCap = 360_000;
    public const char ByteOrderMark = '\uFEFF';
    private const double FallbackTargetDuration = 6;
    private const string Header = "#EXTM3U";
    private const string StreamInfTag = "#EXT-X-STREAM-INF:";
    private const string MediaTag = "#EXT-X-MEDIA:";
    private const string TargetDurationTag = "#EXT-X-TARGETDURATION:";
    private const string MediaSequenceTag = "#EXT-X-MEDIA-SEQUENCE:";
    private const string SegmentTag = "#EXTINF:";
    private const string DiscontinuityTag = "#EXT-X-DISCONTINUITY";
    private const string EndListTag = "#EXT-X-ENDLIST";
    private const string KeyTag = "#EXT-X-KEY:";
    private const string MapTag = "#EXT-X-MAP:";
    private static readonly string[] VideoCodecPrefixes = { "avc", "hvc", "hev", "vp0", "vp8", "vp9", "av01", "mp4v" };

    public static bool IsHls(string text)
    {
        return text.Contains(StreamInfTag, StringComparison.Ordinal)
            || text.Contains(TargetDurationTag, StringComparison.Ordinal)
            || text.Contains(SegmentTag, StringComparison.Ordinal) && text.Contains(MediaSequenceTag, StringComparison.Ordinal);
    }

    public static bool IsMaster(string text)
    {
        return text.Contains(StreamInfTag, StringComparison.Ordinal);
    }

    public static bool StartsWithHeader(string text)
    {
        return text.AsSpan().TrimStart(ByteOrderMark).TrimStart().StartsWith(Header, StringComparison.Ordinal);
    }

    public static HlsMasterPlaylist ParseMaster(string text, Uri baseUri)
    {
        var variants = new List<HlsVariant>();
        var renditions = new List<Uri>();
        var pendingBandwidth = -1;
        var pendingVideo = false;
        var lines = text.Split('\n');
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith(StreamInfTag, StringComparison.Ordinal))
            {
                var attributes = line.AsSpan(StreamInfTag.Length);
                pendingBandwidth = ParseInt(Attribute(attributes, "BANDWIDTH"));
                pendingVideo = HasVideo(attributes);
                continue;
            }

            if (line.StartsWith(MediaTag, StringComparison.Ordinal))
            {
                AddAudioRendition(line.AsSpan(MediaTag.Length), baseUri, renditions);
                continue;
            }

            if (line[0] == '#' || pendingBandwidth < 0)
            {
                continue;
            }

            if (TryResolve(baseUri, line, out var uri))
            {
                variants.Add(new HlsVariant(uri, pendingBandwidth, pendingVideo));
            }

            pendingBandwidth = -1;
            pendingVideo = false;
        }

        return new HlsMasterPlaylist(variants.ToArray(), renditions.ToArray());
    }

    public static HlsMediaPlaylist ParseMedia(string text, Uri baseUri)
    {
        var segments = new List<HlsSegment>();
        var targetDuration = FallbackTargetDuration;
        var sequence = 0L;
        var pendingDuration = -1d;
        var pendingDiscontinuity = false;
        var endList = false;
        var encrypted = false;
        var fragmented = false;
        var lines = text.Split('\n');
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith(TargetDurationTag, StringComparison.Ordinal))
            {
                targetDuration = Math.Max(1, ParseDouble(line.AsSpan(TargetDurationTag.Length)));
            }
            else if (line.StartsWith(MediaSequenceTag, StringComparison.Ordinal))
            {
                sequence = long.TryParse(line.AsSpan(MediaSequenceTag.Length), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
            }
            else if (line.StartsWith(SegmentTag, StringComparison.Ordinal))
            {
                var value = line.AsSpan(SegmentTag.Length);
                var comma = value.IndexOf(',');
                pendingDuration = ParseDouble(comma >= 0 ? value[..comma] : value);
            }
            else if (line.StartsWith(DiscontinuityTag, StringComparison.Ordinal)
                && !line.StartsWith("#EXT-X-DISCONTINUITY-SEQUENCE", StringComparison.Ordinal))
            {
                pendingDiscontinuity = true;
            }
            else if (line.StartsWith(EndListTag, StringComparison.Ordinal))
            {
                endList = true;
            }
            else if (line.StartsWith(KeyTag, StringComparison.Ordinal))
            {
                var method = Attribute(line.AsSpan(KeyTag.Length), "METHOD");
                encrypted = !method.Equals("NONE", StringComparison.OrdinalIgnoreCase);
            }
            else if (line.StartsWith(MapTag, StringComparison.Ordinal))
            {
                fragmented = true;
            }
            else if (line[0] != '#' && pendingDuration >= 0)
            {
                if (TryResolve(baseUri, line, out var uri))
                {
                    segments.Add(new HlsSegment(uri, sequence, pendingDuration, pendingDiscontinuity));
                }

                sequence++;
                pendingDuration = -1;
                pendingDiscontinuity = false;
            }
        }

        return new HlsMediaPlaylist(segments.ToArray(), targetDuration, endList, encrypted, fragmented);
    }

    // Audio-only variants first, the best one under the cap; failing that a separate audio
    // rendition; and only then the leanest muxed variant, whose video the demuxer simply ignores.
    public static Uri? SelectAudio(HlsMasterPlaylist master, int bitrateCap)
    {
        var best = -1;
        var leanest = -1;
        var variants = master.Variants;
        for (var index = 0; index < variants.Length; index++)
        {
            if (variants[index].HasVideo)
            {
                continue;
            }

            var bandwidth = variants[index].Bandwidth;
            if (bandwidth <= bitrateCap && (best < 0 || bandwidth > variants[best].Bandwidth))
            {
                best = index;
            }

            if (leanest < 0 || bandwidth < variants[leanest].Bandwidth)
            {
                leanest = index;
            }
        }

        if (best >= 0)
        {
            return variants[best].Uri;
        }

        if (leanest >= 0)
        {
            return variants[leanest].Uri;
        }

        if (master.AudioRenditions.Length > 0)
        {
            return master.AudioRenditions[0];
        }

        for (var index = 0; index < variants.Length; index++)
        {
            if (leanest < 0 || variants[index].Bandwidth < variants[leanest].Bandwidth)
            {
                leanest = index;
            }
        }

        return leanest >= 0 ? variants[leanest].Uri : null;
    }

    public static string Attribute(ReadOnlySpan<char> attributes, string name)
    {
        var position = 0;
        while (position < attributes.Length)
        {
            var equals = attributes[position..].IndexOf('=');
            if (equals < 0)
            {
                return string.Empty;
            }

            var key = attributes.Slice(position, equals).Trim();
            position += equals + 1;
            int valueStart;
            int valueEnd;
            if (position < attributes.Length && attributes[position] == '"')
            {
                valueStart = position + 1;
                var close = attributes[valueStart..].IndexOf('"');
                valueEnd = close < 0 ? attributes.Length : valueStart + close;
                position = valueEnd + 1;
            }
            else
            {
                valueStart = position;
                var comma = attributes[position..].IndexOf(',');
                valueEnd = comma < 0 ? attributes.Length : position + comma;
                position = valueEnd;
            }

            if (key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return attributes[valueStart..valueEnd].ToString();
            }

            var nextComma = position < attributes.Length ? attributes[position..].IndexOf(',') : -1;
            if (nextComma < 0)
            {
                return string.Empty;
            }

            position += nextComma + 1;
        }

        return string.Empty;
    }

    private static void AddAudioRendition(ReadOnlySpan<char> attributes, Uri baseUri, List<Uri> renditions)
    {
        if (!Attribute(attributes, "TYPE").Equals("AUDIO", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var target = Attribute(attributes, "URI");
        if (target.Length == 0 || !TryResolve(baseUri, target, out var uri))
        {
            return;
        }

        if (Attribute(attributes, "DEFAULT").Equals("YES", StringComparison.OrdinalIgnoreCase))
        {
            renditions.Insert(0, uri);
            return;
        }

        renditions.Add(uri);
    }

    private static bool HasVideo(ReadOnlySpan<char> attributes)
    {
        if (Attribute(attributes, "RESOLUTION").Length > 0)
        {
            return true;
        }

        var codecs = Attribute(attributes, "CODECS");
        for (var index = 0; index < VideoCodecPrefixes.Length; index++)
        {
            if (codecs.Contains(VideoCodecPrefixes[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryResolve(Uri baseUri, string reference, out Uri uri)
    {
        return Uri.TryCreate(baseUri, reference.Trim(), out uri!)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static double ParseDouble(ReadOnlySpan<char> value)
    {
        return double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }
}
