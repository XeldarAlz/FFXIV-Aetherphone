using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Fishing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Fishing;

internal sealed class FishDetailText
{
    public uint ItemId;
    public CultureInfo? Culture;
    public string EorzeaRange = string.Empty;
    public string Hook = string.Empty;
    public string Intuition = string.Empty;
    public string Extras = string.Empty;
    public string Coordinates = string.Empty;
    public string Location = string.Empty;
    public string[] Badges = Array.Empty<string>();
    public string[] BaitLines = Array.Empty<string>();
}

internal sealed partial class FishingApp
{
    private const int NextWindowCount = 4;
    private const float DetailIconSize = 56f;
    private const float DetailHeroHeight = 112f;
    private const float DetailBarHeight = 8f;
    private const float WeatherIconSize = 20f;
    private const float WeatherChipGap = 10f;
    private const float WeatherChipHeight = 26f;
    private const float BaitRowHeight = 36f;
    private const float BaitIconSize = 26f;
    private const float WindowRowHeight = 40f;
    private const float MapButtonHeight = 36f;
    private const float MapButtonWidth = 150f;
    private const float SectionInnerGap = 8f;

    private readonly FishDetailText detailText = new();
    private readonly FishWindow[] nextWindows = new FishWindow[NextWindowCount];
    private readonly CachedText[] nextWindowLines = new CachedText[NextWindowCount];
    private readonly CachedText[] nextWindowRelative = new CachedText[NextWindowCount];
    private CachedText detailStatus;
    private CachedText detailRange;
    private uint nextWindowsItem;
    private long nextWindowsUntil;
    private int nextWindowCount;

    private void DrawFishDetail(Rect area, uint itemId)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        if (!catalog.TryIndex(itemId, out var entryIndex))
        {
            using (AppSurface.Begin(navBar.Body))
            {
                EmptyState.Draw(navBar.Body, ui, FontAwesomeIcon.Fish, Loc.T(L.Fishing.LoadFailedTitle),
                    Loc.T(L.Fishing.LoadFailedHint));
            }

            AppHeader.EndLargeTitle(in navBar, context, "fishing.detail.nav", string.Empty, NavBarStyle.From(ui),
                ReadOnlySpan<NavBarButton>.Empty, Loc.T(L.Fishing.FishTab), back);
            return;
        }

        var entry = catalog.Entries[entryIndex];
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        EnsureDetailText(entry);
        EnsureNextWindows(entry, nowUnix);
        using (ImRaii.PushId("fishing.detail"))
        using (AppSurface.Begin(navBar.Body))
        {
            DrawDetailHero(entry, entryIndex, nowUnix, scale);
            DrawWindowCard(entry, nowUnix, scale);
            DrawCatchCard(entry, scale);
            DrawLocationCard(entry, scale);
            DrawFootnote(Loc.T(L.Fishing.WindowsNote), scale);
            BottomSpacer(scale);
        }

        var alarmed = alerts.HasAlarm(itemId);
        var count = 0;
        if (!entry.Fish.Rule.AlwaysOpen)
        {
            navButtons[0] = new NavBarButton(alarmed ? PhoneIcons.BellFilled : PhoneIcons.Bell,
                alarmed ? Loc.T(L.Fishing.AlarmOff) : Loc.T(L.Fishing.AlarmOn));
            count = 1;
            UiAnchors.Report("fishing.detail.alarm", AppHeader.LargeTitleButtonRect(in navBar, 0, count));
        }

