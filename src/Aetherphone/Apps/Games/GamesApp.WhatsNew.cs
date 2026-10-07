using Aetherphone.Apps.Games.Hub;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Changelog;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games;

internal static class WhatsNewNotice
{
    public static bool Shows(Configuration configuration, bool consentShowing) =>
        !consentShowing && configuration.HasUnseenFeaturePin(NewFeaturePins.GamesWhatsNew);

    public static bool ShowsJoinCard(bool optedOut, bool noticeShowing) => optedOut && !noticeShowing;
}

internal sealed partial class GamesApp
{
    private const string WhatsNewGotItId = "games.whatsNew.gotIt";
    private const string WhatsNewSeeAllId = "games.whatsNew.seeAll";
    private const float WhatsNewPadding = Metrics.Space.Lg;
    private const float WhatsNewIconSize = 40f;
    private const float WhatsNewGlyphColumn = 28f;
    private const float WhatsNewGlyphSize = 16f;
    private const float WhatsNewBlockGap = Metrics.Space.Md;
    private const float WhatsNewLineGap = Metrics.Space.Xs;
    private const float WhatsNewButtonGap = Metrics.Space.Sm;

    private static readonly LocString[] WhatsNewLines =
    {
        L.GamesHub.WhatsNewGames, L.GamesHub.WhatsNewLeaderboards, L.GamesHub.WhatsNewRooms,
        L.GamesHub.WhatsNewRedesign,
    };

    private static readonly string[] WhatsNewGlyphs =
    {
        PhoneIcons.Gamepad, PhoneIcons.Crown, PhoneIcons.Users, PhoneIcons.Sparkles,
    };

    private bool whatsNewShowing;

    private float DrawWhatsNew(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        whatsNewShowing = WhatsNewNotice.Shows(configuration, ShowsConsent());
        if (!whatsNewShowing)
        {
            return top;
        }

        var height = WhatsNewHeight(width, scale);
        var bottom = top + height;
        if (!HomeOutOfView(top, bottom))
        {
            DrawWhatsNewCard(drawList, left, top, width, height, scale);
        }

        return bottom + Metrics.Space.Md * scale;
    }

    private float WhatsNewHeight(float width, float scale)
    {
        var inner = MathF.Max(1f, width - WhatsNewPadding * 2f * scale);
        var icon = WhatsNewIconSize * scale;
        var title = Typography.MeasureWrappedBlock(Loc.T(L.GamesHub.WhatsNewTitle), TextStyles.Headline,
            MathF.Max(1f, inner - icon - Metrics.Space.Md * scale)).Y;
        var lineWidth = MathF.Max(1f, inner - WhatsNewGlyphColumn * scale);
        var lines = 0f;
        for (var index = 0; index < WhatsNewLines.Length; index++)
        {
            lines += Typography.MeasureWrappedBlock(Loc.T(WhatsNewLines[index]), TextStyles.Subheadline, lineWidth).Y;
        }

        return (WhatsNewPadding * 2f + WhatsNewBlockGap * 2f + WhatsNewLineGap * (WhatsNewLines.Length - 1)
                + Button.RegularHeight) * scale + MathF.Max(icon, title) + lines;
    }

    private void DrawWhatsNewCard(ImDrawListPtr drawList, float left, float top, float width, float height,
        float scale)
    {
        ui.Card(drawList, new Vector2(left, top), new Vector2(left + width, top + height),
            HubMetrics.CardRadius * scale);
        var pad = WhatsNewPadding * scale;
        var inner = MathF.Max(1f, width - pad * 2f);
        var x = left + pad;
        var y = top + pad;
        var icon = WhatsNewIconSize * scale;
        var titleLeft = x + icon + Metrics.Space.Md * scale;
        var titleWidth = MathF.Max(1f, x + inner - titleLeft);
        var title = Loc.T(L.GamesHub.WhatsNewTitle);
        var titleHeight = Typography.MeasureWrappedBlock(title, TextStyles.Headline, titleWidth).Y;
        var headerHeight = MathF.Max(icon, titleHeight);
        DrawGlyphTile(drawList, new Vector2(x, y + (headerHeight - icon) * 0.5f), icon, PhoneIcons.Sparkles, scale);
        Typography.DrawWrappedLeft(new Vector2(titleLeft, y + (headerHeight - titleHeight) * 0.5f), title,
            ui.TitleInk, TextStyles.Headline, titleWidth);
        y += headerHeight + WhatsNewBlockGap * scale;
        y = DrawWhatsNewLines(drawList, x, y, inner, scale) + WhatsNewBlockGap * scale;
        DrawWhatsNewButtons(drawList, x, y, inner, scale);
    }

    private float DrawWhatsNewLines(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var column = WhatsNewGlyphColumn * scale;
        var lineWidth = MathF.Max(1f, width - column);
        var firstLine = Typography.LineHeight(TextStyles.Subheadline);
        var y = top;
        for (var index = 0; index < WhatsNewLines.Length; index++)
        {
            if (index > 0)
            {
                y += WhatsNewLineGap * scale;
            }

            PhoneIcon.Draw(drawList, new Vector2(left + WhatsNewGlyphSize * 0.5f * scale, y + firstLine * 0.5f),
                WhatsNewGlyphs[index], ui.Accent, WhatsNewGlyphSize * scale);
            y += Typography.DrawWrappedLeft(new Vector2(left + column, y), Loc.T(WhatsNewLines[index]), ui.BodyInk,
                TextStyles.Subheadline, lineWidth);
        }

        return y;
    }

    private void DrawWhatsNewButtons(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var height = Button.RegularHeight * scale;
        var canOpenChangelog = navigation.IsAvailable(SettingsAppId);
        var gap = WhatsNewButtonGap * scale;
        var gotItLeft = canOpenChangelog ? left + (width + gap) * 0.5f : left;
        var gotIt = new Rect(new Vector2(gotItLeft, top), new Vector2(left + width, top + height));
        if (Button.Draw(drawList, gotIt, Loc.T(L.Onboarding.GotIt), ui.Ink, id: WhatsNewGotItId))
        {
            configuration.MarkFeaturePinSeen(NewFeaturePins.GamesWhatsNew);
        }

        if (!canOpenChangelog)
        {
            return;
        }

        var seeAll = new Rect(new Vector2(left, top), new Vector2(gotItLeft - gap, top + height));
        if (!Button.Draw(drawList, seeAll, Loc.T(L.GamesHub.WhatsNewSeeAll), ui.Ink, ButtonStyle.Gray,
                id: WhatsNewSeeAllId))
        {
            return;
        }

        configuration.MarkFeaturePinSeen(NewFeaturePins.GamesWhatsNew);
        settingsLauncher.Request(SettingsPageKind.Changelog);
        navigation.Open(SettingsAppId);
    }
}
