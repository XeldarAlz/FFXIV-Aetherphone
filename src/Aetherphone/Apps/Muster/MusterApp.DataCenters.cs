using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Muster;

internal sealed partial class MusterApp
{
    private const float DataCenterRowHeight = 50f;
    private const float DataCenterGlyph = 15f;
    private const float RegionHeaderHeight = 36f;

    private void DrawDataCenters(in PhoneContext context, MusterRoute route)
    {
        var scale = UiScale.Current;
        var navBar = AppHeader.BeginLargeTitle(context);
        using (UiScope.PushId("muster.datacenters"))
        using (AppSurface.Begin(navBar.Body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var pad = Metrics.Space.Lg * scale;
            var hintHeight = Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y),
                Loc.T(L.Muster.DataCenterHint), ui.MutedInk, TextStyles.Footnote, width - pad * 2f);
            var pinned = configuration.MusterDataCenterId;
            var cursorY = origin.Y + hintHeight + Metrics.Space.Md * scale;
            var homeRow = new Rect(new Vector2(origin.X, cursorY),
                new Vector2(origin.X + width, cursorY + DataCenterRowHeight * scale));
            ui.Card(drawList, homeRow.Min, homeRow.Max, Metrics.Radius.Grouped * scale);
            if (DrawDataCenterRow(drawList, homeRow, Loc.T(L.Muster.MyDataCenter),
                    MusterDataCenters.Name(store.CurrentDataCenterId), pinned == 0, FontAwesomeIcon.Home, scale))
            {
                PinDataCenter(0);
            }

            cursorY = DrawRegionGroups(drawList, origin.X, homeRow.Max.Y, width, pinned, scale);
            MusterArt.Reserve(origin, width, cursorY + MusterArt.BottomPad * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "muster.datacenters.nav", Loc.T(L.Muster.DataCenterSection),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty, route.BackTitle, back);
    }

    private float DrawRegionGroups(ImDrawListPtr drawList, float left, float top, float width, int pinned,
        float scale)
    {
        var all = MusterDataCenters.All;
        var rowHeight = DataCenterRowHeight * scale;
        var cursorY = top;
        var start = 0;
        while (start < all.Length)
        {
            var region = all[start].RegionBit;
            var end = start;
            while (end < all.Length && all[end].RegionBit == region)
            {
                end++;
            }

            var headerTop = cursorY + MusterArt.CardGap * scale;
            var label = Loc.T(MusterCategories.RegionLabel(region));
            var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList,
                new Vector2(left + Metrics.Space.Lg * scale,
                    headerTop + RegionHeaderHeight * scale - labelHeight - Metrics.Space.Xs * scale), label,
                ui.MutedInk, TextStyles.FootnoteEmphasized);
            var cardTop = headerTop + RegionHeaderHeight * scale;
            var cardMax = new Vector2(left + width, cardTop + (end - start) * rowHeight);
            ui.Card(drawList, new Vector2(left, cardTop), cardMax, Metrics.Radius.Grouped * scale);
            for (var index = start; index < end; index++)
            {
                var rowTop = cardTop + (index - start) * rowHeight;
                if (index > start)
                {
                    FeedCell.Hairline(drawList, left + Metrics.Space.Lg * scale, cardMax.X, rowTop, ui.Hairline);
                }

                var row = new Rect(new Vector2(left, rowTop), new Vector2(cardMax.X, rowTop + rowHeight));
                var dataCenter = all[index];
                if (DrawDataCenterRow(drawList, row, dataCenter.Name, string.Empty, pinned == dataCenter.Id,
                        FontAwesomeIcon.Server, scale))
                {
                    PinDataCenter(dataCenter.Id);
                }
            }

            cursorY = cardMax.Y;
            start = end;
        }

        return cursorY;
    }

    private bool DrawDataCenterRow(ImDrawListPtr drawList, Rect row, string label, string detail, bool selected,
        FontAwesomeIcon icon, float scale)
    {
        var hovered = MusterArt.RowWash(drawList, ui, row, scale);
        var pad = Metrics.Space.Lg * scale;
        var glyph = DataCenterGlyph * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(row.Min.X + pad + glyph * 0.5f, row.Center.Y), icon,
            selected ? ui.Accent : ui.MutedInk, glyph);
        var textLeft = row.Min.X + pad + glyph + MusterArt.TextGap * scale;
        var trailing = selected ? glyph + Metrics.Space.Md * scale : 0f;
        MusterArt.Labels(drawList, textLeft, row.Max.X - pad - trailing, row.Center.Y, label, detail, ui.TitleInk,
            ui.MutedInk, scale);
        if (selected)
        {
            ProgressRing.CenterIcon(drawList, new Vector2(row.Max.X - pad - glyph * 0.5f, row.Center.Y),
                FontAwesomeIcon.Check, ui.Accent, glyph);
        }

        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private void PinDataCenter(int dataCenterId)
    {
        UiFeedback.Play(UiSound.Tap);
        if (configuration.MusterDataCenterId != dataCenterId)
        {
            configuration.MusterDataCenterId = dataCenterId;
            configuration.MusterScope = MusterScopes.MyDataCenter;
            configuration.Save();
            store.RefreshDirectory();
        }

        router.Pop();
    }
}
