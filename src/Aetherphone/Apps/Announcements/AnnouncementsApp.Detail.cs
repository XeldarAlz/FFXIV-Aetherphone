using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Announcements;

internal sealed partial class AnnouncementsApp
{
    private const float ReadingInset = 4f;
    private const float ArticleTopGap = 4f;
    private const float TitleToMeta = 8f;
    private const float MetaToRule = 18f;
    private const float RuleToBody = 18f;
    private const float ParagraphGap = 12f;
    private const float BodyToNeighbors = 36f;
    private const float NeighborGap = 10f;
    private const float NeighborPad = 14f;
    private const float NeighborRadius = Metrics.Radius.Widget;
    private const float NeighborLabelGap = 6f;
    private const float NeighborChevronGap = 6f;
    private const int NeighborTitleLines = 2;
    private const float DetailBottomGap = 28f;
    private const float LoadingOffset = 120f;
    private const float LoadingRadius = 13f;
    private const float VeilEpsilon = 0.999f;
    private const string NewerId = "announcements.newer";
    private const string OlderId = "announcements.older";
    private const string DetailNavId = "announcements.detail.nav";
    private const string DetailAnchor = "announcements.detail";
    private const string ClipboardSeparator = "\n\n";

    private readonly NavBarButton[] detailButtons = new NavBarButton[1];
    private string? readingId;
    private string? pendingSwapId;
    private int olderRequestedAt = -1;
    private Spring swapReveal = new(1f);

    private void ApplyPendingSwap()
    {
        if (pendingSwapId is null)
        {
            return;
        }

        var target = pendingSwapId;
        pendingSwapId = null;
        if (router.Current.Screen != AnnouncementsScreen.Detail || router.IsTransitioning)
        {
            return;
        }

        if (readingId is not null)
        {
            store.MarkRead(readingId);
            readingId = null;
        }

        router.Replace(AnnouncementsRoute.Detail(target));
        swapReveal.SnapTo(0f);
    }

