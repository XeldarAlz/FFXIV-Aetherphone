namespace Aetherphone.Core.Announcements;

internal sealed class AnnouncementsLauncher
{
    private string? pendingAnnouncementId;

    public void RequestDetail(string announcementId)
    {
        pendingAnnouncementId = announcementId;
    }

    public bool TryConsumeDetail(out string announcementId)
    {
        if (pendingAnnouncementId is null)
        {
            announcementId = string.Empty;
            return false;
        }

        announcementId = pendingAnnouncementId;
        pendingAnnouncementId = null;
        return true;
    }
}
