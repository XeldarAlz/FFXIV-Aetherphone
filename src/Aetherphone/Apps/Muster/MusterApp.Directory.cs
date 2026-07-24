using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Muster;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Muster;

internal sealed partial class MusterApp
{
    private const float PinnedCardHeight = 58f;
    private const float CtaHeight = 46f;
    private const float ChipHeight = 32f;
    private const int SectionFallbackRebuildSeconds = 30;

    private readonly List<MusterDto> friendSection = new();
    private readonly List<MusterDto> liveSection = new();
    private readonly List<MusterDto> soonSection = new();
    private readonly string[] chipLabels = new string[12];
    private readonly bool[] chipActive = new bool[12];
    private MusterDto[] lastContacts = Array.Empty<MusterDto>();
    private MusterDto[] lastDirectory = Array.Empty<MusterDto>();
    private MusterDto? lastMine;
    private long nextSectionRebuildUnix;

    private void DrawDirectory(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var nowUnix = NowUnix();
        DrawDirectoryHeader(area, scale);
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        EnsureSections(nowUnix);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            if (store.Mine is { } mine)
            {
                DrawMinePinned(mine, nowUnix, scale);
            }
            else
            {
                DrawStartCta(scale);
            }

            DrawFilters(scale);
            var anyCards = friendSection.Count > 0 || liveSection.Count > 0 || soonSection.Count > 0;
            if (!anyCards)
            {
                DrawDirectoryEmpty(body, scale);
            }
            else
            {
                if (friendSection.Count > 0)
                {
                    ui.SectionHeading(Loc.T(L.Muster.FriendsSection), 6f);
                    DrawCards(friendSection, nowUnix, scale);
                }

                if (liveSection.Count > 0)
                {
                    ui.SectionHeading(Loc.T(L.Muster.HappeningNow), 6f);
                    DrawCards(liveSection, nowUnix, scale);
                }

                if (soonSection.Count > 0)
                {
                    ui.SectionHeading(Loc.T(L.Muster.StartingSoon), 6f);
                    DrawCards(soonSection, nowUnix, scale);
                }

                DrawLoadMore(scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
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
            return;
        }

        if (ui.IconButton(actionCenter, 14f * scale, FontAwesomeIcon.Sync.ToIconString(),
                AppPalettes.Muster.BodyInk, AppSkin.Transparent, 0.9f))
        {
            store.SyncNow();
            store.RefreshDirectory();
        }
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

    private void DrawStartCta(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + CtaHeight * scale));
        if (ui.PillButton(rect, Loc.T(L.Muster.StartMuster), true))
        {
            router.Push(MusterRoute.Create);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, CtaHeight * scale + Metrics.Space.Md * scale));
    }

    private void DrawFilters(float scale)
    {
        var categories = MusterCategories.All;
        for (var index = 0; index < categories.Length; index++)
        {
            chipLabels[index] = Loc.T(MusterCategories.Label(categories[index]));
            chipActive[index] = (configuration.MusterCategoryFilter & (1 << categories[index])) != 0;
        }

        var tappedCategory = DrawChipFlow(categories.Length, scale);
        if (tappedCategory >= 0)
        {
            configuration.MusterCategoryFilter ^= 1 << categories[tappedCategory];
            configuration.Save();
            store.RefreshDirectory();
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var regions = MusterCategories.AllRegions;
        for (var index = 0; index < regions.Length; index++)
        {
            chipLabels[index] = Loc.T(MusterCategories.RegionLabel(regions[index]));
            chipActive[index] = (configuration.MusterRegionFilter & regions[index]) != 0;
        }

        var tappedRegion = DrawChipFlow(regions.Length, scale);
        if (tappedRegion >= 0)
        {
            configuration.MusterRegionFilter ^= regions[tappedRegion];
            configuration.Save();
            store.RefreshDirectory();
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
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

    private void DrawCards(List<MusterDto> items, long nowUnix, float scale)
    {
        for (var index = 0; index < items.Count; index++)
        {
            var muster = items[index];
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var height = MusterCard.Height(muster, width, scale);
            var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
            if (ImGui.IsRectVisible(card.Min, card.Max) && MusterCard.Draw(card, muster, images, ui, nowUnix))
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
        var mine = store.Mine;
        if (ReferenceEquals(contacts, lastContacts) && ReferenceEquals(directory, lastDirectory)
            && ReferenceEquals(mine, lastMine) && nowUnix < nextSectionRebuildUnix)
        {
            return;
        }

        lastContacts = contacts;
        lastDirectory = directory;
        lastMine = mine;
        friendSection.Clear();
        liveSection.Clear();
        soonSection.Clear();
        var nextBoundary = long.MaxValue;
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
