using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Settings.Pages;

internal static class MusicMediaSettings
{
    public const string AppId = "music";
    private const int RowCount = 2;
    private const int FriendsRowCount = 1;

    public static void Draw(Configuration configuration, PhoneTheme theme)
    {
        DrawPlayback(configuration, theme);
        DrawWindowsMedia(configuration, theme);
        DrawFriends(configuration, theme);
    }

    private static void DrawWindowsMedia(Configuration configuration, PhoneTheme theme)
    {
        SettingsSection.Header(Loc.T(L.Music.PcMedia.SettingsHeader), theme);
        var card = GroupCard.Begin(theme, RowCount);
        var show = SettingsRow.Bool(card.NextRow(), Loc.T(L.Music.PcMedia.ShowWindowsMedia),
            configuration.ShowWindowsMedia, theme, "settings.music.showWindowsMedia");
        var publish = SettingsRow.Bool(card.NextRow(), Loc.T(L.Music.PcMedia.PublishToWindowsMedia),
            configuration.PublishToWindowsMedia, theme, "settings.music.publishToWindowsMedia");
        card.End();
        SettingsSection.Hint(Loc.T(L.Music.PcMedia.SettingsHint), theme);
        if (show == configuration.ShowWindowsMedia && publish == configuration.PublishToWindowsMedia)
        {
            return;
        }

        configuration.ShowWindowsMedia = show;
        configuration.PublishToWindowsMedia = publish;
        configuration.Save();
    }

    private static void DrawFriends(Configuration configuration, PhoneTheme theme)
    {
        SettingsSection.Header(Loc.T(L.Music.Friends.SettingsHeader), theme);
        var card = GroupCard.Begin(theme, FriendsRowCount);
        var share = SettingsRow.Bool(card.NextRow(), Loc.T(L.Music.Friends.ShareToggle),
            configuration.ShareListeningActivity, theme, "settings.music.shareListening");
        card.End();
        SettingsSection.Hint(Loc.T(L.Music.Friends.SettingsHint), theme);
        if (share == configuration.ShareListeningActivity)
        {
            return;
        }

        configuration.ShareListeningActivity = share;
        configuration.ListeningPromptShown = true;
        configuration.Save();
    }

    private static void DrawPlayback(Configuration configuration, PhoneTheme theme)
    {
        SettingsSection.Header(Loc.T(L.Music.SoundCheck.SettingsHeader), theme);
        var card = GroupCard.Begin(theme, 1);
        var soundCheck = SettingsRow.Bool(card.NextRow(), Loc.T(L.Music.SoundCheck.Title),
            configuration.MusicSoundCheck, theme, "settings.music.soundCheck", Loc.T(L.Music.SoundCheck.Hint));
        card.End();
        if (soundCheck == configuration.MusicSoundCheck)
        {
            return;
        }

        configuration.MusicSoundCheck = soundCheck;
        configuration.Save();
    }
}
