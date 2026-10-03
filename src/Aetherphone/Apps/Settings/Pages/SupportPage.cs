using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class SupportPage : ISettingsPage
{
    public static readonly Vector4 PatreonCoral = new(1f, 0.259f, 0.302f, 1f);
    private static readonly Vector4 MemberAmber = new(0.96f, 0.72f, 0.20f, 1f);
    private const float PreviewRowHeight = 92f;
    private const float PreviewAvatarRadius = 30f;
    private const float PreviewSelectorHeight = 44f;
    private const float PreviewHintGap = 3f;
    private const int AvatarSegments = 48;
    private const float MonogramScale = 1.1f;
    private const float TierHeaderHeight = 66f;
    private const float TierPerkMinHeight = 34f;
    private const float TierPerkPadding = 10f;
    private const float TierPadding = 16f;
    private const float TierGap = 14f;
    private const float TierButtonHeight = 40f;
    private const float TierButtonGap = 12f;
    private const float ChipHeight = 22f;
    private const float ChipPadX = 10f;
    private const float CheckSize = 12f;
    private const float SectionGap = 22f;
    private const float HeaderBandAlpha = 0.26f;
    private const float ComingSoonAlpha = 0.62f;
    private const float ChipFillAlpha = 0.10f;
    private const float ChipInkAlpha = 0.80f;
    private const float SubtitleAlpha = 0.72f;
    private const int MaxPerks = 16;

    private readonly ISettingsNavigator navigator;
    private readonly ISettingsPage accountPage;
    private readonly AethernetSession session;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly FrameCatalogStore frames;
    private readonly SupportCard card = new();
    private int previewTier = 1;

    public SupportPage(ISettingsNavigator navigator, ISettingsPage accountPage, AethernetSession session,
        RemoteImageCache images, LodestoneService lodestone, FrameCatalogStore frames)
    {
        this.navigator = navigator;
        this.accountPage = accountPage;
        this.session = session;
        this.images = images;
        this.lodestone = lodestone;
        this.frames = frames;
    }

    public string Title => Loc.T(L.Settings.SupportAetherphone);
    public string Summary => Loc.T(L.Settings.SupportBecomeMember);
    public FontAwesomeIcon Icon => FontAwesomeIcon.Heart;
    public Vector4 Tint => PatreonCoral;

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            card.Draw(theme);
            ImGui.Dummy(new Vector2(0f, SectionGap * scale));
            SettingsSection.Header(Loc.T(L.Settings.SupportPreviewTitle), theme);
            DrawPreview(theme, scale);
            ImGui.Dummy(new Vector2(0f, SectionGap * scale));
            SettingsSection.Header(Loc.T(L.Settings.SupportTiersTitle), theme);
            DrawTiers(theme, scale);
            ImGui.Dummy(new Vector2(0f, SectionGap * scale));
            SettingsSection.Header(Loc.T(L.Settings.SupportMembershipTitle), theme);
            DrawMembership(theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawPreview(PhoneTheme theme, float scale)
    {
        var card = GroupCard.Begin(theme, PreviewRowHeight + PreviewSelectorHeight);
        var row = card.NextRow(PreviewRowHeight);
        var drawList = ImGui.GetWindowDrawList();
        var radius = PreviewAvatarRadius * scale;
        var center = new Vector2(row.Min.X + radius, row.Center.Y);
        var tier = PatreonTiers.All[Math.Clamp(previewTier, 0, PatreonTiers.All.Length - 1)];
        var frame = tier.FrameId.Length > 0 ? frames.Find(tier.FrameId) : null;
        var user = session.CurrentUser;
        if (user is not null)
        {
            AvatarView.DrawRemote(drawList, center, radius, theme, user.Name, user.World, user.AvatarUrl, images,
                lodestone, MonogramScale, AvatarSegments, 1f, frame);
        }
        else
        {
            drawList.AddCircleFilled(center, radius,
                ImGui.GetColorU32(Palette.Mix(theme.GroupedCard, PatreonCoral, 0.35f)), AvatarSegments);
            ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Heart, Vector4.One, radius * 0.9f);
            AvatarView.DrawFrame(drawList, center, radius, images, frame);
        }

        var name = user?.DisplayName is { Length: > 0 } display ? display : Loc.T(L.Settings.SupportPreviewName);
        var textLeft = center.X + radius + Metrics.Space.Lg * scale;
        var maxWidth = MathF.Max(1f, row.Max.X - textLeft);
        var light = Palette.Luminance(theme.AppBackground) >= 0.5f;
        var hint = Loc.T(L.Settings.SupportPreviewHint);
        var hintBlock = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, maxWidth);
        var nameHeight = Typography.Measure(name, TextStyles.Headline).Y;
        var stack = nameHeight + PreviewHintGap * scale + hintBlock.Y;
        var top = row.Center.Y - stack * 0.5f;
        UserName.Draw(drawList, "settings.support.preview", name, (int)AccountBadges.Patreon, tier.BadgeIds,
            textLeft, top, maxWidth, TextStyles.Headline, theme.TextStrong, false, light);
        Typography.DrawWrappedLeft(new Vector2(textLeft, top + nameHeight + PreviewHintGap * scale), hint,
            theme.TextMuted, TextStyles.Footnote, maxWidth);

        var selector = card.NextRow(PreviewSelectorHeight);
        var strip = new Rect(new Vector2(selector.Min.X, selector.Min.Y + Metrics.Space.Xs * scale),
            new Vector2(selector.Max.X, selector.Max.Y - Metrics.Space.Sm * scale));
        previewTier = SegmentStrip.Draw("##settings.support.tier", strip, PatreonTiers.Names, previewTier, theme);
        card.End();
    }

    private void DrawMembership(PhoneTheme theme)
    {
        var member = session.CurrentUser is { } user && (user.Badges & (int)AccountBadges.Patreon) != 0;
        var card = GroupCard.Begin(theme, 1);
        card.SeparatorInset = SettingsRow.TileTextInset;
        var value = Loc.T(member ? L.Settings.SupportMemberActive : L.Settings.SupportMemberInactive);
        if (SettingsRow.Link(card.NextRow(), FontAwesomeIcon.Star, MemberAmber, Loc.T(L.Account.PatreonLink), value,
                theme))
        {
            navigator.Open(accountPage);
        }

        card.End();
    }

    private static void DrawTiers(PhoneTheme theme, float scale)
    {
        var tiers = PatreonTiers.All;
        for (var index = 0; index < tiers.Length; index++)
        {
            if (index > 0)
            {
                ImGui.Dummy(new Vector2(0f, TierGap * scale));
            }

            DrawTier(in tiers[index], index, theme, scale);
        }
    }

    private static void DrawTier(in PatreonTier tier, int index, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var padding = TierPadding * scale;
        var headerHeight = TierHeaderHeight * scale;
        var buttonHeight = TierButtonHeight * scale;
        var comingSoon = Loc.T(L.Settings.SupportComingSoon);
        var chipWidth = Typography.Measure(comingSoon, TextStyles.Caption2).X + ChipPadX * 2f * scale;
        var textLeft = origin.X + padding + CheckSize * scale + Metrics.Space.Sm * scale;
        var fullRight = origin.X + width - padding;
        var chipRight = fullRight - chipWidth - Metrics.Space.Sm * scale;
        var perkCount = Math.Min(tier.Perks.Length, MaxPerks);
        Span<float> rowHeights = stackalloc float[MaxPerks];
        var perksHeight = 0f;
        for (var perkIndex = 0; perkIndex < perkCount; perkIndex++)
        {
            var perk = tier.Perks[perkIndex];
            var maxWidth = MathF.Max(1f, (perk.ComingSoon ? chipRight : fullRight) - textLeft);
            var block = Typography.MeasureWrappedBlock(Loc.T(perk.Label), TextStyles.Subheadline, maxWidth);
            rowHeights[perkIndex] = MathF.Max(TierPerkMinHeight * scale, block.Y + TierPerkPadding * scale);
            perksHeight += rowHeights[perkIndex];
        }

        var height = headerHeight + perksHeight + TierButtonGap * scale + buttonHeight + padding;
        var max = origin + new Vector2(width, height);
        var radius = Metrics.Radius.Grouped * scale;
        Squircle.Fill(drawList, origin, max, radius, ImGui.GetColorU32(theme.GroupedCard));
        drawList.PushClipRect(origin, new Vector2(max.X, origin.Y + headerHeight), true);
        Squircle.Fill(drawList, origin, max, radius, ImGui.GetColorU32(Palette.WithAlpha(tier.Accent, HeaderBandAlpha)));
        drawList.PopClipRect();
        Material.EdgeSquircle(drawList, origin, max, radius, scale);
        var nameSize = Typography.Measure(tier.Name, TextStyles.Headline);
        var headerTop = origin.Y + padding * 0.7f;
        Typography.Draw(drawList, new Vector2(origin.X + padding, headerTop), tier.Name, theme.TextStrong,
            TextStyles.Headline);
        var membership = Loc.T(L.Settings.SupportTierMembership, tier.Name);
        Typography.Draw(drawList, new Vector2(origin.X + padding, headerTop + nameSize.Y), membership,
            Palette.WithAlpha(theme.TextStrong, SubtitleAlpha), TextStyles.Footnote);
        if (tier.Popular)
        {
            DrawChip(drawList, new Vector2(fullRight, headerTop + nameSize.Y * 0.5f),
                Loc.T(L.Settings.SupportMostPopular), tier.Accent, Vector4.One, scale);
        }

        var rowTop = origin.Y + headerHeight;
        var comingSoonInk = Palette.WithAlpha(theme.TextStrong, ComingSoonAlpha);
        for (var perkIndex = 0; perkIndex < perkCount; perkIndex++)
        {
            var perk = tier.Perks[perkIndex];
            var rowHeight = rowHeights[perkIndex];
            var rowCenterY = rowTop + rowHeight * 0.5f;
            var check = new Vector2(origin.X + padding + CheckSize * scale * 0.5f, rowCenterY);
            ProgressRing.CenterIcon(drawList, check, perk.ComingSoon ? FontAwesomeIcon.Clock : FontAwesomeIcon.Check,
                perk.ComingSoon ? comingSoonInk : tier.Accent, CheckSize * scale);
            var maxWidth = MathF.Max(1f, (perk.ComingSoon ? chipRight : fullRight) - textLeft);
            var label = Loc.T(perk.Label);
            var block = Typography.MeasureWrappedBlock(label, TextStyles.Subheadline, maxWidth);
            Typography.DrawWrappedLeft(new Vector2(textLeft, rowCenterY - block.Y * 0.5f), label,
                perk.ComingSoon ? comingSoonInk : theme.TextStrong, TextStyles.Subheadline, maxWidth);
            if (perk.ComingSoon)
            {
                DrawChip(drawList, new Vector2(fullRight, rowCenterY), comingSoon,
                    Palette.WithAlpha(theme.TextStrong, ChipFillAlpha), Palette.WithAlpha(theme.TextStrong, ChipInkAlpha),
                    scale);
            }

            rowTop += rowHeight;
        }

        var buttonMin = new Vector2(origin.X + padding, max.Y - padding - buttonHeight);
        var buttonMax = new Vector2(fullRight, max.Y - padding);
        DrawJoinButton(drawList, buttonMin, buttonMax, in tier, index, scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static void DrawJoinButton(ImDrawListPtr drawList, Vector2 min, Vector2 max, in PatreonTier tier,
        int index, float scale)
    {
        var hovered = UiInteract.Hover(min, max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(unchecked(0x5E70u + (uint)index), pressed, PressFx.ControlPressedScale);
        var center = (min + max) * 0.5f;
        var half = (max - min) * 0.5f * grow;
        var drawMin = center - half;
        var drawMax = center + half;
        Squircle.FillVerticalGradient(drawList, drawMin, drawMax, half.Y,
            ImGui.GetColorU32(Palette.Lighten(tier.Accent, hovered ? 0.14f : 0.06f)),
            ImGui.GetColorU32(Palette.Darken(tier.Accent, 0.12f)));
        Material.EdgeSquircle(drawList, drawMin, drawMax, half.Y, scale);
        var label = Loc.T(L.Settings.SupportJoinTier, tier.Name);
        var size = Typography.Measure(label, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f), label,
            Vector4.One, TextStyles.SubheadlineEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(min, max, hovered))
        {
            UrlActions.OpenInBrowser(tier.JoinUrl);
        }
    }

    private static void DrawChip(ImDrawListPtr drawList, Vector2 rightCenter, string label, Vector4 fill, Vector4 ink,
        float scale)
    {
        var size = Typography.Measure(label, TextStyles.Caption2);
        var height = ChipHeight * scale;
        var width = size.X + ChipPadX * 2f * scale;
        var min = new Vector2(rightCenter.X - width, rightCenter.Y - height * 0.5f);
        var max = new Vector2(rightCenter.X, rightCenter.Y + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(fill));
        Typography.Draw(drawList, new Vector2(min.X + ChipPadX * scale, rightCenter.Y - size.Y * 0.5f), label, ink,
            TextStyles.Caption2);
    }
}
