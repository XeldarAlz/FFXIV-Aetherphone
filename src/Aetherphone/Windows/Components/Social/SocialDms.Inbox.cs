using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly record struct DmPreviewLine(long At, string Source, string SenderId, long Minute, string Line,
    int StampLength);

internal sealed partial class SocialDms
{
    private const float InboxRowHeight = 72f;
    private const float InboxAvatarRadius = 28f;
    private const float InboxSearchHeight = 52f;
    private const float InboxHeadingHeight = 44f;
    private const float InboxUnreadDotInset = 8f;
    private const float InboxTextGap = 12f;
    private const float InboxArchivedRowHeight = 52f;
    private const float InboxArchiveGlyph = 21f;
    private const float InboxPinGlyph = 14f;
    private const float InboxPinGap = 6f;
    private const float InboxTickGlyph = 14f;
    private const float InboxTickGap = 4f;
    private const long MinuteTicks = 60000;

    private static readonly TextStyle InboxHeadingStyle = TextStyles.Title3;
    private static readonly TextStyle InboxLinkStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle InboxNameUnreadStyle = TextStyles.Headline;
    private static readonly TextStyle InboxNameStyle = TextStyles.BodyEmphasized;
    private static readonly TextStyle InboxPreviewUnreadStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle InboxPreviewStyle = TextStyles.Subheadline;

    private readonly List<DmThreadDto> inboxPinned = new();
    private readonly List<DmThreadDto> inboxFiltered = new();
    private readonly Dictionary<string, DmPreviewLine> inboxPreviews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RichLineFit> inboxPreviewFits = new(StringComparer.Ordinal);
    private DmThreadDto[] inboxFilterSource = Array.Empty<DmThreadDto>();
    private string inboxFilterQuery = string.Empty;
    private bool inboxFilterRequests;
    private bool inboxFilterArchived;
    private bool inboxFilterDirty;
    private string inboxArchivedCountLabel = string.Empty;
    private int inboxArchivedCountValue = -1;
    private string inboxDraft = string.Empty;
    private bool inboxShowRequests;
    private string inboxRequestsLabel = string.Empty;
    private int inboxRequestsLabelCount = -1;

