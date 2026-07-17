using Aetherphone.Core;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class SoundSettingsPage : ISettingsPage
{
    private readonly SoundService sound;
    private readonly IAnalyticsService analytics;
    private readonly LocString title;
    private readonly FontAwesomeIcon icon;
    private readonly Vector4 tint;
    private readonly string segmentId;
    private readonly string analyticsKey;
    private readonly Func<string> getToken;
    private readonly Action<string> setToken;
    private readonly Func<float> getVolume;
    private readonly Action<float> setVolume;
    private readonly SoundImport import = new();

    public SoundSettingsPage(SoundService sound, IAnalyticsService analytics, LocString title, FontAwesomeIcon icon,
        Vector4 tint, string segmentId, string analyticsKey, Func<string> getToken, Action<string> setToken,
        Func<float> getVolume, Action<float> setVolume)
    {
        this.sound = sound;
        this.analytics = analytics;
        this.title = title;
        this.icon = icon;
        this.tint = tint;
        this.segmentId = segmentId;
        this.analyticsKey = analyticsKey;
        this.getToken = getToken;
        this.setToken = setToken;
        this.getVolume = getVolume;
        this.setVolume = setVolume;
    }

    public string Title => Loc.T(title);
    public string Summary => sound.Label(getToken());
    public FontAwesomeIcon Icon => icon;
    public Vector4 Tint => tint;

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = ImGuiHelpers.GlobalScale;
        if (import.TryTake(out var importedPath))
        {
            TryImport(importedPath);
        }

        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Settings.Sound), theme);
            SoundOptionList.Draw(theme, sound, getToken(), false, Select);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            SettingsSection.Header(Loc.T(L.Settings.Volume), theme);
            var volumeCard = GroupCard.Begin(theme, 1);
            var volumeIndex = SegmentStrip.Draw(segmentId, volumeCard.NextRow(), VolumeCatalog.Labels,
                VolumeCatalog.IndexOf(getVolume()), theme);
            volumeCard.End();
            var volume = VolumeCatalog.Scales[volumeIndex];
            if (MathF.Abs(volume - getVolume()) > 0.001f)
            {
                setVolume(volume);
                analytics.Track(AnalyticsEvents.SettingChanged(analyticsKey + "_volume",
                    volume.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)));
                sound.Preview(getToken(), volume);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            var importCard = GroupCard.Begin(theme, 1);
            if (SettingsRow.Disclosure(importCard.NextRow(), Loc.T(L.Settings.ImportSound), string.Empty, theme))
            {
                import.Launch(Loc.T(title));
            }

            importCard.End();
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            SettingsSection.Hint(Loc.T(L.Settings.SoundImportHint), theme);
        }
    }

    private void Select(string? token)
    {
        if (token is null)
        {
            return;
        }

        setToken(token);
        analytics.Track(AnalyticsEvents.SettingChanged(analyticsKey, token));
        sound.Preview(token, getVolume());
    }

    private void TryImport(string path)
    {
        try
        {
            var token = sound.AddUserFile(path);
            setToken(token);
            analytics.Track(AnalyticsEvents.SettingChanged(analyticsKey, "file"));
            sound.Preview(token, getVolume());
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Sound] import failed: {exception.Message}");
        }
    }
}
