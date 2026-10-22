using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
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
    private const int HostingTitleLines = 3;
    private const float TimeTileSize = 48f;
    private const float StatusDotRadius = 4f;
    private const float StatusDotGap = 6f;

    private readonly PullToRefresh plansRefresh = new();
    private Action? syncNow;

    private void DrawPlans(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var nowUnix = NowUnix();
        syncNow ??= SyncPlans;
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (UiScope.PushId("muster.plans"))
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            plansRefresh.Draw(navBar.Body, surface.Pull, surface.Dragging, store.Syncing, ui.MutedInk, syncNow);
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var mine = store.Mine;
            var hosting = mine is not null && mine.EndsAtUnix > nowUnix;
            float bottom;
            if (!hosting && sections.Going.Count == 0)
            {
                bottom = DrawPlansEmpty(drawList, origin, width, scale);
            }
            else
            {
                var cursorY = origin.Y;
                if (hosting)
                {
                    cursorY = DrawHosting(drawList, mine!, origin.X, cursorY, width, nowUnix, scale);
                }

                bottom = DrawGoing(drawList, origin.X, cursorY, width, nowUnix, scale);
            }

            MusterArt.Reserve(origin, width, bottom + MusterArt.BottomPad * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "muster.plans.nav", Loc.T(L.Muster.TabPlans),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty);
    }

    private void SyncPlans()
    {
        UiFeedback.Play(UiSound.Refresh);
        store.SyncNow();
    }

    private float DrawPlansEmpty(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var centerX = origin.X + width * 0.5f;
        var bottom = MusterArt.StateScreen(drawList, ui, centerX, origin.Y + StateTop * scale, width,
            FontAwesomeIcon.CalendarCheck, Loc.T(L.Muster.PlansEmptyTitle), Loc.T(L.Muster.PlansEmptyHint), scale);
        if (MusterArt.StateAction(ui, centerX, bottom, width, Loc.T(L.Muster.BrowseMusters), scale, out bottom))
        {
            activeTab = MusterTab.Discover;
        }

        return bottom;
    }

    private float DrawHosting(ImDrawListPtr drawList, MusterDto mine, float left, float top, float width,
        long nowUnix, float scale)
    {
        var cursorY = top + CardSectionHeader.Draw(drawList, new Vector2(left, top), width, Loc.T(L.Muster.HostingSection), ui.TitleInk);
        var poster = new MusterPoster
        {
            Category = mine.Category,
            Eyebrow = Loc.T(MusterCategories.Label(mine.Category)),
            Title = mine.Description.Length > 0 ? mine.Description : Loc.T(MusterCategories.Label(mine.Category)),
            Status = labels.Countdown(mine, nowUnix),
            Live = mine.StartsAtUnix <= nowUnix,
            HostName = string.Empty,
            HostWorld = string.Empty,
            HostFrameId = string.Empty,
            Identity = string.Empty,
            Place = MusterText.Place(mine),
            Count = labels.Capacity(mine),
            Full = mine.MaxAttendees > 0 && mine.RsvpCount >= mine.MaxAttendees,
        };
        var height = MusterArt.PosterHeight(in poster, width, HostingTitleLines, scale);
        if (MusterArt.Poster(drawList, KeyFor("hosting", mine.Id), in poster, new Vector2(left, cursorY), width,
                HostingTitleLines, theme, images, lodestone, true, scale))
        {
            OpenManage();
        }

        return cursorY + height + MusterArt.SectionGap * scale;
    }

    private float DrawGoing(ImDrawListPtr drawList, float left, float top, float width, long nowUnix, float scale)
    {
        var going = sections.Going;
        if (going.Count == 0)
        {
            return top;
        }

        var cursorY = top + CardSectionHeader.Draw(drawList, new Vector2(left, top), width, Loc.T(L.Muster.StatGoing), ui.TitleInk);
        var rowHeight = MusterArt.RowHeight * scale;
        var max = new Vector2(left + width, cursorY + going.Count * rowHeight);
        ui.Card(drawList, new Vector2(left, cursorY), max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        for (var index = 0; index < going.Count; index++)
        {
            var muster = going[index];
            var rowTop = cursorY + index * rowHeight;
            var row = new Rect(new Vector2(left, rowTop), new Vector2(max.X, rowTop + rowHeight));
            var textLeft = left + pad + (TimeTileSize + MusterArt.TextGap) * scale;
            if (index > 0)
            {
                FeedCell.Hairline(drawList, textLeft, max.X, rowTop, ui.Hairline);
            }

            if (!ImGui.IsRectVisible(row.Min, row.Max))
            {
                continue;
            }

            var hovered = MusterArt.RowWash(drawList, ui, row, scale);
            DrawTimeTile(drawList, muster, new Vector2(left + pad, row.Center.Y - TimeTileSize * 0.5f * scale),
                nowUnix, scale);
            var trailingLeft = DrawMyStatus(drawList, max.X - pad, row.Center.Y, store.MyStatus(muster.Id), scale);
            var title = muster.Description.Length > 0
                ? muster.Description
                : Loc.T(MusterCategories.Label(muster.Category));
            MusterArt.Labels(drawList, textLeft, trailingLeft - Metrics.Space.Md * scale, row.Center.Y, title,
                MusterText.Identity(muster), ui.TitleInk, ui.MutedInk, scale);
            if (UiInteract.Click(row.Min, row.Max, hovered))
            {
                UiFeedback.Play(UiSound.Tap);
                OpenDetail(muster.Id);
            }
        }

        return max.Y;
    }

    private void DrawTimeTile(ImDrawListPtr drawList, MusterDto muster, Vector2 min, long nowUnix, float scale)
    {
        var size = TimeTileSize * scale;
        var max = min + new Vector2(size, size);
        IconTile.FillShaded(drawList, min, max, size * Metrics.Radius.TileFactor, MusterArt.Surface(muster.Category));
        var center = (min + max) * 0.5f;
        var live = muster.StartsAtUnix <= nowUnix;
        if (live)
        {
            MusterArt.LiveDot(drawList, new Vector2(center.X, center.Y - 8f * scale), scale);
            Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + 9f * scale), Loc.T(L.Common.Live),
                AccentRing.Ink, TextStyles.Caption2);
            return;
        }

        var clock = Typography.FitText(labels.StartClock(muster), size - Metrics.Space.Xs * scale,
            TextStyles.SubheadlineEmphasized);
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y - 6f * scale), clock, AccentRing.Ink,
            TextStyles.SubheadlineEmphasized);
        var countdown = Typography.FitText(labels.Countdown(muster, nowUnix), size - Metrics.Space.Xs * scale,
            TextStyles.Caption2);
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + 10f * scale), countdown,
            AccentRing.Ink with { W = 0.8f }, TextStyles.Caption2);
    }

    private static float DrawMyStatus(ImDrawListPtr drawList, float right, float centerY, int status, float scale)
    {
        var label = StatusLabel(status);
        var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var color = MusterArt.StatusColor(status);
        Typography.Draw(drawList, new Vector2(right - size.X, centerY - size.Y * 0.5f), label, color,
            TextStyles.FootnoteEmphasized);
        var dotCenter = new Vector2(right - size.X - (StatusDotGap + StatusDotRadius) * scale, centerY);
        drawList.AddCircleFilled(dotCenter, StatusDotRadius * scale, ImGui.GetColorU32(color), 16);
        return dotCenter.X - StatusDotRadius * scale;
    }

    private static string StatusLabel(int status) =>
        status switch
        {
            MusterStatuses.RunningLate => Loc.T(L.Muster.StatusRunningLate),
            MusterStatuses.Here => Loc.T(L.Muster.StatusHere),
            MusterStatuses.WhereExactly => Loc.T(L.Muster.StatusWhereExactly),
            _ => Loc.T(L.Muster.OnMyWay),
        };
}
