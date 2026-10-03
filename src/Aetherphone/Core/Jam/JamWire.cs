using Aetherphone.Core.Songs;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Core.Video;

namespace Aetherphone.Core.Jam;

internal static class JamWire
{
    public const int MaxTitleLength = 64;
    public const int MaxNearbyJams = 20;
    public const int MaxChatLength = 300;
    private const int MaxTrackTextLength = 256;
    private const int MaxUrlLength = 2048;

    public static Song ToSong(JamTrack? track)
    {
        if (track is null || string.IsNullOrEmpty(track.VideoId))
        {
            return default;
        }

        var duration = track.DurationSeconds is { } seconds && double.IsFinite(seconds) && seconds > 0
            ? (int)Math.Round(seconds)
            : 0;
        return new Song(track.VideoId, track.Title ?? string.Empty, track.Author ?? string.Empty,
            track.ThumbnailUrl ?? string.Empty, duration);
    }

    public static JamTrack? ToTrack(in Song song)
    {
        if (song.IsEmpty)
        {
            return null;
        }

        return new JamTrack(song.VideoId, Cap(song.Title, MaxTrackTextLength), Cap(song.Author, MaxTrackTextLength),
            song.ThumbnailUrl is { Length: > 0 and <= MaxUrlLength } thumbnail ? thumbnail : null,
            song.DurationSeconds > 0 ? song.DurationSeconds : null);
    }

    public static JamQueueItem[] ToQueue(JamQueueEntry[]? entries)
    {
        if (entries is null || entries.Length == 0)
        {
            return Array.Empty<JamQueueItem>();
        }

        var count = 0;
        var items = new JamQueueItem[entries.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            var song = ToSong(entry.Track);
            if (song.IsEmpty)
            {
                continue;
            }

            items[count] = new JamQueueItem(entry.EntryId, song, entry.AddedByUserId ?? string.Empty,
                entry.AddedByName ?? string.Empty);
            count++;
        }

        if (count < items.Length)
        {
            Array.Resize(ref items, count);
        }

        return items;
    }

    public static JamJoinRequest? ToJoinRequest(CallControl message)
    {
        var userId = message.UserId ?? message.From?.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var from = message.From;
        return new JamJoinRequest(userId, from?.DisplayName ?? string.Empty, from?.Handle ?? string.Empty,
            from?.AvatarUrl);
    }

    public static JamNearbyJam[] ToNearby(JamNearbyInfo[]? incoming, string currentJamId)
    {
        if (incoming is null || incoming.Length == 0)
        {
            return Array.Empty<JamNearbyJam>();
        }

        var result = new JamNearbyJam[Math.Min(incoming.Length, MaxNearbyJams)];
        var count = 0;
        for (var index = 0; index < incoming.Length && count < result.Length; index++)
        {
            var info = incoming[index];
            if (info is null || string.IsNullOrEmpty(info.JamId)
                || string.Equals(info.JamId, currentJamId, StringComparison.Ordinal)
                || PartyCode.Normalize(info.Code) is not { Length: > 0 } code
                || ContainsJam(result, count, info.JamId))
            {
                continue;
            }

            var handle = info.HostHandle ?? string.Empty;
            var handleLabel = handle.Length > 0 ? string.Concat("@", handle) : string.Empty;
            var displayName = info.HostDisplayName ?? string.Empty;
            result[count] = new JamNearbyJam(info.JamId, code, NormalizeTitle(info.Title), info.HostId ?? string.Empty,
                displayName.Length > 0 ? displayName : handleLabel, handleLabel, info.HostAvatarUrl,
                Math.Max(1, info.MemberCount), ToSong(info.Track));
            count++;
        }

        if (count < result.Length)
        {
            Array.Resize(ref result, count);
        }

        return result;
    }

    public static string? ValidateChat(string text, out JamChatRefusal refusal)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            refusal = JamChatRefusal.Empty;
            return null;
        }

        if (trimmed.Length > MaxChatLength)
        {
            refusal = JamChatRefusal.TooLong;
            return null;
        }

        refusal = JamChatRefusal.None;
        return trimmed;
    }

    public static string PublicName(ParticipantInfo? from)
    {
        if (from is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(from.DisplayName))
        {
            return from.DisplayName;
        }

        return string.IsNullOrEmpty(from.Handle) ? string.Empty : string.Concat("@", from.Handle);
    }

    public static string NormalizeTitle(string? title)
    {
        if (title is null)
        {
            return string.Empty;
        }

        var trimmed = title.Trim();
        return trimmed.Length > MaxTitleLength ? trimmed[..MaxTitleLength].TrimEnd() : trimmed;
    }

    public static int IndexOfEntry(JamQueueItem[] items, int entryId)
    {
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].EntryId == entryId)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool ContainsJam(JamNearbyJam[] items, int count, string jamId)
    {
        for (var index = 0; index < count; index++)
        {
            if (string.Equals(items[index].JamId, jamId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? Cap(string? text, int length)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text.Length > length ? text[..length] : text;
    }
}

internal static class JamQueueVersion
{
    public static bool ShouldApply(int current, int incoming, bool snapshot)
    {
        return snapshot || incoming > current;
    }
}
