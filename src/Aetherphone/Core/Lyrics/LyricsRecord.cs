using System.Globalization;
using System.Text;

namespace Aetherphone.Core.Lyrics;

internal enum LyricsRecordKind : byte
{
    Synced,
    Plain,
    Instrumental,
    Missing,
}

internal readonly struct LyricsRecord
{
    public static readonly TimeSpan FoundLifetime = TimeSpan.FromDays(90);
    public static readonly TimeSpan MissingLifetime = TimeSpan.FromDays(7);
    private const string Header = "aep-lyrics/1";
    private static readonly string[] KindNames = { "synced", "plain", "instrumental", "missing" };

    public readonly LyricsRecordKind Kind;
    public readonly string Text;
    public readonly long WrittenUnixSeconds;

    public LyricsRecord(LyricsRecordKind kind, string text, long writtenUnixSeconds)
    {
        Kind = kind;
        Text = text ?? string.Empty;
        WrittenUnixSeconds = writtenUnixSeconds;
    }

    public static LyricsRecord FromTrack(in LrcLibTrack track, long nowUnixSeconds)
    {
        if (track.HasSynced)
        {
            return new LyricsRecord(LyricsRecordKind.Synced, track.SyncedLyrics, nowUnixSeconds);
        }

        if (track.HasPlain)
        {
            return new LyricsRecord(LyricsRecordKind.Plain, track.PlainLyrics, nowUnixSeconds);
        }

        return new LyricsRecord(track.Instrumental ? LyricsRecordKind.Instrumental : LyricsRecordKind.Missing,
            string.Empty, nowUnixSeconds);
    }

    public bool IsFresh(long nowUnixSeconds)
    {
        var lifetime = Kind == LyricsRecordKind.Missing ? MissingLifetime : FoundLifetime;
        var age = nowUnixSeconds - WrittenUnixSeconds;
        return age >= 0 && age < (long)lifetime.TotalSeconds;
    }

    public LyricsState ToState()
    {
        return Kind switch
        {
            LyricsRecordKind.Synced => LyricsState.Ready(LrcDocument.Parse(Text)),
            LyricsRecordKind.Plain => LyricsState.Ready(LrcDocument.Plain(Text)),
            LyricsRecordKind.Instrumental => LyricsState.Instrumental,
            _ => LyricsState.NotFound,
        };
    }

    public byte[] Encode()
    {
        var builder = new StringBuilder(Header.Length + Text.Length + 32);
        builder.Append(Header).Append('\n');
        builder.Append(KindNames[(int)Kind]).Append('\n');
        builder.Append(WrittenUnixSeconds.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append(Text);
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public static bool TryDecode(byte[] bytes, out LyricsRecord record)
    {
        record = default;
        if (bytes is null || bytes.Length == 0)
        {
            return false;
        }

        var text = Encoding.UTF8.GetString(bytes);
        var headerEnd = text.IndexOf('\n');
        if (headerEnd < 0 || !text.AsSpan(0, headerEnd).SequenceEqual(Header))
        {
            return false;
        }

        var kindEnd = text.IndexOf('\n', headerEnd + 1);
        if (kindEnd < 0)
        {
            return false;
        }

        var kindIndex = Array.IndexOf(KindNames, text.Substring(headerEnd + 1, kindEnd - headerEnd - 1));
        if (kindIndex < 0)
        {
            return false;
        }

        var timeEnd = text.IndexOf('\n', kindEnd + 1);
        var timeSpan = timeEnd < 0 ? text.AsSpan(kindEnd + 1) : text.AsSpan(kindEnd + 1, timeEnd - kindEnd - 1);
        if (!long.TryParse(timeSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var written))
        {
            return false;
        }

        var body = timeEnd < 0 ? string.Empty : text.Substring(timeEnd + 1);
        record = new LyricsRecord((LyricsRecordKind)kindIndex, body, written);
        return true;
    }
}
