using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private const float InfoHeroTile = 52f;
    private const float InfoTile = 36f;
    private const float InfoColumnIcon = 13f;
    private const float InfoListRowGap = 3f;

    private static readonly Vector4 InfoStartupTint = new(0.36f, 0.62f, 1f, 1f);
    private static readonly Vector4 InfoSitesTint = new(0.30f, 0.80f, 0.52f, 1f);
    private static readonly Vector4 InfoPartiesTint = new(0.66f, 0.50f, 1f, 1f);
    private static readonly Vector4 InfoCodesTint = new(0.98f, 0.48f, 0.72f, 1f);
    private static readonly Vector4 InfoFailuresTint = new(1f, 0.62f, 0.26f, 1f);
    private static readonly Vector4 InfoVpnTint = new(0.30f, 0.78f, 0.86f, 1f);

    private static readonly string[] PlayingSites = ["YouTube", "Twitch", "Kick", "Dailymotion", "Bilibili", "Niconico"];

    private static readonly string[] DrmSites = ["Netflix", "Disney+", "Prime Video", "Max", "Crunchyroll", "Plex"];

    private const string SignInSite = "Vimeo";

    private void DrawInfo(Rect area, float scale)
    {
        SocialChrome.DrawScreenHeader(area, Loc.T(L.AetherStream.InfoTitle), Ink, back, ScreenTitleStyle);
        var content = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(content))
        {
            DrawInfoHero(scale);

            Gap(Metrics.Space.Lg);
            SectionLabel(Loc.T(L.AetherStream.InfoSectionPlayback));
            DrawInfoCard(FontAwesomeIcon.HourglassHalf, InfoStartupTint, L.AetherStream.InfoStartupTitle,
                L.AetherStream.InfoStartupBody);
            Gap(Metrics.Space.Sm);
            DrawSitesCard(scale);

            Gap(Metrics.Space.Lg);
            SectionLabel(Loc.T(L.AetherStream.InfoSectionTogether));
            DrawInfoCard(FontAwesomeIcon.Users, InfoPartiesTint, L.AetherStream.InfoPartiesTitle,
                L.AetherStream.InfoPartiesBody);
            Gap(Metrics.Space.Sm);
            DrawInfoCard(FontAwesomeIcon.TicketAlt, InfoCodesTint, L.AetherStream.InfoCodesTitle,
                L.AetherStream.InfoCodesBody);

            Gap(Metrics.Space.Lg);
            SectionLabel(Loc.T(L.AetherStream.InfoSectionHelp));
            DrawInfoCard(FontAwesomeIcon.Wrench, InfoFailuresTint, L.AetherStream.InfoFailuresTitle,
                L.AetherStream.InfoFailuresBody);
            Gap(Metrics.Space.Sm);
            DrawInfoCard(FontAwesomeIcon.ShieldAlt, InfoVpnTint, L.AetherStream.InfoVpnTitle,
                L.AetherStream.InfoVpnBody);
            Gap(Metrics.Space.Xl);
        }
    }

    private void DrawInfoHero(float scale)
    {
        var intro = Loc.T(L.AetherStream.InfoIntro);
        var width = ScrollLayout.StableContentWidth() - PadX * 2f * scale;
        var tile = InfoHeroTile * scale;
        var introGap = Metrics.Space.Md * scale;
        var introHeight = Typography.MeasureWrappedBlock(intro, TextStyles.Subheadline, width).Y;
        var block = BeginBlock(Metrics.Space.Md * scale + tile + introGap + introHeight);
        var drawList = ImGui.GetWindowDrawList();

        var tileMin = new Vector2(block.Center.X - tile * 0.5f, block.Min.Y + Metrics.Space.Md * scale);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tile, tile), tile * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, tileMin + new Vector2(tile * 0.5f, tile * 0.5f), FontAwesomeIcon.Lightbulb,
            AccentRing.Ink, tile * 0.48f);

        Typography.DrawWrappedCentered(new Vector2(block.Center.X, tileMin.Y + tile + introGap), intro, Ink.MutedInk,
            TextStyles.Subheadline, width);
        EndBlock();
    }

    private void DrawInfoCard(FontAwesomeIcon icon, Vector4 tint, LocString titleKey, LocString bodyKey)
    {
        var scale = UiScale.Current;
        var title = Loc.T(titleKey);
        var body = Loc.T(bodyKey);
        var pad = Metrics.Space.Lg * scale;
        var tile = InfoTile * scale;
        var textInset = pad + tile + Metrics.Space.Md * scale;
        var textWidth = ScrollLayout.StableContentWidth() - PadX * 2f * scale - textInset - pad;
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var bodyHeight = Typography.MeasureWrappedBlock(body, TextStyles.Footnote, textWidth).Y;
        var textHeight = titleHeight + Metrics.Space.Xs * scale + bodyHeight;
        var card = BeginBlock(MathF.Max(tile, textHeight) + pad * 2f);
        var drawList = ImGui.GetWindowDrawList();
        GlassCard(drawList, card);
        DrawInfoTile(drawList, new Vector2(card.Min.X + pad, card.Min.Y + pad), tile, icon, tint);
        DrawInfoText(drawList, new Vector2(card.Min.X + textInset, card.Min.Y + pad), title, body, textWidth,
            scale);
        EndBlock();
    }

    private void DrawSitesCard(float scale)
    {
        var title = Loc.T(L.AetherStream.InfoSitesTitle);
        var lead = Loc.T(L.AetherStream.InfoSitesLead);
        var pad = Metrics.Space.Lg * scale;
        var tile = InfoTile * scale;
        var cardWidth = ScrollLayout.StableContentWidth() - PadX * 2f * scale;
        var textInset = pad + tile + Metrics.Space.Md * scale;
        var textWidth = cardWidth - textInset - pad;
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var leadHeight = Typography.MeasureWrappedBlock(lead, TextStyles.Footnote, textWidth).Y;
        var headerHeight = MathF.Max(tile, titleHeight + Metrics.Space.Xs * scale + leadHeight);
        var headingHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var rowHeight = Typography.LineHeight(TextStyles.Footnote) + InfoListRowGap * scale;
        var playingRows = PlayingSites.Length + 2;
        var blockedRows = DrmSites.Length + 1;
        var listHeight = headingHeight + Metrics.Space.Sm * scale + Math.Max(playingRows, blockedRows) * rowHeight;
        var dividerGap = Metrics.Space.Md * scale;
        var card = BeginBlock(pad + headerHeight + dividerGap * 2f + listHeight + pad);
        var drawList = ImGui.GetWindowDrawList();
        GlassCard(drawList, card);

        DrawInfoTile(drawList, new Vector2(card.Min.X + pad, card.Min.Y + pad), tile, FontAwesomeIcon.Globe,
            InfoSitesTint);
        DrawInfoText(drawList, new Vector2(card.Min.X + textInset, card.Min.Y + pad), title, lead, textWidth, scale);

        var dividerY = card.Min.Y + pad + headerHeight + dividerGap;
        drawList.AddLine(new Vector2(card.Min.X + pad, dividerY), new Vector2(card.Max.X - pad, dividerY),
            ImGui.GetColorU32(Ink.Hairline), Metrics.Stroke.Hairline * scale);

        var columnGap = Metrics.Space.Lg * scale;
        var columnWidth = (cardWidth - pad * 2f - columnGap) * 0.5f;
        var listTop = dividerY + dividerGap;
        var leftX = card.Min.X + pad;
        var rightX = leftX + columnWidth + columnGap;

        var playingTop = DrawSiteColumnHeading(drawList, new Vector2(leftX, listTop), columnWidth,
            FontAwesomeIcon.CheckCircle, Ink.PresenceGreen, Loc.T(L.AetherStream.InfoSitesPlays), scale);
        for (var index = 0; index < PlayingSites.Length; index++)
        {
            DrawSiteRow(drawList, new Vector2(leftX, playingTop + index * rowHeight), columnWidth, PlayingSites[index],
                null);
        }

        DrawSiteRow(drawList, new Vector2(leftX, playingTop + PlayingSites.Length * rowHeight), columnWidth,
            Loc.T(L.AetherStream.InfoSitesDirectLinks), null);
        DrawSiteRow(drawList, new Vector2(leftX, playingTop + (PlayingSites.Length + 1) * rowHeight), columnWidth,
            Loc.T(L.AetherStream.InfoSitesYourFiles), null);

        var blockedTop = DrawSiteColumnHeading(drawList, new Vector2(rightX, listTop), columnWidth,
            FontAwesomeIcon.Lock, Ink.Danger, Loc.T(L.AetherStream.InfoSitesBlocked), scale);
        var drmLabel = Loc.T(L.AetherStream.InfoSitesDrm);
        for (var index = 0; index < DrmSites.Length; index++)
        {
            DrawSiteRow(drawList, new Vector2(rightX, blockedTop + index * rowHeight), columnWidth, DrmSites[index],
                drmLabel);
        }

        DrawSiteRow(drawList, new Vector2(rightX, blockedTop + DrmSites.Length * rowHeight), columnWidth, SignInSite,
            Loc.T(L.AetherStream.InfoSitesSignIn));
        EndBlock();
    }

    private static void DrawInfoTile(ImDrawListPtr drawList, Vector2 min, float tile, FontAwesomeIcon icon,
        Vector4 tint)
    {
        IconTile.FillShaded(drawList, min, min + new Vector2(tile, tile), tile * Metrics.Radius.TileFactor,
            IconTile.Surface(tint));
        ProgressRing.CenterIcon(drawList, min + new Vector2(tile * 0.5f, tile * 0.5f), icon, AccentRing.Ink,
            tile * 0.46f);
    }

    private static void DrawInfoText(ImDrawListPtr drawList, Vector2 topLeft, string title, string body,
        float width, float scale)
    {
        Typography.Draw(drawList, topLeft, Typography.FitText(title, width, TextStyles.BodyEmphasized),
            Ink.TitleInk, TextStyles.BodyEmphasized);
        var bodyTop = topLeft.Y + Typography.LineHeight(TextStyles.BodyEmphasized) + Metrics.Space.Xs * scale;
        Typography.DrawWrappedLeft(new Vector2(topLeft.X, bodyTop), body, Ink.MutedInk, TextStyles.Footnote, width);
    }

    private static float DrawSiteColumnHeading(ImDrawListPtr drawList, Vector2 topLeft, float width,
        FontAwesomeIcon icon, Vector4 tint, string label, float scale)
    {
        var headingHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var iconSize = InfoColumnIcon * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(topLeft.X + iconSize * 0.5f, topLeft.Y + headingHeight * 0.5f),
            icon, tint, iconSize);
        var labelX = topLeft.X + iconSize + Metrics.Space.Sm * scale;
        Typography.Draw(drawList, new Vector2(labelX, topLeft.Y),
            Typography.FitText(label, width - (labelX - topLeft.X), TextStyles.FootnoteEmphasized), tint,
            TextStyles.FootnoteEmphasized);
        return topLeft.Y + headingHeight + Metrics.Space.Sm * scale;
    }

    private static void DrawSiteRow(ImDrawListPtr drawList, Vector2 topLeft, float width, string name, string? reason)
    {
        var reasonWidth = 0f;
        if (reason is not null)
        {
            reasonWidth = Typography.Measure(reason, TextStyles.Caption1).X;
            var reasonTop = topLeft.Y + (Typography.LineHeight(TextStyles.Footnote)
                - Typography.LineHeight(TextStyles.Caption1)) * 0.5f;
            Typography.Draw(drawList, new Vector2(topLeft.X + width - reasonWidth, reasonTop), reason, Ink.FaintInk,
                TextStyles.Caption1);
            reasonWidth += Metrics.Space.Sm * UiScale.Current;
        }

        Typography.Draw(drawList, topLeft, Typography.FitText(name, width - reasonWidth, TextStyles.Footnote),
            Ink.BodyInk, TextStyles.Footnote);
    }
}
