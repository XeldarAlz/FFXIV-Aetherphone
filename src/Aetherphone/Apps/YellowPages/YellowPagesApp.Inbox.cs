using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.YellowPages;

internal readonly record struct InboxPreviewLine(string Source, string? MessageId, string Language, string Line);

internal sealed partial class YellowPagesApp
{
    private const float InboxRowHeight = 76f;
    private const float InboxAvatarRadius = 26f;
    private const float InboxThumbSide = 22f;
    private const float InboxTextGap = 12f;
    private const float InboxSearchHeight = 52f;
    private const float InboxArchivedRowHeight = 52f;
    private const float InboxPinGlyph = 14f;
    private const float InboxUnreadDotInset = 8f;
    private const int MaxPinnedInquiries = 3;
    private const int InboxFilterAll = 0;
    private const int InboxFilterMine = 1;
    private const int InboxFilterAsked = 2;

    private static readonly TextStyle InboxNameStyle = TextStyles.BodyEmphasized;
    private static readonly TextStyle InboxNameUnreadStyle = TextStyles.Headline;
    private static readonly TextStyle InboxAdStyle = TextStyles.Caption1;
    private static readonly TextStyle InboxPreviewStyle = TextStyles.Footnote;
    private static readonly TextStyle InboxPreviewUnreadStyle = TextStyles.FootnoteEmphasized;

    private readonly ActionSheet.Item[] inboxSheetItems = new ActionSheet.Item[3];
    private readonly Dictionary<string, InboxPreviewLine> inboxPreviews = new(StringComparer.Ordinal);
    private readonly List<AdInquiryDto> inboxPinned = new();
    private readonly List<AdInquiryDto> inboxFiltered = new();
    private readonly string[] inboxFilterLabels = new string[3];
    private readonly bool[] inboxFilterActive = new bool[3];
    private readonly ChipRail inboxRail = new();
    private AdInquiryDto[] inboxFilterSource = Array.Empty<AdInquiryDto>();
    private string inboxFilterQuery = string.Empty;
    private int inboxFilterKind;
    private bool inboxFilterArchived;
    private bool inboxFilterDirty = true;
    private string inboxDraft = string.Empty;
    private int inboxFilter;
    private string inboxArchivedCountLabel = string.Empty;
    private int inboxArchivedCountValue = -1;
    private string? inboxSheetThreadId;
    private string inboxSheetTitle = string.Empty;

    private void DrawInbox(Rect area)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        if (!inquiries.ThreadsLoaded && !inquiries.LoadingThreads)
        {
            inquiries.RefreshThreads();
        }

        DrawScreenHeader(area, Loc.T(L.YellowPages.InquiriesTitle), 0, false, false);
        if (inquiries.LoadingThreads)
        {
            LoadingPulse.Spinner(SocialChrome.HeaderSlot(area, 0), 8f * scale, Ink.Accent);
        }

        DrawHairline(drawList, area.Min.X, area.Max.X, area.Min.Y + AppHeader.Height * scale);
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var threads = inquiries.Threads;
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            DrawInboxSearch(scale);
            DrawInboxFilterRail(scale);
            RefreshInboxFilter(threads, false);
            if (inboxFilterQuery.Length == 0 && configuration.YellowPagesArchivedInquiries.Count > 0)
            {
                DrawInboxArchivedRow(scale);
            }

            if (inboxPinned.Count == 0 && inboxFiltered.Count == 0)
            {
                DrawInboxEmpty(listRect, threads.Length);
            }
            else
            {
                for (var index = 0; index < inboxPinned.Count; index++)
                {
                    DrawInboxRow(inboxPinned[index], true, scale);
                }

                for (var index = 0; index < inboxFiltered.Count; index++)
                {
                    DrawInboxRow(inboxFiltered[index], false, scale);
                }
            }

