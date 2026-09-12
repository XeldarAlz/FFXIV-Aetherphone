using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private readonly ChatAppearancePickers pickers;
    private readonly Action<string> pickTheme;
    private readonly Action<string> pickWallpaper;
    private readonly Action<bool> setWallpaperPattern;
    private readonly Action clearWallpaperOverride;
    private string wallpaperScope = string.Empty;

    private void DrawSettingsTab(Rect area)
    {
        var scale = UiScale.Current;
        using (AppSurface.BeginEdgeToEdge(area))
        {
            var drawList = ImGui.GetWindowDrawList();
            if (session.IsSignedIn)
            {
                DrawMyProfileRow(drawList);
            }

            DrawSectionLabel(Loc.T(L.Message.TabChats));
            if (DrawSettingRow(drawList, PhoneIcons.Palette, activeTheme.Accent, Loc.T(L.Message.ChatTheme),
                    Loc.T(activeTheme.Name)))
            {
                router.Push(MessageRoute.ChatTheme);
            }

            if (DrawSettingRow(drawList, PhoneIcons.Wallpaper, TintTeal, Loc.T(L.Message.Wallpaper)))
            {
                router.Push(MessageRoute.DefaultWallpaper);
            }

            var starredCount = configuration.MessageStarredMessages.Count;
            if (DrawSettingRow(drawList, PhoneIcons.Star, TintGold, Loc.T(L.Message.StarredTitle),
                    starredCount > 0 ? starredCount.ToString(Loc.Culture) : string.Empty))
            {
                router.Push(MessageRoute.Starred);
            }

            var archivedCount = configuration.MessageArchivedChats.Count;
            if (DrawSettingRow(drawList, PhoneIcons.Archive, TintSlate, Loc.T(L.Message.Archived),
                    archivedCount > 0 ? archivedCount.ToString(Loc.Culture) : string.Empty, separator: false))
            {
                router.Push(MessageRoute.Archived);
            }

            DrawSectionLabel(Loc.T(L.Settings.General));
            if (DrawSettingRow(drawList, PhoneIcons.Bell, TintRed, Loc.T(L.Settings.Notifications)))
            {
                settingsLauncher.Request(SettingsPageKind.Notifications);
                navigation.Open("settings");
            }

            if (DrawSettingRow(drawList, PhoneIcons.Shield, TintAzure, Loc.T(L.Settings.Privacy)))
            {
                settingsLauncher.Request(SettingsPageKind.Privacy);
                navigation.Open("settings");
            }

            if (DrawSettingRow(drawList, PhoneIcons.Phone, TintGreen, Loc.T(L.Phone.SettingsTitle),
                    Loc.T(calls.Enabled ? L.Common.On : L.Common.Off)))
            {
                settingsLauncher.Request(SettingsPageKind.Calls);
                navigation.Open("settings");
            }

            if (DrawSettingRow(drawList, PhoneIcons.ShieldCheck, TintViolet, Loc.T(L.Friends.NewNumberTitle),
                    separator: false) && session.IsSignedIn)
            {
                router.Push(MessageRoute.Safety);
            }

            DrawInlineEmpty(drawList, Loc.T(L.Message.OpensSettings));
            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawChatTheme(Rect area)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Message.ChatTheme));
        var top = area.Min.Y + AppHeader.Height * scale;
        pickers.DrawThemePicker(new ChatThemePickerModel
        {
            Area = new Rect(new Vector2(area.Min.X, top), area.Max),
            ThemeId = configuration.MessageChatTheme,
            WallpaperId = DefaultWallpaperId(),
            Pattern = configuration.MessageWallpaperPattern,
            Hint = Loc.T(L.Message.ChatThemeHint),
            Pick = pickTheme,
        });
    }

    private void DrawWallpaper(Rect area, string conversationId)
    {
        var scale = UiScale.Current;
        var scoped = conversationId.Length > 0;
        wallpaperScope = conversationId;
        DrawScreenHeader(area, Loc.T(L.Message.Wallpaper));
        var top = area.Min.Y + AppHeader.Height * scale;
        pickers.DrawWallpaperPicker(new ChatWallpaperPickerModel
        {
            Area = new Rect(new Vector2(area.Min.X, top), area.Max),
            ThemeId = configuration.MessageChatTheme,
            WallpaperId = ChatWallpapers.Effective(configuration.MessageChatWallpapers, configuration.MessageWallpaper,
                conversationId),
            Pattern = configuration.MessageWallpaperPattern,
            Scoped = scoped,
            HasOverride = scoped && configuration.MessageChatWallpapers.ContainsKey(conversationId),
            Hint = Loc.T(scoped ? L.Message.WallpaperChatOnly : L.Message.WallpaperHint),
            Pick = pickWallpaper,
            SetPattern = setWallpaperPattern,
            ClearOverride = clearWallpaperOverride,
        });
    }

    private string DefaultWallpaperId() =>
        ChatWallpapers.Effective(configuration.MessageChatWallpapers, configuration.MessageWallpaper, string.Empty);

    private void SetTheme(string id)
    {
        configuration.MessageChatTheme = id;
        configuration.Save();
    }

    private void SetWallpaperPattern(bool pattern)
    {
        configuration.MessageWallpaperPattern = pattern;
        configuration.Save();
    }

    private void ClearWallpaperOverride()
    {
        if (configuration.MessageChatWallpapers.Remove(wallpaperScope))
        {
            configuration.Save();
        }
    }

    private void SetWallpaper(string conversationId, string id)
    {
        if (conversationId.Length > 0)
        {
            configuration.MessageChatWallpapers[conversationId] = id;
        }
        else
        {
            configuration.MessageWallpaper = id;
        }

        configuration.Save();
    }
}
