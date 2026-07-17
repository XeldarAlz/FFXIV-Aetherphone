using System.Numerics;
using Aetherphone.Core.Animation;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Fishing;

internal sealed class FishingApp : IPhoneApp
{
    private const int VoyageCount = 12;
    private const float RefreshIntervalSeconds = 5f;
    private const float RouteSwitchHeight = 34f;
    private const float CardRounding = 18f;
    private const float CardPadding = 16f;
    private const float HeroFishRowHeight = 24f;
    private const float UpcomingRowHeight = 56f;
    private const float UpcomingTileSize = 32f;
    private const double VoyagePeriodSeconds = 7200;

    public string Id => "fishing";
    public string DisplayName => Loc.T(L.Apps.Fishing);
    public string Glyph => "F";
    public int BadgeCount => 0;

    private readonly OceanVoyageSlot[] voyages = new OceanVoyageSlot[VoyageCount];
    private readonly string[] routeLabels = new string[2];
    private readonly AppSkin ui = new(AppPalettes.Fishing);
    private OceanRoute route;
    private RefreshCadence refreshCadence;

    public void OnOpened() => Refresh();

    public void OnClosed()
    {
    }

    private void Refresh()
    {
        GameSchedule.UpcomingOceanVoyages(DateTime.UtcNow, route, voyages);
        refreshCadence.Reset();
    }

    public void Draw(in PhoneContext context)
    {
        if (refreshCadence.Advance(ImGui.GetIO().DeltaTime, RefreshIntervalSeconds))
        {
            Refresh();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        var utcNow = DateTime.UtcNow;

        ui.Theme = context.Theme;
        var screen = SceneChrome.ScreenFrom(content, context.Theme, scale);
        ui.Backdrop(screen);
        AppHeader.Draw(context, DisplayName);

        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        using (AppSurface.Begin(body))
        {
            DrawRouteSwitch(scale);
            DrawHero(utcNow, scale);
            DrawUpcoming(utcNow, scale);
            DrawNote(scale);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }
    }

    private void DrawRouteSwitch(float scale)
    {
        routeLabels[0] = Loc.T(L.Fishing.IndigoRoute);
        routeLabels[1] = Loc.T(L.Fishing.RubyRoute);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var row = new Rect(origin, origin + new Vector2(width, RouteSwitchHeight * scale));
        var selected = SegmentStrip.Draw("fishing.route", row, routeLabels, (int)route, ui.Palette);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, (RouteSwitchHeight + 12f) * scale));
        if (selected == (int)route)
        {
            return;
        }

