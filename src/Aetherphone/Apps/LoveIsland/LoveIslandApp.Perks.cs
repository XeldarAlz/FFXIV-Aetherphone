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
    private const float PerkPreview = 84f;
    private const int PerkRays = 10;
    private const float PerkRayHalfAngle = 0.11f;
    private const float PerkRaySecondsEarned = 14f;
    private const float PerkRaySecondsLocked = 40f;
    private const float PerkLockBadge = 11f;
    private const float PerkGlintPeak = 0.07f;
    private const float PerkAvatarRadius = 28f;
    private const float PerkBadgeSize = 50f;
    private const float PerkRingStroke = 3f;
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
            PrepareRootSurface(surface);
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
        var firstVertex = CardEntrance.Mark(drawList);
        var rounding = LoveIslandKit.CardRadius * scale;
        if (earned)
        {
            LoveIslandKit.FeaturedCard(drawList, card.Min, card.Max, rounding, ui.Accent, scale);
        }
        else
        {
            LoveIslandKit.GlassCard(drawList, card.Min, card.Max, rounding, ui.Accent, scale);
        }

        var previewCenter = new Vector2(left + pad + preview * 0.5f, card.Center.Y);
        DrawPerkStage(drawList, previewCenter, preview * 0.5f, earned, (int)art);
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

        if (earned)
        {
            DrawEmblemMotes(drawList, previewCenter, preview * 0.55f, scale);
        }
        else
        {
            DrawPerkLock(drawList, previewCenter + new Vector2(preview * 0.34f, preview * 0.34f), scale);
        }

        EclipseFx.Glint(drawList, card.Min, card.Max, rounding, PerkGlintPeak, (int)art * 0.21f);
        UiAnchors.Report(art == PerkArt.Frame ? "loveisland.perks.frame" : art == PerkArt.Badge
            ? "loveisland.perks.badge"
            : "loveisland.perks.coins", card);
        entrance.Settle(drawList, firstVertex, scale);
        return card.Max.Y;
    }

    private void DrawPerkStage(ImDrawListPtr drawList, Vector2 center, float radius, bool earned, int seed)
    {
        var time = (float)ImGui.GetTime();
        var seconds = earned ? PerkRaySecondsEarned : PerkRaySecondsLocked;
        var rotation = time * MathF.Tau / seconds + seed * 0.7f;
        var breath = EclipseFx.Breath(2600.0, seed * 0.3f);
        var strength = earned ? 1f : 0.55f;
        SoftGlow.Draw(drawList, center, radius * 1.25f, ui.Accent with { W = (0.40f + 0.15f * breath) * strength });
        EclipseFx.Rays(drawList, center, radius * 1.15f, PerkRays, rotation, PerkRayHalfAngle,
            EclipseFx.Rose(ui.Accent, 0.5f) with { W = (0.22f + 0.10f * breath) * strength });
        drawList.AddCircleFilled(center, radius * 0.78f, ImGui.GetColorU32(LoveIslandKit.Night with { W = 0.55f }), 48);
        drawList.AddCircle(center, radius * 0.78f,
            ImGui.GetColorU32(EclipseFx.Rose(ui.Accent, 0.5f) with { W = 0.35f * strength + 0.2f }), 48, 1f);
    }

    private void DrawPerkLock(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var radius = PerkLockBadge * scale;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(LoveIslandKit.Night), 24);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(LoveIslandKit.White with { W = 0.25f }), 24, 1f);
        PhoneIcon.Draw(drawList, center, PhoneIcons.Lock, LoveIslandKit.OffPillInk, radius * 1.05f);
    }

    private void DrawEarnedPill(ImDrawListPtr drawList, Vector2 topLeft, bool earned, float scale)
    {
        if (earned)
        {
            LoveIslandKit.LivePill(drawList, topLeft, Texts.T(CampaignTextKeys.Earned), ui.Accent, scale);
            return;
        }

        LoveIslandKit.OffPill(drawList, topLeft, Texts.T(CampaignTextKeys.NotYet), scale);
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
            false, Featured: !earned), 1, out var action);
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
