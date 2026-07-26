namespace Aetherphone.Apps.Announcements;

internal enum AnnouncementsScreen
{
    List,
    Detail,
}

internal readonly record struct AnnouncementsRoute(AnnouncementsScreen Screen, string? Id = null)
{
    public static readonly AnnouncementsRoute List = new(AnnouncementsScreen.List);

    public static AnnouncementsRoute Detail(string announcementId) =>
        new(AnnouncementsScreen.Detail, announcementId);
}
