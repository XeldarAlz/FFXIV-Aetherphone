using Aetherphone.Apps.Clock.Widgets;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Clock;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Clock;

internal sealed partial class ClockApp
{
    private const float WorldRowHeight = 76f;
    private const float WorldDialRadius = 21f;
    private const float WorldTextGap = 12f;
    private const float WorldTimeGap = 10f;
    private const float WorldSubtitleGap = 1f;
    private const float WorldBadgeRadius = 11f;
    private const float WorldGripWidth = 18f;
    private const float WorldGripHit = 44f;
    private const float WorldLiftRadius = 16f;
    private const int PinnedWorldRows = 2;
    private const float HeroPad = 16f;
    private const float HeroTrackHeight = 6f;
    private const float HeroTrackGap = 12f;
    private const float HeroLabelGap = 5f;
    private const float HeroFooterGap = 12f;
    private const float HeroMarkerRadius = 6f;
    private const float HeroCelestialRadius = 16f;
    private const int HeroTrackSegments = 48;
    private const int HeroTrackLabelStep = 6;
    private const float HeroNightAlpha = 0.20f;
    private const double HeroFlightPeriodSeconds = 15.0;
    private const float HeroFlightWindow = 0.4f;
    private const float HeroBatSize = 0.9f;

    private static readonly TextStyle HeroClockStyle = TextStyles.WidgetDisplay;
    private static readonly TextStyle WorldTimeStyle = TextStyles.WidgetDisplayCompact;
    private static readonly Vector4 SunCore = new(1.00f, 0.86f, 0.46f, 1f);
    private static readonly Vector4 SunBand = new(1.00f, 0.80f, 0.40f, 0.92f);
    private static readonly Vector4 MoonCore = new(0.93f, 0.94f, 0.98f, 1f);
    private static readonly Vector4 MarkerShadow = new(0f, 0f, 0f, 0.28f);

    private CachedText heroEyebrow;
    private CachedText heroNextBell;
    private CachedText localDetail;
    private CachedText[] worldDetails = new CachedText[8];
    private Spring[] citySlots = Array.Empty<Spring>();
    private bool editingWorld;
    private int dragCity = -1;
    private float dragPressY;
    private float dragOffset;
    private int dragTarget = -1;
    private int pendingRemoveCity = -1;

    private void DrawWorld(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var cities = configuration.WorldClocks;
        if (cities.Count == 0)
        {
            editingWorld = false;
        }

        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (ImRaii.PushId("clock.world"))
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            var width = ImGui.GetContentRegionAvail().X;
            DrawEorzeaHero(width, scale);
            DrawWorldList(width, scale);
            ImGui.Dummy(new Vector2(0f, ClockArt.BottomPad * scale));
            if (dragCity >= 0)
            {
                surface.CancelDrag();
            }
        }

        ApplyPendingCityRemoval();
        var count = 0;
        if (cities.Count > 0)
        {
            count = NavButton(count, editingWorld ? PhoneIcons.Check : PhoneIcons.Edit,
                Loc.T(editingWorld ? L.Clock.Done : L.Clock.Edit));
        }

