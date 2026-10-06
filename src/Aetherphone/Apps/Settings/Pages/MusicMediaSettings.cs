using Aetherphone.Core.Localization;
using Aetherphone.Core.SystemMedia;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal static class MusicMediaSettings
{
    public const string AppId = "music";
    private const int RowCount = 2;
    private const int FriendsRowCount = 1;

    private static readonly string[] SourceIds = new string[PcMediaView.SourceCapacity];
    private static readonly string[] SourceLabels = new string[PcMediaView.SourceCapacity];
    private static readonly FontAwesomeIcon[] SourceGlyphs = new FontAwesomeIcon[PcMediaView.SourceCapacity];

    public static void Draw(Configuration configuration, PcMediaSource pcMedia, PhoneTheme theme)
    {
        DrawPlayback(configuration, theme);
        DrawWindowsMedia(configuration, theme);
        if (configuration.ShowWindowsMedia)
        {
            DrawSource(pcMedia, theme);
        }

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
        configuration.SetWindowsMedia(show, publish);
    }

    public static void DrawSource(PcMediaSource pcMedia, PhoneTheme theme)
    {
        SettingsSection.Header(Loc.T(L.Music.PcMedia.SourceHeader), theme);
        var count = PcMediaView.SourceOptions(pcMedia, SourceIds, SourceLabels, SourceGlyphs,
            out var selected);
        var card = GroupCard.Begin(theme, count);
        var picked = -1;
        for (var index = 0; index < count; index++)
        {
            if (SettingsRow.Selectable(card.NextRow(), SourceLabels[index], index == selected, theme,
                    SourceIds[index].Length > 0 ? SourceIds[index] : "settings.music.source.automatic"))
            {
                picked = index;
            }
        }

        card.End();
        SettingsSection.Hint(Loc.T(L.Music.PcMedia.SourceHint), theme);
        if (picked >= 0)
        {
            pcMedia.Pin(SourceIds[picked]);
        }
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
