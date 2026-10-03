using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Audio;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class CallsPage : ISettingsPage
{
    private static readonly SettingsEntry[] Searchable =
    {
        new(L.Phone.EnablePhoneCalls),
        new(L.Phone.Microphone),
        new(L.Phone.Speaker),
    };

    public string Title => Loc.T(L.Phone.SettingsTitle);
    public string Summary => calls.Enabled ? string.Empty : Loc.T(L.Phone.SummaryOff);
    public FontAwesomeIcon Icon => FontAwesomeIcon.Phone;
    public Vector4 Tint => new(0.20f, 0.78f, 0.35f, 1f);
    public ReadOnlySpan<SettingsEntry> Entries => Searchable;
    private readonly CallHub calls;
    private readonly Configuration configuration;

    public CallsPage(CallHub calls, Configuration configuration)
    {
        this.calls = calls;
        this.configuration = configuration;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            var toggleCard = GroupCard.Begin(theme, 1);
            var enabled = SettingsRow.Bool(toggleCard.NextRow(), Loc.T(L.Phone.EnablePhoneCalls), calls.Enabled, theme);
            toggleCard.End();
            if (enabled != calls.Enabled)
            {
                calls.SetEnabled(enabled);
            }

            SettingsSection.Header(Loc.T(L.Phone.Microphone), theme, Loc.T(L.Phone.AudioHint));
            var inputs = AudioDevices.InputNames();
            var current = configuration.CallInputDevice;
            var micCard = GroupCard.Begin(theme, inputs.Length + 1);
            if (SettingsRow.Selectable(micCard.NextRow(), Loc.T(L.Phone.SystemDefault), string.IsNullOrEmpty(current),
                    theme))
            {
                SetInput(string.Empty);
            }

            for (var index = 0; index < inputs.Length; index++)
            {
                var name = inputs[index];
                if (SettingsRow.Selectable(micCard.NextRow(), DeviceLabel(name, index), current == name, theme))
                {
                    SetInput(name);
                }
            }

            micCard.End();
            SettingsSection.Header(Loc.T(L.Phone.Speaker), theme);
            var outputs = AudioDevices.OutputNames();
            var currentOutput = configuration.CallOutputDevice;
            var speakerCard = GroupCard.Begin(theme, outputs.Length + 1);
            if (SettingsRow.Selectable(speakerCard.NextRow(), Loc.T(L.Phone.SystemDefault),
                    string.IsNullOrEmpty(currentOutput), theme))
            {
                SetOutput(string.Empty);
            }

            for (var index = 0; index < outputs.Length; index++)
            {
                var name = outputs[index];
                if (SettingsRow.Selectable(speakerCard.NextRow(), DeviceLabel(name, index), currentOutput == name,
                        theme))
                {
                    SetOutput(name);
                }
            }

                speakerCard.End();
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }
    }

    private void SetInput(string name)
    {
        if (configuration.CallInputDevice == name)
        {
            return;
        }

        configuration.CallInputDevice = name;
        configuration.Save();
    }

    private void SetOutput(string name)
    {
        if (configuration.CallOutputDevice == name)
        {
            return;
        }

        configuration.CallOutputDevice = name;
        configuration.Save();
    }

    private static string DeviceLabel(string name, int index)
    {
        return string.IsNullOrWhiteSpace(name) ? Loc.T(L.Phone.DeviceFallback, index + 1) : name;
    }
}
