using Aetherphone.Core;
using Aetherphone.Core.Animation;
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
    private const int VoyageCount = 12;
    private const float VoyageRefreshSeconds = 5f;
    private const float RouteSwitchHeight = 34f;
    private const float RouteSwitchGap = 12f;
    private const float HeroStatusTop = 16f;
    private const float HeroTitleGap = 6f;
    private const float HeroStopsHeight = 58f;
    private const float HeroStopTile = 26f;
    private const float HeroFishRow = 44f;
    private const float HeroFishIcon = 32f;
    private const float HeroBellSize = 34f;
    private const float HeroBarHeight = 4f;
    private const float UpcomingRowHeight = 60f;
    private const float UpcomingTile = 34f;
    private const float UpcomingBellGlyph = 12f;
    private const float RowGap = 12f;
    private const float LineGap = 2f;
    private const double VoyagePeriodSeconds = 7200;

    private readonly OceanVoyageSlot[] voyages = new OceanVoyageSlot[VoyageCount];
    private readonly string[] routeLabels = new string[2];
    private readonly CachedText[] voyageClocks = new CachedText[VoyageCount];
    private readonly CachedText[] voyageRelatives = new CachedText[VoyageCount];
    private CachedText heroStatus;
    private CachedText heroSubtitle;
    private OceanRoute route;
    private RefreshCadence voyageCadence;

    private void RefreshVoyages()
    {
        GameSchedule.UpcomingOceanVoyages(DateTime.UtcNow, route, voyages);
        voyageCadence.Reset();
    }

    private void TickVoyages()
    {
        if (voyageCadence.Advance(ImGui.GetIO().DeltaTime, VoyageRefreshSeconds))
        {
            RefreshVoyages();
        }
    }

    private void DrawVoyages(Rect body, float scale)
    {
        using var id = ImRaii.PushId("fishing.voyages");
        using var surface = AppSurface.Begin(body);
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        DrawRouteSwitch(scale);
        DrawHero(nowUnix, scale);
        DrawUpcoming(nowUnix, scale);
        DrawFootnote(Loc.T(L.Fishing.DeparturesNote), scale);
        BottomSpacer(scale);
    }

    private void DrawRouteSwitch(float scale)
    {
        routeLabels[0] = Loc.T(L.Fishing.IndigoRoute);
        routeLabels[1] = Loc.T(L.Fishing.RubyRoute);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var row = new Rect(origin, origin + new Vector2(width, RouteSwitchHeight * scale));
        UiAnchors.Report("fishing.route", row);
        var selected = SegmentStrip.Draw("fishing.route", row, routeLabels, (int)route, ui.Palette);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, (RouteSwitchHeight + RouteSwitchGap) * scale));
        if (selected == (int)route)
        {
            return;
        }

        route = (OceanRoute)selected;
        UiFeedback.Play(UiSound.Tap);
        RefreshVoyages();
    }

    private void DrawHero(long nowUnix, float scale)
    {
        var current = voyages[0];
        var labels = voyageText.For(current.Destination, current.Time);
        var boardingUnix = new DateTimeOffset(current.BoardingUtc).ToUnixTimeSeconds();
        var drawList = ImGui.GetWindowDrawList();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = FishingArt.CardPadding * scale;
        var fishRows = Math.Max(1, labels.BlueFish.Length);
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        var statusHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var subtitleHeight = Typography.LineHeight(TextStyles.Subheadline);
        var height = HeroStatusTop * scale + statusHeight + HeroTitleGap * scale + titleHeight + LineGap * scale +
                     subtitleHeight + RowGap * scale + HeroStopsHeight * scale + RowGap * scale +
                     fishRows * HeroFishRow * scale + RowGap * scale + HeroBarHeight * scale + pad;
        var origin = ImGui.GetCursorScreenPos();
        var card = new Rect(origin, origin + new Vector2(width, height));
        UiAnchors.Report("fishing.hero", card);

        var bellRect = new Rect(new Vector2(card.Max.X - pad - HeroBellSize * scale, card.Min.Y + pad * 0.75f),
            new Vector2(card.Max.X - pad, card.Min.Y + pad * 0.75f + HeroBellSize * scale));
        var showBell = !current.BoardingNow;
        var overBell = showBell && UiInteract.Hover(bellRect.Min, bellRect.Max);
        var hovered = !overBell && UiInteract.Hover(card.Min, card.Max);
        var drawn = FishingArt.Pressed(card, ImGui.GetID("fishing.hero"), hovered);
        ui.Card(drawList, drawn.Min, drawn.Max, FishingArt.CardRadius * scale, elevated: true);
        if (hovered)
        {
            Squircle.Fill(drawList, drawn.Min, drawn.Max, FishingArt.CardRadius * scale,
                ImGui.GetColorU32(ui.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var left = card.Min.X + pad;
        var right = card.Max.X - pad;
        var textRight = showBell ? bellRect.Min.X - RowGap * scale : right;
        var top = card.Min.Y + HeroStatusTop * scale;
        DrawHeroStatus(drawList, current, boardingUnix, nowUnix, left, textRight, top, scale);
        top += statusHeight + HeroTitleGap * scale;
        Marquee.DrawLeftAuto(drawList, "fishing.hero.title", labels.Destination, left, top, textRight - left,
            TextStyles.Title2, ui.TitleInk);
        top += titleHeight + LineGap * scale;
        var subtitleKey = current.Destination * 65536L + current.Time * 4L + (long)route;
        var timeLine = heroSubtitle.IsCurrent(subtitleKey)
            ? heroSubtitle.Value
            : heroSubtitle.Store(subtitleKey, FishingText.Join(
                Loc.T(L.Fishing.ArrivesAt, FishingText.TimeOfDay(labels.TimeOfDay)), routeLabels[(int)route]));
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(timeLine, right - left,
            TextStyles.Subheadline), ui.MutedInk, TextStyles.Subheadline);
        top += subtitleHeight + RowGap * scale;
        DrawStops(drawList, labels, new Rect(new Vector2(left, top), new Vector2(right, top + HeroStopsHeight * scale)),
            scale);
        top += HeroStopsHeight * scale + RowGap * scale * 0.5f;
        drawList.AddLine(new Vector2(left, top), new Vector2(right, top), ImGui.GetColorU32(ui.Hairline), 1f);
        top += RowGap * scale * 0.5f;
        var fishArea = new Rect(new Vector2(left, top), new Vector2(right, top + fishRows * HeroFishRow * scale));
        UiAnchors.Report("fishing.bluefish", fishArea);
        DrawBlueFishRows(drawList, labels, fishArea, scale);
        top = fishArea.Max.Y + RowGap * scale;
        var fraction = current.BoardingNow
            ? 1f
            : 1f - (float)Math.Clamp((boardingUnix - nowUnix) / VoyagePeriodSeconds, 0d, 1d);
        FishingArt.Progress(drawList, new Rect(new Vector2(left, top), new Vector2(right, top + HeroBarHeight * scale)),
            fraction, ui.TitleInk, ui.Accent);

        if (showBell)
        {
            var reminded = alerts.HasVoyageReminder(boardingUnix);
            if (BellButton(bellRect, reminded,
                    reminded ? Loc.T(L.Fishing.ReminderOff) : Loc.T(L.Fishing.RemindMe)))
            {
                ToggleVoyageReminder(boardingUnix);
            }
        }

        if (!overBell && UiInteract.Click(card.Min, card.Max, hovered))
        {
            UiFeedback.Play(UiSound.Tap);
            router.Push(FishingRoute.Voyage(boardingUnix, route));
        }

        ImGui.SetCursorScreenPos(card.Min);
        ImGui.Dummy(new Vector2(width, height + FishingArt.CardGap * scale));
    }

    private void DrawHeroStatus(ImDrawListPtr drawList, in OceanVoyageSlot current, long boardingUnix, long nowUnix,
        float left, float right, float top, float scale)
    {
        var textLeft = left;
        if (current.BoardingNow)
        {
            var dotCenter = new Vector2(left + 4f * scale, top + Typography.LineHeight(TextStyles.FootnoteEmphasized) * 0.5f);
            drawList.AddCircleFilled(dotCenter, 3.5f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.45f + 0.55f * Pulse.Wave(Pulse.Breath))), 16);
            textLeft += 14f * scale;
        }

        var closesUnix = boardingUnix + FishingAlerts.BoardingWindowSeconds;
        var remaining = current.BoardingNow ? closesUnix - nowUnix : boardingUnix - nowUnix;
        var key = current.BoardingNow ? -1 - remaining : remaining / 60;
        var status = heroStatus.IsCurrent(key)
            ? heroStatus.Value
            : heroStatus.Store(key, Loc.Culture.TextInfo.ToUpper(current.BoardingNow
                ? Loc.T(L.Fishing.BoardingClosesIn, FishingClock.Countdown(remaining))
                : FishingText.Join(Loc.T(L.Fishing.NextVoyage), FishingText.Relative(remaining))));
        var color = current.BoardingNow ? ui.Accent : ui.Palette.HeaderInk;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(status, right - textLeft, TextStyles.FootnoteEmphasized),
            color, TextStyles.FootnoteEmphasized);
    }

    private void DrawStops(ImDrawListPtr drawList, OceanVoyageLabels labels, Rect area, float scale)
    {
        var count = labels.Stops.Length;
        if (count == 0)
        {
            return;
        }

        var tile = HeroStopTile * scale;
        var column = area.Width / count;
        var tileY = area.Min.Y + tile * 0.5f;
        var lineColor = ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.18f));
        for (var index = 0; index < count - 1; index++)
        {
            var fromX = area.Min.X + column * (index + 0.5f) + tile * 0.5f + 4f * scale;
            var toX = area.Min.X + column * (index + 1.5f) - tile * 0.5f - 4f * scale;
            drawList.AddLine(new Vector2(fromX, tileY), new Vector2(toX, tileY), lineColor, 1.5f * scale);
        }

        var nameTop = area.Min.Y + tile + 6f * scale;
        for (var index = 0; index < count; index++)
        {
            var centerX = area.Min.X + column * (index + 0.5f);
            var stop = labels.Stops[index];
            FishingArt.TimeTile(drawList, new Vector2(centerX, tileY), tile, stop.TimeOfDay);
            Marquee.DrawCenteredAuto(drawList, new MarqueeId("fishing.hero.stop.", labels.StopNames[index]),
                labels.StopNames[index], centerX, nameTop, column - 6f * scale, TextStyles.Caption1, ui.BodyInk);
        }
    }

    private void DrawBlueFishRows(ImDrawListPtr drawList, OceanVoyageLabels labels, Rect area, float scale)
    {
        var rowHeight = HeroFishRow * scale;
        if (labels.BlueFish.Length == 0)
        {
            var lineHeight = Typography.LineHeight(TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(area.Min.X, area.Min.Y + (rowHeight - lineHeight) * 0.5f),
                Typography.FitText(Loc.T(L.Fishing.NoBlueFish), area.Width, TextStyles.Footnote), ui.MutedInk,
                TextStyles.Footnote);
            return;
        }

        for (var index = 0; index < labels.BlueFish.Length; index++)
        {
            var row = new Rect(new Vector2(area.Min.X, area.Min.Y + index * rowHeight),
                new Vector2(area.Max.X, area.Min.Y + (index + 1) * rowHeight));
            DrawBlueFishRow(drawList, labels, index, row, "fishing.hero.blue.", scale);
        }
    }

    private void DrawBlueFishRow(ImDrawListPtr drawList, OceanVoyageLabels labels, int index, Rect row,
        string idPrefix, float scale)
    {
        var fish = labels.BlueFish[index];
        var icon = HeroFishIcon * scale;
        FishingArt.ItemIcon(drawList, textures, catalog.ItemIcon(fish.ItemId),
            new Vector2(row.Min.X, row.Center.Y - icon * 0.5f), icon, scale, Palette.WithAlpha(Accent.BlueSoft, 0.4f));
        var textLeft = row.Min.X + icon + RowGap * scale;
        var textRight = row.Max.X;
        if (alerts.IsCaught(fish.ItemId))
        {
            var checkSize = 16f * scale;
            PhoneIcon.Draw(drawList, new Vector2(row.Max.X - checkSize * 0.5f, row.Center.Y),
                PhoneIcons.CircleCheckFilled, ui.Accent, checkSize);
            textRight -= checkSize + RowGap * scale * 0.5f;
        }

        var nameHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var detailHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = row.Center.Y - (nameHeight + LineGap * scale + detailHeight) * 0.5f;
        var width = MathF.Max(1f, textRight - textLeft);
        Marquee.DrawLeftAuto(drawList, new MarqueeId(idPrefix, -1L - index), labels.BlueNames[index],
            textLeft, top, width, TextStyles.SubheadlineEmphasized, Accent.BlueSoft);
        var detail = FishingText.Join(labels.BlueStops[index], labels.BlueBaits[index]);
        Marquee.DrawLeftAuto(drawList, new MarqueeId(idPrefix, index + 1L), detail, textLeft,
            top + nameHeight + LineGap * scale, width, TextStyles.Footnote, ui.MutedInk);
    }

    private void DrawUpcoming(long nowUnix, float scale)
    {
        ui.SectionLabel(Loc.T(L.Fishing.Upcoming), TextStyles.FootnoteEmphasized, 6f);
        var card = GroupCard.Begin(ui, VoyageCount - 1, UpcomingRowHeight);
        card.SeparatorInset = (UpcomingTile + RowGap) * scale;
        for (var index = 1; index < VoyageCount; index++)
        {
            var row = card.NextRow();
            if (index == 1)
            {
                UiAnchors.Report("fishing.upcoming", row);
            }

            DrawVoyageRow(row, card.Bounds, index, nowUnix, scale);
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, 4f * scale));
    }

    private void DrawVoyageRow(Rect row, Rect card, int index, long nowUnix, float scale)
    {
        var voyage = voyages[index];
        var labels = voyageText.For(voyage.Destination, voyage.Time);
        var boardingUnix = new DateTimeOffset(voyage.BoardingUtc).ToUnixTimeSeconds();
        var drawList = ImGui.GetWindowDrawList();
        var bounds = new Rect(new Vector2(card.Min.X, row.Min.Y), new Vector2(card.Max.X, row.Max.Y));
        var hovered = UiInteract.Hover(bounds.Min, bounds.Max);
        if (hovered)
        {
            drawList.AddRectFilled(bounds.Min, bounds.Max, ImGui.GetColorU32(ui.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var tile = UpcomingTile * scale;
        FishingArt.TimeTile(drawList, new Vector2(row.Min.X + tile * 0.5f, row.Center.Y), tile, labels.TimeOfDay);
        var clockKey = boardingUnix;
        var clock = voyageClocks[index].IsCurrent(clockKey)
            ? voyageClocks[index].Value
            : voyageClocks[index].Store(clockKey, FishingText.LocalDayClock(boardingUnix));
        var relativeKey = (boardingUnix - nowUnix) / 60;
        var relative = voyageRelatives[index].IsCurrent(relativeKey)
            ? voyageRelatives[index].Value
            : voyageRelatives[index].Store(relativeKey, FishingText.Relative(boardingUnix - nowUnix));
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var subHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = row.Center.Y - (titleHeight + LineGap * scale + subHeight) * 0.5f;
        var clockWidth = Typography.Measure(clock, TextStyles.SubheadlineEmphasized).X;
        var relativeWidth = Typography.Measure(relative, TextStyles.Footnote).X;
        Typography.Draw(drawList, new Vector2(row.Max.X - clockWidth, top + (titleHeight -
                Typography.LineHeight(TextStyles.SubheadlineEmphasized)) * 0.5f), clock, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var relativeLeft = row.Max.X - relativeWidth;
        Typography.Draw(drawList, new Vector2(relativeLeft, top + titleHeight + LineGap * scale), relative,
            ui.MutedInk, TextStyles.Footnote);
        if (alerts.HasVoyageReminder(boardingUnix))
        {
            var bell = UpcomingBellGlyph * scale;
            PhoneIcon.Draw(drawList, new Vector2(relativeLeft - bell, top + titleHeight + LineGap * scale + subHeight * 0.5f),
                PhoneIcons.BellFilled, ui.Accent, bell);
            relativeLeft -= bell * 2f;
        }

        var textLeft = row.Min.X + tile + RowGap * scale;
        var titleWidth = MathF.Max(1f, row.Max.X - clockWidth - RowGap * scale - textLeft);
        var subWidth = MathF.Max(1f, relativeLeft - RowGap * scale - textLeft);
        Marquee.DrawLeftAuto(drawList, new MarqueeId("fishing.row.", boardingUnix), labels.Destination,
            textLeft, top, titleWidth, TextStyles.Headline, ui.TitleInk);
        Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight + LineGap * scale),
            Typography.FitText(labels.BlueSummary, subWidth, TextStyles.Footnote),
            labels.BlueFish.Length > 0 ? Accent.BlueSoft : ui.MutedInk, TextStyles.Footnote);

        if (UiInteract.Click(bounds.Min, bounds.Max, hovered))
        {
            UiFeedback.Play(UiSound.Tap);
            router.Push(FishingRoute.Voyage(boardingUnix, route));
        }
    }

    private void ToggleVoyageReminder(long boardingUnix)
    {
        var enabled = alerts.ToggleVoyageReminder(boardingUnix, route);
        UiFeedback.Play(enabled ? UiSound.ToggleOn : UiSound.ToggleOff);
    }

    private void DrawFootnote(string text, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 6f * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = Typography.DrawWrappedLeft(origin, text, ui.MutedInk, TextStyles.Footnote, width);
        ImGui.Dummy(new Vector2(width, height));
    }
}
