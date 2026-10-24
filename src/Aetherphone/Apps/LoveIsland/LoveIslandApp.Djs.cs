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
    private const float DjRowPadY = 12f;
    private const float DjPortraitRadius = 22f;
    private const float DjTimeColumn = 58f;
    private const float DjTextGap = 12f;
    private const float DjDetailGap = 4f;
    private const float DjLiveGlow = 1.8f;
    private const float DjLiveRingAlpha = 0.9f;
    private const int DjNameLines = 2;

    private void DrawDjsTab(Rect area)
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
            var live = frameCampaign is not null
                       && CampaignCountdown.Evaluate(frameCampaign, nowUnix).CurrentDjIndex >= 0;
            var cursorY = DrawPageTitle(drawList, left, origin.Y, inner, Texts.T(CampaignTextKeys.TabDjs),
                Texts.T(CampaignTextKeys.DjsSub), live);
            ImGui.SetCursorScreenPos(new Vector2(origin.X, cursorY));
            if (DrawLoadState(frame.Body, out var stateBottom))
            {
                cursorY = stateBottom;
            }
            else
            {
                cursorY = DrawDjTimetable(drawList, left, cursorY, inner, scale);
            }

            Reserve(origin, width, cursorY + Metrics.Space.Xl * scale);
        }

        EndRoot(in frame, in context, "loveisland.nav.djs");
    }

    private float DrawDjTimetable(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var djSets = frameCampaign!.DjSets;
        if (djSets.Length == 0 || labels.DjSets.Length != djSets.Length)
        {
            return top + Typography.DrawWrappedLeft(new Vector2(left, top), Texts.T(CampaignTextKeys.NoDjs), ui.MutedInk,
                TextStyles.Body, width);
        }

        var cursorY = top;
        var start = 0;
        while (start < djSets.Length)
        {
            var end = start + 1;
            while (end < djSets.Length && labels.DjSets[end].LocalDay == labels.DjSets[start].LocalDay)
            {
                end++;
            }

            if (start > 0)
            {
                cursorY += DayHeaderTopGap * scale;
            }

            cursorY += CardSectionHeader.Draw(drawList, new Vector2(left, cursorY), width, labels.DjSets[start].DayLabel,
                ui.TitleInk);
            for (var index = start; index < end; index++)
            {
                if (index > start)
                {
                    cursorY += RowCardGap * scale;
                }

                cursorY = DrawDjRow(drawList, djSets[index], labels.DjSets[index], left, cursorY, width, scale, index == 0);
            }

            start = end;
        }

        return cursorY;
    }

    private float DrawDjRow(ImDrawListPtr drawList, CampaignDjSetDto djSet, DjLabels djLabels, float left, float top,
        float width, float scale, bool first)
    {
        var state = CampaignCountdown.StateOf(djSet, nowUnix);
        var live = state == ExperienceState.Live;
        var pad = LoveIslandKit.CardPad * scale;
        var radius = DjPortraitRadius * scale;
        var timeWidth = DjTimeColumn * scale;
        var textLeft = left + pad + radius * 2f + DjTextGap * scale;
        var textRight = left + width - pad - timeWidth - Metrics.Space.Sm * scale;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var nameLines = djLabels.NameLines.Get(djSet.Name, TextStyles.Headline, textWidth, DjNameLines, fontKey);
        var headlineHeight = Typography.LineHeight(TextStyles.Headline);
        var nameHeight = Math.Max(1, nameLines.Length) * headlineHeight;
        var detailHeight = MathF.Max(LoveIslandKit.PillHeight(scale), Typography.LineHeight(TextStyles.Footnote));
        var textHeight = nameHeight + DjDetailGap * scale + detailHeight;
        var height = DjRowPadY * 2f * scale + MathF.Max(textHeight, radius * 2f);
        var row = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        if (first)
        {
            UiAnchors.Report("loveisland.djs.first", row);
        }

        var firstVertex = CardEntrance.Mark(drawList);
        if (!ImGui.IsRectVisible(row.Min, row.Max))
        {
            entrance.Settle(drawList, firstVertex, scale);
            return row.Max.Y;
        }

        var member = djSet.MemberId is null ? null : Ledger.FindMember(djSet.MemberId);
        var tapped = LoveIslandKit.PressableCard(drawList, ui, row, djLabels.RowId, scale, member is not null, out _,
            live);
        var center = new Vector2(left + pad + radius, row.Center.Y);
        if (live)
        {
            SoftGlow.Draw(drawList, center, radius * DjLiveGlow, ui.Accent with { W = 0.45f });
        }

        LoveIslandKit.Portrait(drawList, images, member?.PortraitUrl, center, radius, ui.Accent,
            MonogramFor(djLabels.RowId, djSet.Name), ui.TitleInk);
        drawList.AddCircle(center, radius + 1.5f * scale,
            ImGui.GetColorU32(EclipseFx.Rose(ui.Accent, 0.5f) with { W = live ? DjLiveRingAlpha : LineupRingAlpha }), 48,
            1.5f * scale);

        var cursorY = row.Min.Y + (height - textHeight) * 0.5f;
        for (var lineIndex = 0; lineIndex < nameLines.Length; lineIndex++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, cursorY), nameLines[lineIndex], ui.TitleInk,
                TextStyles.Headline);
            cursorY += headlineHeight;
        }

        if (nameLines.Length == 0)
        {
            cursorY += headlineHeight;
        }

        cursorY += DjDetailGap * scale;
        var detailLeft = textLeft;
        if (live)
        {
            detailLeft += LoveIslandKit.LivePill(drawList, new Vector2(detailLeft, cursorY), Loc.T(L.LoveIsland.NowPill),
                ui.Accent, scale) + Metrics.Space.Xs * scale;
        }

        var detailWidth = textRight - detailLeft;
        if (detailWidth > 0f && djLabels.Detail.Length > 0)
        {
            var detail = Typography.FitText(djLabels.Detail, detailWidth, TextStyles.Footnote);
            var footnoteHeight = Typography.LineHeight(TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(detailLeft, cursorY + (detailHeight - footnoteHeight) * 0.5f), detail,
                ui.MutedInk, TextStyles.Footnote);
        }

        DrawDjClock(drawList, djLabels, left + width - pad, row.Center.Y, timeWidth, live);
        if (state == ExperienceState.Ended)
        {
            CardEntrance.Fade(drawList, firstVertex, EndedRowAlpha);
        }

        entrance.Settle(drawList, firstVertex, scale);
        if (tapped && member is not null)
        {
            OpenMember(member.Id);
        }

        return row.Max.Y;
    }

    private void DrawDjClock(ImDrawListPtr drawList, DjLabels djLabels, float right, float centerY, float width,
        bool live)
    {
        var startHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var endHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = centerY - (startHeight + endHeight) * 0.5f;
        var start = Typography.FitText(djLabels.StartClock, width, TextStyles.SubheadlineEmphasized);
        var startWidth = Typography.Measure(start, TextStyles.SubheadlineEmphasized).X;
        Typography.Draw(drawList, new Vector2(right - startWidth, top), start,
            live ? EclipseFx.Rose(ui.Accent, 0.55f) : ui.TitleInk, TextStyles.SubheadlineEmphasized);
        var end = Typography.FitText(djLabels.EndClock, width, TextStyles.Footnote);
        var endWidth = Typography.Measure(end, TextStyles.Footnote).X;
        Typography.Draw(drawList, new Vector2(right - endWidth, top + startHeight), end, ui.MutedInk,
            TextStyles.Footnote);
    }
}
