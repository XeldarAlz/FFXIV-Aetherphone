using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class CallsPage : ISettingsPage
{
    private static readonly SettingsEntry[] Searchable =
    {
        new(L.Phone.EnablePhoneCalls),
        new(L.Phone.CallVolume),
        new(L.Phone.MicVolume),
        new(L.Phone.AutoGain),
        new(L.Phone.Microphone),
        new(L.Phone.Speaker),
    };

    public string Title => Loc.T(L.Phone.SettingsTitle);
    public string Summary => calls.Enabled ? string.Empty : Loc.T(L.Phone.SummaryOff);
    public FontAwesomeIcon Icon => FontAwesomeIcon.Phone;
    public Vector4 Tint => new(0.20f, 0.78f, 0.35f, 1f);
    public ReadOnlySpan<SettingsEntry> Entries => Searchable;
    private readonly CallHub calls;
    private readonly CallAudioPanel audioPanel;

    public CallsPage(CallHub calls, Configuration configuration)
    {
        this.calls = calls;
        audioPanel = new CallAudioPanel(calls, configuration);
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        using (var surface = AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            var toggleCard = GroupCard.Begin(theme, 1);
            var enabled = SettingsRow.Bool(toggleCard.NextRow(), Loc.T(L.Phone.EnablePhoneCalls), calls.Enabled, theme);
            toggleCard.End();
            if (enabled != calls.Enabled)
            {
                calls.SetEnabled(enabled);
            }

            if (audioPanel.Draw(theme, calls.Snapshot()))
            {
                surface.CancelDrag();
            }
        }
    }
}
