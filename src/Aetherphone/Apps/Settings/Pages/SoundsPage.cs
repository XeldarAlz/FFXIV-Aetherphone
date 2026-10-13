using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class SoundsPage : ISettingsPage
{
    private static readonly SettingsEntry[] Searchable =
    {
        new(L.Settings.Ringtone),
        new(L.Settings.NotificationSound),
        new(L.Settings.Vibration),
        new(L.Settings.UiSounds),
        new(L.Settings.UiSoundTaps, L.Settings.UiSounds),
        new(L.Settings.UiSoundTransitions, L.Settings.UiSounds),
        new(L.Settings.UiSoundToggles, L.Settings.UiSounds),
        new(L.Settings.UiSoundKeyboard, L.Settings.UiSounds),
        new(L.Settings.SeasonalSounds, L.Settings.UiSounds),
        new(L.Settings.GameSounds),
    };

    public string Title => Loc.T(L.Settings.Sounds);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.VolumeUp;
    public Vector4 Tint => new(0.95f, 0.40f, 0.65f, 1f);
    public ReadOnlySpan<SettingsEntry> Entries => Searchable;
    private readonly Configuration configuration;
    private readonly SoundService sound;
    private readonly ISettingsNavigator navigator;
    private readonly ISettingsPage ringtonePage;
    private readonly ISettingsPage notificationSoundPage;

    public SoundsPage(Configuration configuration, SoundService sound, ISettingsNavigator navigator,
        ISettingsPage ringtonePage, ISettingsPage notificationSoundPage)
    {
        this.configuration = configuration;
        this.sound = sound;
        this.navigator = navigator;
        this.ringtonePage = ringtonePage;
        this.notificationSoundPage = notificationSoundPage;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        using (var surface = AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            DrawSoundCard(theme, surface, SoundKind.Ringtone);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            DrawSoundCard(theme, surface, SoundKind.Notification);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            var vibrationCard = GroupCard.Begin(theme, 1);
            var vibration = SettingsRow.Bool(vibrationCard.NextRow(), Loc.T(L.Settings.Vibration),
                configuration.Vibration, theme, null, Loc.T(L.Settings.VibrationHint));
            vibrationCard.End();
            if (vibration != configuration.Vibration)
            {
                configuration.Vibration = vibration;
                configuration.Save();
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            DrawInterfaceSounds(theme, surface);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            DrawGameSounds(theme, surface);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }
    }

    private void DrawGameSounds(PhoneTheme theme, in AppSurface.SurfaceScope surface)
    {
        var card = GroupCard.Begin(theme, configuration.GameSounds ? 2 : 1);
        var master = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.GameSounds), configuration.GameSounds,
            theme, null, Loc.T(L.Settings.GameSoundsHint));
        if (master != configuration.GameSounds)
        {
            configuration.GameSounds = master;
            configuration.Save();
            if (master)
            {
                UiFeedback.Play(UiSound.GameCollect);
            }
        }

        if (configuration.GameSounds)
        {
            var volume = VolumeSlider.Draw("##gameSoundVolume", card.NextRow(), configuration.GameSoundVolume,
                theme);
            if (volume.Dragging)
            {
                surface.CancelDrag();
            }

            if (volume.Released && MathF.Abs(volume.Value - configuration.GameSoundVolume) > 0.001f)
            {
                configuration.GameSoundVolume = volume.Value;
                configuration.Save();
                UiFeedback.Play(UiSound.GameCollect);
            }
        }

        card.End();
    }

    private void DrawSoundCard(PhoneTheme theme, in AppSurface.SurfaceScope surface, SoundKind kind)
    {
        var ringtone = kind == SoundKind.Ringtone;
        var enabled = ringtone ? configuration.RingtoneEnabled : configuration.NotificationSoundsEnabled;
        var token = ringtone ? configuration.RingtoneSound : configuration.NotificationSound;
        var volume = ringtone ? configuration.RingtoneVolume : configuration.NotificationVolume;
        var card = GroupCard.Begin(theme, enabled ? 3 : 1);
        var toggled = SettingsRow.Bool(card.NextRow(),
            Loc.T(ringtone ? L.Settings.Ringtone : L.Settings.NotificationSound), enabled, theme,
            ringtone ? "##ringtoneEnabled" : "##notificationEnabled",
            Loc.T(ringtone ? L.Settings.RingtoneHint : L.Settings.NotificationSoundsHint));
        if (enabled)
        {
            if (SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Settings.Sound), sound.Label(kind, token), theme,
                    ringtone ? "##ringtoneSound" : "##notificationSound"))
            {
                navigator.Open(ringtone ? ringtonePage : notificationSoundPage);
            }

            var slider = VolumeSlider.Draw(ringtone ? "##ringtoneVolume" : "##notificationVolume", card.NextRow(),
                volume, theme);
            if (slider.Dragging)
            {
                surface.CancelDrag();
            }

            if (slider.Released && MathF.Abs(slider.Value - volume) > 0.001f)
            {
                if (ringtone)
                {
                    configuration.RingtoneVolume = slider.Value;
                }
                else
                {
                    configuration.NotificationVolume = slider.Value;
                }

                configuration.Save();
                sound.Preview(kind, token, slider.Value);
            }
        }

        card.End();
        if (toggled == enabled)
        {
            return;
        }

        if (ringtone)
        {
            configuration.RingtoneEnabled = toggled;
        }
        else
        {
            configuration.NotificationSoundsEnabled = toggled;
            if (toggled)
            {
                sound.Preview(kind, token, configuration.NotificationVolume);
            }
        }

        configuration.Save();
    }

    private void DrawInterfaceSounds(PhoneTheme theme, in AppSurface.SurfaceScope surface)
    {
        var masterCard = GroupCard.Begin(theme, configuration.UiSounds ? 2 : 1);
        var master = SettingsRow.Bool(masterCard.NextRow(), Loc.T(L.Settings.UiSounds), configuration.UiSounds,
            theme, null, Loc.T(L.Settings.UiSoundsHint));
        if (master != configuration.UiSounds)
        {
            configuration.UiSounds = master;
            configuration.Save();
            if (master)
            {
                UiFeedback.Play(UiSound.Success);
            }
        }

        if (configuration.UiSounds)
        {
            var volume = VolumeSlider.Draw("##uiSoundVolume", masterCard.NextRow(), configuration.UiSoundVolume,
                theme);
            if (volume.Dragging)
            {
                surface.CancelDrag();
            }

            if (volume.Released && MathF.Abs(volume.Value - configuration.UiSoundVolume) > 0.001f)
            {
                configuration.UiSoundVolume = volume.Value;
                configuration.Save();
                UiFeedback.Play(UiSound.MessageSent);
            }
        }

        masterCard.End();
        if (!configuration.UiSounds)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * UiScale.Current));
        var extrasCard = GroupCard.Begin(theme, 5);
        var taps = SettingsRow.Bool(extrasCard.NextRow(), Loc.T(L.Settings.UiSoundTaps),
            configuration.UiSoundTaps, theme);
        var transitions = SettingsRow.Bool(extrasCard.NextRow(), Loc.T(L.Settings.UiSoundTransitions),
            configuration.UiSoundTransitions, theme);
        var toggles = SettingsRow.Bool(extrasCard.NextRow(), Loc.T(L.Settings.UiSoundToggles),
            configuration.UiSoundToggles, theme);
        var keyboard = SettingsRow.Bool(extrasCard.NextRow(), Loc.T(L.Settings.UiSoundKeyboard),
            configuration.UiSoundKeyboard, theme, null, Loc.T(L.Settings.UiSoundExtrasHint));
        var seasonal = SettingsRow.Bool(extrasCard.NextRow(), Loc.T(L.Settings.SeasonalSounds),
            configuration.SeasonalSounds, theme, null, Loc.T(L.Settings.SeasonalSoundsHint));
        extrasCard.End();
        if (taps == configuration.UiSoundTaps && transitions == configuration.UiSoundTransitions &&
            toggles == configuration.UiSoundToggles && keyboard == configuration.UiSoundKeyboard &&
            seasonal == configuration.SeasonalSounds)
        {
            return;
        }

        configuration.UiSoundTaps = taps;
        configuration.UiSoundTransitions = transitions;
        configuration.UiSoundToggles = toggles;
        configuration.UiSoundKeyboard = keyboard;
        configuration.SeasonalSounds = seasonal;
        configuration.Save();
    }
}
