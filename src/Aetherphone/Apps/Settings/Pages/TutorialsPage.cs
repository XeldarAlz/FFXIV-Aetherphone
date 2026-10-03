using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class TutorialsPage : ISettingsPage
{
    private static readonly SettingsEntry[] Searchable =
    {
        new(L.Settings.TutorialsShow),
        new(L.Settings.TutorialsReplay),
        new(L.Settings.TutorialsReset),
    };

    public string Title => Loc.T(L.Settings.Tutorials);

    public string Summary => configuration.TutorialsEnabled ? string.Empty : Loc.T(L.Settings.TutorialsOff);

    public FontAwesomeIcon Icon => FontAwesomeIcon.GraduationCap;
    public Vector4 Tint => new(0.62f, 0.42f, 0.96f, 1f);
    public string? GuideAnchor => "settings.row.tutorials";
    public ReadOnlySpan<SettingsEntry> Entries => Searchable;
    private readonly Configuration configuration;

    public TutorialsPage(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            var card = GroupCard.Begin(theme, 1);
            var enabled = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.TutorialsShow),
                configuration.TutorialsEnabled, theme, null, Loc.T(L.Settings.TutorialsHint));
            card.End();
            if (enabled != configuration.TutorialsEnabled)
            {
                OnboardingState.SetEnabled(enabled);
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            var actions = GroupCard.Begin(theme, 2);
            var replayRow = actions.NextRow();
            var replay = SettingsRow.Disclosure(replayRow, Loc.T(L.Settings.TutorialsReplay), string.Empty, theme);
            var resetRow = actions.NextRow();
            var reset = SettingsRow.Disclosure(resetRow, Loc.T(L.Settings.TutorialsReset), string.Empty, theme);
            actions.End();
            UiAnchors.Report("settings.tutorials.actions", new Rect(replayRow.Min, resetRow.Max));
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
        }
    }
}
