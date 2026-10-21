using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Campaigns;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Aetherphone.Windows;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.LoveIsland;

internal sealed partial class LoveIslandApp
{
    private const float LineupAvatar = 26f;
    private const float BannerStripHeight = 72f;
    private const int LineupColumns = 4;
    private const string AddressCardId = "loveisland.detail.address";
    private const string DetailTeleportId = "loveisland.detail.teleport";
    private const string DetailCopyId = "loveisland.detail.copy";
    private const string DetailTwitchId = "loveisland.detail.twitch";
    private const string ReminderToggleId = "loveisland.detail.reminder";

    private CountdownText detailCountdown;
    private string detailCountdownSource = string.Empty;
    private string detailCountdownText = string.Empty;

    private void DrawExperienceDetail(Rect area, string experienceId)
    {
        var index = IndexOfExperience(experienceId);
        var experience = index >= 0 ? frameCampaign!.Experiences[index] : null;
        var frame = BeginPushedInline(area, out var headerContext, out var body);
        using (AppSurface.BeginEdgeToEdge(body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var left = LeftOf(origin.X);
            var inner = InnerWidth(width);
            if (experience is null || index < 0 || index >= labels.Experiences.Length)
            {
                var missing = store.LoadedOnce ? Loc.T(L.LoveIsland.ExperienceGone) : Loc.T(L.Common.Loading);
                var bottom = origin.Y + Metrics.Space.Xl * scale + Typography.DrawWrappedLeft(
                    new Vector2(left, origin.Y + Metrics.Space.Xl * scale), missing, ui.MutedInk, TextStyles.Body, inner);
                Reserve(origin, width, bottom);
            }
            else
            {
                var cursorY = DrawExperienceBody(drawList, experience, labels.Experiences[index], origin, width, left,
                    inner, scale);
                Reserve(origin, width, cursorY + Metrics.Space.Xl * scale);
            }
        }

        EndPushed(in frame, in headerContext, "loveisland.nav.experience", experience?.Title ?? string.Empty,
            Texts.T(activeTab == LoveIslandTab.Experiences ? CampaignTextKeys.TabExperiences : CampaignTextKeys.TabTonight));
    }

    private int IndexOfExperience(string experienceId)
    {
        var campaign = frameCampaign;
        if (campaign is null)
        {
            return -1;
        }

        var experiences = campaign.Experiences;
        for (var index = 0; index < experiences.Length; index++)
        {
            if (string.Equals(experiences[index].Id, experienceId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private float DrawExperienceBody(ImDrawListPtr drawList, CampaignExperienceDto experience,
        ExperienceLabels experienceLabels, Vector2 origin, float width, float left, float inner, float scale)
    {
        var bannerHeight = string.IsNullOrEmpty(experience.BannerUrl)
            ? BannerStripHeight * scale
            : MathF.Min(width * LoveIslandKit.HeroAspect, LoveIslandKit.HeroMaxHeight * scale);
        var banner = new Rect(origin, new Vector2(origin.X + width, origin.Y + bannerHeight));
        LoveIslandKit.CoverImage(drawList, images, experience.BannerUrl, banner, 0f, ui.Accent);
        var cursorY = banner.Max.Y + Metrics.Space.Lg * scale;
        cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), experience.Title, ui.TitleInk,
            TextStyles.Title1, inner);
        cursorY += Metrics.Space.Xs * scale;
        var metaLeft = left;
        if (experience.Stage.Length > 0)
        {
            metaLeft += LoveIslandKit.StagePill(drawList, new Vector2(metaLeft, cursorY), experience.Stage, ui.Accent,
                scale) + Metrics.Space.Xs * scale;
        }

        var pillHeight = LoveIslandKit.PillHeight(scale);
        var metaHeight = Typography.LineHeight(TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(metaLeft, cursorY + (pillHeight - metaHeight) * 0.5f),
            Typography.FitText(experienceLabels.Meta, MathF.Max(1f, left + inner - metaLeft), TextStyles.Footnote),
            ui.MutedInk, TextStyles.Footnote);
        cursorY += pillHeight + Metrics.Space.Md * scale;

        var state = CampaignCountdown.StateOf(experience, nowUnix);
        switch (state)
        {
            case ExperienceState.Live:
                LoveIslandKit.LivePill(drawList, new Vector2(left, cursorY), Loc.T(L.LoveIsland.OnNow), scale);
                break;
            case ExperienceState.Ended:
                LoveIslandKit.OffPill(drawList, new Vector2(left, cursorY), Loc.T(L.LoveIsland.Finished), scale);
                break;
            default:
                LoveIslandKit.StagePill(drawList, new Vector2(left, cursorY),
                    DetailCountdown(experience.StartsAtUnix - nowUnix), ui.Accent, scale);
                break;
        }

        cursorY += pillHeight + Metrics.Space.Md * scale;
        cursorY = DrawAddressCard(drawList, experience, experienceLabels, left, cursorY, inner, scale);
        cursorY = DrawLineup(drawList, experience, left, cursorY, inner, scale);
        if (!string.IsNullOrEmpty(experience.TwitchUrl))
        {
            cursorY += LoveIslandKit.CardGap * scale;
            if (DrawWideButton(drawList, left, cursorY, inner, Texts.T(CampaignTextKeys.WatchTwitch), DetailTwitchId,
                    false, out var buttonBottom))
            {
                UrlActions.AskThenOpen(experience.TwitchUrl!);
            }

            cursorY = buttonBottom;
        }

        if (state != ExperienceState.Upcoming)
        {
            return cursorY;
        }

        var reminded = Ledger.HasReminder(experience.Id);
        cursorY = DrawToggleRow(drawList, left, cursorY + LoveIslandKit.CardGap * scale, inner, ReminderToggleId,
            PhoneIcons.Bell, labels.RemindMe, string.Empty, reminded, out var next);
        if (next != reminded)
        {
            var experienceId = experience.Id;
            _ = Plugin.Framework.RunOnFrameworkThread(() => Ledger.SetReminder(experienceId, next));
            toast.Show(next
                ? Loc.T(L.LoveIsland.ReminderOn, Ledger.ReminderLeadMinutes)
                : Loc.T(L.LoveIsland.ReminderOff));
        }

        return cursorY;
    }

    private string DetailCountdown(long remainingSeconds)
    {
        var text = detailCountdown.Get(Math.Max(0L, remainingSeconds));
        if (ReferenceEquals(text, detailCountdownSource))
        {
            return detailCountdownText;
        }

        detailCountdownSource = text;
        detailCountdownText = Loc.T(L.LoveIsland.InCountdown, text);
        return detailCountdownText;
    }

    private float DrawAddressCard(ImDrawListPtr drawList, CampaignExperienceDto experience,
        ExperienceLabels experienceLabels, float left, float top, float width, float scale)
    {
        cardButtons[0] = new CardButton(Loc.T(L.Maps.Teleport), true, DetailTeleportId);
        cardButtons[1] = new CardButton(Loc.T(L.LoveIsland.CopyCommand), false, DetailCopyId);
        var bottom = DrawCard(drawList, left, top, width, new CardContent(AddressCardId, labels.WhereLine,
            CardPill.None, string.Empty, experienceLabels.Address, experienceLabels.WardPlot, 0, 0, false), 2,
            out var action);
        UiAnchors.Report("loveisland.detail.address", new Rect(new Vector2(left, top), new Vector2(left + width, bottom)));
        if (action == 0)
        {
            Teleport(experience, experienceLabels);
        }
        else if (action == 1)
        {
            CopyText(experienceLabels.Command);
        }

        var noteTop = bottom + Metrics.Space.Xs * scale;
        return DrawMutedNote(left + Metrics.Space.Xxs * scale, noteTop, width - Metrics.Space.Xxs * 2f * scale,
            Texts.T(CampaignTextKeys.DcTravelNote));
    }

    private float DrawLineup(ImDrawListPtr drawList, CampaignExperienceDto experience, float left, float top,
        float width, float scale)
    {
        var ids = experience.LineupMemberIds;
        if (ids is null || ids.Length == 0)
        {
            return top;
        }

        var cardTop = top + LoveIslandKit.CardGap * scale;
        var pad = LoveIslandKit.CardPad * scale;
        var eyebrowHeight = Typography.LineHeight(LoveIslandKit.EyebrowStyle);
        var rows = (ids.Length + LineupColumns - 1) / LineupColumns;
        var cellHeight = LineupAvatar * 2f * scale + (Metrics.Space.Xs + Metrics.Space.Md) * scale
                         + Typography.LineHeight(TextStyles.Caption1);
        var height = pad * 2f + eyebrowHeight + Metrics.Space.Sm * scale + rows * cellHeight;
        var card = new Rect(new Vector2(left, cardTop), new Vector2(left + width, cardTop + height));
        ui.Card(drawList, card.Min, card.Max, LoveIslandKit.CardRadius * scale);
        LoveIslandKit.Eyebrow(drawList, new Vector2(card.Min.X + pad, card.Min.Y + pad),
            Texts.T(CampaignTextKeys.WhoIsOn), ui.Accent, width - pad * 2f);
        var gridTop = card.Min.Y + pad + eyebrowHeight + Metrics.Space.Sm * scale;
        var cellWidth = (width - pad * 2f) / LineupColumns;
        for (var index = 0; index < ids.Length; index++)
        {
            var column = index % LineupColumns;
            var row = index / LineupColumns;
            var cell = new Rect(new Vector2(card.Min.X + pad + column * cellWidth, gridTop + row * cellHeight),
                new Vector2(card.Min.X + pad + (column + 1) * cellWidth, gridTop + (row + 1) * cellHeight));
            DrawLineupCell(drawList, cell, ids[index], scale);
        }

        return card.Max.Y;
    }

    private void DrawLineupCell(ImDrawListPtr drawList, Rect cell, string memberId, float scale)
    {
        var member = Ledger.FindMember(memberId);
        var radius = LineupAvatar * scale;
        var center = new Vector2(cell.Center.X, cell.Min.Y + radius);
        var name = member?.Name ?? memberId;
        LoveIslandKit.Portrait(drawList, images, member?.PortraitUrl, center, radius, ui.Accent,
            MonogramFor(memberId, name), ui.TitleInk);
        var label = Typography.FitText(name, cell.Width - Metrics.Space.Xxs * scale, TextStyles.Caption1);
        var labelWidth = Typography.Measure(label, TextStyles.Caption1).X;
        Typography.Draw(drawList, new Vector2(cell.Center.X - labelWidth * 0.5f, center.Y + radius + Metrics.Space.Xs * scale),
            label, ui.BodyInk, TextStyles.Caption1);
        if (member is null)
        {
            return;
        }

        var hovered = UiInteract.Hover(cell.Min, cell.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(cell.Min, cell.Max, hovered))
        {
            OpenMember(member.Id);
        }
    }
}
