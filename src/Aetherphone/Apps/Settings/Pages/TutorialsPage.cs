using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class TutorialsPage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.Tutorials);

    public string Summary =>
        configuration.TutorialsEnabled ? Loc.T(L.Settings.TutorialsSummary) : Loc.T(L.Settings.TutorialsOff);

    public FontAwesomeIcon Icon => FontAwesomeIcon.GraduationCap;
    public Vector4 Tint => new(0.62f, 0.42f, 0.96f, 1f);
    private readonly Configuration configuration;

    public TutorialsPage(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Settings.Tutorials), theme);
            var card = GroupCard.Begin(theme, 1);
            var enabled = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.TutorialsShow),
                configuration.TutorialsEnabled, theme);
            card.End();
            if (enabled != configuration.TutorialsEnabled)
            {
                OnboardingState.SetEnabled(enabled);
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            var actions = GroupCard.Begin(theme, 2);
            var replay = SettingsRow.Disclosure(actions.NextRow(), Loc.T(L.Settings.TutorialsReplay), string.Empty,
                theme);
            var reset = SettingsRow.Disclosure(actions.NextRow(), Loc.T(L.Settings.TutorialsReset), string.Empty,
                theme);
            actions.End();
            if (replay)
            {
                OnboardingState.SetEnabled(true);
                OnboardingState.RequestReplayWelcome();
                context.Navigation.GoHome();
            }

            if (reset)
            {
                OnboardingState.ResetAll();
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 16f * scale);
            using (Plugin.Fonts.Push(0.8f))
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
            {
                ImGui.TextWrapped(Loc.T(L.Settings.TutorialsHint));
            }
        }
    }
}