        var pressed = AppHeader.EndLargeTitle(in navBar, context, "fishing.detail.nav", entry.Name,
            NavBarStyle.From(ui), navButtons.AsSpan(0, count), Loc.T(L.Fishing.FishTab), back);
        if (pressed == 0 && count == 1)
        {
            var enabled = alerts.ToggleAlarm(itemId);
            fishListDirty = true;
            UiFeedback.Play(enabled ? UiSound.ToggleOn : UiSound.ToggleOff);
        }
    }

    private void EnsureDetailText(FishEntry entry)
    {
        if (detailText.ItemId == entry.ItemId && ReferenceEquals(detailText.Culture, Loc.Culture))
        {
            return;
        }

        var fish = entry.Fish;
        detailText.ItemId = entry.ItemId;
        detailText.Culture = Loc.Culture;
        detailText.EorzeaRange = fish.Rule.AllDay
            ? Loc.T(L.Fishing.AllDayEorzea)
            : Loc.T(L.Fishing.EorzeaRange, FishingText.EorzeaClock(fish.Rule.StartMinute),
                FishingText.EorzeaClock(fish.Rule.EndMinute));
        detailText.Hook = FishingText.Join(FishingText.Hookset(fish.Hookset), FishingText.Tug(fish.Tug));
        var predators = FishingText.Predators(catalog, fish.Predators);
        detailText.Intuition = predators.Length == 0
            ? string.Empty
            : fish.IntuitionSeconds > 0
                ? Loc.T(L.Fishing.IntuitionTimed, predators, fish.IntuitionSeconds)
                : Loc.T(L.Fishing.IntuitionLine, predators);
        var extras = string.Empty;
        if ((fish.Flags & TimedFishFlags.Snagging) != 0)
        {
            extras = FishingText.Join(extras, Loc.T(L.Fishing.NeedsSnagging));
        }

        if ((fish.Flags & TimedFishFlags.AmbitiousLure) != 0)
        {
            extras = FishingText.Join(extras, Loc.T(L.Fishing.AmbitiousLure));
        }

        if ((fish.Flags & TimedFishFlags.ModestLure) != 0)
        {
            extras = FishingText.Join(extras, Loc.T(L.Fishing.ModestLure));
        }

        if ((fish.Flags & TimedFishFlags.Folklore) != 0)
        {
            extras = FishingText.Join(extras, Loc.T(L.Fishing.NeedsFolklore));
        }

        detailText.Extras = extras;
        detailText.Location = FishingText.Join(entry.SpotName, entry.ZoneName);
        detailText.Coordinates = entry.MapId == 0
            ? string.Empty
            : Loc.T(L.Fishing.Coordinates, entry.MapPosition.X.ToString("0.0", Loc.Culture),
                entry.MapPosition.Y.ToString("0.0", Loc.Culture));
        var badges = new List<string>(4);
        if (fish.IsBigFish)
        {
            badges.Add(Loc.T(L.Fishing.BigFish));
        }

        if ((fish.Flags & TimedFishFlags.Collectable) != 0)
        {
            badges.Add(Loc.T(L.Fishing.CollectableBadge));
        }

        if (fish.Patch > 0f)
        {
            badges.Add(Loc.T(L.Fishing.PatchBadge, fish.Patch.ToString("0.0#", Loc.Culture)));
        }

        detailText.Badges = badges.ToArray();
        var bait = new string[fish.BaitChain.Length];
        for (var index = 0; index < bait.Length; index++)
        {
            var name = catalog.ItemName(fish.BaitChain[index]);
            bait[index] = index == 0 ? Loc.T(L.Fishing.BaitWith, name) : Loc.T(L.Fishing.MoochWith, name);
        }

        detailText.BaitLines = bait;
    }

    private void EnsureNextWindows(FishEntry entry, long nowUnix)
    {
        if (nextWindowsItem == entry.ItemId && nowUnix < nextWindowsUntil)
        {
            return;
        }

        nextWindowsItem = entry.ItemId;
        if (entry.Fish.Rule.AlwaysOpen)
        {
            nextWindowCount = 0;
            nextWindowsUntil = long.MaxValue;
            return;
        }

        nextWindowCount = FishWindowMath.Upcoming(entry.Fish.Rule, entry.Odds, nowUnix, nextWindows);
        nextWindowsUntil = nextWindowCount > 0 ? nextWindows[0].EndUnix : nowUnix + 3600;
    }

    private void DrawDetailHero(FishEntry entry, int entryIndex, long nowUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = FishingArt.CardPadding * scale;
        var origin = ImGui.GetCursorScreenPos();
        var card = new Rect(origin, origin + new Vector2(width, DetailHeroHeight * scale));
        ui.Card(drawList, card.Min, card.Max, FishingArt.CardRadius * scale, elevated: true);
        var icon = DetailIconSize * scale;
        var iconMin = new Vector2(card.Min.X + pad, card.Min.Y + pad);
        FishingArt.ItemIcon(drawList, textures, entry.IconId, iconMin, icon, scale, ui.FieldSurface);
        var left = iconMin.X + icon + RowGap * scale;
        var right = card.Max.X - pad;
        var window = nextWindowCount > 0 ? nextWindows[0] : catalog.WindowAt(entryIndex);
        var open = entry.Fish.Rule.AlwaysOpen || window.IsOpen(nowUnix);
        var status = DetailStatus(entry, window, open, nowUnix);
        var range = DetailRange(entry, window, open);
        var statusHeight = Typography.LineHeight(TextStyles.Title3);
        var rangeHeight = Typography.LineHeight(TextStyles.Subheadline);
        var top = iconMin.Y + (icon - statusHeight - LineGap * scale - rangeHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(status, right - left, TextStyles.Title3),
            open ? ui.Accent : ui.TitleInk, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(left, top + statusHeight + LineGap * scale),
            Typography.FitText(range, right - left, TextStyles.Subheadline), ui.MutedInk, TextStyles.Subheadline);
        var badgeY = card.Max.Y - pad - FishingArt.CapsuleHeight(scale);
        var badgeX = card.Min.X + pad;
        var badgeFill = Palette.WithAlpha(ui.Accent, 0.18f);
        for (var index = 0; index < detailText.Badges.Length; index++)
        {
            badgeX += FishingArt.Capsule(drawList, new Vector2(badgeX, badgeY), detailText.Badges[index], badgeFill,
                ui.TitleInk, scale) + 6f * scale;
        }

        if (alerts.CaughtKnown && entry.LogRow != 0)
        {
            var caught = alerts.IsCaught(entry.ItemId);
            var label = caught ? Loc.T(L.Fishing.CaughtBadge) : Loc.T(L.Fishing.NotCaughtBadge);
            var fill = caught ? Palette.WithAlpha(Accent.Mint, 0.24f) : Palette.WithAlpha(ui.TitleInk, 0.10f);
            FishingArt.Capsule(drawList, new Vector2(badgeX, badgeY), label, fill, ui.TitleInk, scale);
        }

        ImGui.Dummy(new Vector2(width, card.Height + FishingArt.CardGap * scale));
    }

    private string DetailStatus(FishEntry entry, in FishWindow window, bool open, long nowUnix)
    {
        if (entry.Fish.Rule.AlwaysOpen)
        {
            return detailStatus.IsCurrent(long.MinValue)
                ? detailStatus.Value
                : detailStatus.Store(long.MinValue, Loc.T(L.Fishing.AnyTime));
        }

        if (!window.Exists)
        {
            return detailStatus.IsCurrent(long.MaxValue)
                ? detailStatus.Value
                : detailStatus.Store(long.MaxValue, Loc.T(L.Fishing.NoWindowSoon));
        }

        var remaining = open ? window.EndUnix - nowUnix : window.StartUnix - nowUnix;
        var key = open ? -1 - remaining : remaining + entry.ItemId * 100000L;
        if (detailStatus.IsCurrent(key))
        {
            return detailStatus.Value;
        }

        return detailStatus.Store(key, open
            ? Loc.T(L.Fishing.UpForCountdown, FishingClock.Countdown(remaining))
            : Loc.T(L.Fishing.OpensInCountdown, FishingClock.Countdown(remaining)));
    }

    private string DetailRange(FishEntry entry, in FishWindow window, bool open)
    {
        if (entry.Fish.Rule.AlwaysOpen || !window.Exists)
        {
            return detailText.Location;
        }

        var key = window.StartUnix * 2 + (open ? 1 : 0);
        if (detailRange.IsCurrent(key))
        {
            return detailRange.Value;
        }

        return detailRange.Store(key, Loc.T(L.Fishing.LocalRange, FishingText.LocalDayClock(window.StartUnix),
            FishingText.LocalClock(window.EndUnix)));
    }

    private void DrawWindowCard(FishEntry entry, long nowUnix, float scale)
    {
        ui.SectionLabel(Loc.T(L.Fishing.WindowSection), TextStyles.FootnoteEmphasized, 6f);
        var rule = entry.Fish.Rule;
        var drawList = ImGui.GetWindowDrawList();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = FishingArt.CardPadding * scale;
        var inner = width - pad * 2f;
        var headlineHeight = Typography.LineHeight(TextStyles.Headline);
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var weatherHeight = rule.Weather.Length > 0
            ? labelHeight + SectionInnerGap * scale + WeatherFlow(drawList, rule.Weather, Vector2.Zero, inner, scale,
                false) + RowGap * scale
            : 0f;
        var previousHeight = rule.PreviousWeather.Length > 0
            ? labelHeight + SectionInnerGap * scale + WeatherFlow(drawList, rule.PreviousWeather, Vector2.Zero, inner,
                scale, false) + RowGap * scale
            : 0f;
        var windowsHeight = nextWindowCount > 0
            ? labelHeight + nextWindowCount * WindowRowHeight * scale + RowGap * scale
            : 0f;
        var height = pad + headlineHeight + SectionInnerGap * scale + DetailBarHeight * scale + RowGap * scale * 1.5f +
                     weatherHeight + previousHeight + windowsHeight + pad * 0.5f;
        var origin = ImGui.GetCursorScreenPos();
        var card = new Rect(origin, origin + new Vector2(width, height));
        ui.Card(drawList, card.Min, card.Max, FishingArt.CardRadius * scale);
        var left = card.Min.X + pad;
        var right = card.Max.X - pad;
        var top = card.Min.Y + pad;
        Typography.Draw(drawList, new Vector2(left, top),
            Typography.FitText(detailText.EorzeaRange, inner, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        top += headlineHeight + SectionInnerGap * scale;
        var bar = new Rect(new Vector2(left, top), new Vector2(right, top + DetailBarHeight * scale));
        FishingArt.DayBar(drawList, bar, rule, EorzeaClock.MinuteOfDay(nowUnix), ui.TitleInk, ui.Accent, ui.TitleInk,
            scale);
        top += DetailBarHeight * scale + RowGap * scale * 1.5f;
        if (rule.Weather.Length > 0)
        {
            DrawLabel(drawList, Loc.T(L.Fishing.WeatherLabel), left, top);
            top += labelHeight + SectionInnerGap * scale;
            top += WeatherFlow(drawList, rule.Weather, new Vector2(left, top), inner, scale, true) + RowGap * scale;
        }

        if (rule.PreviousWeather.Length > 0)
        {
            DrawLabel(drawList, Loc.T(L.Fishing.PreviousWeatherLabel), left, top);
            top += labelHeight + SectionInnerGap * scale;
            top += WeatherFlow(drawList, rule.PreviousWeather, new Vector2(left, top), inner, scale, true) +
                   RowGap * scale;
        }

        if (nextWindowCount > 0)
        {
            DrawLabel(drawList, Loc.T(L.Fishing.NextWindowsLabel), left, top);
            top += labelHeight;
            for (var index = 0; index < nextWindowCount; index++)
            {
                var row = new Rect(new Vector2(left, top), new Vector2(right, top + WindowRowHeight * scale));
                DrawNextWindowRow(drawList, index, row, nowUnix, scale);
                top += WindowRowHeight * scale;
            }
        }

        ImGui.Dummy(new Vector2(width, height + FishingArt.CardGap * scale));
    }

    private void DrawLabel(ImDrawListPtr drawList, string text, float left, float top)
    {
        Typography.Draw(drawList, new Vector2(left, top), text, ui.Palette.HeaderInk,
            TextStyles.FootnoteEmphasized);
    }

    private float WeatherFlow(ImDrawListPtr drawList, byte[] weather, Vector2 origin, float width, float scale,
        bool draw)
    {
        var chipHeight = WeatherChipHeight * scale;
        var iconSize = WeatherIconSize * scale;
        var gap = WeatherChipGap * scale;
        var x = 0f;
        var y = 0f;
        for (var index = 0; index < weather.Length; index++)
        {
            var name = catalog.WeatherName(weather[index]);
            var chipWidth = iconSize + 6f * scale + Typography.Measure(name, TextStyles.Subheadline).X;
            if (x > 0f && x + chipWidth > width)
            {
                x = 0f;
                y += chipHeight + 4f * scale;
            }

            if (draw)
            {
                var chipMin = origin + new Vector2(x, y);
                var iconMin = new Vector2(chipMin.X, chipMin.Y + (chipHeight - iconSize) * 0.5f);
                GameIconTile.Draw(drawList, textures, catalog.WeatherIcon(weather[index]), iconMin,
                    iconMin + new Vector2(iconSize, iconSize), iconSize * 0.25f, scale);
                var textY = chipMin.Y + (chipHeight - Typography.LineHeight(TextStyles.Subheadline)) * 0.5f;
                Typography.Draw(drawList, new Vector2(iconMin.X + iconSize + 6f * scale, textY),
                    Typography.FitText(name, MathF.Max(1f, width - iconSize - 6f * scale), TextStyles.Subheadline),
                    ui.BodyInk, TextStyles.Subheadline);
            }

            x += chipWidth + gap;
        }

        return y + chipHeight;
    }

    private void DrawNextWindowRow(ImDrawListPtr drawList, int index, Rect row, long nowUnix, float scale)
    {
        var window = nextWindows[index];
        var line = nextWindowLines[index].IsCurrent(window.StartUnix)
            ? nextWindowLines[index].Value
            : nextWindowLines[index].Store(window.StartUnix, Loc.T(L.Fishing.LocalRange,
                FishingText.LocalDayClock(window.StartUnix), FishingText.LocalClock(window.EndUnix)));
        var open = window.IsOpen(nowUnix);
        var seconds = open ? window.EndUnix - nowUnix : window.StartUnix - nowUnix;
        var relativeKey = open ? -1 - seconds / 60 : seconds / 60 + 1;
        var relative = nextWindowRelative[index].IsCurrent(relativeKey)
            ? nextWindowRelative[index].Value
            : nextWindowRelative[index].Store(relativeKey,
                open ? Loc.T(L.Fishing.UpNowSection) : FishingText.Relative(seconds));
        var lineHeight = Typography.LineHeight(TextStyles.Body);
        var relativeWidth = Typography.Measure(relative, TextStyles.Subheadline).X;
        Typography.Draw(drawList, new Vector2(row.Max.X - relativeWidth,
                row.Center.Y - Typography.LineHeight(TextStyles.Subheadline) * 0.5f), relative,
            open ? ui.Accent : ui.MutedInk, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - lineHeight * 0.5f),
            Typography.FitText(line, MathF.Max(1f, row.Width - relativeWidth - RowGap * scale), TextStyles.Body),
            ui.TitleInk, TextStyles.Body);
        if (index > 0)
        {
            drawList.AddLine(row.Min, new Vector2(row.Max.X, row.Min.Y), ImGui.GetColorU32(ui.Hairline), 1f);
        }
    }

    private void DrawCatchCard(FishEntry entry, float scale)
    {
        var baitCount = detailText.BaitLines.Length;
        var hasText = detailText.Hook.Length > 0 || detailText.Intuition.Length > 0 || detailText.Extras.Length > 0;
        if (baitCount == 0 && !hasText)
        {
            return;
        }

        ui.SectionLabel(Loc.T(L.Fishing.CatchSection), TextStyles.FootnoteEmphasized, 6f);
        var drawList = ImGui.GetWindowDrawList();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = FishingArt.CardPadding * scale;
        var inner = width - pad * 2f;
        var textHeight = TextBlockHeight(detailText.Hook, inner) + TextBlockHeight(detailText.Intuition, inner) +
                         TextBlockHeight(detailText.Extras, inner);
        var height = pad + baitCount * BaitRowHeight * scale + textHeight + pad * 0.5f;
        var origin = ImGui.GetCursorScreenPos();
        var card = new Rect(origin, origin + new Vector2(width, height));
        ui.Card(drawList, card.Min, card.Max, FishingArt.CardRadius * scale);
        var left = card.Min.X + pad;
        var top = card.Min.Y + pad;
        var fish = entry.Fish;
        var icon = BaitIconSize * scale;
        for (var index = 0; index < baitCount; index++)
        {
            var centerY = top + BaitRowHeight * scale * 0.5f;
            FishingArt.ItemIcon(drawList, textures, catalog.ItemIcon(fish.BaitChain[index]),
                new Vector2(left, centerY - icon * 0.5f), icon, scale, ui.FieldSurface);
            var lineHeight = Typography.LineHeight(TextStyles.Body);
            Typography.Draw(drawList, new Vector2(left + icon + RowGap * scale, centerY - lineHeight * 0.5f),
                Typography.FitText(detailText.BaitLines[index], inner - icon - RowGap * scale, TextStyles.Body),
                ui.TitleInk, TextStyles.Body);
            top += BaitRowHeight * scale;
        }

        top += DrawTextBlock(detailText.Hook, left, top, inner, ui.BodyInk);
        top += DrawTextBlock(detailText.Intuition, left, top, inner, ui.BodyInk);
        DrawTextBlock(detailText.Extras, left, top, inner, ui.MutedInk);
        ImGui.Dummy(new Vector2(width, height + FishingArt.CardGap * scale));
    }

    private static float TextBlockHeight(string text, float width) =>
        text.Length == 0
            ? 0f
            : Typography.MeasureWrappedBlock(text, TextStyles.Subheadline, width).Y + SectionInnerGap * UiScale.Current;

    private static float DrawTextBlock(string text, float left, float top, float width, Vector4 color)
    {
        if (text.Length == 0)
        {
            return 0f;
        }

        return Typography.DrawWrappedLeft(new Vector2(left, top), text, color, TextStyles.Subheadline, width) +
               SectionInnerGap * UiScale.Current;
    }

    private void DrawLocationCard(FishEntry entry, float scale)
    {
        ui.SectionLabel(Loc.T(L.Fishing.LocationSection), TextStyles.FootnoteEmphasized, 6f);
        var drawList = ImGui.GetWindowDrawList();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = FishingArt.CardPadding * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var subHeight = Typography.LineHeight(TextStyles.Subheadline);
        var canMap = entry.MapId != 0;
        var height = pad + titleHeight + LineGap * scale + subHeight +
                     (detailText.Coordinates.Length > 0 ? LineGap * scale + subHeight : 0f) +
                     (canMap ? RowGap * scale + MapButtonHeight * scale : 0f) + pad;
        var origin = ImGui.GetCursorScreenPos();
        var card = new Rect(origin, origin + new Vector2(width, height));
        ui.Card(drawList, card.Min, card.Max, FishingArt.CardRadius * scale);
        var left = card.Min.X + pad;
        var inner = width - pad * 2f;
        var top = card.Min.Y + pad;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(entry.SpotName, inner, TextStyles.Headline),
            ui.TitleInk, TextStyles.Headline);
        top += titleHeight + LineGap * scale;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(entry.ZoneName, inner, TextStyles.Subheadline),
            ui.MutedInk, TextStyles.Subheadline);
        top += subHeight;
        if (detailText.Coordinates.Length > 0)
        {
            top += LineGap * scale;
            Typography.Draw(drawList, new Vector2(left, top), detailText.Coordinates, ui.MutedInk,
                TextStyles.Subheadline);
            top += subHeight;
        }

        if (canMap)
        {
            top += RowGap * scale;
            var buttonWidth = MathF.Min(inner, MathF.Max(MapButtonWidth * scale,
                AppSkin.PillWidthFor(Loc.T(L.Fishing.ShowOnMap), MapButtonHeight * scale)));
            var button = new Rect(new Vector2(left, top), new Vector2(left + buttonWidth, top + MapButtonHeight * scale));
            UiAnchors.Report("fishing.detail.map", button);
            if (ui.PillButton(button, Loc.T(L.Fishing.ShowOnMap), true, "fishing.detail.map"))
            {
                UiFeedback.Play(UiSound.Tap);
                OpenMap(entry);
            }
        }

        ImGui.SetCursorScreenPos(card.Min);
        ImGui.Dummy(new Vector2(width, height + FishingArt.CardGap * scale));
    }

    private static void OpenMap(FishEntry entry)
    {
        try
        {
            Plugin.GameGui.OpenMapWithMapLink(new MapLinkPayload(entry.TerritoryId, entry.MapId, entry.MapPosition.X,
                entry.MapPosition.Y));
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Fishing] open map failed");
        }
    }
}
