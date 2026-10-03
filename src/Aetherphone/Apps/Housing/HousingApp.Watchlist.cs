using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Housing;

internal sealed partial class HousingApp
{
    private const float WatchCardPad = 16f;
    private const float WatchCardGap = 12f;
    private const float WatchLineGap = 10f;
    private const float WatchActionRadius = 16f;
    private const float WatchPillHeight = 30f;
    private const float WatchPillPad = 12f;
    private const float WatchPillGlyph = 14f;

    private readonly NavBarButton[] watchButtons = new NavBarButton[1];
    private CachedText[] watchCardCountdowns = Array.Empty<CachedText>();
    private CachedText[] watchCardAges = Array.Empty<CachedText>();
    private CachedText[] watchCardTitles = Array.Empty<CachedText>();
    private CachedText[] watchCardPlaces = Array.Empty<CachedText>();
    private CachedText[] watchCardFacts = Array.Empty<CachedText>();
    private CachedText[] watchCardReminders = Array.Empty<CachedText>();

    private void DrawWatchlistTab(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        var scale = UiScale.Current;
        var watched = housing.Watch.Watched;
        if (watched.Count == 0)
        {
            HousingArt.StateScreen(ImGui.GetWindowDrawList(), ui, navBar.Body, FontAwesomeIcon.Bookmark,
                Loc.T(L.Housing.WatchlistEmpty), Loc.T(L.Housing.WatchlistEmptyHint), string.Empty, scale);
        }
        else
        {
            EnsureWatchCaches(watched.Count);
            using (AppSurface.Begin(navBar.Body))
            {
                var drawList = ImGui.GetWindowDrawList();
                var origin = ImGui.GetCursorScreenPos();
                var width = ScrollLayout.StableContentWidth();
                var cursorY = origin.Y;
                var now = DateTime.UtcNow;
                for (var index = 0; index < watched.Count; index++)
                {
                    if (index > 0)
                    {
                        cursorY += WatchCardGap * scale;
                    }

                    cursorY = DrawWatchCard(drawList, new Vector2(origin.X, cursorY), width, watched[index], index,
                        now, scale);
                }

                cursorY += HousingArt.SectionGap * scale;
                cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), Loc.T(L.Housing.EntriesCaveat),
                    ui.MutedInk, TextStyles.Footnote, width);
                ReserveTo(origin, width, cursorY + BottomPad * scale);
            }
        }

        var count = watched.Count > 0 ? 1 : 0;
        if (count > 0)
        {
            watchButtons[0] = new NavBarButton(PhoneIcons.Trash, Loc.T(L.Housing.ClearWatchlist));
        }

        var pressed = AppHeader.EndLargeTitle(in navBar, context, "housing.nav.watchlist", Loc.T(L.Housing.Watchlist),
            NavBarStyle.From(ui), new ReadOnlySpan<NavBarButton>(watchButtons, 0, count));
        if (pressed == 0)
        {
            AskClearWatchlist();
        }
    }

    private void EnsureWatchCaches(int count)
    {
        if (watchCardTitles.Length >= count)
        {
            return;
        }

        var size = Math.Max(count, watchCardTitles.Length * 2);
        Array.Resize(ref watchCardCountdowns, size);
        Array.Resize(ref watchCardAges, size);
        Array.Resize(ref watchCardTitles, size);
        Array.Resize(ref watchCardPlaces, size);
        Array.Resize(ref watchCardFacts, size);
        Array.Resize(ref watchCardReminders, size);
    }

    private float WatchCardHeight(float scale) =>
        WatchCardPad * 2f * scale + HousingArt.TileSize * scale + WatchLineGap * scale +
        Typography.LineHeight(TextStyles.SubheadlineEmphasized) + HousingArt.LineGap * 3f * scale +
        HousingArt.BarHeight * scale + WatchLineGap * scale + WatchPillHeight * scale;

    private float DrawWatchCard(ImDrawListPtr drawList, Vector2 origin, float width, HousingWatchRecord record,
        int index, DateTime now, float scale)
    {
        var height = WatchCardHeight(scale);
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        if (!ImGui.IsRectVisible(min, max))
        {
            return max.Y;
        }

        if (index == 0)
        {
            UiAnchors.Report("housing.watch.first", new Rect(min, max));
        }

        var pad = WatchCardPad * scale;
        var left = min.X + pad;
        var right = max.X - pad;
        var tile = HousingArt.TileSize * scale;
        var unwatchCenter = new Vector2(right - WatchActionRadius * scale, min.Y + pad + tile * 0.5f);
        var unwatchHit = new Vector2(WatchActionRadius * scale, WatchActionRadius * scale);
        var live = record.StillReported && record.WorldId == housing.WorldId ? FindPlot(record.Key) : null;
        var reminder = housing.Watch.FindReminder(record.Key);
        var hasReminder = reminder is { Notified: false };
        var pillRect = WatchPillRect(record, reminder, left, max.Y - pad, scale, index);
        var overChild = UiInteract.Hover(unwatchCenter - unwatchHit, unwatchCenter + unwatchHit) ||
                        live?.PhaseEndsUtc is not null && UiInteract.Hover(pillRect.Min, pillRect.Max);
        var hovered = !overChild && UiInteract.Hover(min, max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(unchecked(ImGui.GetID("housing.watch.card") + (uint)index), down,
            Motion.PressScaleCard);
        var center = (min + max) * 0.5f;
        var half = (max - min) * 0.5f * press;
        HousingArt.Card(drawList, ui, center - half, center + half, scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var tileCenter = new Vector2(left + tile * 0.5f, min.Y + pad + tile * 0.5f);
        HousingArt.DistrictTile(drawList, tileCenter, tile, record.DistrictId);
        var key = (long)record.Key.GetHashCode();
        var title = watchCardTitles[index].IsCurrent(key)
            ? watchCardTitles[index].Value
            : watchCardTitles[index].Store(key, Loc.T(L.Housing.PlotAndWard, record.Plot, record.Ward));
        var worldName = record.WorldName.Length > 0 ? record.WorldName : housing.WorldNameOf(record.WorldId);
        var place = watchCardPlaces[index].IsCurrent(key)
            ? watchCardPlaces[index].Value
            : watchCardPlaces[index].Store(key, string.Concat(HousingDistricts.DisplayName(record.DistrictId), " · ",
                worldName, " · ", HousingFormat.SizeLabel((HousingPlotSize)record.Size)));
        HousingArt.Labels(drawList, tileCenter.X + tile * 0.5f + HousingArt.TextGap * scale,
            unwatchCenter.X - WatchActionRadius * scale - HousingArt.TextGap * scale, tileCenter.Y, title, place,
            ui.TitleInk, ui.MutedInk, scale);
        if (HousingArt.CircleAction(drawList, unchecked(ImGui.GetID("housing.watch.remove") + (uint)index), unwatchCenter,
                WatchActionRadius * scale, PhoneIcons.BookmarkFilled, ui, Loc.T(L.Housing.Unwatch), true))
        {
            housing.Watch.CancelReminder(record.Key);
            housing.Watch.Unwatch(record.Key);
            UiFeedback.Play(UiSound.ToggleOff);
            InvalidateCache();
            return max.Y;
        }

        var y = min.Y + pad + tile + WatchLineGap * scale;
        var phase = (HousingLotteryPhase)record.Phase;
        var phaseEnd = OptionalUnix(record.PhaseEndUnix);
        var hue = record.StillReported ? HousingMarkers.PhaseColor(phase, ui.Accent) : AppPalettes.HousingResults;
        var phaseLabel = record.StillReported
            ? HousingFormat.PhaseLabel(phase)
            : NoLongerReportedText(ref watchCardAges[index], record, now);
        var countdown = record.StillReported
            ? HousingText.Countdown(ref watchCardCountdowns[index], phaseEnd, now)
            : string.Empty;
        var countdownWidth = countdown.Length > 0
            ? WidgetText.TabularWidth(countdown, TextStyles.SubheadlineEmphasized)
            : 0f;
        if (countdown.Length > 0)
        {
            WidgetText.Tabular(drawList, new Vector2(right - countdownWidth, y), countdown, ui.TitleInk,
                TextStyles.SubheadlineEmphasized);
        }

        Typography.Draw(drawList, new Vector2(left, y),
            Typography.FitText(phaseLabel, MathF.Max(1f, right - left - countdownWidth - HousingArt.TextGap * scale),
                TextStyles.SubheadlineEmphasized), hue, TextStyles.SubheadlineEmphasized);
        y += Typography.LineHeight(TextStyles.SubheadlineEmphasized) + HousingArt.LineGap * 3f * scale;
        var progress = record.StillReported ? HousingLottery.Progress(phase, phaseEnd, now) : 0f;
        HousingArt.Bar(drawList, new Vector2(left, y), new Vector2(right, y + HousingArt.BarHeight * scale),
            MathF.Max(0f, progress), hue);

        var factKey = key ^ ((long)(record.Entries ?? -1) << 33);
        var facts = watchCardFacts[index].IsCurrent(factKey)
            ? watchCardFacts[index].Value
            : watchCardFacts[index].Store(factKey, record.Entries is { } entries
                ? Loc.Plural(L.Housing.EntriesCount, entries)
                : Loc.T(L.Housing.EntriesUnknown));
        var factHeight = Typography.LineHeight(TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(left, pillRect.Center.Y - factHeight * 0.5f),
            Typography.FitText(facts, MathF.Max(1f, pillRect.Min.X - left - HousingArt.TextGap * scale),
                TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        DrawWatchPill(drawList, pillRect, live, hasReminder, index, scale);
        if (UiInteract.Click(min, max, hovered))
        {
            OpenWatched(record);
        }

        return max.Y;
    }

    private Rect WatchPillRect(HousingWatchRecord record, HousingReminderRecord? reminder, float left, float bottom,
        float scale, int index)
    {
        var label = WatchPillLabel(record, reminder, index);
        var width = Typography.Measure(label, TextStyles.Footnote).X + WatchPillPad * 2f * scale +
                    WatchPillGlyph * scale + Metrics.Space.Xs * scale;
        var right = left + ScrollLayout.StableContentWidth() - WatchCardPad * 2f * scale;
        return new Rect(new Vector2(right - width, bottom - WatchPillHeight * scale), new Vector2(right, bottom));
    }

    private string WatchPillLabel(HousingWatchRecord record, HousingReminderRecord? reminder, int index)
    {
        var minutes = reminder is { Notified: false } ? reminder.OffsetMinutes : 0;
        var key = (long)record.Key.GetHashCode() ^ ((long)minutes << 40);
        if (watchCardReminders[index].IsCurrent(key))
        {
            return watchCardReminders[index].Value;
        }

        return watchCardReminders[index].Store(key, minutes > 0
            ? Loc.T(L.Housing.ReminderLeadShort, HousingFormat.LeadTime(minutes))
            : Loc.T(L.Housing.RemindMe));
    }

    private void DrawWatchPill(ImDrawListPtr drawList, Rect rect, HousingPlot? live, bool active, int index,
        float scale)
    {
        var enabled = live is not null && live.PhaseEndsUtc is not null;
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        var fill = active ? Palette.WithAlpha(ui.Accent, 0.22f) : ui.FieldSurface;
        if (hovered)
        {
            fill = Palette.Mix(fill, ui.TitleInk, 0.08f);
        }

        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        var ink = !enabled ? ui.MutedInk : active ? Palette.Lighten(ui.Accent, 0.25f) : ui.TitleInk;
        var glyphCenter = new Vector2(rect.Min.X + WatchPillPad * scale + WatchPillGlyph * scale * 0.5f,
            rect.Center.Y);
        PhoneIcon.Draw(drawList, glyphCenter, active ? PhoneIcons.BellFilled : PhoneIcons.Bell, ink,
            WatchPillGlyph * scale);
        var label = watchCardReminders[index].Value;
        var labelLeft = glyphCenter.X + WatchPillGlyph * scale * 0.5f + Metrics.Space.Xs * scale;
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(labelLeft, rect.Center.Y - labelHeight * 0.5f), label, ink,
            TextStyles.Footnote);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (enabled && UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            OpenReminderSheet(live!.Key);
        }
    }

    private void AskClearWatchlist()
    {
        var count = housing.Watch.Watched.Count;
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Housing.Watchlist),
            Message = Loc.T(L.Housing.ClearWatchlistConfirm, count),
            ConfirmLabel = Loc.T(L.Housing.ClearWatchlist),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Confirm = () =>
            {
                var watched = housing.Watch.Watched;
                for (var index = watched.Count - 1; index >= 0; index--)
                {
                    housing.Watch.CancelReminder(watched[index].Key);
                }

                housing.Watch.ClearWatched();
                UiFeedback.Play(UiSound.ToggleOff);
                InvalidateCache();
            },
        });
    }
}
