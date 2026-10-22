using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Campaigns;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.LoveIsland;

internal sealed partial class LoveIslandApp
{
    private const float RowPadY = 12f;
    private const float RowTimeColumn = 64f;
    private const float RowTitleGap = 4f;
    private const float RowChevron = 14f;
    private const float RailWidth = 22f;
    private const float RailCenterShare = 0.36f;
    private const float RailThickness = 2f;
    private const float RailTrackAlpha = 0.12f;
    private const float RailNodeRadius = 5f;
    private const float RailLiveGlow = 16f;
    private const float RailHeadGlow = 12f;
    private const float RailHeadRadius = 3f;
    private const float RowCardGap = 8f;
    private const float EndedRowAlpha = 0.55f;
    private const float DayHeaderTopGap = 18f;
    private const int RowTitleLines = 2;

    private void DrawExperiencesTab(Rect area)
    {
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
            var cursorY = DrawPageTitle(drawList, left, origin.Y, inner, Texts.T(CampaignTextKeys.TabExperiences),
                labels.ExperiencesSub);
            ImGui.SetCursorScreenPos(new Vector2(origin.X, cursorY));
            if (DrawLoadState(frame.Body, out var stateBottom))
            {
                cursorY = stateBottom;
            }
            else
            {
                cursorY = DrawAgenda(drawList, left, cursorY, inner, scale);
            }

            Reserve(origin, width, cursorY + Metrics.Space.Xl * scale);
        }

