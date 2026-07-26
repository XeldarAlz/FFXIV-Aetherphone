using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Announcements;

internal readonly record struct AnnouncementText(string Title, string Body)
{
    public static AnnouncementText For(AnnouncementDto announcement)
    {
        var translations = announcement.Translations;
        if (translations is null)
        {
            return new AnnouncementText(announcement.Title, announcement.Body);
        }

        var code = Loc.Current.Code;
        for (var index = 0; index < translations.Length; index++)
        {
            var translation = translations[index];
            if (translation.Lang != code)
            {
                continue;
            }

            var title = string.IsNullOrEmpty(translation.Title) ? announcement.Title : translation.Title;
            var body = string.IsNullOrEmpty(translation.Body) ? announcement.Body : translation.Body;
            return new AnnouncementText(title, body);
        }

        return new AnnouncementText(announcement.Title, announcement.Body);
    }
}
