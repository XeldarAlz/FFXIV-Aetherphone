using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;

namespace Aetherphone.Core.Jam;

internal static class JamInviteNotification
{
    public const string AppId = "music";

    public static string GroupKey(string code) => string.Concat(JamSession.InviteGroupPrefix, code);

    public static bool TryParseCode(string? groupKey, out string code)
    {
        if (groupKey is null || !groupKey.StartsWith(JamSession.InviteGroupPrefix, StringComparison.Ordinal)
            || groupKey.Length == JamSession.InviteGroupPrefix.Length)
        {
            code = string.Empty;
            return false;
        }

        code = groupKey[JamSession.InviteGroupPrefix.Length..];
        return true;
    }

    public static PhoneNotification Build(string code, string fromName, string? jamTitle, string? fromUserId)
    {
        var name = fromName.Length > 0 ? fromName : Loc.T(L.Music.Jam.SomeoneName);
        var body = string.IsNullOrWhiteSpace(jamTitle)
            ? string.Format(Loc.Culture, Loc.T(L.Music.Jam.InviteBody), name)
            : string.Format(Loc.Culture, Loc.T(L.Music.Jam.InviteBodyTitled), name, jamTitle);
        return new PhoneNotification(AppId, Loc.T(L.Music.Jam.InviteTitle), body, DateTime.Now,
            AppAccents.For(AppId), GroupKey(code))
        {
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ActorId = fromUserId,
        };
    }
}