    private void DrawDetail(Rect area, string announcementId, int depth)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        var inlineHeight = NavBarMetrics.InlineHeight * scale;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + inlineHeight), area.Max);
        AppSurface.ArmNavBar(body.Min.Y, inlineHeight);
        var frame = new NavBarFrame(area, body, scale, inlineHeight);
        var index = IndexOf(announcementId);
        if (index < 0)
        {
            DrawMissingArticle(body, scale);
            AppHeader.EndLargeTitle(in frame, context, DetailNavId, string.Empty, NavStyle(),
                ReadOnlySpan<NavBarButton>.Empty, DisplayName, back);
            return;
        }

        var entry = entries[index];
        if (depth == router.Depth)
        {
            readingId = entry.Source.Id;
        }

        using (AppSurface.Begin(body))
        {
            DrawArticle(entry, scale);
            ImGui.Dummy(new Vector2(0f, BodyToNeighbors * scale));
            DrawNeighbors(index, scale);
            ImGui.Dummy(new Vector2(0f, DetailBottomGap * scale));
            DrawSwapVeil();
        }

        detailButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Copy), Loc.T(L.Announcements.CopyText));
        var pressed = AppHeader.EndLargeTitle(in frame, context, DetailNavId, entry.Title, NavStyle(), detailButtons,
            DisplayName, back);
        if (pressed == 0)
        {
            CopyArticle(entry);
        }
    }

    private void DrawMissingArticle(Rect body, float scale)
    {
        if (!store.LoadedOnce && !store.Failed)
        {
            LoadingPulse.Draw(new Vector2(body.Center.X, body.Min.Y + LoadingOffset * scale), LoadingRadius * scale,
                ui.Accent, ui.MutedInk, Loc.T(L.Common.Loading));
            return;
        }

        AnnouncementsStatePanel.Draw(body, ui, FontAwesomeIcon.Bullhorn, Loc.T(L.Announcements.UnavailableTitle),
            Loc.T(L.Announcements.UnavailableHint), string.Empty);
    }

    private void DrawArticle(AnnouncementEntry entry, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = ReadingInset * scale;
        var left = origin.X + inset;
        var measure = MathF.Max(1f, width - inset * 2f);
        var cursorY = origin.Y + ArticleTopGap * scale;

        if (entry.Title.Length > 0)
        {
            cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), entry.Title, ui.TitleInk,
                TextStyles.LargeTitle, measure);
            cursorY += TitleToMeta * scale;
        }

        var meta = Typography.FitText(entry.Meta, measure, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(left, cursorY), meta, ui.MutedInk, TextStyles.Subheadline);
        cursorY += Typography.LineHeight(TextStyles.Subheadline) + MetaToRule * scale;
        FeedCell.Hairline(drawList, left, left + measure, cursorY, ui.Hairline);
        cursorY += RuleToBody * scale;
        cursorY = DrawBlocks(drawList, entry, left, cursorY, measure, scale);

        if (UiAnchors.Recording)
        {
            var visibleBottom = ImGui.GetWindowPos().Y + ImGui.GetWindowSize().Y;
            UiAnchors.Report(DetailAnchor,
                new Rect(origin, new Vector2(origin.X + width, MathF.Min(cursorY, visibleBottom))));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cursorY - origin.Y));
    }

    private float DrawBlocks(ImDrawListPtr drawList, AnnouncementEntry entry, float left, float top, float measure,
        float scale)
    {
        var cursorY = top;
        var blocks = entry.Blocks;
        for (var blockIndex = 0; blockIndex < blocks.Length; blockIndex++)
        {
            if (entry.BlockOpensParagraph[blockIndex])
            {
                cursorY += ParagraphGap * scale;
            }

            var block = blocks[blockIndex];
            RichTextLayout? layout;
            using (Plugin.Fonts.Push(TextStyles.Body.Scale, TextStyles.Body.Weight))
            {
                layout = LinkText.LayoutFor(block, measure);
            }

            if (layout is null)
            {
                cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), block, ui.BodyInk, TextStyles.Body,
                    measure);
                continue;
            }

            using (Plugin.Fonts.Push(TextStyles.Body.Scale, TextStyles.Body.Weight))
            {
                LinkText.Draw(drawList, layout, new Vector2(left, cursorY), 1f, ui.BodyInk, ui.Accent, 1f, true);
            }

            cursorY += layout.Size.Y;
        }

        return cursorY;
    }

    private void DrawNeighbors(int index, float scale)
    {
        NeighborsOf(index, out var newerIndex, out var olderIndex);
        if (olderIndex < 0 && store.HasMore && !store.LoadingMore && olderRequestedAt != entries.Length)
        {
            olderRequestedAt = entries.Length;
            store.LoadMore();
        }

        if (newerIndex < 0 && olderIndex < 0)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var gap = NeighborGap * scale;
        var cardWidth = (width - gap) * 0.5f;
        var innerWidth = MathF.Max(1f, cardWidth - NeighborPad * 2f * scale);
        var newerLines = newerIndex >= 0 ? NeighborLines(entries[newerIndex], innerWidth) : Array.Empty<string>();
        var olderLines = olderIndex >= 0 ? NeighborLines(entries[olderIndex], innerWidth) : Array.Empty<string>();
        var lineCount = Math.Max(1, Math.Max(newerLines.Length, olderLines.Length));
        var height = NeighborPad * 2f * scale + Typography.LineHeight(TextStyles.FootnoteEmphasized)
            + NeighborLabelGap * scale + lineCount * Typography.LineHeight(TextStyles.SubheadlineEmphasized);

        if (newerIndex >= 0)
        {
            var rest = new Rect(origin, new Vector2(origin.X + cardWidth, origin.Y + height));
            DrawNeighbor(drawList, rest, entries[newerIndex], newerLines, NewerId, false, scale);
        }

        if (olderIndex >= 0)
        {
            var rest = new Rect(new Vector2(origin.X + width - cardWidth, origin.Y),
                new Vector2(origin.X + width, origin.Y + height));
            DrawNeighbor(drawList, rest, entries[olderIndex], olderLines, OlderId, true, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private string[] NeighborLines(AnnouncementEntry entry, float innerWidth) =>
        entry.NeighborTitle.Get(entry.Title, TextStyles.SubheadlineEmphasized, innerWidth, NeighborTitleLines,
            fontKey);

    private void NeighborsOf(int index, out int newerIndex, out int olderIndex)
    {
        for (var position = 0; position < visibleCount; position++)
        {
            if (visible[position] != index)
            {
                continue;
            }

            newerIndex = position > 0 ? visible[position - 1] : -1;
            olderIndex = position + 1 < visibleCount ? visible[position + 1] : -1;
            return;
        }

        newerIndex = index - 1;
        olderIndex = index + 1 < entries.Length ? index + 1 : -1;
    }

    private void DrawNeighbor(ImDrawListPtr drawList, Rect rest, AnnouncementEntry entry, string[] titleLines,
        string id, bool older, float scale)
    {
        var hovered = UiInteract.Hover(rest.Min, rest.Max);
        Lift(drawList, rest, id, hovered, NeighborRadius * scale, scale);
        var pad = NeighborPad * scale;
        var left = rest.Min.X + pad;
        var right = rest.Max.X - pad;
        var label = Loc.T(older ? L.Announcements.Older : L.Announcements.Newer);
        var labelSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var labelCenterY = rest.Min.Y + pad + Typography.LineHeight(TextStyles.FootnoteEmphasized) * 0.5f;
        var chevron = ChevronSize * scale;
        var chevronGap = NeighborChevronGap * scale;
        var stroke = Metrics.Stroke.Thin * scale;
        if (older)
        {
            var tip = new Vector2(right, labelCenterY);
            DrawChevron(drawList, tip, chevron, stroke, ui.Accent, true);
            Typography.Draw(drawList,
                new Vector2(tip.X - chevron - chevronGap - labelSize.X, labelCenterY - labelSize.Y * 0.5f), label,
                ui.Accent, TextStyles.FootnoteEmphasized);
        }
        else
        {
            var tip = new Vector2(left, labelCenterY);
            DrawChevron(drawList, tip, chevron, stroke, ui.Accent, false);
            Typography.Draw(drawList, new Vector2(tip.X + chevron + chevronGap, labelCenterY - labelSize.Y * 0.5f),
                label, ui.Accent, TextStyles.FootnoteEmphasized);
        }

        var lineHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var cursorY = rest.Min.Y + pad + Typography.LineHeight(TextStyles.FootnoteEmphasized)
            + NeighborLabelGap * scale;
        for (var lineIndex = 0; lineIndex < titleLines.Length; lineIndex++)
        {
            var line = titleLines[lineIndex];
            var lineLeft = older ? right - Typography.Measure(line, TextStyles.SubheadlineEmphasized).X : left;
            Typography.Draw(drawList, new Vector2(lineLeft, cursorY), line, ui.TitleInk,
                TextStyles.SubheadlineEmphasized);
            cursorY += lineHeight;
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(rest, entry.Title);
        if (UiInteract.Click(rest.Min, rest.Max, hovered))
        {
            pendingSwapId = entry.Source.Id;
        }
    }

    private void DrawSwapVeil()
    {
        var reveal = swapReveal.Step(1f, Motion.Appear, deltaSeconds);
        if (reveal >= VeilEpsilon)
        {
            return;
        }

        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();
        ImGui.GetWindowDrawList().AddRectFilled(min, max,
            ImGui.GetColorU32(ui.Palette.BackdropBottom with { W = 1f - reveal }));
    }

    private void CopyArticle(AnnouncementEntry entry)
    {
        var text = entry.Body.Length == 0 ? entry.Title : string.Concat(entry.Title, ClipboardSeparator, entry.Body);
        ImGui.SetClipboardText(text);
        toast.Show(Loc.T(L.Common.Copied));
    }
}
