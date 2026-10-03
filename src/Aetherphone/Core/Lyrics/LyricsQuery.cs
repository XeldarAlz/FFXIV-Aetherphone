using System.Text;

namespace Aetherphone.Core.Lyrics;

internal readonly struct LyricsCandidate
{
    public readonly string Artist;
    public readonly string Track;

    public LyricsCandidate(string artist, string track)
    {
        Artist = artist;
        Track = track;
    }

    public bool HasArtist => Artist.Length > 0;
}

internal readonly struct LyricsQuery
{
    private static readonly HashSet<string> BracketNoise = new(StringComparer.Ordinal)
    {
        "official", "video", "audio", "lyric", "lyrics", "hd", "hq", "4k", "8k", "1080p", "720p", "mv", "pv",
        "visualizer", "visualiser", "remaster", "remastered", "explicit", "clean", "subtitles", "subtitled", "sub",
        "subs", "romaji", "eng", "kan", "rom", "feat", "ft", "featuring", "theme", "ost", "soundtrack", "bgm",
        "prod", "coded", "audiotrack",
    };

    private static readonly HashSet<string> EdgeNoise = new(StringComparer.Ordinal)
    {
        "official", "video", "audio", "lyric", "lyrics", "hd", "hq", "4k", "mv", "pv", "visualizer", "visualiser",
    };

    private static readonly HashSet<string> WeakArtistWords = new(StringComparer.Ordinal)
    {
        "ost", "soundtrack", "ffxiv", "ffxv", "ffxvi", "ff14", "bgm",
    };

    private static readonly string[] FeaturingMarkers =
    {
        " feat. ", " feat ", " ft. ", " ft ", " featuring ",
    };

    private static readonly string[] ChannelSuffixes =
    {
        " - Topic", "VEVO", " Official Channel", " Official", "Official", " Channel",
    };

    private static readonly string[] Separators =
    {
        " - ", Spaced(EnDash), Spaced(EmDash), "|", " / ", " ~ ",
    };

    private static readonly char[] QuoteOpeners = { '「', '『', '"', '“' };

    private const char PipeSeparatorKind = '|';
    private const char EnDash = (char)0x2013;
    private const char EmDash = (char)0x2014;

    public readonly LyricsCandidate[] Candidates;
    public readonly int DurationSeconds;

    public LyricsQuery(LyricsCandidate[] candidates, int durationSeconds)
    {
        Candidates = candidates;
        DurationSeconds = durationSeconds;
    }

    public bool IsEmpty => Candidates is null || Candidates.Length == 0;

    public static LyricsQuery FromYoutube(string title, string channel, int durationSeconds)
    {
        var candidates = new List<LyricsCandidate>(6);
        var cleanTitle = StripBrackets((title ?? string.Empty).Normalize(NormalizationForm.FormKC)).Trim();
        var channelArtist = CleanChannel((channel ?? string.Empty).Normalize(NormalizationForm.FormKC),
            out var isTopic);
        if (IsWeakArtist(channelArtist))
        {
            channelArtist = string.Empty;
        }

        if (isTopic && channelArtist.Length > 0)
        {
            Add(candidates, channelArtist, TrimEdgeNoise(DropNoiseParts(cleanTitle)));
        }

        if (TrySplitQuoted(cleanTitle, out var quotedArtist, out var quotedTrack))
        {
            Add(candidates, quotedArtist.Length > 0 ? quotedArtist : channelArtist, quotedTrack);
            Add(candidates, string.Empty, quotedTrack);
            return new LyricsQuery(candidates.ToArray(), durationSeconds);
        }

        var parts = SplitParts(cleanTitle, out var separatorKind);
        if (parts.Count >= 2)
        {
            AddSplit(candidates, parts[0], parts[1], separatorKind, channelArtist);
        }
        else if (parts.Count == 1)
        {
            Add(candidates, channelArtist, parts[0]);
            Add(candidates, string.Empty, parts[0]);
        }

        return new LyricsQuery(candidates.ToArray(), durationSeconds);
    }

    private static string Spaced(char separator)
    {
        return string.Concat(" ", separator.ToString(), " ");
    }

    private static void AddSplit(List<LyricsCandidate> candidates, string left, string right, char separatorKind,
        string channelArtist)
    {
        var normalizedChannel = LyricsText.Normalize(channelArtist);
        var leftMatchesChannel = MatchesChannel(normalizedChannel, left);
        var rightMatchesChannel = MatchesChannel(normalizedChannel, right);
        var artistOnRight = rightMatchesChannel && !leftMatchesChannel
            || separatorKind == PipeSeparatorKind && !leftMatchesChannel
            || IsWeakArtist(right) && !IsWeakArtist(left);
        var artist = artistOnRight ? right : left;
        var track = artistOnRight ? left : right;
        if (IsWeakArtist(artist))
        {
            Add(candidates, string.Empty, track);
            Add(candidates, channelArtist, track);
            return;
        }

        Add(candidates, artist, track);
        if (!leftMatchesChannel && !rightMatchesChannel)
        {
            Add(candidates, track, artist);
        }

        if (channelArtist.Length > 0 && !leftMatchesChannel && !rightMatchesChannel)
        {
            Add(candidates, channelArtist, track);
        }

        Add(candidates, string.Empty, track);
    }

    private static bool MatchesChannel(string normalizedChannel, string side)
    {
        if (normalizedChannel.Length == 0)
        {
            return false;
        }

        return LyricsText.Similarity(normalizedChannel, LyricsText.Normalize(StripFeaturing(side))) >= 0.85;
    }

    private static void Add(List<LyricsCandidate> candidates, string artist, string track)
    {
        var cleanArtist = TrimEdgeNoise(StripFeaturing(artist)).Trim();
        var cleanTrack = TrimEdgeNoise(StripFeaturing(track)).Trim().Trim('"', '\'', '“', '”');
        if (cleanTrack.Length == 0 || LyricsText.Normalize(cleanTrack).Length == 0)
        {
            return;
        }

        if (IsWeakArtist(cleanArtist))
        {
            cleanArtist = string.Empty;
        }

        var normalizedArtist = LyricsText.Normalize(cleanArtist);
        var normalizedTrack = LyricsText.Normalize(cleanTrack);
        for (var index = 0; index < candidates.Count; index++)
        {
            var existing = candidates[index];
            if (string.Equals(LyricsText.Normalize(existing.Artist), normalizedArtist, StringComparison.Ordinal)
                && string.Equals(LyricsText.Normalize(existing.Track), normalizedTrack, StringComparison.Ordinal))
            {
                return;
            }
        }

        candidates.Add(new LyricsCandidate(cleanArtist, cleanTrack));
    }

    private static string CleanChannel(string channel, out bool isTopic)
    {
        isTopic = false;
        var value = channel.Trim();
        var changed = true;
        while (changed && value.Length > 0)
        {
            changed = false;
            for (var index = 0; index < ChannelSuffixes.Length; index++)
            {
                var suffix = ChannelSuffixes[index];
                if (value.Length <= suffix.Length || !value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (index == 0)
                {
                    isTopic = true;
                }

                value = value[..^suffix.Length].Trim();
                changed = true;
            }
        }

        return value;
    }

    private static bool IsWeakArtist(string artist)
    {
        var normalized = LyricsText.Normalize(artist);
        if (normalized.Length == 0)
        {
            return false;
        }

        if (normalized.Contains("final fantasy", StringComparison.Ordinal)
            || normalized.Contains("square enix", StringComparison.Ordinal))
        {
            return true;
        }

        var words = normalized.Split(' ');
        for (var index = 0; index < words.Length; index++)
        {
            if (WeakArtistWords.Contains(words[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static string StripBrackets(string title)
    {
        var builder = new StringBuilder(title.Length);
        var cursor = 0;
        while (cursor < title.Length)
        {
            var character = title[cursor];
            var closing = ClosingFor(character);
            if (closing == '\0')
            {
                builder.Append(character);
                cursor++;
                continue;
            }

            var close = title.IndexOf(closing, cursor + 1);
            if (close < 0)
            {
                builder.Append(character);
                cursor++;
                continue;
            }

            var inner = title.Substring(cursor + 1, close - cursor - 1);
            if (character != '(' || IsNoiseGroup(inner))
            {
                builder.Append(' ');
            }
            else
            {
                builder.Append(title, cursor, close - cursor + 1);
            }

            cursor = close + 1;
        }

        return CollapseSpaces(builder.ToString());
    }

    private static char ClosingFor(char opening)
    {
        return opening switch
        {
            '(' => ')',
            '[' => ']',
            '【' => '】',
            '〔' => '〕',
            _ => '\0',
        };
    }

    private static bool IsNoiseGroup(string inner)
    {
        var normalized = LyricsText.Normalize(inner);
        if (normalized.Length == 0)
        {
            return true;
        }

        var words = normalized.Split(' ');
        for (var index = 0; index < words.Length; index++)
        {
            if (BracketNoise.Contains(words[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TrySplitQuoted(string title, out string artist, out string track)
    {
        artist = string.Empty;
        track = string.Empty;
        var open = title.IndexOfAny(QuoteOpeners);
        if (open < 0)
        {
            return false;
        }

        var closing = title[open] switch
        {
            '「' => '」',
            '『' => '』',
            '“' => '”',
            _ => '"',
        };
        var close = title.IndexOf(closing, open + 1);
        if (close <= open + 1)
        {
            return false;
        }

        track = title.Substring(open + 1, close - open - 1).Trim();
        artist = TrimEdgeNoise(title[..open].Trim().TrimEnd('-', '|', ':', '/').Trim());
        return track.Length > 0;
    }

    private static List<string> SplitParts(string title, out char separatorKind)
    {
        separatorKind = '\0';
        var parts = new List<string>(3);
        var remaining = title;
        while (remaining.Length > 0)
        {
            var bestIndex = -1;
            var bestLength = 0;
            var bestKind = '\0';
            for (var index = 0; index < Separators.Length; index++)
            {
                var found = remaining.IndexOf(Separators[index], StringComparison.Ordinal);
                if (found < 0 || (bestIndex >= 0 && found >= bestIndex))
                {
                    continue;
                }

                bestIndex = found;
                bestLength = Separators[index].Length;
                bestKind = Separators[index].Trim()[0];
            }

            if (bestIndex < 0)
            {
                AddPart(parts, remaining);
                break;
            }

            if (AddPart(parts, remaining[..bestIndex]) && parts.Count == 1 && separatorKind == '\0')
            {
                separatorKind = bestKind;
            }

            remaining = remaining[(bestIndex + bestLength)..];
        }

        return parts;
    }

    private static bool AddPart(List<string> parts, string part)
    {
        var trimmed = TrimEdgeNoise(part.Trim());
        if (trimmed.Length == 0)
        {
            return false;
        }

        parts.Add(trimmed);
        return true;
    }

    private static string DropNoiseParts(string title)
    {
        var parts = SplitParts(title, out _);
        return parts.Count == 0 ? title : string.Join(" - ", parts);
    }

    private static string StripFeaturing(string value)
    {
        var padded = string.Concat(" ", value, " ");
        var cut = -1;
        for (var index = 0; index < FeaturingMarkers.Length; index++)
        {
            var found = padded.IndexOf(FeaturingMarkers[index], StringComparison.OrdinalIgnoreCase);
            if (found >= 0 && (cut < 0 || found < cut))
            {
                cut = found;
            }
        }

        if (cut < 0)
        {
            return value;
        }

        return cut == 0 ? string.Empty : value[..(cut - 1)].Trim();
    }

    private static string TrimEdgeNoise(string value)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var first = 0;
        var last = words.Length - 1;
        var trimmedLeading = TrimmedRun(words, ref first, last, 1);
        var trimmedTrailing = TrimmedRun(words, ref last, first, -1);
        if (!trimmedLeading && !trimmedTrailing)
        {
            return value.Trim();
        }

        if (first > last)
        {
            return string.Empty;
        }

        return string.Join(' ', words, first, last - first + 1);
    }

    private static bool TrimmedRun(string[] words, ref int edge, int limit, int step)
    {
        var cursor = edge;
        var sawStrongNoise = false;
        while (step > 0 ? cursor <= limit : cursor >= limit)
        {
            var normalized = LyricsText.Normalize(words[cursor]);
            if (normalized == "music")
            {
                cursor += step;
                continue;
            }

            if (!EdgeNoise.Contains(normalized))
            {
                break;
            }

            sawStrongNoise = true;
            cursor += step;
        }

        if (!sawStrongNoise)
        {
            return false;
        }

        edge = cursor;
        return true;
    }

    private static string CollapseSpaces(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsWhiteSpace(value[index]))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(value[index]);
        }

        return builder.ToString();
    }
}
