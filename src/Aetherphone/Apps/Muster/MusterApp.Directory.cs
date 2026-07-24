using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Muster;

internal sealed partial class MusterApp
{
    private const float PinnedCardHeight = 58f;
    private const float ControlRowHeight = 42f;
    private const float GoingRowHeight = 48f;
    private const float ChipHeight = 32f;
    private const float SheetRevealSeconds = 0.18f;
    private const int SectionFallbackRebuildSeconds = 30;

    private static readonly Vector4 SheetWhite = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 SheetHoverFill = new(1f, 1f, 1f, 0.16f);
    private static readonly Vector4 SheetGhostStroke = new(1f, 1f, 1f, 0.28f);

    private readonly List<MusterDto> goingSection = new();
    private readonly List<MusterDto> friendSection = new();
    private readonly List<MusterDto> liveSection = new();
    private readonly List<MusterDto> soonSection = new();
    private readonly string[] chipLabels = new string[12];
    private readonly bool[] chipActive = new bool[12];
    private readonly string[] scopeLabels = new string[3];
    private MusterDto[] lastContacts = Array.Empty<MusterDto>();
    private MusterDto[] lastDirectory = Array.Empty<MusterDto>();
    private MusterDto[] lastGoing = Array.Empty<MusterDto>();
    private MusterDto? lastMine;
    private long nextSectionRebuildUnix;
    private bool filterSheetOpen;
    private double filterSheetOpenedAt;
    private int filterSheetOpenedFrame;
    private int lastFilterMask = -1;
    private string filterCountText = string.Empty;

    private void DrawDirectory(Rect area)
    {
        if (filterSheetOpen)
        {
            UiInteract.BlockThisFrame();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var nowUnix = NowUnix();
        var currentDataCenterId = MusterWorlds.CurrentDataCenterId();
        DrawDirectoryHeader(area, scale);
        var controlsTop = area.Min.Y + AppHeader.Height * scale;
        DrawScopeRow(area, controlsTop, scale);
        var body = new Rect(new Vector2(area.Min.X, controlsTop + ControlRowHeight * scale), area.Max);
        EnsureSections(nowUnix);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
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

        DrawFilterSheet(area, scale);
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
            return;
        }

        if (ui.IconButton(actionCenter, 14f * scale, FontAwesomeIcon.Sync.ToIconString(),
                AppPalettes.Muster.BodyInk, AppSkin.Transparent, 0.9f))
        {
            store.SyncNow();
            store.RefreshDirectory();
        }
    }

    private void DrawScopeRow(Rect area, float top, float scale)
    {
        var inset = 16f * scale;
        var row = new Rect(new Vector2(area.Min.X + inset, top),
            new Vector2(area.Max.X - inset, top + ControlRowHeight * scale));
        var pillSide = 32f * scale;
        var gap = Metrics.Space.Sm * scale;
        scopeLabels[0] = Loc.T(L.Muster.ScopeMyDc);
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

        DrawFiltersPill(new Vector2(row.Max.X - pillSide * 0.5f, row.Center.Y), pillSide * 0.5f, scale);
    }

