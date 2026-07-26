using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Muster;

internal sealed partial class MusterApp
{
    private const float PinnedCardHeight = 68f;
    private const float ControlRowHeight = 42f;
    private const float GoingRowHeight = 54f;
    private const float ChipHeight = 32f;
    private const int SectionFallbackRebuildSeconds = 30;

    private readonly List<MusterDto> goingSection = new();
    private readonly List<MusterDto> friendSection = new();
    private readonly List<MusterDto> liveSection = new();
    private readonly List<MusterDto> soonSection = new();
    private readonly string[] chipLabels = new string[16];
    private readonly bool[] chipActive = new bool[16];
    private readonly string[] scopeLabels = new string[3];
    private readonly ChipRail categoryRail = new();
    private readonly PullToRefresh directoryRefresh = new();
    private MusterDto[] lastContacts = Array.Empty<MusterDto>();
    private MusterDto[] lastDirectory = Array.Empty<MusterDto>();
    private MusterDto[] lastGoing = Array.Empty<MusterDto>();
    private MusterDto? lastMine;
    private long nextSectionRebuildUnix;

    private void DrawDirectory(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var nowUnix = NowUnix();
        var currentDataCenterId = store.CurrentDataCenterId;
        DrawDirectoryHeader(area, scale);
        var controlsTop = area.Min.Y + AppHeader.Height * scale;
        DrawScopeRow(area, controlsTop, scale);
        var body = new Rect(new Vector2(area.Min.X, controlsTop + ControlRowHeight * scale), area.Max);
        EnsureSections(nowUnix);
        using (var surface = AppSurface.Begin(body))
        {
            directoryRefresh.Draw(body, surface.Pull, surface.Dragging, store.Syncing || store.DirectoryLoading,
                AppPalettes.Muster.MutedInk, RefreshEverything);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            DrawCategoryRail(scale);
            if (store.Mine is { } mine)
            {
                DrawMinePinned(mine, nowUnix, scale);
            }

            var anyCards = goingSection.Count > 0 || friendSection.Count > 0 || liveSection.Count > 0
                || soonSection.Count > 0;
            if (!anyCards)
            {
                DrawDirectoryEmpty(body, scale);
            }
            else
            {
                if (goingSection.Count > 0)
                {
                    ui.SectionHeading(Loc.T(L.Muster.GoingSection), 6f);
                    DrawGoingRows(nowUnix, scale);
                }

                if (friendSection.Count > 0)
                {
                    ui.SectionHeading(Loc.T(L.Muster.FriendsSection), 6f);
                    DrawCards(friendSection, nowUnix, currentDataCenterId, scale);
                }

                if (liveSection.Count > 0)
                {
                    ui.SectionHeading(Loc.T(L.Muster.HappeningNow), 6f);
                    DrawCards(liveSection, nowUnix, currentDataCenterId, scale);
                }

                if (soonSection.Count > 0)
                {
                    ui.SectionHeading(Loc.T(L.Muster.StartingSoon), 6f);
                    DrawCards(soonSection, nowUnix, currentDataCenterId, scale);
                }

                DrawLoadMore(scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }

        if (store.Mine is null && ComposeFab.Draw(body, "##musterStartFab", ui.Accent,
                FontAwesomeIcon.Bullhorn.ToIconString(), Loc.T(L.Muster.StartMuster)))
        {
            router.Push(MusterRoute.Create);
        }
    }

    private void DrawDirectoryHeader(Rect area, float scale)
    {
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(area.Center.X, rowCenterY), DisplayName, AppPalettes.Muster.TitleInk,
            1.3f, FontWeight.Bold);
        var actionCenter = new Vector2(area.Max.X - 22f * scale, rowCenterY);
        if (store.Syncing || store.DirectoryLoading)
        {
            LoadingPulse.Spinner(actionCenter, 8f * scale, ui.Accent);
        }
        else if (ui.IconButton(actionCenter, 14f * scale, FontAwesomeIcon.Sync.ToIconString(),
                     AppPalettes.Muster.BodyInk, AppSkin.Transparent, 0.9f, Loc.T(L.Common.Refresh),
                     HoverLabelSide.Below))
        {
            RefreshEverything();
        }

        var rulesCenter = new Vector2(actionCenter.X - 28f * scale, rowCenterY);
        if (ui.IconButton(rulesCenter, 14f * scale, FontAwesomeIcon.QuestionCircle.ToIconString(),
                AppPalettes.Muster.MutedInk, AppSkin.Transparent, 0.9f, Loc.T(L.Conduct.Eyebrow),
                HoverLabelSide.Below))
        {
            conduct.ShowRules(Id);
        }
    }

    private void RefreshEverything()
    {
        store.SyncNow();
        store.RefreshDirectory();
    }

    private void DrawScopeRow(Rect area, float top, float scale)
    {
        var inset = 16f * scale;
        var row = new Rect(new Vector2(area.Min.X + inset, top),
            new Vector2(area.Max.X - inset, top + ControlRowHeight * scale));
        var pillSide = 32f * scale;
        var gap = Metrics.Space.Sm * scale;
        var pinned = configuration.MusterDataCenterId != 0
            ? MusterDataCenters.Name(configuration.MusterDataCenterId)
            : string.Empty;
        scopeLabels[0] = pinned.Length > 0 ? pinned : Loc.T(L.Muster.ScopeMyDc);
        scopeLabels[1] = Loc.T(L.Muster.ScopeRegion);
        scopeLabels[2] = Loc.T(L.Muster.ScopeEverywhere);
        var stripRect = new Rect(row.Min, new Vector2(row.Max.X - pillSide - gap, row.Max.Y));
        var selected = Math.Clamp(configuration.MusterScope, MusterScopes.MyDataCenter, MusterScopes.Everywhere);
        var next = SegmentStrip.Draw("##musterScope", stripRect, scopeLabels, selected, AppPalettes.Muster);
        if (next != selected)
        {
            configuration.MusterScope = next;
            configuration.Save();
            store.RefreshDirectory();
        }

        DrawDataCenterPill(new Vector2(row.Max.X - pillSide * 0.5f, row.Center.Y), pillSide * 0.5f, pinned.Length > 0);
    }

    private void DrawDataCenterPill(Vector2 center, float radius, bool pinned)
    {
        var drawList = ImGui.GetWindowDrawList();
        var corner = new Vector2(radius, radius);
        var hovered = UiInteract.Hover(center - corner, center + corner);
        var fill = pinned
            ? Palette.WithAlpha(ui.Accent, hovered ? 0.36f : 0.26f)
            : hovered ? ui.HoverTint : AppPalettes.Muster.FieldSurface;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(fill), 32);
        AppSkin.Icon(drawList, center, FontAwesomeIcon.GlobeAmericas.ToIconString(),
            pinned ? ui.Accent : AppPalettes.Muster.BodyInk, 0.68f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(center - corner, center + corner), Loc.T(L.Muster.DataCenterSection),
            HoverLabelSide.Above);
        if (UiInteract.Click(center - corner, center + corner, hovered))
        {
            router.Push(MusterRoute.DataCenter);
        }
    }

