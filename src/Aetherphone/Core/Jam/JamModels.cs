using Aetherphone.Core.Songs;

namespace Aetherphone.Core.Jam;

internal enum JamMode : byte
{
    Idle,
    Starting,
    Joining,
    Pending,
    Hosting,
    Listening,
}

internal enum JamDeclineReason : byte
{
    None,
    BadCode,
    Full,
    Denied,
    Ended,
    Busy,
    Kicked,
    ConnectionLost,
    NoResponse,
    Offline,
}

internal enum JamRefusal : byte
{
    None,
    HostControlsPlayback,
    HostControlsQueue,
    NotYourEntry,
}

internal enum JamChatRefusal : byte
{
    None,
    Cooldown,
    TooLong,
    Empty,
    NotInJam,
    Unknown,
}

internal static class JamChatRefusalCodes
{
    public const string Cooldown = "cooldown";
    public const string TooLong = "tooLong";
    public const string Empty = "empty";
    public const string NotInJam = "notInJam";

    public static JamChatRefusal Parse(string? reason)
    {
        return reason switch
        {
            Cooldown => JamChatRefusal.Cooldown,
            TooLong => JamChatRefusal.TooLong,
            Empty => JamChatRefusal.Empty,
            NotInJam => JamChatRefusal.NotInJam,
            _ => JamChatRefusal.Unknown,
        };
    }
}

internal static class JamPermission
{
    public const int None = 0;
    public const int AddToQueue = 1;
    public const int ControlPlayback = 2;
    public const int All = AddToQueue | ControlPlayback;

    public static bool Allows(int held, int permission) => (held & permission) != 0;
}

internal static class JamControlAction
{
    public const string Play = "play";
    public const string Pause = "pause";
    public const string Seek = "seek";
    public const string Next = "next";
    public const string Previous = "previous";
}

internal static class JamQueueMode
{
    public const string Next = "next";
    public const string End = "end";
}

internal static class JamDeclineCodes
{
    public const string BadCode = "code";
    public const string Full = "full";
    public const string Denied = "denied";
    public const string Ended = "ended";
    public const string Busy = "busy";

    public static JamDeclineReason Parse(string? reason)
    {
        return reason switch
        {
            BadCode => JamDeclineReason.BadCode,
            Full => JamDeclineReason.Full,
            Denied => JamDeclineReason.Denied,
            Ended => JamDeclineReason.Ended,
            Busy => JamDeclineReason.Busy,
            _ => JamDeclineReason.BadCode,
        };
    }
}

internal readonly struct JamQueueItem
{
    public readonly int EntryId;
    public readonly Song Song;
    public readonly string AddedByUserId;
    public readonly string AddedByName;

    public JamQueueItem(int entryId, in Song song, string addedByUserId, string addedByName)
    {
        EntryId = entryId;
        Song = song;
        AddedByUserId = addedByUserId;
        AddedByName = addedByName;
    }
}

internal sealed record JamNearbyJam(
    string JamId,
    string Code,
    string Title,
    string HostId,
    string HostName,
    string HostHandleLabel,
    string? HostAvatarUrl,
    int MemberCount,
    Song Track);

internal sealed record JamJoinRequest(string UserId, string DisplayName, string Handle, string? AvatarUrl);

internal readonly struct JamReaction
{
    public readonly int Kind;
    public readonly string UserId;
    public readonly string DisplayName;
    public readonly long AtTicks;

    public JamReaction(int kind, string userId, string displayName, long atTicks)
    {
        Kind = kind;
        UserId = userId;
        DisplayName = displayName;
        AtTicks = atTicks;
    }
}
