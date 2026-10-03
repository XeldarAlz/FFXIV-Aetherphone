using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Fishing;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Fishing;

internal sealed partial class FishingApp
{
    private const float StopTile = 34f;
    private const float StopHeaderHeight = 52f;
    private const float StopFishHeight = 50f;
    private const float StopNoteGap = 8f;
    private const float SummaryHeight = 66f;

    private readonly OceanVoyageSlot[] detailProbe = new OceanVoyageSlot[1];
    private CachedText detailWhen;
    private CachedText detailSummary;

    private void DrawVoyageDetail(Rect area, in FishingRoute target)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        var boarding = DateTimeOffset.FromUnixTimeSeconds(target.BoardingUnix).UtcDateTime;
        GameSchedule.UpcomingOceanVoyages(boarding, target.OceanRoute, detailProbe);
        var slot = detailProbe[0];
        var labels = voyageText.For(slot.Destination, slot.Time);
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var navBar = AppHeader.BeginLargeTitle(context);
        using (ImRaii.PushId("fishing.voyage"))
        using (AppSurface.Begin(navBar.Body))
        {
            DrawVoyageSummary(labels, target, nowUnix, scale);
            for (var index = 0; index < labels.Stops.Length; index++)
            {
                DrawStopCard(labels, index, scale);
            }

            DrawFootnote(Loc.T(L.Fishing.SpectralNote), scale);
            BottomSpacer(scale);
        }

        var closed = nowUnix >= target.BoardingUnix;
        var reminded = alerts.HasVoyageReminder(target.BoardingUnix);
        var count = 0;
        if (!closed)
        {
            navButtons[0] = new NavBarButton(reminded ? PhoneIcons.BellFilled : PhoneIcons.Bell,
                reminded ? Loc.T(L.Fishing.ReminderOff) : Loc.T(L.Fishing.RemindMe));
            count = 1;
            UiAnchors.Report("fishing.voyage.remind", AppHeader.LargeTitleButtonRect(in navBar, 0, count));
        }