            if (inquiries.LoadingMoreThreads)
            {
                InfiniteScroll.DrawLoadingRow(listRect.Center.X, Ink.MutedInk);
            }
            else if (inquiries.HasMoreThreads && InfiniteScroll.ReachedBottom())
            {
                inquiries.LoadMoreThreads();
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawInboxSearch(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var top = origin.Y + (InboxSearchHeight - SearchPillHeight) * scale * 0.5f;
        var rect = new Rect(new Vector2(origin.X + pad, top), new Vector2(origin.X + width - pad, top + SearchPillHeight * scale));
        SearchField.Draw(rect, "##yellowPagesInboxSearch", Loc.T(L.YellowPages.InboxSearchHint), ref inboxDraft,
            AppPalettes.YellowPages, 60);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, InboxSearchHeight * scale));
    }

    private void DrawInboxFilterRail(float scale)
    {
        inboxFilterLabels[0] = Loc.T(L.YellowPages.InboxAll);
        inboxFilterLabels[1] = Loc.T(L.YellowPages.InboxMine);
        inboxFilterLabels[2] = Loc.T(L.YellowPages.InboxAsked);
        for (var index = 0; index < inboxFilterActive.Length; index++)
        {
            inboxFilterActive[index] = inboxFilter == index;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var row = new Rect(new Vector2(origin.X + CellPadX * scale, origin.Y),
            new Vector2(origin.X + width - CellPadX * scale, origin.Y + ChipRail.RowHeight * scale));
        var tapped = inboxRail.Draw(row, ui, inboxFilterLabels, inboxFilterActive);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, ChipRail.RowHeight * scale + Metrics.Space.Sm * scale));
        if (tapped >= 0 && tapped != inboxFilter)
        {
            inboxFilter = tapped;
            inboxFilterDirty = true;
        }
    }

    private void RefreshInboxFilter(AdInquiryDto[] threads, bool archivedView)
    {
        var query = inboxDraft.Trim();
        if (!inboxFilterDirty && ReferenceEquals(threads, inboxFilterSource) && inboxFilterKind == inboxFilter
            && inboxFilterArchived == archivedView && string.Equals(query, inboxFilterQuery, StringComparison.Ordinal))
        {
            return;
        }

        inboxFilterDirty = false;
        inboxFilterSource = threads;
        inboxFilterKind = inboxFilter;
        inboxFilterArchived = archivedView;
        inboxFilterQuery = query;
        inboxPinned.Clear();
        inboxFiltered.Clear();
        var archived = configuration.YellowPagesArchivedInquiries;
        var pinned = configuration.YellowPagesPinnedInquiries;
        for (var index = 0; index < threads.Length; index++)
        {
            var thread = threads[index];
            if (!archivedView && inboxFilter == InboxFilterMine && !thread.Mine)
            {
                continue;
            }

            if (!archivedView && inboxFilter == InboxFilterAsked && thread.Mine)
            {
                continue;
            }

            if (archived.Contains(thread.Id) != archivedView)
            {
                continue;
            }

            if (query.Length > 0 && !InboxRowMatches(thread, query))
            {
                continue;
            }

            if (!archivedView && pinned.Contains(thread.Id))
            {
                inboxPinned.Add(thread);
            }
            else
            {
                inboxFiltered.Add(thread);
            }
        }
    }

    private static bool InboxRowMatches(AdInquiryDto thread, string query) =>
        thread.OtherName.Contains(query, StringComparison.OrdinalIgnoreCase)
        || thread.OtherHandle.Contains(query, StringComparison.OrdinalIgnoreCase)
        || thread.AdTitle.Contains(query, StringComparison.OrdinalIgnoreCase);

    private void DrawInboxArchivedRow(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, InboxArchivedRowHeight * scale, Ink.HoverTint);
        var pad = CellPadX * scale;
        var glyphCenter = new Vector2(cell.Bounds.Min.X + pad + InboxAvatarRadius * scale, cell.Bounds.Center.Y);
        PhoneIcon.Draw(drawList, glyphCenter, PhoneIcons.Archive, Ink.MutedInk, 21f * scale);
        var textLeft = glyphCenter.X + InboxAvatarRadius * scale + InboxTextGap * scale;
        var count = configuration.YellowPagesArchivedInquiries.Count;
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
            router.Push(YellowPagesRoute.InboxArchived);
        }

        FeedCell.End(drawList, cell, Ink.Hairline, false);
    }

    private void DrawInboxArchived(Rect area)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Social.ArchivedChats));
        DrawHairline(ImGui.GetWindowDrawList(), area.Min.X, area.Max.X, area.Min.Y + AppHeader.Height * scale);
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var threads = inquiries.Threads;
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
                DrawInboxRow(inboxFiltered[index], false, scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawInboxEmpty(Rect listRect, int totalThreads)
    {
        var area = new Rect(new Vector2(listRect.Min.X, ImGui.GetCursorScreenPos().Y), listRect.Max);
        if (inquiries.LoadingThreads && totalThreads == 0)
        {
            DrawEmptyState(area, Loc.T(L.Common.Loading), string.Empty);
            return;
        }

        if (inboxFilterQuery.Length > 0 || inboxFilter != InboxFilterAll)
        {
            DrawEmptyState(area, Loc.T(L.Social.ListEmpty), string.Empty);
            return;
        }

        DrawEmptyState(area, Loc.T(L.YellowPages.NoInquiriesTitle), Loc.T(L.YellowPages.NoInquiriesHint));
    }

    private void DrawInboxRow(AdInquiryDto thread, bool pinned, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = InboxRowHeight * scale;
        var cell = FeedCell.Begin(drawList, rowHeight, Ink.HoverTint);
        var origin = cell.Bounds.Min;
        var width = cell.Bounds.Width;
        var pad = CellPadX * scale;
        var avatarRadius = InboxAvatarRadius * scale;
        var avatarCenter = new Vector2(origin.X + pad + avatarRadius, origin.Y + rowHeight * 0.5f);
        AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent,
            YellowPagesKit.Monogram(thread.OtherName, thread.OtherHandle), 0.95f,
            images.Avatar(thread.OtherAvatarUrl.Length > 0 ? thread.OtherAvatarUrl : null, avatarRadius * 2f), 40);
        DrawInboxAdBadge(drawList, thread, avatarCenter, avatarRadius, scale);
        var unread = thread.UnreadCount > 0;
        var textLeft = avatarCenter.X + avatarRadius + InboxTextGap * scale;
        var stamp = TimeText.Short(thread.LastMessageAtUnix);
        var stampSize = Typography.Measure(stamp, InboxAdStyle);
        var trailing = stampSize.X + 6f * scale;
        var nameStyle = unread ? InboxNameUnreadStyle : InboxNameStyle;
        var nameHeight = Typography.LineHeight(nameStyle);
        var adHeight = Typography.LineHeight(InboxAdStyle);
        var previewStyle = unread ? InboxPreviewUnreadStyle : InboxPreviewStyle;
        var previewHeight = Typography.LineHeight(previewStyle);
        var blockTop = avatarCenter.Y - (nameHeight + adHeight + previewHeight + 4f * scale) * 0.5f;
        Typography.Draw(drawList, new Vector2(origin.X + width - pad - stampSize.X, blockTop + (nameHeight - stampSize.Y) * 0.5f),
            stamp, Ink.MutedInk, InboxAdStyle);
        if (pinned)
        {
            var glyph = InboxPinGlyph * scale;
            PhoneIcon.Draw(drawList, new Vector2(origin.X + width - pad - trailing - glyph * 0.5f, blockTop + nameHeight * 0.5f),
                PhoneIcons.PinFilled, Ink.MutedInk, glyph);
            trailing += glyph + 6f * scale;
        }

        var textRight = origin.X + width - pad;
        var nameWidth = MathF.Max(1f, textRight - trailing - textLeft);
        var name = SocialIdentity.Name(thread.OtherName, thread.OtherHandle);
        Typography.Draw(drawList, new Vector2(textLeft, blockTop), Typography.FitText(name, nameWidth, nameStyle),
            Ink.TitleInk, nameStyle);
        var adTop = blockTop + nameHeight + 2f * scale;
        var adTitle = thread.AdTitle.Length > 0 ? thread.AdTitle : Loc.T(L.YellowPages.UnavailableTitle);
        var adLine = thread.Mine ? Loc.T(L.YellowPages.InboxAboutMine, adTitle) : adTitle;
        Typography.Draw(drawList, new Vector2(textLeft, adTop), Typography.FitText(adLine, textRight - textLeft, InboxAdStyle),
            Ink.AccentLink, InboxAdStyle);
        var previewTop = adTop + adHeight + 2f * scale;
        var previewWidth = textRight - textLeft - (unread ? 22f * scale : 0f);
        Typography.Draw(drawList, new Vector2(textLeft, previewTop),
            Typography.FitText(InboxPreview(thread), previewWidth, previewStyle), unread ? Ink.TitleInk : Ink.MutedInk,
            previewStyle);
        if (unread)
        {
            SocialChrome.DrawUnreadDot(drawList,
                new Vector2(origin.X + width - pad - InboxUnreadDotInset * scale, previewTop + previewHeight * 0.5f), Ink);
        }

        if (cell.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            OpenInboxRowSheet(thread);
        }
        else if (cell.Tapped)
        {
            OpenThread(thread.Id);
        }

        FeedCell.End(drawList, cell, Ink.Hairline, false);
    }

    private void DrawInboxAdBadge(ImDrawListPtr drawList, AdInquiryDto thread, Vector2 avatarCenter,
        float avatarRadius, float scale)
    {
        var side = InboxThumbSide * scale;
        var center = avatarCenter + new Vector2(avatarRadius * 0.72f, avatarRadius * 0.72f);
        var min = center - new Vector2(side * 0.5f, side * 0.5f);
        var max = center + new Vector2(side * 0.5f, side * 0.5f);
        var rounding = 7f * scale;
        var ring = 2f * scale;
        Squircle.Fill(drawList, min - new Vector2(ring, ring), max + new Vector2(ring, ring), rounding + ring,
            ImGui.GetColorU32(Ink.BackdropTop));
        var texture = string.IsNullOrEmpty(thread.AdMediaUrl) ? null : images.Get(thread.AdMediaUrl);
        if (texture is not null)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            Squircle.FillImage(drawList, min, max, rounding, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
            return;
        }

        var icon = thread.AdTitle.Length == 0 ? Dalamud.Interface.FontAwesomeIcon.Ban : AdCategories.Icon(thread.AdCategory);
        YellowPagesKit.Tile(drawList, min, max, Ink.Accent, icon, rounding, 0.62f);
    }

    private string InboxPreview(AdInquiryDto thread)
    {
        if (inboxPreviews.TryGetValue(thread.Id, out var cached)
            && ReferenceEquals(cached.Source, thread.LastBody)
            && string.Equals(cached.MessageId, thread.LastMessageId, StringComparison.Ordinal)
            && cached.Language == Loc.Culture.Name)
        {
            return cached.Line;
        }

        var body = thread.LastBody.Length == 0 && thread.LastKind == 0
            ? Loc.T(L.Message.DeletedBody)
            : AdInquiryStore.PreviewFor(thread, thread.LastBody);
        if (thread.LastMessageId is null)
        {
            body = Loc.T(L.Message.ThreadEmpty);
        }

        var mine = thread.LastSenderId.Length > 0
            && string.Equals(thread.LastSenderId, inquiries.MyUserId, StringComparison.Ordinal);
        var line = mine ? $"{Loc.T(L.Message.You)}: {body}" : body;
        inboxPreviews[thread.Id] = new InboxPreviewLine(thread.LastBody, thread.LastMessageId, Loc.Culture.Name, line);
        return line;
    }

    private void OpenInboxRowSheet(AdInquiryDto thread)
    {
        inboxSheetThreadId = thread.Id;
        inboxSheetTitle = SocialIdentity.Name(thread.OtherName, thread.OtherHandle);
        var pinned = configuration.YellowPagesPinnedInquiries.Contains(thread.Id);
        var archived = configuration.YellowPagesArchivedInquiries.Contains(thread.Id);
        inboxSheetItems[0] = new ActionSheet.Item(Loc.T(pinned ? L.Common.Unpin : L.Common.Pin),
            pinned ? PhoneIcons.PinFilled : PhoneIcons.Pin);
        inboxSheetItems[1] = new ActionSheet.Item(Loc.T(archived ? L.Social.UnarchiveAction : L.Social.ArchiveAction),
            PhoneIcons.Archive);
        inboxSheetItems[2] = new ActionSheet.Item(Loc.T(L.Message.DeleteConversation), PhoneIcons.Trash, true);
        inboxSheet.Open();
    }

    private void DrawInboxSheet(Rect screen)
    {
        if (!inboxSheet.CapturesPointer)
        {
            return;
        }

        if (inboxSheet.IsOpen && router.Current.Screen is not (YellowPagesScreen.Root or YellowPagesScreen.InboxArchived))
        {
            inboxSheet.Close();
        }

        var picked = inboxSheet.Draw(screen, ActionSheetStyle.From(ui), inboxSheetItems, Loc.T(L.Common.Cancel), false,
            inboxSheetTitle);
        if (picked < 0 || inboxSheetThreadId is not { } threadId)
        {
            return;
        }

        switch (picked)
        {
            case 0:
                ToggleInboxPinned(threadId);
                break;
            case 1:
                ToggleInboxArchived(threadId);
                break;
            case 2:
                AskDeleteConversation(threadId);
                break;
        }
    }

    private void ToggleInboxPinned(string threadId)
    {
        var pinned = configuration.YellowPagesPinnedInquiries;
        if (!pinned.Remove(threadId))
        {
            if (pinned.Count >= MaxPinnedInquiries)
            {
                ShellToast.Show(Loc.T(L.Social.PinChatLimit, MaxPinnedInquiries));
                return;
            }

            pinned.Add(threadId);
            configuration.YellowPagesArchivedInquiries.Remove(threadId);
        }

        configuration.Save();
        inboxFilterDirty = true;
    }

    private void ToggleInboxArchived(string threadId)
    {
        var archived = configuration.YellowPagesArchivedInquiries;
        if (!archived.Remove(threadId))
        {
            archived.Add(threadId);
            configuration.YellowPagesPinnedInquiries.Remove(threadId);
        }

        configuration.Save();
        inboxFilterDirty = true;
    }

    private void AskDeleteConversation(string threadId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Message.DeleteConversation),
            Message = Loc.T(L.Message.DeleteConversationMessage),
            ConfirmLabel = Loc.T(L.YellowPages.DeleteAd),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Danger = true,
            Confirm = () => DeleteConversation(threadId),
        });
    }

    private void DeleteConversation(string threadId)
    {
        var current = router.Current;
        var threadOpen = current.Screen == YellowPagesScreen.Thread && current.Id == threadId;
        if (configuration.YellowPagesPinnedInquiries.Remove(threadId)
            | configuration.YellowPagesArchivedInquiries.Remove(threadId))
        {
            configuration.Save();
            inboxFilterDirty = true;
        }

        inquiries.DeleteThread(threadId);
        if (threadOpen)
        {
            router.Pop();
        }
    }

    private void OpenThread(string inquiryId, bool animate = true)
    {
        router.Push(YellowPagesRoute.Thread(inquiryId), animate);
    }
}
