using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Video;

internal static class PartyCode
{
    internal const int Length = 6;
    internal const int MaxInputLength = 16;

    private const int GroupLength = 3;

    internal static string Normalize(string? input)
    {
        if (input is not { Length: > 0 and <= MaxInputLength })
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[input.Length];
        var count = 0;
        for (var index = 0; index < input.Length; index++)
        {
            var character = char.ToUpperInvariant(input[index]);
            if (char.IsAsciiLetterOrDigit(character))
            {
                buffer[count] = character;
                count++;
            }
        }

        return count == Length ? new string(buffer[..count]) : string.Empty;
    }

    internal static string Display(string code) =>
        code.Length == Length ? string.Concat(code.AsSpan(0, GroupLength), " ", code.AsSpan(GroupLength)) : code;
}

internal static class PartyPermissions
{
    internal static int Held(int guestPermissions, StreamMember[]? members, string userId)
    {
        var held = guestPermissions;
        if (members is null)
        {
            return held;
        }

        for (var index = 0; index < members.Length; index++)
        {
            if (string.Equals(members[index].UserId, userId, StringComparison.Ordinal))
            {
                return held | members[index].Flags;
            }
        }

        return held;
    }

    internal static bool Allows(int held, int permission) => (held & permission) != 0;
}

internal readonly record struct PartyPolicy(bool ApprovalRequired, bool Discoverable, bool CodeEnabled,
    int GuestPermissions)
{
    internal const int GuestMask = StreamPermission.AddToQueue | StreamPermission.ControlPlayback;

    internal bool GuestsCanAdd => PartyPermissions.Allows(GuestPermissions, StreamPermission.AddToQueue);

    internal bool GuestsCanControl => PartyPermissions.Allows(GuestPermissions, StreamPermission.ControlPlayback);

    internal PartyPolicy WithGuestPermission(int permission, bool allowed) =>
        this with { GuestPermissions = allowed ? GuestPermissions | permission : GuestPermissions & ~permission };

    internal static PartyPolicy From(Configuration configuration) => new(
        configuration.VideoStreamApprovalRequired,
        configuration.VideoStreamDiscoverable,
        configuration.VideoPartyCodeEnabled,
        (configuration.VideoPartyGuestsCanAdd ? StreamPermission.AddToQueue : 0)
        | (configuration.VideoPartyGuestsCanControl ? StreamPermission.ControlPlayback : 0));

    internal void Store(Configuration configuration)
    {
        configuration.VideoStreamApprovalRequired = ApprovalRequired;
        configuration.VideoStreamDiscoverable = Discoverable;
        configuration.VideoPartyCodeEnabled = CodeEnabled;
        configuration.VideoPartyGuestsCanAdd = GuestsCanAdd;
        configuration.VideoPartyGuestsCanControl = GuestsCanControl;
        configuration.Save();
    }
}

internal static class PartyIdle
{
    internal const long GraceMilliseconds = 5 * 60 * 1000;

    internal static float RemainingSeconds(long idleSinceTicks, long nowTicks)
    {
        if (idleSinceTicks == 0)
        {
            return 0f;
        }

        return Math.Max(0f, (GraceMilliseconds - (nowTicks - idleSinceTicks)) / 1000f);
    }

    internal static bool Expired(long idleSinceTicks, long nowTicks) =>
        idleSinceTicks != 0 && nowTicks - idleSinceTicks >= GraceMilliseconds;
}

internal static class MediaInput
{
    internal static string Normalize(string? raw)
    {
        if (raw is null)
        {
            return string.Empty;
        }

        var trimmed = raw.Trim();
        while (trimmed.Length >= 2 && IsQuote(trimmed[0]) && trimmed[^1] == trimmed[0])
        {
            trimmed = trimmed[1..^1].Trim();
        }

        return trimmed;
    }

    internal static bool LooksLikeLocalPath(string input)
    {
        if (input.Length < 3 || LocalMediaToken.IsToken(input))
        {
            return false;
        }

        if (input.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return true;
        }

        return char.IsAsciiLetter(input[0]) && input[1] == ':' && (input[2] == '\\' || input[2] == '/');
    }

    internal static string Subtitle(string source, double? durationSeconds)
    {
        if (durationSeconds is not { } seconds || seconds <= 0d)
        {
            return source;
        }

        var clock = Localization.TimeText.MinutesSeconds((int)seconds);
        return source.Length > 0 ? string.Concat(source, "  ·  ", clock) : clock;
    }

    private static bool IsQuote(char character) => character is '"' or '\'';
}