        var pressed = AppHeader.EndLargeTitle(in navBar, context, "fishing.voyage.nav", labels.Destination,
            NavBarStyle.From(ui), navButtons.AsSpan(0, count), Loc.T(L.Fishing.OceanTitle), back);
        if (pressed == 0 && !closed)
        {
            var enabled = alerts.ToggleVoyageReminder(target.BoardingUnix, target.OceanRoute);
            UiFeedback.Play(enabled ? UiSound.ToggleOn : UiSound.ToggleOff);
        }
    }

    private void DrawVoyageSummary(OceanVoyageLabels labels, in FishingRoute target, long nowUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ImGui.GetContentRegionAvail().X;
        var height = SummaryHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var card = new Rect(origin, origin + new Vector2(width, height));
        ui.Card(drawList, card.Min, card.Max, FishingArt.CardRadius * scale, elevated: true);
        var pad = FishingArt.CardPadding * scale;
        var tile = StopTile * scale;
        FishingArt.TimeTile(drawList, new Vector2(card.Min.X + pad + tile * 0.5f, card.Center.Y), tile,
            labels.TimeOfDay);
        var left = card.Min.X + pad + tile + RowGap * scale;
        var right = card.Max.X - pad;
        var whenKey = target.BoardingUnix * 4 + (nowUnix >= target.BoardingUnix ? 1 : 0) +
                      (target.BoardingUnix - nowUnix) / 60 * 8;
        var when = detailWhen.IsCurrent(whenKey)
            ? detailWhen.Value
            : detailWhen.Store(whenKey, FishingText.Join(FishingText.LocalDayClock(target.BoardingUnix),
                nowUnix >= target.BoardingUnix
                    ? Loc.T(L.Fishing.Departed)
                    : FishingText.Relative(target.BoardingUnix - nowUnix)));
        var summaryKey = target.BoardingUnix * 2 + (long)target.OceanRoute;
        var summary = detailSummary.IsCurrent(summaryKey)
            ? detailSummary.Value
            : detailSummary.Store(summaryKey, FishingText.Join(
                target.OceanRoute == OceanRoute.Ruby ? Loc.T(L.Fishing.RubyRoute) : Loc.T(L.Fishing.IndigoRoute),
                Loc.T(L.Fishing.ArrivesAt, FishingText.TimeOfDay(labels.TimeOfDay))));
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var subHeight = Typography.LineHeight(TextStyles.Subheadline);
        var top = card.Center.Y - (titleHeight + LineGap * scale + subHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(when, right - left, TextStyles.Headline),
            ui.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(left, top + titleHeight + LineGap * scale),
            Typography.FitText(summary, right - left, TextStyles.Subheadline), ui.MutedInk, TextStyles.Subheadline);
        ImGui.Dummy(new Vector2(width, height + FishingArt.CardGap * scale));
    }

    private void DrawStopCard(OceanVoyageLabels labels, int stopIndex, float scale)
    {
        var stop = labels.Stops[stopIndex];
        var blueIndex = BlueIndexFor(labels, stop);
        var hasBlue = blueIndex >= 0;
        var pad = FishingArt.CardPadding * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var innerWidth = width - pad * 2f;
        var intuitionText = hasBlue ? labels.BlueIntuition[blueIndex] : string.Empty;
        var intuitionHeight = intuitionText.Length > 0
            ? Typography.MeasureWrappedBlock(intuitionText, TextStyles.Footnote, innerWidth).Y + StopNoteGap * scale
            : 0f;
        var height = StopHeaderHeight * scale + (hasBlue ? StopFishHeight * scale + intuitionHeight : 0f) + pad * 0.5f;
        var origin = ImGui.GetCursorScreenPos();
        var card = new Rect(origin, origin + new Vector2(width, height));
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, card.Min, card.Max, FishingArt.CardRadius * scale);
        var left = card.Min.X + pad;
        var right = card.Max.X - pad;
        var tile = StopTile * scale;
        var headerCenterY = card.Min.Y + StopHeaderHeight * scale * 0.5f + pad * 0.25f;
        FishingArt.TimeTile(drawList, new Vector2(left + tile * 0.5f, headerCenterY), tile, stop.TimeOfDay);
        var textLeft = left + tile + RowGap * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var subHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = headerCenterY - (titleHeight + LineGap * scale + subHeight) * 0.5f;
        Marquee.DrawLeftAuto(drawList, new MarqueeId("fishing.stop.", stopIndex), labels.StopNames[stopIndex], textLeft,
            top, MathF.Max(1f, right - textLeft), TextStyles.Headline, ui.TitleInk);
        Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight + LineGap * scale),
            labels.StopTimes[stopIndex], ui.MutedInk,
            TextStyles.Footnote);

        if (hasBlue)
        {
            var fishTop = card.Min.Y + StopHeaderHeight * scale + pad * 0.25f;
            drawList.AddLine(new Vector2(left, fishTop), new Vector2(right, fishTop), ImGui.GetColorU32(ui.Hairline),
                1f);
            var row = new Rect(new Vector2(left, fishTop), new Vector2(right, fishTop + StopFishHeight * scale));
            DrawBlueFishRow(drawList, labels, blueIndex, row, "fishing.stop.blue.", scale);
            if (intuitionText.Length > 0)
            {
                Typography.DrawWrappedLeft(new Vector2(left, row.Max.Y), intuitionText, ui.MutedInk,
                    TextStyles.Footnote, innerWidth);
            }
        }

        ImGui.Dummy(new Vector2(width, height + RowGap * scale));
    }

    private static int BlueIndexFor(OceanVoyageLabels labels, in OceanStop stop)
    {
        for (var index = 0; index < labels.BlueFish.Length; index++)
        {
            var fish = labels.BlueFish[index];
            if (fish.SpotId == stop.SpotId && fish.TimeOfDay == stop.TimeOfDay)
            {
                return index;
            }
        }

        return -1;
    }
}
