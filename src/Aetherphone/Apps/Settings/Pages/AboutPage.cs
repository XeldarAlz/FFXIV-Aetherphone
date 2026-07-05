using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class AboutPage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.About);
    public string Summary => AepConstants.Version;
    public string Glyph => "i";
    public Vector4 Tint => new(0.40f, 0.62f, 0.92f, 1f);
    private readonly Action showAbout;

    public AboutPage(Action showAbout)
    {
        this.showAbout = showAbout;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Settings.Information), theme);
            var card = GroupCard.Begin(theme, 3);
            SettingsRow.Info(card.NextRow(), Loc.T(L.Settings.Plugin), AepConstants.Name, theme);
            SettingsRow.Info(card.NextRow(), Loc.T(L.Settings.Version), AepConstants.Version, theme);
            SettingsRow.Info(card.NextRow(), Loc.T(L.Settings.Command), AepConstants.PrimaryCommand, theme);
            card.End();
            SettingsSection.Header(Loc.T(L.Settings.CreditsLinks), theme);
            var links = GroupCard.Begin(theme, 1);
            if (SettingsRow.Link(links.NextRow(), Glyph, Tint, Loc.T(L.Settings.AboutAetherphone), string.Empty, theme))
            {
                showAbout();
            }

            links.End();
        }
    }
}