    private void DrawCategoryRail(float scale)
    {
        var categories = MusterCategories.All;
        var mask = configuration.MusterCategoryFilter;
        chipLabels[0] = Loc.T(L.Muster.FilterAll);
        chipActive[0] = mask == 0;
        for (var index = 0; index < categories.Length; index++)
        {
            chipLabels[index + 1] = Loc.T(MusterCategories.Label(categories[index]));
            chipActive[index + 1] = (mask & (1 << categories[index])) != 0;
        }

        var count = categories.Length + 1;
        var tapped = categoryRail.Draw(ui, chipLabels.AsSpan(0, count), chipActive.AsSpan(0, count));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        if (tapped < 0)
        {
            return;
        }

        configuration.MusterCategoryFilter = tapped == 0 ? 0 : mask ^ (1 << categories[tapped - 1]);
        configuration.Save();
        store.RefreshDirectory();
    }

    private void DrawMinePinned(MusterDto mine, long nowUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = PinnedCardHeight * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Card * scale * 1.15f;
        var hovered = UiInteract.Hover(card.Min, card.Max);
        Elevation.Card(drawList, card.Min, card.Max, rounding, scale, hovered ? 1f : 0.7f);
        Squircle.FillVerticalGradient(drawList, card.Min, card.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, hovered ? 0.30f : 0.24f)),
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.07f)));
        Squircle.Stroke(drawList, card.Min, card.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.40f)), 1f * scale);
        var tileSide = 38f * scale;
        var tileCenter = new Vector2(card.Min.X + 15f * scale + tileSide * 0.5f, card.Center.Y);
        IconTile.Draw(tileCenter, tileSide, IconTile.Surface(ui.Accent), MusterCategories.Icon(mine.Category));
        var textLeft = tileCenter.X + tileSide * 0.5f + 13f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, card.Min.Y + 13f * scale), Loc.T(L.Muster.YourMuster),
            AppPalettes.Muster.TitleInk, TextStyles.Title3);
        var live = mine.StartsAtUnix <= nowUnix;
        var status = live
            ? Loc.T(L.Common.Live)
            : Loc.T(L.Muster.StartsIn, MusterText.Span(mine.StartsAtUnix - nowUnix));
        var statusLeft = textLeft;
        if (live)
        {
            MusterCard.DrawLiveDot(drawList, new Vector2(textLeft + 4f * scale, card.Min.Y + 45f * scale), scale);
            statusLeft += 14f * scale;
        }

        var statusText = $"{status} · {Loc.T(L.Muster.GoingCount, mine.RsvpCount)}";
        Typography.Draw(drawList, new Vector2(statusLeft, card.Min.Y + 38f * scale), statusText,
            live ? MusterCard.LiveGreen : AppPalettes.Muster.BodyInk, TextStyles.SubheadlineEmphasized);
        AppSkin.Icon(drawList, new Vector2(card.Max.X - 20f * scale, card.Center.Y),
            FontAwesomeIcon.ChevronRight.ToIconString(), AppPalettes.Muster.MutedInk, 0.7f);
        if (hovered)
        {
            UiInteract.HoverHighlight(drawList, card.Min, card.Max, rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(card.Min, card.Max, hovered))
        {
            OpenManage();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void DrawGoingRows(long nowUnix, float scale)
    {
        for (var index = 0; index < goingSection.Count; index++)
        {
            DrawGoingRow(goingSection[index], nowUnix, scale);
        }
    }

    private void DrawGoingRow(MusterDto muster, long nowUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = GoingRowHeight * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Card * scale;
        var hovered = UiInteract.Hover(card.Min, card.Max);
        ui.Card(drawList, card.Min, card.Max, rounding, elevated: true);
        var pad = 13f * scale;
        var avatarRadius = 16f * scale;
        var avatarCenter = new Vector2(card.Min.X + pad + avatarRadius, card.Center.Y);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, MusterText.HostLabel(muster), muster.HostWorld,
            null, images, lodestone, 0.9f, 32);
        var live = muster.StartsAtUnix <= nowUnix;
        var status = live
            ? Loc.T(L.Common.Live)
            : Loc.T(L.Muster.StartsIn, MusterText.Span(muster.StartsAtUnix - nowUnix));
        var statusSize = Typography.Measure(status, TextStyles.FootnoteEmphasized);
        var statusLeft = card.Max.X - pad - statusSize.X;
        Typography.Draw(drawList, new Vector2(statusLeft, card.Center.Y - statusSize.Y * 0.5f), status,
            live ? MusterCard.LiveGreen : ui.Accent, TextStyles.FootnoteEmphasized);
        if (live)
        {
            MusterCard.DrawLiveDot(drawList, new Vector2(statusLeft - 11f * scale, card.Center.Y), scale);
            statusLeft -= 16f * scale;
        }

        var textLeft = avatarCenter.X + avatarRadius + 11f * scale;
        var textWidth = statusLeft - 8f * scale - textLeft;
        var identity = Typography.FitText(MusterText.Identity(muster), textWidth, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, card.Min.Y + 9f * scale), identity,
            AppPalettes.Muster.TitleInk, TextStyles.Headline);
        var place = Typography.FitText(MusterText.Place(muster), textWidth, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(textLeft, card.Min.Y + 29f * scale), place,
            AppPalettes.Muster.MutedInk, TextStyles.Subheadline);
        if (hovered)
        {
            UiInteract.HoverHighlight(drawList, card.Min, card.Max, rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(card.Min, card.Max, hovered))
        {
            OpenDetail(muster.Id);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private int DrawChipFlow(int count, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var right = origin.X + width;
        var gap = Metrics.Space.Sm * scale;
        var chipHeight = ChipHeight * scale;
        var lineAdvance = chipHeight + gap;
        var cursorX = origin.X;
        var lineTop = origin.Y;
        var tapped = -1;
        for (var index = 0; index < count; index++)
        {
            var label = chipLabels[index];
            var chipWidth = Typography.Measure(label, 0.85f, FontWeight.Medium).X + 26f * scale;
            if (cursorX + chipWidth > right && cursorX > origin.X)
            {
                cursorX = origin.X;
                lineTop += lineAdvance;
            }

            var centerY = lineTop + chipHeight * 0.5f;
            if (ui.FlowChip(ref cursorX, centerY, gap, label, chipActive[index]))
            {
                tapped = index;
            }
        }

        var totalHeight = lineTop + chipHeight - origin.Y;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, totalHeight));
        return tapped;
    }

    private void DrawCards(List<MusterDto> items, long nowUnix, int currentDataCenterId, float scale)
    {
        for (var index = 0; index < items.Count; index++)
        {
            var muster = items[index];
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var height = MusterCard.Height(muster, width, scale);
            var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
            if (ImGui.IsRectVisible(card.Min, card.Max) && MusterCard.Draw(card, muster, images, lodestone, theme,
                    ui, nowUnix, currentDataCenterId))
            {
                OpenDetail(muster.Id);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, height + MusterCard.Gap * scale));
        }
    }

    private void DrawLoadMore(float scale)
    {
        if (!store.DirectoryHasMore)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 36f * scale;
        if (store.DirectoryLoadingMore)
        {
            LoadingPulse.Spinner(new Vector2(origin.X + width * 0.5f, origin.Y + height * 0.5f), 9f * scale,
                ui.Accent);
        }
        else
        {
            var label = Loc.T(L.Muster.LoadMore);
            var buttonWidth = Typography.Measure(label, 0.9f, FontWeight.SemiBold).X + 44f * scale;
            var rect = new Rect(new Vector2(origin.X + (width - buttonWidth) * 0.5f, origin.Y),
                new Vector2(origin.X + (width + buttonWidth) * 0.5f, origin.Y + height));
            if (ui.GhostButton(rect, label))
            {
                store.LoadMoreDirectory();
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawDirectoryEmpty(Rect body, float scale)
    {
        if (store.DirectoryLoading && !store.DirectoryLoadedOnce)
        {
            var origin = ImGui.GetCursorScreenPos();
            LoadingPulse.Draw(new Vector2(body.Center.X, origin.Y + 90f * scale), 13f * scale, ui.Accent,
                AppPalettes.Muster.MutedInk, Loc.T(L.Common.Loading));
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 160f * scale));
            return;
        }

        if (store.Mine is not null)
        {
            return;
        }

        EmptyState.Draw(body, ui, FontAwesomeIcon.Bullhorn, Loc.T(L.Muster.EmptyTitle), Loc.T(L.Muster.EmptyHint));
    }

    private void EnsureSections(long nowUnix)
    {
        var contacts = store.ContactMusters;
        var directory = store.Directory;
        var going = store.GoingMusters;
        var mine = store.Mine;
        if (ReferenceEquals(contacts, lastContacts) && ReferenceEquals(directory, lastDirectory)
            && ReferenceEquals(going, lastGoing) && ReferenceEquals(mine, lastMine)
            && nowUnix < nextSectionRebuildUnix)
        {
            return;
        }

        lastContacts = contacts;
        lastDirectory = directory;
        lastGoing = going;
        lastMine = mine;
        goingSection.Clear();
        friendSection.Clear();
        liveSection.Clear();
        soonSection.Clear();
        var nextBoundary = long.MaxValue;
        for (var index = 0; index < going.Length; index++)
        {
            var muster = going[index];
            if (muster.EndsAtUnix <= nowUnix)
            {
                continue;
            }

            if (mine is not null && (muster.Id == mine.Id || muster.HostId == mine.HostId))
            {
                continue;
            }

            if (ContainsId(contacts, muster.Id))
            {
                continue;
            }

            goingSection.Add(muster);
            nextBoundary = Math.Min(nextBoundary, muster.EndsAtUnix);
            if (muster.StartsAtUnix > nowUnix)
            {
                nextBoundary = Math.Min(nextBoundary, muster.StartsAtUnix);
            }
        }

        for (var index = 0; index < contacts.Length; index++)
        {
            var muster = contacts[index];
            if (muster.EndsAtUnix <= nowUnix)
            {
                continue;
            }

            friendSection.Add(muster);
            nextBoundary = Math.Min(nextBoundary, muster.EndsAtUnix);
            if (muster.StartsAtUnix > nowUnix)
            {
                nextBoundary = Math.Min(nextBoundary, muster.StartsAtUnix);
            }
        }

        for (var index = 0; index < directory.Length; index++)
        {
            var muster = directory[index];
            if (muster.EndsAtUnix <= nowUnix)
            {
                continue;
            }

            if (mine is not null && (muster.Id == mine.Id || muster.HostId == mine.HostId))
            {
                continue;
            }

            if (IsContactHost(contacts, muster.HostId))
            {
                continue;
            }

            nextBoundary = Math.Min(nextBoundary, muster.EndsAtUnix);
            if (muster.StartsAtUnix <= nowUnix)
            {
                liveSection.Add(muster);
            }
            else
            {
                soonSection.Add(muster);
                nextBoundary = Math.Min(nextBoundary, muster.StartsAtUnix);
            }
        }

        nextSectionRebuildUnix = nextBoundary == long.MaxValue
            ? nowUnix + SectionFallbackRebuildSeconds
            : Math.Min(nextBoundary, nowUnix + SectionFallbackRebuildSeconds);
    }

    private static bool ContainsId(MusterDto[] source, string musterId)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id == musterId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsContactHost(MusterDto[] contacts, string hostId)
    {
        for (var index = 0; index < contacts.Length; index++)
        {
            if (contacts[index].HostId == hostId)
            {
                return true;
            }
        }

        return false;
    }
}