        var addIndex = count;
        count = NavButton(count, PhoneIcons.Plus, Loc.T(L.Clock.AddCity));
        UiAnchors.Report("clock.add", AppHeader.LargeTitleButtonRect(in navBar, addIndex, count));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "clock.world.nav", Loc.T(L.Clock.TabWorld),
            NavBarStyle.From(ui), navButtons.AsSpan(0, count));
        if (pressed == addIndex)
        {
            editingWorld = false;
            EndCityDrag(false);
            cityQuery = string.Empty;
            router.Push(ClockScreen.AddCity);
        }
        else if (pressed == 0)
        {
            editingWorld = !editingWorld;
            EndCityDrag(false);
        }
    }

    private void DrawEorzeaHero(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var pad = HeroPad * scale;
        var eyebrowHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var clockHeight = Typography.LineHeight(HeroClockStyle);
        var labelHeight = Typography.LineHeight(TextStyles.Caption1);
        var footerHeight = Typography.LineHeight(TextStyles.Subheadline);
        var height = pad * 2f + eyebrowHeight + clockHeight + labelHeight + footerHeight +
                     (HeroTrackGap + HeroTrackHeight + HeroLabelGap + HeroFooterGap) * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        UiAnchors.Report("clock.world.game", new Rect(origin, max));
        if (!ImGui.IsRectVisible(origin, max))
        {
            ClockArt.Advance(origin, width, height, ClockArt.SectionGap, scale);
            return;
        }

        var secondOfDay = EorzeaTime.CurrentSeconds() % EorzeaClock.SecondsPerDay;
        var hour = (int)(secondOfDay / EorzeaClock.SecondsPerBell);
        var minute = (int)(secondOfDay / 60 % 60);
        var bell = secondOfDay / (float)EorzeaClock.SecondsPerBell;
        var daylight = WeatherSky.Daylight(bell);
        var sky = WeatherSky.Blend(WeatherKind.Clear, daylight);
        var drawList = ImGui.GetWindowDrawList();
        var radius = Metrics.Radius.Grouped * scale;
        Squircle.FillVerticalGradient(drawList, origin, max, radius, ImGui.GetColorU32(sky.Top),
            ImGui.GetColorU32(sky.Bottom));
        Squircle.Stroke(drawList, origin, max, radius, ImGui.GetColorU32(sky.CardStroke), scale);

        var left = origin.X + pad;
        var right = max.X - pad;
        var top = origin.Y + pad;
        var eyebrow = heroEyebrow.IsCurrent(0)
            ? heroEyebrow.Value
            : heroEyebrow.Store(0, Loc.Upper(Loc.T(L.Home.Eorzea)));
        Typography.Draw(drawList, new Vector2(left, top), eyebrow, sky.InkSoft, TextStyles.FootnoteEmphasized);
        var clockTop = top + eyebrowHeight;
        ClockArt.DrawTime(drawList, new Vector2(left, clockTop), hour, minute, true, HeroClockStyle,
            TextStyles.Title3, sky.Ink, sky.InkSoft);
        var celestialRadius = HeroCelestialRadius * scale;
        DrawCelestial(drawList, new Vector2(right - celestialRadius, clockTop + clockHeight * 0.5f), celestialRadius,
            daylight, sky);
        if (SeasonalTheme.Halloween)
        {
            drawList.PushClipRect(origin, max, true);
            Spooks.DrawFlight(drawList, new Rect(origin, max), ImGui.GetTime(), HeroFlightPeriodSeconds,
                HeroFlightWindow, 0.18f, 0.25f, HeroBatSize * scale, Spooks.BatShadow with { W = 0.85f });
            drawList.PopClipRect();
            Treats.Offer(drawList, TreatSpot.Clock, new Rect(origin, max));
        }

        var trackTop = clockTop + clockHeight + HeroTrackGap * scale;
        DrawBellTrack(drawList, left, right, trackTop, bell, sky, scale);
        var labelTop = trackTop + (HeroTrackHeight + HeroLabelGap) * scale;
        DrawBellLabels(drawList, left, right, labelTop, sky);

        var footerTop = labelTop + labelHeight + HeroFooterGap * scale;
        var untilBell = (int)Math.Ceiling((EorzeaClock.SecondsPerBell - secondOfDay % EorzeaClock.SecondsPerBell) /
                                          EorzeaClock.Rate);
        var footer = heroNextBell.IsCurrent(untilBell)
            ? heroNextBell.Value
            : heroNextBell.Store(untilBell, Loc.T(L.Clock.NextBellIn, TimeText.MinutesSeconds(untilBell)));
        Typography.Draw(drawList, new Vector2(left, footerTop),
            Typography.FitText(footer, right - left, TextStyles.Subheadline), sky.Ink, TextStyles.Subheadline);
        ClockArt.Advance(origin, width, height, ClockArt.SectionGap, scale);
    }

    private static void DrawCelestial(ImDrawListPtr drawList, Vector2 center, float radius, float daylight,
        in SkyPalette sky)
    {
        if (daylight >= 0.5f)
        {
            for (var ring = 3; ring >= 1; ring--)
            {
                drawList.AddCircleFilled(center, radius * (1f + ring * 0.28f),
                    ImGui.GetColorU32(SunCore with { W = 0.10f }), 40);
            }

            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(SunCore), 40);
            return;
        }

        drawList.AddCircleFilled(center, radius * 1.45f, ImGui.GetColorU32(MoonCore with { W = 0.08f }), 40);
        if (SeasonalTheme.Halloween)
        {
            NightScene.Glow(drawList, center, radius * 3f, MoonCore with { W = 0.3f }, 10);
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(MoonCore), 40);
            return;
        }

        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(MoonCore), 40);
        drawList.AddCircleFilled(center + new Vector2(radius * 0.42f, -radius * 0.28f), radius * 0.86f,
            ImGui.GetColorU32(Vector4.Lerp(sky.Top, sky.Bottom, 0.3f)), 40);
    }

    private static void DrawBellTrack(ImDrawListPtr drawList, float left, float right, float top, float bell,
        in SkyPalette sky, float scale)
    {
        var height = HeroTrackHeight * scale;
        var bottom = top + height;
        var night = sky.Ink with { W = HeroNightAlpha };
        drawList.AddRectFilled(new Vector2(left, top), new Vector2(right, bottom), ImGui.GetColorU32(night),
            height * 0.5f);
        var segmentWidth = (right - left) / HeroTrackSegments;
        for (var segment = 0; segment < HeroTrackSegments; segment++)
        {
            var light = WeatherSky.Daylight((segment + 0.5f) * EorzeaClock.BellsPerDay / HeroTrackSegments);
            if (light <= 0.01f)
            {
                continue;
            }

            var segmentLeft = left + segment * segmentWidth;
            drawList.AddRectFilled(new Vector2(segmentLeft, top), new Vector2(segmentLeft + segmentWidth, bottom),
                ImGui.GetColorU32(SunBand with { W = SunBand.W * light }));
        }

        var markerRadius = HeroMarkerRadius * scale;
        var marker = new Vector2(left + (right - left) * Math.Clamp(bell / EorzeaClock.BellsPerDay, 0f, 1f),
            top + height * 0.5f);
        drawList.AddCircleFilled(marker + new Vector2(0f, scale), markerRadius + scale,
            ImGui.GetColorU32(MarkerShadow), 20);
        drawList.AddCircleFilled(marker, markerRadius, ImGui.GetColorU32(ClockArt.White), 20);
    }

    private static void DrawBellLabels(ImDrawListPtr drawList, float left, float right, float top, in SkyPalette sky)
    {
        for (var bell = 0; bell <= EorzeaClock.BellsPerDay; bell += HeroTrackLabelStep)
        {
            var text = ClockArt.Pair(bell);
            var textWidth = Typography.Measure(text, TextStyles.Caption1).X;
            var x = left + (right - left) * bell / EorzeaClock.BellsPerDay - textWidth * 0.5f;
            x = Math.Clamp(x, left, right - textWidth);
            Typography.Draw(drawList, new Vector2(x, top), text, sky.InkSoft, TextStyles.Caption1);
        }
    }

    private void DrawWorldList(float width, float scale)
    {
        var cities = configuration.WorldClocks;
        var rowCount = PinnedWorldRows + cities.Count;
        EnsureWorldCaches(cities.Count);
        var rowHeight = WorldRowHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var max = new Vector2(origin.X + width, origin.Y + rowCount * rowHeight);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var utcNow = DateTime.UtcNow;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        UpdateCityDrag(cities.Count, rowHeight);

        DrawWorldRow(drawList, RowRect(origin, width, 0f, rowHeight), ClockZones.Local, ref localDetail, -1, utcNow);
        DrawWorldRow(drawList, RowRect(origin, width, rowHeight, rowHeight), ClockZones.Server, ref worldDetails[0],
            -1, utcNow);
        for (var index = 0; index < cities.Count; index++)
        {
            var slot = CitySlot(index, cities.Count);
            var settled = citySlots[index].Step(slot, Motion.PageSettle, delta);
            if (index == dragCity)
            {
                continue;
            }

            var top = (PinnedWorldRows + settled) * rowHeight;
            FeedCell.Hairline(drawList, origin.X + Metrics.Space.Lg * scale, max.X, origin.Y + top, ui.Hairline);
            DrawWorldRow(drawList, RowRect(origin, width, top, rowHeight), CitySlotOf(cities[index]),
                ref worldDetails[index + 1], index, utcNow);
        }

        FeedCell.Hairline(drawList, origin.X + Metrics.Space.Lg * scale, max.X, origin.Y + rowHeight, ui.Hairline);
        if (dragCity >= 0 && dragCity < cities.Count)
        {
            var top = (PinnedWorldRows + dragCity) * rowHeight + dragOffset;
            var lifted = RowRect(origin, width, top, rowHeight);
            var liftRadius = WorldLiftRadius * scale;
            var cardFill = ui.Palette.CardFill;
            var opaque = Vector4.Lerp(ui.Palette.BackdropTop, cardFill with { W = 1f }, cardFill.W * 2f) with { W = 1f };
            ui.Card(drawList, lifted.Min, lifted.Max, liftRadius);
            Squircle.Fill(drawList, lifted.Min, lifted.Max, liftRadius, ImGui.GetColorU32(opaque));
            Squircle.Stroke(drawList, lifted.Min, lifted.Max, liftRadius, ImGui.GetColorU32(ui.Palette.CardStroke),
                scale);
            DrawWorldRow(drawList, lifted, CitySlotOf(cities[dragCity]), ref worldDetails[dragCity + 1], dragCity,
                utcNow);
        }

        ClockArt.Advance(origin, width, max.Y - origin.Y, 0f, scale);
    }

    private static Rect RowRect(Vector2 origin, float width, float top, float height) =>
        new(new Vector2(origin.X, origin.Y + top), new Vector2(origin.X + width, origin.Y + top + height));

    private static ClockSlot CitySlotOf(WorldClockEntry entry) =>
        new(ClockSlotKind.City, entry.City, ClockZones.Resolve(entry.TimeZoneId));

    private void EnsureWorldCaches(int cityCount)
    {
        if (worldDetails.Length < cityCount + 1)
        {
            Array.Resize(ref worldDetails, Math.Max(cityCount + 1, worldDetails.Length * 2));
        }

        if (citySlots.Length == cityCount)
        {
            return;
        }

        citySlots = new Spring[cityCount];
        for (var index = 0; index < cityCount; index++)
        {
            citySlots[index].SnapTo(index);
        }

        for (var index = 0; index < worldDetails.Length; index++)
        {
            worldDetails[index].Reset();
        }
    }

    private void DrawWorldRow(ImDrawListPtr drawList, Rect row, in ClockSlot slot, ref CachedText detail,
        int cityIndex, DateTime utcNow)
    {
        if (!ImGui.IsRectVisible(row.Min, row.Max))
        {
            return;
        }

        var scale = UiScale.Current;
        var inset = Metrics.Space.Lg * scale;
        var left = row.Min.X + inset;
        var right = row.Max.X - inset;
        var reading = ClockZones.Read(slot, utcNow);
        var editable = editingWorld && cityIndex >= 0;
        var dialRadius = WorldDialRadius * scale;
        var dialCenter = new Vector2(left + dialRadius, row.Center.Y);
        if (editable)
        {
            DrawRemoveBadge(drawList, dialCenter, cityIndex, scale);
        }
        else
        {
            AnalogClock.Draw(drawList, dialCenter, dialRadius, reading.Moment.Hour, reading.Moment.Minute,
                reading.Seconds, AnalogClock.DayNight(reading.Moment.Hour, ui.Accent), scale);
        }

        float contentRight;
        if (editable)
        {
            contentRight = DrawGrip(drawList, row, right, cityIndex, scale);
        }
        else
        {
            var timeWidth = ClockArt.TimeWidth(reading.Moment.Hour, reading.Moment.Minute, false, WorldTimeStyle,
                TextStyles.Subheadline);
            var timeTop = row.Center.Y - Typography.LineHeight(WorldTimeStyle) * 0.5f;
            ClockArt.DrawTime(drawList, new Vector2(right - timeWidth, timeTop), reading.Moment.Hour,
                reading.Moment.Minute, false, WorldTimeStyle, TextStyles.Subheadline, ui.TitleInk, ui.TitleInk);
            contentRight = right - timeWidth - WorldTimeGap * scale;
        }

        var textLeft = dialCenter.X + dialRadius + WorldTextGap * scale;
        var textWidth = MathF.Max(1f, contentRight - textLeft);
        var subtitle = slot.Kind == ClockSlotKind.Local
            ? LocalDetail(ref detail, utcNow)
            : ClockZones.Detail(ref detail, slot, reading);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var nameHeight = Typography.LineHeight(TextStyles.Title3);
        var top = row.Center.Y - (subtitleHeight + WorldSubtitleGap * scale + nameHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(subtitle, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, top + subtitleHeight + WorldSubtitleGap * scale),
            Typography.FitText(slot.Name, textWidth, TextStyles.Title3), ui.TitleInk, TextStyles.Title3);
    }

    private string LocalDetail(ref CachedText cache, DateTime utcNow)
    {
        var local = utcNow.ToLocalTime();
        var offset = (int)TimeZoneInfo.Local.GetUtcOffset(utcNow).TotalMinutes;
        var key = local.Date.Ticks / TimeSpan.TicksPerDay * 10_000L + offset + 5_000L;
        if (cache.IsCurrent(key))
        {
            return cache.Value;
        }

        var date = local.ToString("ddd d MMM", Loc.Culture);
        return cache.Store(key, string.Concat(date, " · ", ClockZones.UtcLabel(offset)));
    }

    private void DrawRemoveBadge(ImDrawListPtr drawList, Vector2 center, int cityIndex, float scale)
    {
        var radius = WorldBadgeRadius * scale;
        var hit = new Vector2(Metrics.Size.TapTarget * 0.5f * scale);
        var hovered = dragCity < 0 && UiInteract.Hover(center - hit, center + hit);
        var grow = PressFx.Scale(ImGui.GetID($"clock.city.remove.{cityIndex}"),
            hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left), Motion.PressScaleControl);
        ClockArt.MinusBadge(drawList, center, radius * grow, theme.Danger);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(center - hit, center + hit), Loc.T(L.Clock.RemoveCity));
        if (UiInteract.Click(center - hit, center + hit, hovered))
        {
            pendingRemoveCity = cityIndex;
        }
    }

    private float DrawGrip(ImDrawListPtr drawList, Rect row, float right, int cityIndex, float scale)
    {
        var hitWidth = WorldGripHit * scale;
        var gripRect = new Rect(new Vector2(row.Max.X - hitWidth, row.Min.Y), row.Max);
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(gripRect.Min);
        ImGui.InvisibleButton($"##clockCityGrip{cityIndex}", gripRect.Size);
        var hovered = ImGui.IsItemHovered() && UiInteract.Hover(gripRect.Min, gripRect.Max);
        var activated = hovered && ImGui.IsItemActivated();
        ImGui.SetCursorScreenPos(cursor);
        if (activated && dragCity < 0)
        {
            dragCity = cityIndex;
            dragPressY = ImGui.GetMousePos().Y;
            dragOffset = 0f;
            dragTarget = cityIndex;
        }

        if (hovered || dragCity == cityIndex)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNs);
        }

        var gripWidth = WorldGripWidth * scale;
        var center = new Vector2(right - gripWidth * 0.5f, row.Center.Y);
        ClockArt.Grip(drawList, center, gripWidth, dragCity == cityIndex ? ui.TitleInk : ui.MutedInk);
        return right - gripWidth - WorldTimeGap * scale;
    }

    private void UpdateCityDrag(int cityCount, float rowHeight)
    {
        if (dragCity < 0)
        {
            return;
        }

        if (dragCity >= cityCount || !editingWorld)
        {
            EndCityDrag(false);
            return;
        }

        var raw = ImGui.GetMousePos().Y - dragPressY;
        dragOffset = Math.Clamp(raw, -dragCity * rowHeight, (cityCount - 1 - dragCity) * rowHeight);
        dragTarget = Math.Clamp(dragCity + (int)MathF.Round(dragOffset / rowHeight), 0, cityCount - 1);
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            EndCityDrag(true);
        }
    }

    private int CitySlot(int index, int cityCount)
    {
        if (dragCity < 0 || dragCity >= cityCount || dragTarget == dragCity)
        {
            return index;
        }

        if (dragCity < dragTarget && index > dragCity && index <= dragTarget)
        {
            return index - 1;
        }

        if (dragTarget < dragCity && index >= dragTarget && index < dragCity)
        {
            return index + 1;
        }

        return index;
    }

    private void EndCityDrag(bool commit)
    {
        if (dragCity < 0)
        {
            return;
        }

        var from = dragCity;
        var to = dragTarget;
        dragCity = -1;
        dragTarget = -1;
        dragOffset = 0f;
        if (!commit || !ClockReorder.Move(configuration.WorldClocks, from, to))
        {
            return;
        }

        for (var index = 0; index < citySlots.Length; index++)
        {
            citySlots[index].SnapTo(index);
        }

        for (var index = 0; index < worldDetails.Length; index++)
        {
            worldDetails[index].Reset();
        }

        UiFeedback.Play(UiSound.Tap);
        configuration.Save();
    }

    private void ApplyPendingCityRemoval()
    {
        var index = pendingRemoveCity;
        pendingRemoveCity = -1;
        var cities = configuration.WorldClocks;
        if (index < 0 || index >= cities.Count)
        {
            return;
        }

        cities.RemoveAt(index);
        if (cities.Count == 0)
        {
            editingWorld = false;
        }

        configuration.Save();
    }
}
