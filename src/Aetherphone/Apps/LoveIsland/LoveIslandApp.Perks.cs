using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Campaigns;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.LoveIsland;

internal enum PerkArt : byte
{
    Frame,
    Badge,
    Coins,
}

internal sealed partial class LoveIslandApp
{
    private const float PerkPreview = 76f;
    private const float PerkAvatarRadius = 28f;
    private const float PerkBadgeSize = 40f;
    private const float PerkRingStroke = 3f;
    private const float PerkBloomAlpha = 0.22f;
    private const string EarnCardId = "loveisland.perks.earn";
    private const string EquipButtonId = "loveisland.perks.equip";
    private const string PerksPostButtonId = "loveisland.perks.post";

    private void DrawPerksTab(Rect area)
    {
        badgeCatalog.EnsureFresh();
        frameCatalog.EnsureFresh();
        var frame = BeginRoot(area, out var context);
        using (var surface = AppSurface.BeginEdgeToEdge(frame.Body))
        {
            ConsumeScrollTop(surface);
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var left = LeftOf(origin.X);
            var inner = InnerWidth(width);
            var texts = Texts;
            var cursorY = DrawPageTitle(drawList, left, origin.Y, inner, texts.T(CampaignTextKeys.TabPerks),
                texts.T(CampaignTextKeys.PerksSub));
            var earned = Ledger.Me?.Earned ?? configuration.LoveIslandEarned;
            var gap = LoveIslandKit.CardGap * scale;
            var reward = frameCampaign?.Reward;
            cursorY = DrawPerkCard(drawList, left, cursorY, inner, PerkArt.Frame, texts.T(CampaignTextKeys.FrameName),
                texts.T(CampaignTextKeys.FrameBody), earned, reward, scale);
            cursorY = DrawPerkCard(drawList, left, cursorY + gap, inner, PerkArt.Badge,
                texts.T(CampaignTextKeys.BadgeName), texts.T(CampaignTextKeys.BadgeBody), earned, reward, scale);
            if (reward is { Coins: > 0 })
            {
                cursorY = DrawPerkCard(drawList, left, cursorY + gap, inner, PerkArt.Coins,
                    texts.T(CampaignTextKeys.CoinsName), labels.CoinsBody, earned, reward, scale);
            }

            cursorY = DrawEarnCard(drawList, left, cursorY + gap * 2f, inner, earned);
            Reserve(origin, width, cursorY + Metrics.Space.Xl * scale);
        }

        EndRoot(in frame, in context, "loveisland.nav.perks");
    }

    private float DrawPerkCard(ImDrawListPtr drawList, float left, float top, float width, PerkArt art, string name,
        string body, bool earned, CampaignRewardDto? reward, float scale)
    {
        var pad = LoveIslandKit.CardPad * scale;
        var preview = PerkPreview * scale;
        var textLeft = left + pad + preview + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, left + width - pad - textLeft);
        var nameHeight = Typography.MeasureWrappedBlock(name, TextStyles.Headline, textWidth).Y;
        var bodyHeight = Typography.MeasureWrappedBlock(body, LoveIslandKit.CardBodyStyle, textWidth).Y;
        var pillHeight = LoveIslandKit.PillHeight(scale);
        var textHeight = nameHeight + LoveIslandKit.TitleGap * scale + bodyHeight + Metrics.Space.Sm * scale + pillHeight;
        var height = pad * 2f + MathF.Max(preview, textHeight);
        var card = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        ui.Card(drawList, card.Min, card.Max, LoveIslandKit.CardRadius * scale);
        var previewCenter = new Vector2(left + pad + preview * 0.5f, card.Center.Y);
        DrawPerkBloom(drawList, previewCenter, preview * 0.5f);
        switch (art)
        {
            case PerkArt.Frame:
                DrawFramePreview(drawList, previewCenter, reward, scale);
                break;
            case PerkArt.Badge:
                DrawBadgePreview(drawList, previewCenter, reward, scale);
                break;
            default:
                CurrencyGlyph.Draw(drawList, Windows.Components.CurrencyKind.Coins, previewCenter, PerkBadgeSize * scale);
                break;
        }

        var cursorY = top + pad + MathF.Max(0f, (height - pad * 2f - textHeight) * 0.5f);
        cursorY += Typography.DrawWrappedLeft(new Vector2(textLeft, cursorY), name, ui.TitleInk, TextStyles.Headline,
            textWidth);
        cursorY += LoveIslandKit.TitleGap * scale;
        cursorY += Typography.DrawWrappedLeft(new Vector2(textLeft, cursorY), body, ui.MutedInk,
            LoveIslandKit.CardBodyStyle, textWidth);
        cursorY += Metrics.Space.Sm * scale;
        if (art == PerkArt.Coins && labels.CoinsAmount.Length > 0)
        {
            var amountWidth = CurrencyGlyph.DrawAmount(drawList, new Vector2(textLeft, cursorY), labels.CoinsAmount,
                Windows.Components.CurrencyKind.Coins, ui.TitleInk, TextStyles.SubheadlineEmphasized).X;
            DrawEarnedPill(drawList, new Vector2(textLeft + amountWidth + Metrics.Space.Sm * scale, cursorY), earned,
                scale);
        }
        else
        {
            DrawEarnedPill(drawList, new Vector2(textLeft, cursorY), earned, scale);
        }

