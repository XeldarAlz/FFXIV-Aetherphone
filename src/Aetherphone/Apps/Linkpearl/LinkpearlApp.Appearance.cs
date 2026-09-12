using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private readonly Action<string> pickTheme;
    private readonly Action<string> pickWallpaper;
    private readonly Action<bool> setWallpaperPattern;
    private readonly Action clearWallpaperOverride;
    private string wallpaperScope = string.Empty;

    private string EffectiveWallpaper(string conversationKey) =>
        ChatWallpapers.Effective(configuration.LinkpearlChatWallpapers, configuration.LinkpearlWallpaper,
            conversationKey);

    private void DrawChatThemeScreen(Rect area)
    {
        var scale = UiScale.Current;
        chrome.DrawScreenHeader(area, Loc.T(L.Message.ChatTheme), backToSettings);
        var top = area.Min.Y + AppHeader.Height * scale;
        pickers.DrawThemePicker(new ChatThemePickerModel
        {
            Area = new Rect(new Vector2(area.Min.X, top), area.Max),
            ThemeId = activeTheme.Id,
            WallpaperId = EffectiveWallpaper(string.Empty),
            Pattern = configuration.LinkpearlWallpaperPattern,
            Hint = Loc.T(L.Linkpearl.ChatThemeHint),
            Pick = pickTheme,
        });
    }

    private void DrawWallpaperScreen(Rect area, string conversationKey)
    {
        var scale = UiScale.Current;
        var scoped = conversationKey.Length > 0;
        wallpaperScope = conversationKey;
        chrome.DrawScreenHeader(area, Loc.T(L.Message.Wallpaper), backToSettings);
        var top = area.Min.Y + AppHeader.Height * scale;
        pickers.DrawWallpaperPicker(new ChatWallpaperPickerModel
        {
            Area = new Rect(new Vector2(area.Min.X, top), area.Max),
            ThemeId = activeTheme.Id,
            WallpaperId = EffectiveWallpaper(conversationKey),
            Pattern = configuration.LinkpearlWallpaperPattern,
            Scoped = scoped,
            HasOverride = scoped && configuration.LinkpearlChatWallpapers.ContainsKey(conversationKey),
            Hint = Loc.T(scoped ? L.Message.WallpaperChatOnly : L.Message.WallpaperHint),
            Pick = pickWallpaper,
            SetPattern = setWallpaperPattern,
            ClearOverride = clearWallpaperOverride,
        });
    }

    private void SetTheme(string id)
    {
        configuration.LinkpearlChatTheme = id;
        configuration.Save();
    }

    private void SetWallpaperPattern(bool pattern)
    {
        configuration.LinkpearlWallpaperPattern = pattern;
        configuration.Save();
    }

    private void ClearWallpaperOverride()
    {
        if (configuration.LinkpearlChatWallpapers.Remove(wallpaperScope))
        {
            configuration.Save();
        }
    }

    private void SetWallpaper(string conversationKey, string id)
    {
        if (conversationKey.Length > 0)
        {
            configuration.LinkpearlChatWallpapers[conversationKey] = id;
        }
        else
        {
            configuration.LinkpearlWallpaper = id;
        }

        configuration.Save();
    }
}