        EndRoot(in frame, in context, "loveisland.nav.experiences");
    }

    private float DrawAgenda(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var campaign = frameCampaign!;
        var experiences = campaign.Experiences;
        if (experiences.Length == 0 || labels.Experiences.Length != experiences.Length)
        {
            return top + Typography.DrawWrappedLeft(new Vector2(left, top), Texts.T(CampaignTextKeys.NothingScheduled),
                ui.MutedInk, TextStyles.Body, width);
        }

        var cursorY = top;
        var start = 0;
        while (start < experiences.Length)
        {
            var end = start + 1;
            while (end < experiences.Length && labels.Experiences[end].LocalDay == labels.Experiences[start].LocalDay)
            {
                end++;
            }

            if (start > 0)
            {
                cursorY += DayHeaderTopGap * scale;
            }

            cursorY += CardSectionHeader.Draw(drawList, new Vector2(left, cursorY), width,
                labels.Experiences[start].DayLabel, ui.TitleInk);
            cursorY = DrawDayGroup(drawList, left, cursorY, width, experiences, start, end, scale);
            start = end;
        }

        return cursorY;
    }

    private float DrawDayGroup(ImDrawListPtr drawList, float left, float top, float width,
        CampaignExperienceDto[] experiences, int start, int end, float scale)
    {
        var rail = RailWidth * scale;
        var cardLeft = left + rail;
        var cardWidth = MathF.Max(1f, width - rail);
        var railX = left + rail * RailCenterShare;
        var gap = RowCardGap * scale;
        var firstNode = -1f;
        var lastNode = -1f;
        var nowY = -1f;
        var liveFound = false;
        var rowTop = top;
        for (var index = start; index < end; index++)
        {
            var height = MeasureAgendaRow(index, cardWidth, scale, out _);
            var node = NodeY(rowTop, scale);
            if (firstNode < 0f)
            {
                firstNode = node;
            }

            lastNode = node;
            var state = CampaignCountdown.StateOf(experiences[index], nowUnix);
            if (!liveFound && state == ExperienceState.Ended)
            {
                nowY = index == end - 1 ? node : NodeY(rowTop + height + gap, scale);
            }
            else if (!liveFound && state == ExperienceState.Live)
            {
                var next = index == end - 1 ? rowTop + height : NodeY(rowTop + height + gap, scale);
                nowY = node + (next - node) * Math.Clamp(LiveProgress(index), 0f, 1f);
                liveFound = true;
            }
            else
            {
                liveFound = true;
            }

            rowTop += height + gap;
        }

        DrawRail(drawList, railX, firstNode, MathF.Max(lastNode, nowY), nowY, scale);
        rowTop = top;
        for (var index = start; index < end; index++)
        {
            var height = MeasureAgendaRow(index, cardWidth, scale, out var titleLines);
            var row = new Rect(new Vector2(cardLeft, rowTop), new Vector2(cardLeft + cardWidth, rowTop + height));
            if (index == start)
            {
                UiAnchors.Report("loveisland.agenda.first", row);
            }

            var firstVertex = CardEntrance.Mark(drawList);
            if (ImGui.IsRectVisible(new Vector2(left, row.Min.Y), row.Max))
            {
                var state = CampaignCountdown.StateOf(experiences[index], nowUnix);
                DrawAgendaRow(drawList, experiences[index], labels.Experiences[index], row, titleLines, state, scale);
                DrawRailNode(drawList, new Vector2(railX, NodeY(rowTop, scale)), state, scale);
                if (state == ExperienceState.Ended)
                {
                    CardEntrance.Fade(drawList, firstVertex, EndedRowAlpha);
                }
            }

            entrance.Settle(drawList, firstVertex, scale);
            rowTop += height + gap;
        }

        return rowTop - gap;
    }

    private static float NodeY(float rowTop, float scale) =>
        rowTop + RowPadY * scale + Typography.LineHeight(TextStyles.SubheadlineEmphasized) * 0.5f;

    private void DrawRail(ImDrawListPtr drawList, float x, float from, float to, float nowY, float scale)
    {
        if (from < 0f || to <= from)
        {
            return;
        }

        var half = RailThickness * 0.5f * scale;
        drawList.AddRectFilled(new Vector2(x - half, from), new Vector2(x + half, to),
            ImGui.GetColorU32(LoveIslandKit.White with { W = RailTrackAlpha }), half);
        if (nowY <= from)
        {
            return;
        }

        drawList.AddRectFilledMultiColor(new Vector2(x - half, from), new Vector2(x + half, nowY),
            ImGui.GetColorU32(Palette.Darken(ui.Accent, 0.2f)), ImGui.GetColorU32(Palette.Darken(ui.Accent, 0.2f)),
            ImGui.GetColorU32(EclipseFx.Rose(ui.Accent, 0.45f)), ImGui.GetColorU32(EclipseFx.Rose(ui.Accent, 0.45f)));
        var head = new Vector2(x, nowY);
        SoftGlow.Draw(drawList, head, RailHeadGlow * scale, EclipseFx.Rose(ui.Accent, 0.3f) with { W = 0.85f });
        drawList.AddCircleFilled(head, RailHeadRadius * scale, ImGui.GetColorU32(LoveIslandKit.White), 16);
    }

    private void DrawRailNode(ImDrawListPtr drawList, Vector2 center, ExperienceState state, float scale)
    {
        switch (state)
        {
            case ExperienceState.Live:
                SoftGlow.Draw(drawList, center, RailLiveGlow * scale, ui.Accent with { W = 0.65f });
                EclipseFx.Beacon(drawList, center, RailNodeRadius * scale, EclipseFx.Rose(ui.Accent, 0.55f), scale);
                break;
            case ExperienceState.Ended:
                drawList.AddCircleFilled(center, RailNodeRadius * 0.8f * scale,
                    ImGui.GetColorU32(EclipseFx.Rose(ui.Accent, 0.3f)), 16);
                break;
            default:
                drawList.AddCircleFilled(center, RailNodeRadius * scale, ImGui.GetColorU32(LoveIslandKit.Night), 16);
                drawList.AddCircle(center, RailNodeRadius * scale,
                    ImGui.GetColorU32(LoveIslandKit.White with { W = 0.45f }), 16, 1.5f * scale);
                break;
        }
    }

    private float MeasureAgendaRow(int index, float width, float scale, out string[] titleLines)
    {
        var experienceLabels = labels.Experiences[index];
        var textWidth = MathF.Max(1f, width - (RowTimeColumn + LoveIslandKit.CardPad + RowChevron) * scale
                                      - Metrics.Space.Md * scale);
        var title = frameCampaign!.Experiences[index].Title;
        titleLines = experienceLabels.RowTitle.Get(title, TextStyles.Headline, textWidth, RowTitleLines, fontKey);
        var titleHeight = Math.Max(1, titleLines.Length) * Typography.LineHeight(TextStyles.Headline);
        var detailHeight = MathF.Max(LoveIslandKit.PillHeight(scale), Typography.LineHeight(TextStyles.Footnote));
        return RowPadY * 2f * scale + titleHeight + RowTitleGap * scale + detailHeight;
    }

    private void DrawAgendaRow(ImDrawListPtr drawList, CampaignExperienceDto experience,
        ExperienceLabels experienceLabels, Rect row, string[] titleLines, ExperienceState state, float scale)
    {
        var live = state == ExperienceState.Live;
        var tapped = LoveIslandKit.PressableCard(drawList, ui, row, experience.Id, scale, true, out _, live);
        var pad = LoveIslandKit.CardPad * scale;
        var top = row.Min.Y + RowPadY * scale;
        var timeLeft = row.Min.X + pad;
        var timeWidth = RowTimeColumn * scale - Metrics.Space.Sm * scale;
        Typography.Draw(drawList, new Vector2(timeLeft, top),
            Typography.FitText(experienceLabels.StartClock, timeWidth, TextStyles.SubheadlineEmphasized),
            live ? EclipseFx.Rose(ui.Accent, 0.55f) : ui.TitleInk, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(timeLeft, top + Typography.LineHeight(TextStyles.SubheadlineEmphasized)),
            Typography.FitText(experienceLabels.EndClock, timeWidth, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);

        var textLeft = timeLeft + RowTimeColumn * scale;
        var textRight = row.Max.X - pad - RowChevron * scale;
        var cursorY = top;
        var headlineHeight = Typography.LineHeight(TextStyles.Headline);
        for (var lineIndex = 0; lineIndex < titleLines.Length; lineIndex++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, cursorY), titleLines[lineIndex], ui.TitleInk,
                TextStyles.Headline);
            cursorY += headlineHeight;
        }

        if (titleLines.Length == 0)
        {
            cursorY += headlineHeight;
        }

        cursorY += RowTitleGap * scale;
        var detailLeft = textLeft;
        var pillHeight = LoveIslandKit.PillHeight(scale);
        if (live)
        {
            detailLeft += LoveIslandKit.LivePill(drawList, new Vector2(detailLeft, cursorY),
                Loc.T(L.LoveIsland.NowPill), ui.Accent, scale) + Metrics.Space.Xs * scale;
        }

        if (experience.Stage.Length > 0)
        {
            detailLeft += LoveIslandKit.StagePill(drawList, new Vector2(detailLeft, cursorY), experience.Stage, ui.Accent,
                scale) + Metrics.Space.Xs * scale;
        }

        var detailWidth = textRight - detailLeft;
        if (detailWidth > 0f && experienceLabels.RowDetail.Length > 0)
        {
            var detail = Typography.FitText(experienceLabels.RowDetail, detailWidth, TextStyles.Footnote);
            var detailHeight = Typography.LineHeight(TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(detailLeft, cursorY + (pillHeight - detailHeight) * 0.5f), detail,
                ui.MutedInk, TextStyles.Footnote);
        }

        PhoneIcon.Draw(drawList, new Vector2(row.Max.X - pad - RowChevron * 0.5f * scale, row.Center.Y),
            PhoneIcons.ChevronRight, ui.MutedInk, RowChevron * scale);
        if (tapped)
        {
            OpenExperience(experience.Id);
        }
    }
}