        UiAnchors.Report(art == PerkArt.Frame ? "loveisland.perks.frame" : art == PerkArt.Badge
            ? "loveisland.perks.badge"
            : "loveisland.perks.coins", card);
        return card.Max.Y;
    }

    private void DrawEarnedPill(ImDrawListPtr drawList, Vector2 topLeft, bool earned, float scale)
    {
        if (earned)
        {
            LoveIslandKit.LivePill(drawList, topLeft, Texts.T(CampaignTextKeys.Earned), scale);
            return;
        }

        LoveIslandKit.OffPill(drawList, topLeft, Texts.T(CampaignTextKeys.NotYet), scale);
    }

    private void DrawPerkBloom(ImDrawListPtr drawList, Vector2 center, float radius)
    {
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, PerkBloomAlpha)), 48);
    }

    private void DrawFramePreview(ImDrawListPtr drawList, Vector2 center, CampaignRewardDto? reward, float scale)
    {
        var radius = PerkAvatarRadius * scale;
        var user = session.CurrentUser;
        var style = frameCatalog.Find(reward?.FrameId);
        if (style is not null)
        {
            AvatarView.DrawRemote(drawList, center, radius, theme, user?.Name ?? string.Empty,
                user?.World ?? string.Empty, user?.AvatarUrl, images, lodestone, 1.2f, 48, 1f, style);
            return;
        }

        AvatarView.DrawRemote(drawList, center, radius, theme, user?.Name ?? string.Empty,
            user?.World ?? string.Empty, user?.AvatarUrl, images, lodestone, 1.2f, 48);
        var artUrl = frameCampaign?.FrameArtUrl;
        var art = string.IsNullOrEmpty(artUrl) ? null : images.Sized(artUrl, radius * 2.8f * LoveIslandKit.ThumbOversample);
        if (art is null)
        {
            drawList.AddCircle(center, radius + PerkRingStroke * scale, ImGui.GetColorU32(ui.Accent), 64,
                PerkRingStroke * scale);
            return;
        }

        var half = new Vector2(radius * 1.4f, radius * 1.4f);
        drawList.AddImage(art.Handle, center - half, center + half, Vector2.Zero, Vector2.One);
    }

    private void DrawBadgePreview(ImDrawListPtr drawList, Vector2 center, CampaignRewardDto? reward, float scale)
    {
        var size = PerkBadgeSize * scale;
        var badgeId = reward?.BadgeId;
        var style = string.IsNullOrEmpty(badgeId) ? null : badgeCatalog.Find(badgeId);
        if (style is not null)
        {
            BadgeStrip.DrawOne(drawList, center, style, images, RoleInk.IsLight(theme), size);
            return;
        }

        var artUrl = frameCampaign?.BadgeArtUrl;
        if (string.IsNullOrEmpty(artUrl))
        {
            artUrl = frameCampaign?.LogoUrl;
        }

        var art = string.IsNullOrEmpty(artUrl) ? null : images.Sized(artUrl, size * LoveIslandKit.ThumbOversample);
        if (art is null)
        {
            PhoneIcon.Draw(drawList, center, PhoneIcons.HeartFilled, ui.Accent, size * 0.8f);
            return;
        }

        var half = new Vector2(size * 0.5f, size * 0.5f);
        drawList.AddImage(art.Handle, center - half, center + half, Vector2.Zero, Vector2.One);
    }

    private float DrawEarnCard(ImDrawListPtr drawList, float left, float top, float width, bool earned)
    {
        var texts = Texts;
        cardButtons[0] = earned
            ? new CardButton(texts.T(CampaignTextKeys.EquipCta), true, EquipButtonId)
            : new CardButton(texts.T(CampaignTextKeys.PostCta), true, PerksPostButtonId);
        var bottom = DrawCard(drawList, left, top, width, new CardContent(EarnCardId,
            texts.T(CampaignTextKeys.HowToEarn), CardPill.None, string.Empty, labels.EarnTitle, labels.EarnBody, 0, 0,
            false), 1, out var action);
        if (action != 0)
        {
            return bottom;
        }

        if (earned)
        {
            UiFeedback.Play(UiSound.Tap);
            navigation.Open(CoinAppId);
            return bottom;
        }

        PostWithHashtag();
        return bottom;
    }
}
