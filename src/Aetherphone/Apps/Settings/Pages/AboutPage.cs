using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Platform;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class AboutPage : ISettingsPage
{
    private readonly Configuration configuration;
    private readonly GameData gameData;
    private readonly AethernetSession aethernetSession;
    private DateTime copiedAt;

    public AboutPage(Configuration configuration, GameData gameData, AethernetSession aethernetSession)
    {
        this.configuration = configuration;
        this.gameData = gameData;
        this.aethernetSession = aethernetSession;
    }

    public string Title => Loc.T(L.Settings.About);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.InfoCircle;
    public Vector4 Tint => new(0.40f, 0.62f, 0.92f, 1f);
    private static readonly TimeSpan CopiedFlashWindow = TimeSpan.FromSeconds(3);

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * UiScale.Current));
            var card = GroupCard.Begin(theme, 4);
            SettingsRow.Info(card.NextRow(), Loc.T(L.Settings.Plugin), AepConstants.Name, theme);
            SettingsRow.Info(card.NextRow(), Loc.T(L.Settings.Version), AepConstants.Version, theme);
            SettingsRow.Info(card.NextRow(), Loc.T(L.Settings.Command), AepConstants.PrimaryCommand, theme);
            var copied = DateTime.UtcNow - copiedAt < CopiedFlashWindow;
            var copyLabel = copied ? Loc.T(L.Settings.SupportInfoCopied) : Loc.T(L.Settings.CopySupportInfo);
            if (SettingsRow.Action(card.NextRow(), copyLabel, theme.Accent, theme))
            {
                ImGui.SetClipboardText(SupportInfo.Build(configuration, gameData, aethernetSession));
                copiedAt = DateTime.UtcNow;
            }

            card.End();
        }
    }
}
