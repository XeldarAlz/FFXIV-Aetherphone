using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Audio;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class CallsPage : ISettingsPage
{
    public string Title => Loc.T(L.Phone.SettingsTitle);
    public string Summary => calls.Enabled ? Loc.T(L.Phone.SummaryOn) : Loc.T(L.Phone.SummaryOff);
    public string Glyph => "Ph";
    public Vector4 Tint => new(0.20f, 0.78f, 0.35f, 1f);
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
        var scale = ImGuiHelpers.GlobalScale;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Phone.Calls), theme);
            var toggleCard = GroupCard.Begin(theme, 1);
            var enabled = SettingsRow.Bool(toggleCard.NextRow(), Loc.T(L.Phone.EnablePhoneCalls), calls.Enabled, theme);
            toggleCard.End();
            if (enabled != calls.Enabled)
            {
                calls.SetEnabled(enabled);
            }

            SettingsSection.Header(Loc.T(L.Phone.Microphone), theme);
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
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
            {
                ImGui.PushTextWrapPos(0f);
                ImGui.TextWrapped(Loc.T(L.Phone.AudioHint));
                ImGui.PopTextWrapPos();
            }
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

    private static string DeviceLabel(string name, int index)
    {
        return string.IsNullOrWhiteSpace(name) ? Loc.T(L.Phone.DeviceFallback, index + 1) : name;
    }
}