        route = (OceanRoute)selected;
        Refresh();
    }

    private void DrawHero(DateTime utcNow, float scale)
    {
        var current = voyages[0];
        var plan = OceanRoutes.Resolve(current.Destination, current.Time);
        var fishLines = Math.Max(1, plan.BlueFish.Length);
        var width = ImGui.GetContentRegionAvail().X;
        var height = (92f + fishLines * HeroFishRowHeight + 24f) * scale;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = origin + new Vector2(width, height);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, min, max, CardRounding * scale, elevated: true);
        UiAnchors.Report("fishing.hero", new Rect(min, max));

        var pad = CardPadding * scale;
        var innerLeft = min.X + pad;
        var innerRight = max.X - pad;
        var tint = TimeOfDayTint(plan.TimeOfDay);

        DrawHeroStatus(current, new Vector2(innerLeft, min.Y + 14f * scale), innerRight, scale);

        var tile = 44f * scale;
        var tileCenter = new Vector2(innerRight - tile * 0.5f, min.Y + 50f * scale);
        ProgressRing.Glow(tileCenter, tile * 0.62f, tint, 0.30f + 0.16f * Pulse.Wave(Pulse.Breath));
        IconTile.Draw(tileCenter, tile, tint, TimeOfDayIcon(plan.TimeOfDay));

        ImGui.PushClipRect(new Vector2(innerLeft, min.Y), new Vector2(innerRight - tile - 10f * scale, max.Y), true);
        Typography.Draw(new Vector2(innerLeft, min.Y + 34f * scale), plan.RouteName, ui.TitleInk, TextStyles.Title2);
        ImGui.PopClipRect();

        DrawHeroTimeLine(current, plan.TimeOfDay, tint, new Vector2(innerLeft, min.Y + 64f * scale), utcNow);

        var separatorY = min.Y + 88f * scale;
        drawList.AddLine(new Vector2(innerLeft, separatorY), new Vector2(innerRight, separatorY),
            ImGui.GetColorU32(ui.Palette.CardStroke), 1f);

        var fishTop = separatorY + 4f * scale;
        DrawHeroBlueFish(plan, innerLeft, innerRight, fishTop, scale);
        UiAnchors.Report("fishing.bluefish",
            new Rect(new Vector2(innerLeft, fishTop), new Vector2(innerRight, fishTop + fishLines * HeroFishRowHeight * scale)));

        DrawHeroProgress(current, utcNow, new Rect(new Vector2(innerLeft, max.Y - 12f * scale),
            new Vector2(innerRight, max.Y - 8f * scale)));

        ImGui.SetCursorScreenPos(min);
        ImGui.Dummy(new Vector2(width, height + 14f * scale));
    }

    private void DrawHeroStatus(in OceanVoyageSlot current, Vector2 position, float innerRight, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var textLeft = position.X;
        if (current.BoardingNow)
        {
            var dotCenter = new Vector2(position.X + 4f * scale, position.Y + 7f * scale);
            drawList.AddCircleFilled(dotCenter, 3.5f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.45f + 0.55f * Pulse.Wave(Pulse.Breath))), 16);
            textLeft += 14f * scale;
        }

        var status = current.BoardingNow ? Loc.T(L.Fishing.NowBoarding) : Loc.T(L.Fishing.NextVoyage);
        Typography.Draw(new Vector2(textLeft, position.Y), Loc.Culture.TextInfo.ToUpper(status),
            current.BoardingNow ? ui.Accent : ui.Palette.HeaderInk, TextStyles.FootnoteEmphasized);

        var when = LocalTime(current.BoardingUtc);
        var whenSize = Typography.Measure(when, TextStyles.FootnoteEmphasized);
        Typography.Draw(new Vector2(innerRight - whenSize.X, position.Y), when, ui.MutedInk,
            TextStyles.FootnoteEmphasized);
    }

    private void DrawHeroTimeLine(in OceanVoyageSlot current, OceanTimeOfDay timeOfDay, Vector4 tint, Vector2 position,
        DateTime utcNow)
    {
        var label = TimeOfDayLabel(timeOfDay);
        Typography.Draw(position, label, tint, TextStyles.SubheadlineEmphasized);
        var labelWidth = Typography.Measure(label, TextStyles.SubheadlineEmphasized).X;
        var rest = current.BoardingNow
            ? $" · {Loc.T(L.Time.Now)}"
            : $" · {Relative(current.BoardingUtc - utcNow)}";
        Typography.Draw(new Vector2(position.X + labelWidth, position.Y), rest, ui.MutedInk, TextStyles.Subheadline);
    }

    private void DrawHeroBlueFish(in OceanRoutePlan plan, float innerLeft, float innerRight, float top, float scale)
    {
        if (plan.BlueFish.Length == 0)
        {
            var rowCenterY = top + HeroFishRowHeight * 0.5f * scale;
            Typography.Draw(new Vector2(innerLeft, rowCenterY - 7f * scale), Loc.T(L.Fishing.NoBlueFish), ui.MutedInk,
                TextStyles.Footnote);
            return;
        }

        ImGui.PushClipRect(new Vector2(innerLeft, top), new Vector2(innerRight, top + plan.BlueFish.Length * HeroFishRowHeight * scale),
            true);
        for (var index = 0; index < plan.BlueFish.Length; index++)
        {
            var fish = plan.BlueFish[index];
            var rowCenterY = top + (index + 0.5f) * HeroFishRowHeight * scale;
            ProgressRing.CenterIcon(new Vector2(innerLeft + 7f * scale, rowCenterY), FontAwesomeIcon.Fish,
                Accent.BlueSoft, 12f * scale);
            var nameLeft = innerLeft + 22f * scale;
            Typography.Draw(new Vector2(nameLeft, rowCenterY - 8f * scale), fish.Name, ui.BodyInk,
                TextStyles.SubheadlineEmphasized);
            var nameWidth = Typography.Measure(fish.Name, TextStyles.SubheadlineEmphasized).X;
            Typography.Draw(new Vector2(nameLeft + nameWidth + 8f * scale, rowCenterY - 6f * scale), fish.Bait,
                ui.MutedInk, TextStyles.Footnote);
        }

        ImGui.PopClipRect();
    }

    private void DrawHeroProgress(in OceanVoyageSlot current, DateTime utcNow, Rect bar)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = bar.Height * 0.5f;
        Squircle.Fill(drawList, bar.Min, bar.Max, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.10f)));
        var fraction = current.BoardingNow
            ? 1f
            : 1f - (float)Math.Clamp((current.BoardingUtc - utcNow).TotalSeconds / VoyagePeriodSeconds, 0f, 1f);
        if (fraction <= 0f)
        {
            return;
        }

        var fillMax = new Vector2(bar.Min.X + Math.Max(bar.Height, bar.Width * fraction), bar.Max.Y);
        Squircle.Fill(drawList, bar.Min, fillMax, radius, ImGui.GetColorU32(ui.Accent));
    }

    private void DrawUpcoming(DateTime utcNow, float scale)
    {
        ui.SectionLabel(Loc.T(L.Fishing.Upcoming), TextStyles.FootnoteEmphasized, 6f);
        var width = ImGui.GetContentRegionAvail().X;
        var rowCount = VoyageCount - 1;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = origin + new Vector2(width, rowCount * UpcomingRowHeight * scale);
        ui.Card(ImGui.GetWindowDrawList(), min, max, CardRounding * scale, elevated: true);

        for (var index = 1; index < VoyageCount; index++)
        {
            var row = UpcomingRow(min, max, index - 1, scale);
            if (index == 1)
            {
                UiAnchors.Report("fishing.upcoming", row);
            }

            DrawVoyageRow(row, voyages[index], utcNow, scale);
        }

        ImGui.SetCursorScreenPos(min);
        ImGui.Dummy(new Vector2(width, max.Y - min.Y + 4f * scale));
    }

    private Rect UpcomingRow(Vector2 cardMin, Vector2 cardMax, int rowIndex, float scale)
    {
        var pad = CardPadding * scale;
        var top = cardMin.Y + rowIndex * UpcomingRowHeight * scale;
        if (rowIndex > 0)
        {
            ImGui.GetWindowDrawList().AddLine(
                new Vector2(cardMin.X + pad + (UpcomingTileSize + 12f) * scale, top),
                new Vector2(cardMax.X - pad, top), ImGui.GetColorU32(ui.Palette.CardStroke), 1f);
        }

        return new Rect(new Vector2(cardMin.X + pad, top),
            new Vector2(cardMax.X - pad, top + UpcomingRowHeight * scale));
    }

    private void DrawVoyageRow(Rect row, in OceanVoyageSlot voyage, DateTime utcNow, float scale)
    {
        var plan = OceanRoutes.Resolve(voyage.Destination, voyage.Time);
        var tile = UpcomingTileSize * scale;
        var tileCenter = new Vector2(row.Min.X + tile * 0.5f, row.Center.Y);
        IconTile.Draw(tileCenter, tile, TimeOfDayTint(plan.TimeOfDay), TimeOfDayIcon(plan.TimeOfDay));

        var time = LocalTime(voyage.BoardingUtc);
        var timeSize = Typography.Measure(time, TextStyles.SubheadlineEmphasized);
        Typography.Draw(new Vector2(row.Max.X - timeSize.X, row.Center.Y - 17f * scale), time, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var relative = Relative(voyage.BoardingUtc - utcNow);
        var relativeSize = Typography.Measure(relative, TextStyles.Caption1);
        Typography.Draw(new Vector2(row.Max.X - relativeSize.X, row.Center.Y + 4f * scale), relative, ui.MutedInk,
            TextStyles.Caption1);

        var textLeft = row.Min.X + tile + 12f * scale;
        var textRight = row.Max.X - timeSize.X - 12f * scale;
        ImGui.PushClipRect(new Vector2(textLeft, row.Min.Y), new Vector2(textRight, row.Max.Y), true);
        if (plan.BlueFish.Length == 0)
        {
            var nameSize = Typography.Measure(plan.RouteName, TextStyles.Headline);
            Typography.Draw(new Vector2(textLeft, row.Center.Y - nameSize.Y * 0.5f), plan.RouteName, ui.TitleInk,
                TextStyles.Headline);
        }
        else
        {
            Typography.Draw(new Vector2(textLeft, row.Center.Y - 18f * scale), plan.RouteName, ui.TitleInk,
                TextStyles.Headline);
            ProgressRing.CenterIcon(new Vector2(textLeft + 5f * scale, row.Center.Y + 9f * scale),
                FontAwesomeIcon.Fish, Accent.BlueSoft, 10f * scale);
            Typography.Draw(new Vector2(textLeft + 14f * scale, row.Center.Y + 2f * scale), BlueFishNames(plan),
                ui.MutedInk, TextStyles.Footnote);
        }

        ImGui.PopClipRect();
    }

    private void DrawNote(float scale)
    {
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 4f * scale);
        using (Plugin.Fonts.Push(TextStyles.Footnote.Scale))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.MutedInk))
        {
            Typography.Wrapped(Loc.T(L.Fishing.DeparturesNote));
        }
    }

    private static string BlueFishNames(in OceanRoutePlan plan)
    {
        if (plan.BlueFish.Length == 1)
        {
            return plan.BlueFish[0].Name;
        }

        return string.Concat(plan.BlueFish[0].Name, " · ", plan.BlueFish[1].Name);
    }

    private static Vector4 TimeOfDayTint(OceanTimeOfDay timeOfDay) =>
        timeOfDay switch
        {
            OceanTimeOfDay.Sunset => Accent.Rose,
            OceanTimeOfDay.Night => Accent.Violet,
            _ => Accent.Amber,
        };

    private static FontAwesomeIcon TimeOfDayIcon(OceanTimeOfDay timeOfDay) =>
        timeOfDay switch
        {
            OceanTimeOfDay.Sunset => FontAwesomeIcon.CloudSun,
            OceanTimeOfDay.Night => FontAwesomeIcon.Moon,
            _ => FontAwesomeIcon.Sun,
        };

    private static string TimeOfDayLabel(OceanTimeOfDay timeOfDay) =>
        timeOfDay switch
        {
            OceanTimeOfDay.Sunset => Loc.T(L.Fishing.Sunset),
            OceanTimeOfDay.Night => Loc.T(L.Fishing.Night),
            _ => Loc.T(L.Fishing.Day),
        };

    private static string LocalTime(DateTime utc)
    {
        var local = utc.ToLocalTime();
        return string.Concat(local.ToString("ddd", Loc.Culture), " ", local.ToString("t", Loc.Culture));
    }

    private static string Relative(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return Loc.T(L.Time.Now);
        }

        var totalMinutes = (int)remaining.TotalMinutes;
        if (totalMinutes < 60)
        {
            return Loc.T(L.Time.InMinutes, Math.Max(1, totalMinutes));
        }

        var totalHours = totalMinutes / 60;
        if (totalHours < 24)
        {
            var minutes = totalMinutes % 60;
            return minutes == 0 ? Loc.T(L.Time.InHours, totalHours) : Loc.T(L.Time.InHoursMinutes, totalHours, minutes);
        }

        var days = totalHours / 24;
        var hours = totalHours % 24;
        return Loc.T(L.Fishing.InDays, days, hours);
    }

    public void Dispose()
    {
    }
}
