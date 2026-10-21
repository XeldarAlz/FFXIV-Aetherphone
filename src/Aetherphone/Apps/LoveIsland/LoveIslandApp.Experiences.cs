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
    private const float RowNowWash = 0.14f;
    private const float DayHeaderTopGap = 18f;
    private const int RowTitleLines = 2;

    private void DrawExperiencesTab(Rect area)
    {
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
        var total = 0f;
        for (var index = start; index < end; index++)
        {
            total += MeasureAgendaRow(index, width, scale, out _);
        }

        var radius = LoveIslandKit.CardRadius * scale;
        ui.Card(drawList, new Vector2(left, top), new Vector2(left + width, top + total), radius);
        var rowTop = top;
        for (var index = start; index < end; index++)
        {
            var height = MeasureAgendaRow(index, width, scale, out var titleLines);
            var row = new Rect(new Vector2(left, rowTop), new Vector2(left + width, rowTop + height));
            if (index == start)
            {
                UiAnchors.Report("loveisland.agenda.first", row);
            }

            if (ImGui.IsRectVisible(row.Min, row.Max))
            {
                DrawAgendaRow(drawList, experiences[index], labels.Experiences[index], row, titleLines,
                    index == start, index == end - 1, scale);
            }

            rowTop += height;
        }

        return rowTop;
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
        ExperienceLabels experienceLabels, Rect row, string[] titleLines, bool first, bool last, float scale)
    {
        var state = CampaignCountdown.StateOf(experience, nowUnix);
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (state == ExperienceState.Live || hovered)
        {
            var corners = (first ? ImDrawFlags.RoundCornersTop : ImDrawFlags.None)
                          | (last ? ImDrawFlags.RoundCornersBottom : ImDrawFlags.None);
            if (corners == ImDrawFlags.None)
            {
                corners = ImDrawFlags.RoundCornersNone;
            }

            var wash = state == ExperienceState.Live ? Palette.WithAlpha(ui.Accent, RowNowWash) : ui.HoverTint;
            drawList.AddRectFilled(row.Min, row.Max, ImGui.GetColorU32(wash), LoveIslandKit.CardRadius * scale, corners);
        }

        var pad = LoveIslandKit.CardPad * scale;
        var top = row.Min.Y + RowPadY * scale;
        var timeLeft = row.Min.X + pad;
        Typography.Draw(drawList, new Vector2(timeLeft, top),
            Typography.FitText(experienceLabels.StartClock, RowTimeColumn * scale - Metrics.Space.Sm * scale,
                TextStyles.SubheadlineEmphasized), ui.TitleInk, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(timeLeft, top + Typography.LineHeight(TextStyles.SubheadlineEmphasized)),
            Typography.FitText(experienceLabels.EndClock, RowTimeColumn * scale - Metrics.Space.Sm * scale,
                TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);

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
        if (state == ExperienceState.Live)
        {
            detailLeft += LoveIslandKit.LivePill(drawList, new Vector2(detailLeft, cursorY),
                Loc.T(L.LoveIsland.NowPill), scale) + Metrics.Space.Xs * scale;
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
        if (!last)
        {
            FeedCell.Hairline(drawList, textLeft, row.Max.X, row.Max.Y, ui.Hairline);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            OpenExperience(experience.Id);
        }
    }
}