    private void DrawFiltersPill(Vector2 center, float radius, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var mask = configuration.MusterCategoryFilter;
        if (mask != lastFilterMask)
        {
            lastFilterMask = mask;
            filterCountText = BitOperations.PopCount((uint)mask).ToString(Loc.Culture);
        }

        var active = mask != 0;
        var corner = new Vector2(radius, radius);
        var hovered = UiInteract.Hover(center - corner, center + corner);
        var fill = active
            ? Palette.WithAlpha(ui.Accent, hovered ? 0.36f : 0.26f)
            : hovered ? SheetHoverFill : AppPalettes.Muster.FieldSurface;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(fill), 32);
        AppSkin.Icon(drawList, center, FontAwesomeIcon.Filter.ToIconString(),
            active ? ui.Accent : AppPalettes.Muster.BodyInk, 0.68f);
        if (active)
        {
            var badgeCenter = center + new Vector2(radius * 0.72f, -radius * 0.72f);
            drawList.AddCircleFilled(badgeCenter, 7f * scale, ImGui.GetColorU32(ui.Accent), 20);
            Typography.DrawCentered(drawList, badgeCenter, filterCountText, SheetWhite, 0.62f, FontWeight.SemiBold);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(center - corner, center + corner), Loc.T(L.Muster.Filters),
            HoverLabelSide.Above);
        if (UiInteract.Click(center - corner, center + corner, hovered))
        {
            filterSheetOpen = true;
            filterSheetOpenedAt = ImGui.GetTime();
            filterSheetOpenedFrame = ImGui.GetFrameCount();
        }
    }

    private void DrawFilterSheet(Rect area, float scale)
    {
        if (!filterSheetOpen)
        {
            return;
        }

        var screen = SceneChrome.ScreenFrom(area, theme, scale);
        ImGui.SetCursorScreenPos(area.Min);
        using var overlay = ImRaii.Child("##musterFilterSheet", area.Size, false,
            ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        var drawList = ImGui.GetWindowDrawList();
        var reveal = Easing.EaseOutQuint(Math.Clamp(
            (float)((ImGui.GetTime() - filterSheetOpenedAt) / SheetRevealSeconds), 0f, 1f));
        drawList.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.45f * reveal)),
            theme.ScreenRounding * scale);
        var categories = MusterCategories.All;
        for (var index = 0; index < categories.Length; index++)
        {
            chipLabels[index] = Loc.T(MusterCategories.Label(categories[index]));
            chipActive[index] = (configuration.MusterCategoryFilter & (1 << categories[index])) != 0;
        }

        var margin = 10f * scale;
        var pad = Metrics.Space.Lg * scale;
        var contentWidth = area.Width - margin * 2f - pad * 2f;
        var chipsHeight = MeasureSheetChips(categories.Length, contentWidth, scale);
        var titleHeight = 30f * scale;
        var doneHeight = 44f * scale;
        var sheetHeight = pad + titleHeight + Metrics.Space.Sm * scale + chipsHeight + Metrics.Space.Md * scale
            + doneHeight + pad;
        var slide = (1f - reveal) * (sheetHeight + margin);
        var sheetMin = new Vector2(area.Min.X + margin, area.Max.Y - margin - sheetHeight + slide);
        var sheetMax = new Vector2(area.Max.X - margin, area.Max.Y - margin + slide);
        ui.PaintGradient(drawList, new Rect(sheetMin, sheetMax), screen, Metrics.Radius.Lg * scale);
        ui.Card(drawList, sheetMin, sheetMax, Metrics.Radius.Lg * scale, elevated: true);
        var titleTop = sheetMin.Y + pad;
        Typography.Draw(drawList, new Vector2(sheetMin.X + pad, titleTop), Loc.T(L.Muster.Filters),
            AppPalettes.Muster.TitleInk, TextStyles.Headline);
        var clearLabel = Loc.T(L.Muster.ClearFilters);
        var clearWidth = Typography.Measure(clearLabel, 0.85f, FontWeight.SemiBold).X + 22f * scale;
        var clearRect = new Rect(new Vector2(sheetMax.X - pad - clearWidth, titleTop - 3f * scale),
            new Vector2(sheetMax.X - pad, titleTop + 23f * scale));
        if (SheetGhostButton(drawList, clearRect, clearLabel) && configuration.MusterCategoryFilter != 0)
        {
            configuration.MusterCategoryFilter = 0;
            configuration.Save();
            store.RefreshDirectory();
        }

        var chipsTop = titleTop + titleHeight + Metrics.Space.Sm * scale;
        var tapped = DrawSheetChips(drawList, new Vector2(sheetMin.X + pad, chipsTop), categories.Length,
            contentWidth, scale);
        if (tapped >= 0)
        {
            configuration.MusterCategoryFilter ^= 1 << categories[tapped];
            configuration.Save();
            store.RefreshDirectory();
        }

        var doneTop = chipsTop + chipsHeight + Metrics.Space.Md * scale;
        var doneRect = new Rect(new Vector2(sheetMin.X + pad, doneTop), new Vector2(sheetMax.X - pad,
            doneTop + doneHeight));
        if (SheetPillButton(drawList, doneRect, Loc.T(L.Muster.Done)))
        {
            filterSheetOpen = false;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsMouseHoveringRect(sheetMin, sheetMax, false)
            && ImGui.GetFrameCount() != filterSheetOpenedFrame)
        {
            filterSheetOpen = false;
        }
    }

    private float MeasureSheetChips(int count, float width, float scale)
    {
        var gap = Metrics.Space.Sm * scale;
        var chipHeight = ChipHeight * scale;
        var cursorX = 0f;
        var lineTop = 0f;
        for (var index = 0; index < count; index++)
        {
            var chipWidth = Typography.Measure(chipLabels[index], 0.85f, FontWeight.Medium).X + 26f * scale;
            if (cursorX + chipWidth > width && cursorX > 0f)
            {
                cursorX = 0f;
                lineTop += chipHeight + gap;
            }

            cursorX += chipWidth + gap;
        }

        return lineTop + chipHeight;
    }

    private int DrawSheetChips(ImDrawListPtr drawList, Vector2 origin, int count, float width, float scale)
    {
        var gap = Metrics.Space.Sm * scale;
        var chipHeight = ChipHeight * scale;
        var right = origin.X + width;
        var cursorX = origin.X;
        var lineTop = origin.Y;
        var tapped = -1;
        for (var index = 0; index < count; index++)
        {
            var label = chipLabels[index];
            var textSize = Typography.Measure(label, 0.85f, FontWeight.Medium);
            var chipWidth = textSize.X + 26f * scale;
            if (cursorX + chipWidth > right && cursorX > origin.X)
            {
                cursorX = origin.X;
                lineTop += chipHeight + gap;
            }

            var min = new Vector2(cursorX, lineTop);
            var max = new Vector2(cursorX + chipWidth, lineTop + chipHeight);
            var hovered = ImGui.IsMouseHoveringRect(min, max, false);
            var fill = chipActive[index]
                ? (hovered ? Palette.Mix(ui.Accent, SheetWhite, 0.10f) : ui.Accent)
                : hovered ? SheetHoverFill : AppPalettes.Muster.FieldSurface;
            Squircle.Fill(drawList, min, max, chipHeight * 0.5f, ImGui.GetColorU32(fill));
            var ink = chipActive[index] ? SheetWhite : hovered ? AppPalettes.Muster.TitleInk
                : AppPalettes.Muster.BodyInk;
            Typography.Draw(drawList, new Vector2(min.X + (chipWidth - textSize.X) * 0.5f,
                lineTop + (chipHeight - textSize.Y) * 0.5f), label, ink, 0.85f, FontWeight.Medium);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    tapped = index;
                }
            }

            cursorX = max.X + gap;
        }

        return tapped;
    }

    private bool SheetPillButton(ImDrawListPtr drawList, Rect rect, string label)
    {
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max, false);
        var fill = hovered ? Palette.Mix(ui.Accent, SheetWhite, 0.12f) : ui.Accent;
        Squircle.Fill(drawList, rect.Min, rect.Max, rect.Height * 0.5f, ImGui.GetColorU32(fill));
        var textSize = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        Typography.Draw(drawList, rect.Center - textSize * 0.5f, label, SheetWhite, 0.9f, FontWeight.SemiBold);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private bool SheetGhostButton(ImDrawListPtr drawList, Rect rect, string label)
    {
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max, false);
        var radius = rect.Height * 0.5f;
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(SheetHoverFill));
        }

        Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(SheetGhostStroke), 1f);
        var textSize = Typography.Measure(label, 0.85f, FontWeight.SemiBold);
        Typography.Draw(drawList, rect.Center - textSize * 0.5f, label, AppPalettes.Muster.TitleInk, 0.85f,
            FontWeight.SemiBold);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawMinePinned(MusterDto mine, long nowUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = PinnedCardHeight * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Lg * scale;
        ui.Card(drawList, card.Min, card.Max, rounding, elevated: true);
        var tileSide = 32f * scale;
        var tileCenter = new Vector2(card.Min.X + 14f * scale + tileSide * 0.5f, card.Center.Y);
        IconTile.Draw(tileCenter, tileSide, IconTile.Surface(ui.Accent), MusterCategories.Icon(mine.Category));
        var textLeft = tileCenter.X + tileSide * 0.5f + 12f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, card.Min.Y + 10f * scale), Loc.T(L.Muster.YourMuster),
            AppPalettes.Muster.TitleInk, TextStyles.Headline);
        var live = mine.StartsAtUnix <= nowUnix;
        var status = live
            ? Loc.T(L.Common.Live)
            : Loc.T(L.Muster.StartsIn, MusterText.Span(mine.StartsAtUnix - nowUnix));
        Typography.Draw(drawList, new Vector2(textLeft, card.Min.Y + 31f * scale), status,
            live ? MusterCard.LiveGreen : ui.Accent, TextStyles.FootnoteEmphasized);
        AppSkin.Icon(drawList, new Vector2(card.Max.X - 20f * scale, card.Center.Y),
            FontAwesomeIcon.ChevronRight.ToIconString(), AppPalettes.Muster.MutedInk, 0.7f);
        var hovered = UiInteract.Hover(card.Min, card.Max);
        if (hovered)
        {
            UiInteract.HoverHighlight(drawList, card.Min, card.Max, rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(card.Min, card.Max, hovered))
        {
            router.Push(MusterRoute.Manage);
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
        var rounding = Metrics.Radius.Md * scale;
        ui.Card(drawList, card.Min, card.Max, rounding);
        var pad = 12f * scale;
        var avatarRadius = 14f * scale;
        var avatarCenter = new Vector2(card.Min.X + pad + avatarRadius, card.Center.Y);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, muster.HostCharacter, muster.HostWorld,
            null, images, lodestone, 0.8f, 32);
        var live = muster.StartsAtUnix <= nowUnix;
        var status = live
            ? Loc.T(L.Common.Live)
            : Loc.T(L.Muster.StartsIn, MusterText.Span(muster.StartsAtUnix - nowUnix));
        var statusSize = Typography.Measure(status, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(card.Max.X - pad - statusSize.X, card.Center.Y - statusSize.Y * 0.5f),
            status, live ? MusterCard.LiveGreen : ui.Accent, TextStyles.FootnoteEmphasized);
        var textLeft = avatarCenter.X + avatarRadius + 10f * scale;
        var textWidth = card.Max.X - pad - statusSize.X - 8f * scale - textLeft;
        var identity = Typography.FitText(MusterText.Identity(muster), textWidth, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, card.Min.Y + 8f * scale), identity,
            AppPalettes.Muster.TitleInk, TextStyles.SubheadlineEmphasized);
        var place = Typography.FitText(MusterText.Place(muster), textWidth, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, card.Min.Y + 26f * scale), place,
            AppPalettes.Muster.MutedInk, TextStyles.Footnote);
        var hovered = UiInteract.Hover(card.Min, card.Max);
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
