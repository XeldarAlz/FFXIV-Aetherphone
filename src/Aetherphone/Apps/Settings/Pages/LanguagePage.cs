using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Translation;
using Aetherphone.Windows.Components;

using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class LanguagePage : ISettingsPage
{
    private static readonly SettingsEntry[] Searchable =
    {
        new(L.Settings.TranslateInto),
    };

    public string Title => Loc.T(L.Settings.Language);
    public string Summary => Loc.Current.NativeName;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Globe;
    public Vector4 Tint => new(0.30f, 0.62f, 0.95f, 1f);
    public ReadOnlySpan<SettingsEntry> Entries => Searchable;
    private readonly Configuration configuration;
    private readonly TranslationService translation;

    public LanguagePage(Configuration configuration, TranslationService translation)
    {
        this.configuration = configuration;
        this.translation = translation;
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
                    configuration.Save();
                    Loc.SetLanguage(language.Code);
                    Plugin.Fonts.OnLanguageChanged();
                    Plugin.OnLanguageChanged();
                }
            }

            card.End();
            if (!translation.Enabled)
            {
                return;
            }

            SettingsSection.Header(Loc.T(L.Settings.TranslateInto), theme);
            var target = configuration.TranslationTargetLanguage;
            var targetCard = GroupCard.Begin(theme, languages.Length + 1);
            if (SettingsRow.Selectable(targetCard.NextRow(), Loc.T(L.Settings.TranslateSameAsPhone), target.Length == 0,
                    theme, "settings.translate.same") && target.Length > 0)
            {
                SaveTarget(string.Empty);
            }

            for (var index = 0; index < languages.Length; index++)
            {
                var language = languages[index];
                if (SettingsRow.Selectable(targetCard.NextRow(), language.NativeName, language.Code == target, theme,
                        language.Code) && language.Code != target)
                {
                    SaveTarget(language.Code);
                }
            }

            targetCard.End();
            SettingsSection.Hint(Loc.T(L.Settings.TranslateIntoHint), theme);
        }
    }

    private void SaveTarget(string code)
    {
        configuration.TranslationTargetLanguage = code;
        configuration.Save();
    }
}
