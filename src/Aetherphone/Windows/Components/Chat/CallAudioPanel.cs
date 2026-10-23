using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Audio;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal sealed class CallAudioPanel
{
    private const float MeterRowHeight = 20f;
    private const float MeterThickness = 4f;
    private const float ThresholdMarkWidth = 2f;
    private const float ThresholdMarkHeight = 10f;
    private static readonly Vector4 MeterInk = new(0.20f, 0.78f, 0.35f, 1f);
    private readonly CallHub calls;
    private readonly Configuration configuration;
    private readonly Dictionary<string, string> volumeIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> muteIds = new(StringComparer.Ordinal);

    public CallAudioPanel(CallHub calls, Configuration configuration)
    {
        this.calls = calls;
        this.configuration = configuration;
    }

    public bool Draw(PhoneTheme theme, in CallView view)
    {
        AudioDevices.Refresh();
        var scale = UiScale.Current;
        var dragging = DrawCallVolume(theme, view.Volume);
        var participants = view.Participants;
        for (var index = 0; index < participants.Length; index++)
        {
            var participant = participants[index];
            if (participant.UserId == view.LocalUserId || participant.State != ParticipantState.Active)
            {
                continue;
            }

            dragging |= DrawPeer(theme, participant);
        }

        dragging |= DrawMicrophoneVolume(theme);
        dragging |= DrawMicrophoneThreshold(theme, view);
        SettingsSection.Header(Loc.T(L.Phone.Speaker), theme);
        DrawOutputDevices(theme);
        SettingsSection.Header(Loc.T(L.Phone.Microphone), theme);
        DrawInputDevices(theme);
        SettingsSection.Hint(Loc.T(L.Phone.AudioDevicesHint), theme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        return dragging;
    }

    private bool DrawCallVolume(PhoneTheme theme, float volume)
    {
        SettingsSection.Header(Loc.T(L.Phone.CallVolume), theme);
        var card = GroupCard.Begin(theme, 1);
        var result = VolumeSlider.Draw("##call.volume", card.NextRow(), volume, theme, VoiceMixer.MaximumGain);
        card.End();
        if (result.Dragging || result.Released)
        {
            calls.SetVolume(result.Value);
        }

        if (result.Released)
        {
            calls.SaveAudioSettings();
        }

        return result.Dragging;
    }

    private bool DrawPeer(PhoneTheme theme, ParticipantInfo participant)
    {
        var userId = participant.UserId;
        SettingsSection.Header(calls.DisplayNameOf(participant), theme);
        var card = GroupCard.Begin(theme, 2);
        var muted = calls.PeerMuted(userId);
        var result = VolumeSlider.Draw(IdFor(volumeIds, "##call.peer.volume.", userId), card.NextRow(),
            calls.PeerVolume(userId), theme, VoiceMixer.MaximumGain);
        var mutedNext = SettingsRow.Bool(card.NextRow(), Loc.T(L.Phone.MuteForMe), muted, theme,
            IdFor(muteIds, "call.peer.mute.", userId));
        card.End();
        if (result.Dragging || result.Released)
        {
            calls.SetPeerVolume(userId, result.Value);
        }

        if (result.Released)
        {
            calls.SaveAudioSettings();
        }

        if (mutedNext != muted)
        {
            calls.SetPeerMuted(userId, mutedNext);
        }

        return result.Dragging;
    }

    private bool DrawMicrophoneVolume(PhoneTheme theme)
    {
        SettingsSection.Header(Loc.T(L.Phone.MicVolume), theme);
        var autoGain = calls.AutoGain;
        var card = GroupCard.Begin(theme, autoGain ? 1 : 2);
        var autoGainNext = SettingsRow.Bool(card.NextRow(), Loc.T(L.Phone.AutoGain), autoGain, theme, "call.autoGain",
            Loc.T(L.Phone.AutoGainHint));
        var dragging = false;
        if (!autoGain)
        {
            var result = VolumeSlider.Draw("##call.micGain", card.NextRow(), calls.InputGain, theme,
                AudioCapture.MaximumGain);
            if (result.Dragging || result.Released)
            {
                calls.SetInputGain(result.Value);
            }

            if (result.Released)
            {
                calls.SaveAudioSettings();
            }

            dragging = result.Dragging;
        }

        card.End();
        SettingsSection.Hint(Loc.T(autoGain ? L.Phone.AutoGainVolumeHint : L.Phone.MicVolumeHint), theme);
        if (autoGainNext != autoGain)
        {
            calls.SetAutoGain(autoGainNext);
        }

        return dragging;
    }

    private bool DrawMicrophoneThreshold(PhoneTheme theme, in CallView view)
    {
        SettingsSection.Header(Loc.T(L.Phone.MicThreshold), theme);
        var live = view.InCall;
        if (live && calls.MicTestActive)
        {
            calls.SetMicTest(false);
        }

        var testing = !live && calls.MicTestActive;
        if (testing)
        {
            calls.KeepMicTestAlive();
        }

        var metering = live || testing;
        var card = GroupCard.Begin(theme, GroupCard.DefaultRowHeight + (metering ? MeterRowHeight : 0f)
            + (live ? 0f : GroupCard.DefaultRowHeight));
        var result = VolumeSlider.DrawStepped("##call.micThreshold", card.NextRow(GroupCard.DefaultRowHeight),
            calls.InputThreshold, theme, FontAwesomeIcon.Microphone, MicThreshold.Readouts);
        var clipping = metering && calls.MicClipping;
        if (metering)
        {
            var level = live ? (view.Muted ? 0f : view.MicLevel) : calls.MicTestLevel;
            DrawMeter(card.NextRow(MeterRowHeight), result.Track, level, result.Value, clipping, theme);
        }

        var testNext = testing;
        if (!live)
        {
            testNext = SettingsRow.Bool(card.NextRow(GroupCard.DefaultRowHeight), Loc.T(L.Phone.TestMicrophone),
                testing, theme, "call.micTest");
        }

        card.End();
        SettingsSection.Hint(Loc.T(L.Phone.MicThresholdHint), theme);
        if (clipping)
        {
            SettingsSection.Hint(Loc.T(L.Phone.MicClipping), theme.Danger);
        }

        if (testNext != testing)
        {
            calls.SetMicTest(testNext);
        }

        if (result.Dragging || result.Released)
        {
            calls.SetInputThreshold(result.Value);
        }

        if (result.Released)
        {
            calls.SaveAudioSettings();
        }

        return result.Dragging;
    }

    private static void DrawMeter(Rect row, Rect track, float level, float threshold, bool clipping,
        PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var top = row.Min.Y + Metrics.Space.Xs * scale;
        var min = new Vector2(track.Min.X, top);
        var max = new Vector2(track.Max.X, top + MeterThickness * scale);
        var radius = MeterThickness * 0.5f * scale;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(theme.ToggleOff), radius);
        var fill = MicThreshold.MeterFill(level);
        if (fill > 0f)
        {
            drawList.AddRectFilled(min, new Vector2(min.X + (max.X - min.X) * fill, max.Y),
                ImGui.GetColorU32(MeterColor(level, threshold, clipping, theme)), radius);
        }

        var thresholdX = min.X + (max.X - min.X) * Math.Clamp(threshold, 0f, 1f);
        var markCenterY = (min.Y + max.Y) * 0.5f;
        var markHalf = ThresholdMarkHeight * 0.5f * scale;
        drawList.AddRectFilled(new Vector2(thresholdX - ThresholdMarkWidth * 0.5f * scale, markCenterY - markHalf),
            new Vector2(thresholdX + ThresholdMarkWidth * 0.5f * scale, markCenterY + markHalf),
            ImGui.GetColorU32(theme.TextStrong));
    }

    private static Vector4 MeterColor(float level, float threshold, bool clipping, PhoneTheme theme)
    {
        if (clipping)
        {
            return theme.Danger;
        }

        return MicThreshold.Opens(level, threshold) ? MeterInk : theme.TextMuted;
    }

    private void DrawOutputDevices(PhoneTheme theme)
    {
        var outputs = AudioDevices.Outputs;
        var current = configuration.CallOutputDevice;
        var card = GroupCard.Begin(theme, outputs.Length + 1);
        if (SettingsRow.Selectable(card.NextRow(), Loc.T(L.Phone.SystemDefault), string.IsNullOrEmpty(current), theme,
                "call.speaker.default"))
        {
            calls.SelectOutputDevice(string.Empty);
        }

        for (var index = 0; index < outputs.Length; index++)
        {
            var name = outputs[index];
            if (SettingsRow.Selectable(card.NextRow(), name, current == name, theme))
            {
                calls.SelectOutputDevice(name);
            }
        }

        card.End();
    }

    private void DrawInputDevices(PhoneTheme theme)
    {
        var inputs = AudioDevices.Inputs;
        var current = configuration.CallInputDevice;
        var card = GroupCard.Begin(theme, inputs.Length + 1);
        if (SettingsRow.Selectable(card.NextRow(), Loc.T(L.Phone.SystemDefault), string.IsNullOrEmpty(current), theme,
                "call.microphone.default"))
        {
            calls.SelectInputDevice(string.Empty);
        }

        for (var index = 0; index < inputs.Length; index++)
        {
            var name = inputs[index];
            var label = string.IsNullOrWhiteSpace(name) ? Loc.T(L.Phone.DeviceFallback, index + 1) : name;
            if (SettingsRow.Selectable(card.NextRow(), label, current == name, theme))
            {
                calls.SelectInputDevice(name);
            }
        }

        card.End();
    }

    private static string IdFor(Dictionary<string, string> cache, string prefix, string userId)
    {
        if (cache.TryGetValue(userId, out var id))
        {
            return id;
        }

        id = prefix + userId;
        cache[userId] = id;
        return id;
    }
}
