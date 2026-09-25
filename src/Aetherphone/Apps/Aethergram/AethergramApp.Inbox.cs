using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Aethergram;

internal readonly record struct InboxPreviewLine(long At, string Source, string SenderId, long Minute, string Line,
    int StampLength);

internal sealed partial class AethergramApp
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
    private const int MaxPinnedChats = 3;
    private const long MinuteTicks = 60000;

    private static readonly TextStyle InboxHeadingStyle = TextStyles.Title3;
    private static readonly TextStyle InboxLinkStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle InboxNameUnreadStyle = TextStyles.Headline;
    private static readonly TextStyle InboxNameStyle = TextStyles.BodyEmphasized;
    private static readonly TextStyle InboxPreviewUnreadStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle InboxPreviewStyle = TextStyles.Subheadline;

    private readonly ActionSheet.Item[] inboxRowSheetItems = new ActionSheet.Item[3];
    private readonly List<GramThreadDto> inboxPinned = new();
    private readonly List<GramThreadDto> inboxFiltered = new();
    private readonly Dictionary<string, InboxPreviewLine> inboxPreviews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RichLineFit> inboxPreviewFits = new(StringComparer.Ordinal);
    private GramThreadDto[] inboxFilterSource = Array.Empty<GramThreadDto>();
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
    private string? inboxSheetThreadId;
    private string inboxSheetTitle = string.Empty;

    private void DrawInbox(Rect area)
    {
        var scale = UiScale.Current;
        if (!dmStore.ThreadsLoaded && !dmStore.LoadingThreads)
        {
            dmStore.RefreshThreads();
        }

        var title = store.Me is { } me && me.Handle.Length > 0 ? me.Handle : Loc.T(L.Aethergram.InboxTitle);
        DrawScreenHeader(area, title, 1);
        if (DrawHeaderIcon(ImGui.GetWindowDrawList(), SocialChrome.HeaderSlot(area, 0), PhoneIcons.Edit,
                Loc.T(L.Aethergram.NewMessage)))
        {
            OpenNewMessage();
        }

        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var threads = dmStore.Threads;
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            var searchOrigin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            DrawInboxSearchPill(new Rect(new Vector2(searchOrigin.X + CellPadX * scale, searchOrigin.Y),
                new Vector2(searchOrigin.X + width - CellPadX * scale, searchOrigin.Y + InboxSearchHeight * scale)),
                "##aethergramInboxSearch", Loc.T(L.Common.Search), ref inboxDraft);
            DrawInboxHeading(dmStore.RequestCount);
            RefreshInboxFilter(threads, false);
            if (!inboxShowRequests && inboxFilterQuery.Length == 0 && configuration.AethergramArchivedThreads.Count > 0)
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

            if (dmStore.LoadingMoreThreads)
            {
                InfiniteScroll.DrawLoadingRow(listRect.Center.X, Ink.MutedInk);
            }
            else if (dmStore.HasMoreThreads && InfiniteScroll.ReachedBottom())
            {
                dmStore.LoadMoreThreads();
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private static void DrawInboxSearchPill(Rect bar, string id, string hint, ref string draft)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        SearchField.Draw(bar, id, hint, ref draft, AppPalettes.Aethergram);
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
        var heading = Loc.T(inboxShowRequests ? L.Aethergram.Requests : L.Aethergram.InboxTitle);
        var link = inboxShowRequests ? Loc.T(L.Aethergram.InboxTitle) : RequestsLinkLabel(requestCount);
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
            return Loc.T(L.Aethergram.Requests);
        }

        if (inboxRequestsLabelCount != requestCount)
        {
            inboxRequestsLabelCount = requestCount;
            inboxRequestsLabel = Loc.T(L.Aethergram.RequestsCount, requestCount);
        }

        return inboxRequestsLabel;
    }

    private void RefreshInboxFilter(GramThreadDto[] threads, bool archivedView)
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

            if (configuration.AethergramArchivedThreads.Contains(thread.OtherUserId) != archivedView)
            {
                continue;
            }

            if (query.Length > 0 && !InboxRowMatches(thread, query))
            {
                continue;
            }

            if (!archivedView && configuration.AethergramPinnedThreads.Contains(thread.OtherUserId))
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
        var count = configuration.AethergramArchivedThreads.Count;
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
            router.Push(AethergramRoute.InboxArchived);
        }

        FeedCell.End(drawList, cell, Ink.Hairline, false);
    }

    private void DrawInboxArchived(Rect area)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Social.ArchivedChats));
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var threads = dmStore.Threads;
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

    private static bool InboxRowMatches(GramThreadDto thread, string query) =>
        thread.OtherDisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
        || thread.OtherHandle.Contains(query, StringComparison.OrdinalIgnoreCase);

    private void DrawInboxEmptyState(Rect listRect, int totalThreads)
    {
        var area = new Rect(new Vector2(listRect.Min.X, ImGui.GetCursorScreenPos().Y), listRect.Max);
        if (dmStore.LoadingThreads && totalThreads == 0)
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
            DrawEmptyState(area, Loc.T(L.Aethergram.RequestsEmpty), string.Empty);
            return;
        }

        DrawEmptyState(area, Loc.T(L.Aethergram.InboxEmpty), Loc.T(L.Aethergram.InboxEmptyHint));
    }

    private void DrawInboxRow(GramThreadDto thread, bool pinned)
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
        AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent,
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
        var preview = InboxPreview(thread, out var stampLength);
        if (!inboxPreviewFits.TryGetValue(thread.OtherUserId, out var previewFit)
            || !RichLine.Valid(previewFit, preview, textWidth, previewStyle))
        {
            previewFit = RichLine.Fit(preview, stampLength, textWidth, previewStyle);
            inboxPreviewFits[thread.OtherUserId] = previewFit;
        }

        RichLine.Draw(drawList, previewFit, new Vector2(textLeft, blockTop + nameHeight + 3f * scale),
            unread ? Ink.TitleInk : Ink.MutedInk);
        if (unread)
        {
            SocialChrome.DrawUnreadDot(drawList,
                new Vector2(origin.X + width - pad - InboxUnreadDotInset * scale, avatarCenter.Y), Ink);
        }

        if (cell.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            OpenInboxRowSheet(thread);
        }
        else if (cell.Tapped)
        {
            OpenThread(thread.OtherUserId);
        }

        FeedCell.End(drawList, cell, Ink.Hairline, false);
    }

    private string InboxPreview(GramThreadDto thread, out int stampLength)
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
            ? Loc.T(L.Aethergram.ThreadEmpty)
            : ChatText.ListPreview(thread.LastMessagePreview);
        var mine = thread.LastMessagePreview.Length > 0
            && string.Equals(thread.LastMessageSenderId, dmStore.MyUserId, StringComparison.Ordinal);
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

        inboxPreviews[thread.OtherUserId] = new InboxPreviewLine(thread.LastMessageAtUnix, thread.LastMessagePreview,
            thread.LastMessageSenderId, minute, line, stampLength);
        return line;
    }

    private void OpenInboxRowSheet(GramThreadDto thread)
    {
        inboxSheetThreadId = thread.OtherUserId;
        inboxSheetTitle = SocialIdentity.Name(thread.OtherDisplayName, thread.OtherHandle);
        var pinned = configuration.AethergramPinnedThreads.Contains(thread.OtherUserId);
        var archived = configuration.AethergramArchivedThreads.Contains(thread.OtherUserId);
        inboxRowSheetItems[0] = new ActionSheet.Item(Loc.T(pinned ? L.Common.Unpin : L.Common.Pin),
            pinned ? PhoneIcons.PinFilled : PhoneIcons.Pin);
        inboxRowSheetItems[1] = new ActionSheet.Item(
            Loc.T(archived ? L.Social.UnarchiveAction : L.Social.ArchiveAction), PhoneIcons.Archive);
        inboxRowSheetItems[2] = new ActionSheet.Item(Loc.T(L.Aethergram.DeleteConversation), PhoneIcons.Trash, true);
        inboxRowSheet.Open();
    }

    private void DrawInboxRowSheet(Rect screen)
    {
        if (!inboxRowSheet.CapturesPointer)
        {
            return;
        }

        if (inboxRowSheet.IsOpen
            && router.Current.Screen is not (AethergramScreen.Inbox or AethergramScreen.InboxArchived))
        {
            inboxRowSheet.Close();
        }

        var picked = inboxRowSheet.Draw(screen, ActionSheetStyle.From(ui), inboxRowSheetItems,
            Loc.T(L.Common.Cancel), false, inboxSheetTitle);
        if (picked < 0 || inboxSheetThreadId is not { } otherId)
        {
            return;
        }

        switch (picked)
        {
            case 0:
                ToggleInboxPinned(otherId);
                break;
            case 1:
                ToggleInboxArchived(otherId);
                break;
            case 2:
                AskDeleteConversation(otherId);
                break;
        }
    }

    private void ToggleInboxPinned(string otherId)
    {
        var pinned = configuration.AethergramPinnedThreads;
        if (!pinned.Remove(otherId))
        {
            if (pinned.Count >= MaxPinnedChats)
            {
                toast.Show(Loc.T(L.Social.PinChatLimit, MaxPinnedChats));
                return;
            }

            pinned.Add(otherId);
            configuration.AethergramArchivedThreads.Remove(otherId);
        }

        configuration.Save();
        inboxFilterDirty = true;
    }

    private void ToggleInboxArchived(string otherId)
    {
        var archived = configuration.AethergramArchivedThreads;
        if (!archived.Remove(otherId))
        {
            archived.Add(otherId);
            configuration.AethergramPinnedThreads.Remove(otherId);
        }

        configuration.Save();
        inboxFilterDirty = true;
    }

    private void AskDeleteConversation(string otherId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Aethergram.DeleteConversation),
            Message = Loc.T(L.Aethergram.DeleteConversationMessage),
            ConfirmLabel = Loc.T(L.Aethergram.DeleteConfirm),
            CancelLabel = Loc.T(L.Aethergram.DeleteCancel),
            Sheet = true,
            Danger = true,
            Confirm = () => DeleteConversation(otherId),
        });
    }

    private void DeleteConversation(string otherId)
    {
        var current = router.Current;
        var threadOpen = current.Screen == AethergramScreen.Thread && current.Id == otherId;
        if (configuration.AethergramPinnedThreads.Remove(otherId) | configuration.AethergramArchivedThreads.Remove(otherId))
        {
            configuration.Save();
            inboxFilterDirty = true;
        }

        dmStore.DeleteThread(otherId);
        if (threadOpen)
        {
            router.Pop();
        }
    }

    private void OpenInbox()
    {
        inboxDraft = string.Empty;
        inboxShowRequests = false;
        router.Push(AethergramRoute.Inbox);
    }

    private void OpenThread(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        router.Push(AethergramRoute.Thread(userId));
    }
}
