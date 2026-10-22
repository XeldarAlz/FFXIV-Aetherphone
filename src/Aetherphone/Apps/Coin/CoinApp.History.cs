using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp
{
    private const float FilterHeight = 32f;
    private const float DayCaptionGap = 6f;

    private void DrawHistory(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (UiScope.PushId("coin.history"))
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            historyRefresh.Draw(navBar.Body, surface.Pull, surface.Dragging, !store.LoadedOnce, ui.MutedInk,
                store.RefreshNow);
            DrawHistoryBody(navBar.Body);
        }

        AppHeader.EndLargeTitle(in navBar, context, "coin.history.nav", TabTitle(CoinTab.History),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty);
    }

    private void DrawHistoryBody(Rect body)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var filterRow = new Rect(origin, new Vector2(origin.X + width, origin.Y + FilterHeight * scale));
        filterOptions[CoinLedgerText.FilterAll] = Loc.T(L.Coin.FilterAll);
        filterOptions[CoinLedgerText.FilterEarned] = Loc.T(L.Coin.FilterEarned);
        filterOptions[CoinLedgerText.FilterSpent] = Loc.T(L.Coin.FilterSpent);
        historyFilter = SegmentStrip.Draw("coin.historyFilter", filterRow, filterOptions, historyFilter, ui.Palette);
        var cursorY = filterRow.Max.Y + Metrics.Space.Sm * scale;
        if (!store.LoadedOnce)
        {
            LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            CoinArt.Reserve(origin, width, cursorY);
            return;
        }

        ledgerText.Sync(store.Entries);
        if (ledgerText.Count(historyFilter) == 0)
        {
            var empty = new Rect(new Vector2(body.Min.X, cursorY), body.Max);
            CoinArt.StateScreen(drawList, ui, empty, FontAwesomeIcon.Receipt, Loc.T(L.Coin.HistoryEmptyTitle),
                Loc.T(L.Coin.HistoryEmptyHint), string.Empty, 0u, scale);
            CoinArt.Reserve(origin, width, cursorY);
            return;
        }

        cursorY = DrawLedgerDays(drawList, origin.X, cursorY, width, scale);
        CoinArt.Reserve(origin, width, cursorY);
        if (store.LoadingMore)
        {
            InfiniteScroll.DrawLoadingRow(origin.X + width * 0.5f, ui.MutedInk);
        }
        else if (!store.EndReached && InfiniteScroll.ReachedBottom())
        {
            store.LoadMore();
        }

        ImGui.Dummy(new Vector2(0f, CoinArt.BottomPad * scale));
    }

    private float DrawLedgerDays(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var entries = ledgerText.Entries;
        var rowHeight = CoinArt.RowHeight * scale;
        var captionHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var cursorY = top;
        var index = 0;
        while (index < entries.Length)
        {
            if (!CoinLedgerText.Matches(entries[index], historyFilter))
            {
                index++;
                continue;
            }

            var dayStart = index;
            var dayEnd = index;
            var count = 0;
            while (dayEnd < entries.Length &&
                   TimeText.SameLocalDay(entries[dayStart].CreatedAtUnix, entries[dayEnd].CreatedAtUnix))
            {
                if (CoinLedgerText.Matches(entries[dayEnd], historyFilter))
                {
                    count++;
                }

                dayEnd++;
            }

            cursorY += Metrics.Space.Lg * scale;
            Typography.Draw(drawList, new Vector2(left, cursorY), Loc.Upper(ledgerText.Day(dayStart)), ui.MutedInk,
                TextStyles.FootnoteEmphasized);
            cursorY += captionHeight + DayCaptionGap * scale;
            var min = new Vector2(left, cursorY);
            var max = new Vector2(left + width, cursorY + count * rowHeight);
            if (ImGui.IsRectVisible(min, max))
            {
                CoinArt.Card(drawList, ui, min, max, scale);
                var drawn = 0;
                for (var entryIndex = dayStart; entryIndex < dayEnd; entryIndex++)
                {
                    if (!CoinLedgerText.Matches(entries[entryIndex], historyFilter))
                    {
                        continue;
                    }

                    var rowTop = cursorY + drawn * rowHeight;
                    var row = new Rect(new Vector2(left, rowTop), new Vector2(left + width, rowTop + rowHeight));
                    if (drawn > 0)
                    {
                        DrawRowHairline(drawList, row, scale);
                    }

                    drawn++;
                    if (ImGui.IsRectVisible(row.Min, row.Max) && DrawLedgerRow(drawList, row, entryIndex, scale))
                    {
                        OpenEntry(entries[entryIndex].Id);
                    }
                }
            }

            cursorY = max.Y;
            index = dayEnd;
        }

        return cursorY;
    }

    private bool DrawLedgerRow(ImDrawListPtr drawList, Rect row, int index, float scale)
    {
        var entry = ledgerText.Entries[index];
        var hovered = CoinArt.RowInteraction(drawList, ui, row, scale);
        var pad = Metrics.Space.Lg * scale;
        var tile = CoinArt.RowTile * scale;
        var appId = entry.App.Length == 0 ? Id : entry.App;
        var tileCenter = new Vector2(row.Min.X + pad + tile * 0.5f, row.Center.Y);
        IconTile.DrawApp(drawList, appId, tileCenter, tile, IconTile.Surface(AppAccents.For(appId)));
        var valueInk = entry.Amount > 0 ? CoinArt.GainInk : ui.TitleInk;
        var valueWidth = CoinArt.Value(drawList, row.Max.X - pad, row.Center.Y, ledgerText.Amount(index), valueInk);
        var textLeft = tileCenter.X + tile * 0.5f + CoinArt.TextGap * scale;
        var textRight = row.Max.X - pad - valueWidth - CoinArt.ValueGap * scale;
        CoinArt.Labels(drawList, textLeft, textRight, row.Center.Y, ledgerText.Title(index),
            ledgerText.Subtitle(index), ui.TitleInk, ui.MutedInk, scale);
        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private void OpenEntry(string entryId)
    {
        UiFeedback.Play(UiSound.Tap);
        router.Push(CoinRoute.Entry(entryId));
    }
}
