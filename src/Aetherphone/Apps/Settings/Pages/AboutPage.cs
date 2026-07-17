using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class AboutPage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.About);
    public string Summary => AepConstants.Version;
    public FontAwesomeIcon Icon => FontAwesomeIcon.InfoCircle;
    public Vector4 Tint => new(0.40f, 0.62f, 0.92f, 1f);
    private static readonly Vector4 DiscordTint = new(0.345f, 0.396f, 0.949f, 1f);
    private static readonly Vector4 WebsiteTint = new(0.13f, 0.63f, 0.60f, 1f);
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
            var links = GroupCard.Begin(theme, 3);
            if (SettingsRow.Link(links.NextRow(), FontAwesomeIcon.Users, DiscordTint, Loc.T(L.Settings.JoinDiscord),
                    string.Empty, theme))
            {
                UrlActions.OpenInBrowser(AepConstants.DiscordUrl);
            }

            if (SettingsRow.Link(links.NextRow(), FontAwesomeIcon.Globe, WebsiteTint, Loc.T(L.Settings.VisitWebsite),
                    string.Empty, theme))
            {
                UrlActions.OpenInBrowser(AepConstants.WebsiteUrl);
            }

            if (SettingsRow.Link(links.NextRow(), Icon, Tint, Loc.T(L.Settings.AboutAetherphone), string.Empty, theme))
            {
                showAbout();
            }

            links.End();
        }
    }
}