    private void DrawInbox(Rect area)
    {
        var scale = UiScale.Current;
        if (!dm.ThreadsLoaded && !dm.LoadingThreads)
        {
            dm.RefreshThreads();
        }

        var title = store.Me is { } me && me.Handle.Length > 0 ? me.Handle : Loc.T(labels.InboxTitle);
        DrawScreenHeader(area, title, 1);
        if (DrawHeaderIcon(ImGui.GetWindowDrawList(), SocialChrome.HeaderSlot(area, 0), PhoneIcons.Edit,
                Loc.T(labels.NewMessage), Ink))
        {
            OpenNewMessage();
        }

        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var threads = dm.Threads;
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            var searchOrigin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            DrawInboxSearchPill(new Rect(new Vector2(searchOrigin.X + CellPadX * scale, searchOrigin.Y),
                new Vector2(searchOrigin.X + width - CellPadX * scale, searchOrigin.Y + InboxSearchHeight * scale)),
                "##dmInboxSearch", Loc.T(L.Common.Search), ref inboxDraft);
            DrawInboxHeading(dm.RequestCount);
            RefreshInboxFilter(threads, false);
            if (!inboxShowRequests && inboxFilterQuery.Length == 0 && rowSheet.ArchivedCount > 0)
            {
                DrawInboxArchivedRow();
            }

            if (inboxPinned.Count == 0 && inboxFiltered.Count == 0)
            {
                DrawInboxEmptyState(listRect, threads.Length);
                return;
            }

            for (var index = 0; index < inboxPinned.Count; index++)
            {
                DrawInboxRow(inboxPinned[index], true);
            }

            for (var index = 0; index < inboxFiltered.Count; index++)
            {
                DrawInboxRow(inboxFiltered[index], false);
            }

            if (dm.LoadingMoreThreads)
            {
                InfiniteScroll.DrawLoadingRow(listRect.Center.X, Ink.MutedInk);
            }
            else if (dm.HasMoreThreads && InfiniteScroll.ReachedBottom())
            {
                dm.LoadMoreThreads();
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawInboxSearchPill(Rect bar, string id, string hint, ref string draft)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        SearchField.Draw(bar, id, hint, ref draft, host.SearchPalette);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, bar.Max.Y - origin.Y));
    }

    private void DrawInboxHeading(int requestCount)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = InboxHeadingHeight * scale;
        var centerY = origin.Y + height * 0.5f;
        var pad = CellPadX * scale;
        var heading = Loc.T(inboxShowRequests ? labels.Requests : labels.InboxTitle);
        var link = inboxShowRequests ? Loc.T(labels.InboxTitle) : RequestsLinkLabel(requestCount);
        var linkSize = Typography.Measure(link, InboxLinkStyle);
        var linkMin = new Vector2(origin.X + width - pad - linkSize.X, centerY - linkSize.Y * 0.5f);
        var linkMax = new Vector2(origin.X + width - pad, centerY + linkSize.Y * 0.5f);
        var headingHeight = Typography.LineHeight(InboxHeadingStyle);
        var headingFitted = Typography.FitText(heading, MathF.Max(1f, linkMin.X - 12f * scale - origin.X - pad),
            InboxHeadingStyle);
        Typography.Draw(drawList, new Vector2(origin.X + pad, centerY - headingHeight * 0.5f), headingFitted,
            Ink.TitleInk, InboxHeadingStyle);
        var hovered = UiInteract.Hover(linkMin, linkMax);
        Typography.Draw(drawList, linkMin, link, Ink.AccentLink, InboxLinkStyle);
        if (hovered)
        {
            drawList.AddLine(new Vector2(linkMin.X, linkMax.Y), linkMax, ImGui.GetColorU32(Ink.AccentLink), 1f);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(linkMin, linkMax, hovered))
        {
            inboxShowRequests = !inboxShowRequests;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private string RequestsLinkLabel(int requestCount)
    {
        if (requestCount <= 0)
        {
            return Loc.T(labels.Requests);
        }

        if (inboxRequestsLabelCount != requestCount)
        {
            inboxRequestsLabelCount = requestCount;
            inboxRequestsLabel = Loc.T(labels.RequestsCount, requestCount);
        }

        return inboxRequestsLabel;
    }

    private void RefreshInboxFilter(DmThreadDto[] threads, bool archivedView)
    {
        var query = inboxDraft.Trim();
        if (!inboxFilterDirty && ReferenceEquals(threads, inboxFilterSource) && inboxFilterRequests == inboxShowRequests
            && inboxFilterArchived == archivedView && string.Equals(query, inboxFilterQuery, StringComparison.Ordinal))
        {
            return;
        }

        inboxFilterDirty = false;
        inboxFilterSource = threads;
        inboxFilterRequests = inboxShowRequests;
        inboxFilterArchived = archivedView;
        inboxFilterQuery = query;
        inboxPinned.Clear();
        inboxFiltered.Clear();
        for (var index = 0; index < threads.Length; index++)
        {
            var thread = threads[index];
            if (!archivedView && thread.Pending != inboxShowRequests)
            {
                continue;
            }

            if (rowSheet.IsArchived(thread.OtherUserId) != archivedView)
            {
                continue;
            }

            if (query.Length > 0 && !InboxRowMatches(thread, query))
            {
                continue;
            }

            if (!archivedView && rowSheet.IsPinned(thread.OtherUserId))
            {
                inboxPinned.Add(thread);
            }
            else
            {
                inboxFiltered.Add(thread);
            }
        }
    }

    private void DrawInboxArchivedRow()
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, InboxArchivedRowHeight * scale, Ink.HoverTint);
        var pad = CellPadX * scale;
        var glyphCenter = new Vector2(cell.Bounds.Min.X + pad + InboxAvatarRadius * scale, cell.Bounds.Center.Y);
        PhoneIcon.Draw(drawList, glyphCenter, PhoneIcons.Archive, Ink.MutedInk, InboxArchiveGlyph * scale);
        var textLeft = glyphCenter.X + InboxAvatarRadius * scale + InboxTextGap * scale;
        var count = rowSheet.ArchivedCount;
        if (inboxArchivedCountValue != count)
        {
            inboxArchivedCountValue = count;
            inboxArchivedCountLabel = count.ToString(Loc.Culture);
        }

        var countSize = Typography.Measure(inboxArchivedCountLabel, InboxPreviewStyle);
        Typography.Draw(drawList,
            new Vector2(cell.Bounds.Max.X - pad - countSize.X, cell.Bounds.Center.Y - countSize.Y * 0.5f),
            inboxArchivedCountLabel, Ink.MutedInk, InboxPreviewStyle);
        var labelHeight = Typography.LineHeight(InboxNameStyle);
        Typography.Draw(drawList, new Vector2(textLeft, cell.Bounds.Center.Y - labelHeight * 0.5f),
            Loc.T(L.Social.ArchivedChats), Ink.TitleInk, InboxNameStyle);
        if (cell.Tapped)
        {
            host.PushDm(SocialDmScreen.Archived);
        }

        FeedCell.End(drawList, cell, Ink.Hairline, false);
    }

    private void DrawInboxArchived(Rect area)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Social.ArchivedChats));
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var threads = dm.Threads;
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            RefreshInboxFilter(threads, true);
            if (inboxFiltered.Count == 0)
            {
                DrawEmptyState(listRect, Loc.T(L.Social.NoArchivedChats), string.Empty);
                return;
            }

            for (var index = 0; index < inboxFiltered.Count; index++)
            {
                DrawInboxRow(inboxFiltered[index], false);
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private static bool InboxRowMatches(DmThreadDto thread, string query) =>
        thread.OtherDisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
        || thread.OtherHandle.Contains(query, StringComparison.OrdinalIgnoreCase);

    private void DrawInboxEmptyState(Rect listRect, int totalThreads)
    {
        var area = new Rect(new Vector2(listRect.Min.X, ImGui.GetCursorScreenPos().Y), listRect.Max);
        if (dm.LoadingThreads && totalThreads == 0)
        {
            DrawEmptyState(area, Loc.T(L.Common.Loading), string.Empty);
            return;
        }

        if (inboxFilterQuery.Length > 0)
        {
            DrawEmptyState(area, Loc.T(L.Social.ListEmpty), string.Empty);
            return;
        }

        if (inboxShowRequests)
        {
            DrawEmptyState(area, Loc.T(labels.RequestsEmpty), string.Empty);
            return;
        }

        DrawEmptyState(area, Loc.T(labels.InboxEmpty), Loc.T(labels.InboxEmptyHint));
    }

    private void DrawInboxRow(DmThreadDto thread, bool pinned)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = InboxRowHeight * scale;
        var cell = FeedCell.Begin(drawList, rowHeight, Ink.HoverTint);
        var origin = cell.Bounds.Min;
        var width = cell.Bounds.Width;
        var pad = CellPadX * scale;
        var avatarRadius = InboxAvatarRadius * scale;
        var avatarCenter = new Vector2(origin.X + pad + avatarRadius, origin.Y + rowHeight * 0.5f);
        AvatarView.Draw(drawList, avatarCenter, avatarRadius, host.Accent,
            Monogram(thread.OtherDisplayName, thread.OtherHandle), 0.95f,
            images.Avatar(thread.OtherAvatarUrl, avatarRadius * 2f), 40);
        var dotInset = avatarRadius * 0.72f;
        PresenceDot(drawList, new Vector2(avatarCenter.X + dotInset, avatarCenter.Y + dotInset), thread.Presence);
        var unread = thread.UnreadCount > 0;
        var textLeft = avatarCenter.X + avatarRadius + InboxTextGap * scale;
        var trailing = unread ? 20f * scale : 0f;
        if (pinned)
        {
            var glyph = InboxPinGlyph * scale;
            PhoneIcon.Draw(drawList, new Vector2(origin.X + width - pad - trailing - glyph * 0.5f, avatarCenter.Y),
                PhoneIcons.PinFilled, Ink.MutedInk, glyph);
            trailing += glyph + InboxPinGap * scale;
        }

        var textRight = origin.X + width - pad - trailing;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var nameStyle = unread ? InboxNameUnreadStyle : InboxNameStyle;
        var previewStyle = unread ? InboxPreviewUnreadStyle : InboxPreviewStyle;
        var nameHeight = Typography.LineHeight(nameStyle);
        var previewHeight = Typography.LineHeight(previewStyle);
        var blockTop = avatarCenter.Y - (nameHeight + previewHeight + 3f * scale) * 0.5f;
        var name = SocialIdentity.Name(thread.OtherDisplayName, thread.OtherHandle);
        Typography.Draw(drawList, new Vector2(textLeft, blockTop), Typography.FitText(name, textWidth, nameStyle),
            Ink.TitleInk, nameStyle);
        var previewTop = blockTop + nameHeight + 3f * scale;
        var previewLeft = textLeft;
        var previewWidth = textWidth;
        if (thread.LastMessagePreview.Length > 0
            && string.Equals(thread.LastMessageSenderId, dm.MyUserId, StringComparison.Ordinal))
        {
            var tick = InboxTickGlyph * scale;
            PhoneIcon.Draw(drawList, new Vector2(previewLeft + tick * 0.5f, previewTop + previewHeight * 0.5f),
                thread.LastMessageSeen ? PhoneIcons.Checks : PhoneIcons.Check,
                thread.LastMessageSeen ? Core.Theme.ChatInk.SeenTick : Ink.MutedInk, tick);
            previewLeft += tick + InboxTickGap * scale;
            previewWidth = MathF.Max(1f, textRight - previewLeft);
        }

        var preview = InboxPreview(thread, out var stampLength);
        if (!inboxPreviewFits.TryGetValue(thread.OtherUserId, out var previewFit)
            || !RichLine.Valid(previewFit, preview, previewWidth, previewStyle))
        {
            previewFit = RichLine.Fit(preview, stampLength, previewWidth, previewStyle);
            inboxPreviewFits[thread.OtherUserId] = previewFit;
        }

        RichLine.Draw(drawList, previewFit, new Vector2(previewLeft, previewTop),
            unread ? Ink.TitleInk : Ink.MutedInk);
        if (unread)
        {
            SocialChrome.DrawUnreadDot(drawList,
                new Vector2(origin.X + width - pad - InboxUnreadDotInset * scale, avatarCenter.Y), Ink);
        }

        if (cell.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            rowSheet.Open(thread.OtherUserId, SocialIdentity.Name(thread.OtherDisplayName, thread.OtherHandle));
        }
        else if (cell.Tapped)
        {
            OpenThread(thread.OtherUserId);
        }

        FeedCell.End(drawList, cell, Ink.Hairline, false);
    }

    private string InboxPreview(DmThreadDto thread, out int stampLength)
    {
        var minute = Environment.TickCount64 / MinuteTicks;
        if (inboxPreviews.TryGetValue(thread.OtherUserId, out var cached)
            && cached.At == thread.LastMessageAtUnix
            && ReferenceEquals(cached.Source, thread.LastMessagePreview)
            && ReferenceEquals(cached.SenderId, thread.LastMessageSenderId)
            && cached.Minute == minute)
        {
            stampLength = cached.StampLength;
            return cached.Line;
        }

        var body = string.IsNullOrEmpty(thread.LastMessagePreview)
            ? Loc.T(labels.ThreadEmpty)
            : ChatText.ListPreview(thread.LastMessagePreview);
        var mine = thread.LastMessagePreview.Length > 0
            && string.Equals(thread.LastMessageSenderId, dm.MyUserId, StringComparison.Ordinal);
        if (mine)
        {
            body = $"{Loc.T(L.Message.You)}: {body}";
        }

        var line = body;
        stampLength = 0;
        if (thread.LastMessageAtUnix > 0)
        {
            var stamp = $" · {TimeText.Short(thread.LastMessageAtUnix)}";
            line = body + stamp;
            stampLength = stamp.Length;
        }

        inboxPreviews[thread.OtherUserId] = new DmPreviewLine(thread.LastMessageAtUnix, thread.LastMessagePreview,
            thread.LastMessageSenderId, minute, line, stampLength);
        return line;
    }
}
