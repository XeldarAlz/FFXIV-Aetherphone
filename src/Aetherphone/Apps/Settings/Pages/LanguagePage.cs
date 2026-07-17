using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class LanguagePage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.Language);
    public string Summary => Loc.Current.NativeName;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Globe;
    public Vector4 Tint => new(0.30f, 0.62f, 0.95f, 1f);
    private readonly Configuration configuration;
    private readonly IAnalyticsService analytics;

    public LanguagePage(Configuration configuration, IAnalyticsService analytics)
    {
        this.configuration = configuration;
        this.analytics = analytics;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Settings.Language), theme);
            var languages = Languages.All;
            var card = GroupCard.Begin(theme, languages.Length);
            for (var index = 0; index < languages.Length; index++)
            {
                var language = languages[index];
                if (SettingsRow.Selectable(card.NextRow(), language.NativeName, language.Code == Loc.Current.Code,
                        theme) && language.Code != configuration.Language)
                {
                    configuration.Language = language.Code;
                    analytics.Track(AnalyticsEvents.SettingChanged("language", language.Code));
                    configuration.Save();
                    Loc.SetLanguage(language.Code);
                    Plugin.Fonts.OnLanguageChanged();
                    Plugin.OnLanguageChanged();
                }
            }

            card.End();
        }
    }
}
