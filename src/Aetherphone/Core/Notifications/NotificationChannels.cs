using System.Numerics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Notifications;

internal readonly record struct NotificationChannel(string AppId, LocString Name, Vector4 Accent);

internal static class NotificationChannels
{
    public static readonly IReadOnlyList<NotificationChannel> All = new NotificationChannel[]
    {
        new("message", L.Apps.Message, AppAccents.For("message")),
        new("messages", L.Apps.Chat, AppAccents.For("messages")),
        new("chirper", L.Apps.Chirper, AppAccents.For("chirper")),
        new("aethergram", L.Apps.Aethergram, AppAccents.For("aethergram")),
        new("velvet", L.Apps.Velvet, AppAccents.For("velvet")),
        new("market", L.Apps.Market, AppAccents.For("market")),
        new("venues", L.Apps.Venues, AppAccents.For("venues")),
        new("timers", L.Apps.Timers, AppAccents.For("timers")),
        new("calendar", L.Apps.Calendar, AppAccents.For("calendar")),
        new("clock", L.Apps.Clock, AppAccents.For("clock")),
        new("notes", L.Apps.Notes, AppAccents.For("notes")),
    };
}
