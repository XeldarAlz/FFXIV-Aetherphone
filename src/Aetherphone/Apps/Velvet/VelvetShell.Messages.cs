using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float MessagesSearchHeight = 52f;
    private const float MessagesSearchRevealSeconds = 0.12f;
    private const float MessagesHeadingHeight = 44f;

    private const int IntroLimit = 140;
    private const float HeroAvatarRadius = 38f;
    private const float HeroHaloSpread = 13f;
    private const float HeroHaloAlpha = 0.10f;
    private const int HeroHaloSegments = 48;
    private const float HeroTextInset = 40f;
    private const float HeroNameGap = 16f;
    private const float HeroHandleGap = 4f;
    private const float IntroTopGap = 14f;
    private const float IntroHeroGap = 22f;
    private const float IntroHeaderGap = 12f;
    private const float IntroFieldHeight = 118f;
    private const float IntroFieldPad = 10f;
    private const float IntroWellAlpha = 0.55f;
    private const float IntroHintGap = 10f;
    private const float IntroSendGap = 18f;
    private const float IntroSendHeight = 48f;
    private const float IntroBottomGap = 32f;
    private const float RequestTopGap = 18f;
    private const float RequestMetaGap = 10f;
    private const float RequestActionGap = 22f;
    private const float RequestActionHeight = 48f;
    private const float RequestSecondaryHeight = 44f;
    private const float RequestSecondaryGap = 10f;
    private const float RequestBottomGap = 32f;

    private static readonly TextStyle MessagesHeadingStyle = TextStyles.Title3;
    private static readonly TextStyle MessagesLinkStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle HeroNameStyle = TextStyles.Title2;
    private static readonly TextStyle HeroHandleStyle = TextStyles.Subheadline;
    private static readonly TextStyle IntroSendStyle = TextStyles.SubheadlineEmphasized;

    private readonly List<VelvetThreadDto> chatsFiltered = new();
    private VelvetThreadDto[] chatsFilterSource = Array.Empty<VelvetThreadDto>();
    private string chatsFilterQuery = string.Empty;
    private string chatsDraft = string.Empty;
    private bool chatsSearchOpen;
    private bool chatsSearchFocus;
    private Spring chatsSearchReveal = new(0f);
    private string introName = string.Empty;
    private string introHandle = string.Empty;
    private string? introAvatarUrl;
    private string introText = string.Empty;
    private string introPrompt = string.Empty;
    private LanguageInfo? introPromptLanguage;
    private string introCounter = string.Empty;
    private int introCounterLength = -1;
    private string requestLabelId = string.Empty;
    private string requestName = string.Empty;
    private string requestHandle = string.Empty;
    private string requestNote = string.Empty;

    private void DrawMessages(Rect area)
    {
        var scale = UiScale.Current;
        var pad = SocialChrome.CellPadX * scale;
        using (AppSurface.BeginEdgeToEdge(area))
        {
            var width = ScrollLayout.StableContentWidth();
            DrawMessagesHeading(width, pad);
            if (messagesTab == VelvetMessagesTab.Chats)
            {
                DrawChatsSearchRow(width, pad, scale);
            }

            if (messagesTab == VelvetMessagesTab.Chats)
            {
                DrawChatsList(area);
            }
            else
            {
                DrawRequestsList(area);
            }
        }
    }

    private void DrawMessagesHeading(float width, float pad)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var height = MessagesHeadingHeight * scale;
        var centerY = origin.Y + height * 0.5f;
        var showingRequests = messagesTab == VelvetMessagesTab.Requests;
        var heading = Loc.T(showingRequests ? L.Velvet.Requests : L.Velvet.ChatsTab);
        var requestCount = store.RequestCount;
        var link = showingRequests
            ? Loc.T(L.Velvet.ChatsTab)
            : requestCount > 0 ? Loc.T(L.Velvet.RequestsCount, requestCount) : Loc.T(L.Velvet.Requests);
        var linkSize = Typography.Measure(link, MessagesLinkStyle);
        var linkMin = new Vector2(origin.X + width - pad - linkSize.X, centerY - linkSize.Y * 0.5f);
        var linkMax = new Vector2(origin.X + width - pad, centerY + linkSize.Y * 0.5f);
        var headingHeight = Typography.LineHeight(MessagesHeadingStyle);
        Typography.Draw(drawList, new Vector2(origin.X + pad, centerY - headingHeight * 0.5f),
            Typography.FitText(heading, MathF.Max(1f, linkMin.X - 12f * scale - origin.X - pad),
                MessagesHeadingStyle), VelvetTheme.TitleInk, MessagesHeadingStyle);
        var hovered = UiInteract.Hover(linkMin, linkMax);
        Typography.Draw(drawList, linkMin, link, VelvetTheme.RoseInk, MessagesLinkStyle);
        if (hovered)
        {
            drawList.AddLine(new Vector2(linkMin.X, linkMax.Y), linkMax, ImGui.GetColorU32(VelvetTheme.RoseInk), 1f);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(linkMin, linkMax, hovered))
        {
            messagesTab = showingRequests ? VelvetMessagesTab.Chats : VelvetMessagesTab.Requests;
            CloseChatsSearch();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawChatsSearchRow(float width, float pad, float scale)
    {
        var target = chatsSearchOpen ? 1f : 0f;
        var frameSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var reveal = chatsSearchReveal.Step(target, MessagesSearchRevealSeconds, frameSeconds);
        if (chatsSearchReveal.IsResting(target, 0.005f, 0.05f))
        {
            chatsSearchReveal.SnapTo(target);
            reveal = target;
        }

        var rowHeight = MessagesSearchHeight * scale;
        var height = rowHeight * Math.Clamp(reveal, 0f, 1f);
        if (height < 1f)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var bottom = origin.Y + height;
        drawList.PushClipRect(origin, new Vector2(origin.X + width, bottom), true);
        SearchField.Draw(new Rect(new Vector2(origin.X + pad, bottom - rowHeight),
                new Vector2(origin.X + width - pad, bottom)), "##velvetChatSearch", Loc.T(L.Common.Search),
            ref chatsDraft, VelvetTheme.Palette, focus: chatsSearchFocus);
        chatsSearchFocus = false;
        drawList.PopClipRect();
        ImGui.SetCursorScreenPos(origin);
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
        {
            ImGui.Dummy(new Vector2(width, height));
        }
    }

    private void ToggleChatsSearch()
    {
        if (chatsSearchOpen)
        {
            CloseChatsSearch();
            return;
        }

        chatsSearchOpen = true;
        chatsSearchFocus = true;
    }

    private void CloseChatsSearch()
    {
        chatsSearchOpen = false;
        chatsSearchFocus = false;
        chatsDraft = string.Empty;
    }

    private void ResetChatsSearch()
    {
        CloseChatsSearch();
        chatsSearchReveal.SnapTo(0f);
    }

    private void RefreshChatsFilter(VelvetThreadDto[] threads)
    {
        var query = chatsDraft.Trim();
        if (ReferenceEquals(threads, chatsFilterSource) &&
            string.Equals(query, chatsFilterQuery, StringComparison.Ordinal))
        {
            return;
        }

        chatsFilterSource = threads;
        chatsFilterQuery = query;
        chatsFiltered.Clear();
        for (var index = 0; index < threads.Length; index++)
        {
            var thread = threads[index];
            if (query.Length == 0 || ChatRowMatches(thread, query))
            {
                chatsFiltered.Add(thread);
            }
        }
    }

    private static bool ChatRowMatches(VelvetThreadDto thread, string query) =>
        thread.OtherDisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
        || thread.OtherHandle.Contains(query, StringComparison.OrdinalIgnoreCase);

    private void DrawChatsList(Rect listRect)
    {
        var scale = UiScale.Current;
        if (!store.ThreadsLoaded && !store.LoadingThreads)
        {
            store.RefreshThreads();
        }

        if (!store.ConnectionsLoaded && !store.LoadingConnections)
        {
            store.RefreshConnections();
        }

        var threads = store.Threads;
        RefreshChatsFilter(threads);
        if (chatsFiltered.Count == 0)
        {
            var empty = new Rect(new Vector2(listRect.Min.X, ImGui.GetCursorScreenPos().Y), listRect.Max);
            if (threads.Length > 0)
            {
                DrawEmpty(empty, Loc.T(L.Social.ListEmpty), string.Empty);
            }
            else
            {
                DrawEmpty(empty, Loc.T(L.Velvet.MessagesEmpty), Loc.T(L.Velvet.MessagesEmptyHint));
            }

            return;
        }

        Gap(2f);
        for (var index = 0; index < chatsFiltered.Count; index++)
        {
            var thread = chatsFiltered[index];
            var preview = string.IsNullOrEmpty(thread.LastMessagePreview)
                ? Loc.T(L.Velvet.ThreadEmpty)
                : ChatText.ListPreview(thread.LastMessagePreview);
            var model = new VRowModel
            {
                Title = DisplayNameOf(thread.OtherDisplayName, thread.OtherHandle),
                Subtitle = preview,
                Height = 64f,
                Leading = VRowLeading.Avatar,
                AvatarRadius = 22f,
                Name = DisplayNameOf(thread.OtherDisplayName, thread.OtherHandle),
                World = string.Empty,
                AvatarUrl = thread.OtherAvatarUrl,
                Presence = thread.Presence,
                Time = TimeText.Short(thread.LastMessageAtUnix),
                Badge = thread.UnreadCount,
            };
            var hit = VRow.Cell(in model, ui, theme, images, lodestone);
            if (hit == VRowHit.Body)
            {
                OpenThread(thread.OtherUserId);
            }
            else if (hit == VRowHit.Overflow)
            {
                OpenThreadSheet(thread.OtherUserId);
            }
        }

        if (store.LoadingMoreThreads)
        {
            InfiniteScroll.DrawLoadingRow(listRect.Center.X, VelvetTheme.MutedInk);
        }
        else if (store.HasMoreThreads && InfiniteScroll.ReachedBottom())
        {
            store.LoadMoreThreads();
        }

        Gap(40f);
    }

    private void DrawRequestsList(Rect listRect)
    {
        var scale = UiScale.Current;
        if (!store.RequestsLoaded && !store.LoadingRequests)
        {
            store.RefreshRequests();
        }

        if (!store.SentRequestsLoaded && !store.LoadingSentRequests)
        {
            store.RefreshSentRequests();
        }

        var requests = store.Requests;
        var sent = store.SentRequests;
        if (requests.Length == 0 && sent.Length == 0)
        {
            DrawEmpty(new Rect(new Vector2(listRect.Min.X, ImGui.GetCursorScreenPos().Y), listRect.Max),
                Loc.T(L.Velvet.RequestsEmpty), Loc.T(L.Velvet.RequestsEmptyHint));
            return;
        }

        Gap(4f);
        if (requests.Length > 0)
        {
            VSectionHeader.Overline(Loc.T(L.Velvet.Requests), requests.Length.ToString(Loc.Culture),
                FeedCell.PadX * scale);
            for (var index = 0; index < requests.Length; index++)
            {
                DrawRequestRow(requests[index]);
            }
        }

        if (sent.Length > 0)
        {
            Gap(14f);
            VSectionHeader.Overline(Loc.T(L.Velvet.SentRequests), sent.Length.ToString(Loc.Culture),
                FeedCell.PadX * scale);
            for (var index = 0; index < sent.Length; index++)
            {
                var request = sent[index];
                var model = new VRowModel
                {
                    Title = DisplayNameOf(request.DisplayName, request.Handle),
                    Subtitle = "@" + request.Handle,
                    Height = 60f,
                    Leading = VRowLeading.Avatar,
                    AvatarRadius = 20f,
                    Name = DisplayNameOf(request.DisplayName, request.Handle),
                    AvatarUrl = request.AvatarUrl,
                    Pill = Loc.T(L.Velvet.Requested),
                    PillFilled = false,
                    PillEnabled = true,
                };
                var hit = VRow.Cell(in model, ui, theme, images, lodestone);
                if (hit == VRowHit.Pill)
                {
                    store.CancelRequest(request.UserId);
                }
                else if (hit == VRowHit.Body)
                {
                    OpenThread(request.UserId);
                }
            }
        }

        Gap(40f);
    }

    private void DrawRequestRow(VelvetConnectionDto request)
    {
        var model = new VRowModel
        {
            Title = DisplayNameOf(request.DisplayName, request.Handle),
            Subtitle = IntroLineOf(request),
            Height = 64f,
            Leading = VRowLeading.Avatar,
            AvatarRadius = 22f,
            Name = DisplayNameOf(request.DisplayName, request.Handle),
            AvatarUrl = request.AvatarUrl,
            Chevron = true,
        };
        if (VRow.Cell(in model, ui, theme, images, lodestone) == VRowHit.Body)
        {
            OpenRequest(request.UserId);
        }
    }

    private void OpenThreadSheet(string otherId)
    {
        sheetThreadId = otherId;
        threadSheetItems[0] = new ActionSheet.Item(Loc.T(L.Velvet.DeleteConversation), string.Empty, true);
        threadSheet.Open();
    }

    private void DrawThreadSheet(Rect screen)
    {
        if (!threadSheet.CapturesPointer)
        {
            return;
        }

        var picked = threadSheet.Draw(screen, ActionSheetStyle.From(ui), threadSheetItems, Loc.T(L.Common.Cancel),
            false);
        if (picked == 0 && sheetThreadId is { } otherId)
        {
            AskDeleteConversation(otherId);
        }
    }

    private void AskDeleteConversation(string otherId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Velvet.DeleteConversation),
            Message = Loc.T(L.Velvet.DeleteConversationMessage),
            ConfirmLabel = Loc.T(L.Velvet.DeleteConfirm),
            CancelLabel = Loc.T(L.Velvet.DeleteCancel),
            Sheet = true,
            Danger = true,
            Confirm = () => DeleteConversation(otherId),
        });
    }

    private void DeleteConversation(string otherId)
    {
        var current = router.Current;
        var threadOpen = current.Screen == VelvetScreenId.Thread && current.Arg == otherId;
        store.DeleteThread(otherId);
        if (threadOpen)
        {
            router.Pop();
        }
    }

    private void OpenRequest(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        requestLabelId = string.Empty;
        store.OpenProfile(userId);
        router.Push(VelvetView.RequestDetail(userId));
    }

    private VelvetConnectionDto? FindRequest(string userId)
    {
        var requests = store.Requests;
        for (var index = 0; index < requests.Length; index++)
        {
            if (requests[index].UserId == userId)
            {
                return requests[index];
            }
        }

        return null;
    }

    private void EnsureRequestLabels(VelvetConnectionDto request)
    {
        if (requestLabelId == request.UserId)
        {
            return;
        }

        requestLabelId = request.UserId;
        requestName = DisplayNameOf(request.DisplayName, request.Handle);
        requestHandle = string.IsNullOrWhiteSpace(request.DisplayName) || request.Handle.Length == 0
            ? string.Empty
            : "@" + request.Handle;
        requestNote = request.Intro.Trim();
    }

    private void DrawRequestDetail(Rect area, string userId)
    {
        var request = FindRequest(userId);
        if (request is { } known)
        {
            EnsureRequestLabels(known);
        }

        if (VHeader.Push(area, request is null ? Loc.T(L.Velvet.Requests) : requestName))
        {
            router.Pop();
            return;
        }

        if (request is not { } req)
        {
            router.Pop();
            return;
        }

        var scale = UiScale.Current;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        var profile = store.ProfileUserId == userId ? store.ProfileUser : null;
        using (AppSurface.BeginEdgeToEdge(body))
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
        {
            var drawList = ImGui.GetWindowDrawList();
            var width = ScrollLayout.StableContentWidth();
            var inset = SocialChrome.CellPadX * scale;
            var contentWidth = MathF.Max(1f, width - inset * 2f);
            Gap(RequestTopGap);
            DrawPersonHero(drawList, userId, requestName, requestHandle, req.AvatarUrl, width, scale);
            Gap(RequestMetaGap);
            DrawRequestMeta(drawList, width, scale);
            ImGui.Indent(inset);
            DrawRequestNote(drawList, contentWidth, scale);
            if (profile is not null)
            {
                EnsureFit(profile);
                DrawDeckFit(contentWidth);
                DrawIntroCard(profile, contentWidth);
            }

            ImGui.Unindent(inset);
            Gap(RequestActionGap);
            DrawRequestActions(drawList, userId, width, inset, contentWidth, scale);
            Gap(RequestBottomGap);
        }
    }

    private static void DrawRequestMeta(ImDrawListPtr drawList, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var textWidth = MathF.Max(1f, width - HeroTextInset * 2f * scale);
        var bottom = Typography.DrawWrappedCentered(drawList, Loc.T(L.Velvet.WantsToConnect), TextStyles.Subheadline,
            VelvetTheme.MutedInk, new Vector2(origin.X + width * 0.5f, origin.Y), textWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, bottom - origin.Y));
    }

    private void DrawRequestNote(ImDrawListPtr drawList, float width, float scale)
    {
        if (requestNote.Length == 0)
        {
            return;
        }

        var textWidth = MathF.Max(1f, width - VCard.Pad * 2f * scale);
        var textHeight = Typography.MeasureWrappedBlock(requestNote, TextStyles.Body, textWidth).Y;
        Gap(VCard.Gap);
        var card = VCard.Begin(drawList, width, VCard.HeaderBlock * scale + textHeight, scale);
        VCard.Header(drawList, card.ContentOrigin, card.ContentWidth, PhoneIcons.Quote, VelvetTheme.Rose,
            Loc.T(L.Velvet.TheirIntro), scale);
        Typography.DrawWrappedLeft(new Vector2(card.ContentOrigin.X, card.ContentOrigin.Y + VCard.HeaderBlock * scale),
            requestNote, VelvetTheme.BodyInk, TextStyles.Body, card.ContentWidth);
        VCard.End(card);
    }

    private void DrawRequestActions(ImDrawListPtr drawList, string userId, float width, float inset,
        float contentWidth, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var accept = new Rect(new Vector2(origin.X + inset, origin.Y),
            new Vector2(origin.X + inset + contentWidth, origin.Y + RequestActionHeight * scale));
        if (SocialPill.Accent(drawList, accept, Loc.T(L.Velvet.Accept), VelvetInk.Shared,
                TextStyles.SubheadlineEmphasized, accept.Height * 0.5f))
        {
            store.AcceptRequest(userId);
            router.Pop(false);
            OpenThread(userId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, RequestActionHeight * scale));
        Gap(RequestSecondaryGap);
        var secondary = ImGui.GetCursorScreenPos();
        var half = (contentWidth - RequestSecondaryGap * scale) * 0.5f;
        var bottom = secondary.Y + RequestSecondaryHeight * scale;
        var decline = new Rect(new Vector2(secondary.X + inset, secondary.Y),
            new Vector2(secondary.X + inset + half, bottom));
        var view = new Rect(new Vector2(decline.Max.X + RequestSecondaryGap * scale, secondary.Y),
            new Vector2(decline.Max.X + RequestSecondaryGap * scale + half, bottom));
        var ink = VelvetInk.Shared;
        if (SocialPill.Flat(drawList, decline, Loc.T(L.Phone.Decline), ink.ButtonFill, ink.ButtonHover,
                VelvetTheme.Hairline, VelvetTheme.TitleInk, TextStyles.SubheadlineEmphasized,
                decline.Height * 0.5f))
        {
            store.DeclineRequest(userId);
            router.Pop();
        }

        if (SocialPill.Flat(drawList, view, Loc.T(L.Social.ViewProfile), ink.ButtonFill, ink.ButtonHover,
                VelvetTheme.Hairline, VelvetTheme.TitleInk, TextStyles.SubheadlineEmphasized,
                view.Height * 0.5f))
        {
            OpenProfile(userId);
        }

        ImGui.SetCursorScreenPos(secondary);
        ImGui.Dummy(new Vector2(width, RequestSecondaryHeight * scale));
    }

    private void RequestIntro(string userId, string displayName, string handle, string? avatarUrl)
    {
        introName = DisplayNameOf(displayName, handle);
        introHandle = string.IsNullOrWhiteSpace(displayName) || handle.Length == 0 ? string.Empty : "@" + handle;
        introAvatarUrl = avatarUrl;
        introText = string.Empty;
        introPrompt = string.Empty;
        introCounterLength = -1;
        router.Push(VelvetView.Intro(userId));
    }

    private void DrawIntro(Rect area, string userId)
    {
        var scale = UiScale.Current;
        if (VHeader.Push(area, Loc.T(L.Velvet.IntroTitle)))
        {
            router.Pop();
            return;
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(body))
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
        {
            var drawList = ImGui.GetWindowDrawList();
            var width = ScrollLayout.StableContentWidth();
            var inset = SocialChrome.CellPadX * scale;
            var contentWidth = MathF.Max(1f, width - inset * 2f);
            Gap(IntroTopGap);
            DrawPersonHero(drawList, userId, introName, introHandle, introAvatarUrl, width, scale);
            Gap(IntroHeroGap);
            DrawIntroCard(drawList, width, inset, contentWidth, scale);
            Gap(IntroHintGap);
            DrawInsetHelpText(Loc.T(L.Velvet.IntroSheetHint));
            Gap(IntroSendGap);
            var sendOrigin = ImGui.GetCursorScreenPos();
            var send = new Rect(new Vector2(sendOrigin.X + inset, sendOrigin.Y),
                new Vector2(sendOrigin.X + inset + contentWidth, sendOrigin.Y + IntroSendHeight * scale));
            if (SocialPill.Accent(drawList, send, Loc.T(L.Velvet.SendIntro), VelvetInk.Shared, IntroSendStyle,
                    send.Height * 0.5f, !string.IsNullOrWhiteSpace(introText) && !store.IntroBusy))
            {
                SendIntro(userId);
            }

            ImGui.SetCursorScreenPos(sendOrigin);
            ImGui.Dummy(new Vector2(width, IntroSendHeight * scale));
            Gap(IntroBottomGap);
        }
    }

    private void DrawPersonHero(ImDrawListPtr drawList, string userId, string name, string handle, string? avatarUrl,
        float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var centerX = origin.X + width * 0.5f;
        var radius = HeroAvatarRadius * scale;
        var center = new Vector2(centerX, origin.Y + radius);
        drawList.AddCircleFilled(center, radius + HeroHaloSpread * scale,
            VelvetTheme.Alpha(VelvetTheme.RoseGlow, HeroHaloAlpha).Packed(), HeroHaloSegments);
        VAvatar.Draw(drawList, center, radius, theme, name, string.Empty, avatarUrl, images, lodestone, -1,
            VelvetTheme.Rose);
        var textWidth = MathF.Max(1f, width - HeroTextInset * 2f * scale);
        var bottom = Typography.DrawWrappedCentered(drawList, name, HeroNameStyle, VelvetTheme.TitleInk,
            new Vector2(centerX, center.Y + radius + HeroNameGap * scale), textWidth);
        if (handle.Length > 0)
        {
            bottom = Typography.DrawWrappedCentered(drawList, handle, HeroHandleStyle, VelvetTheme.MutedInk,
                new Vector2(centerX, bottom + HeroHandleGap * scale), textWidth);
        }

        var height = bottom - origin.Y;
        if (UiInteract.HoverClick(new Vector2(origin.X, origin.Y), new Vector2(origin.X + width, origin.Y + height)))
        {
            OpenProfile(userId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawIntroCard(ImDrawListPtr drawList, float width, float inset, float contentWidth, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var cardMin = new Vector2(origin.X + inset, origin.Y);
        var cardHeight = (VCard.Pad * 2f + VCard.HeaderTile + IntroHeaderGap * 2f + IntroFieldHeight) * scale;
        var cardMax = new Vector2(cardMin.X + contentWidth, cardMin.Y + cardHeight);
        VCard.Paint(drawList, cardMin, cardMax, scale);
        var pad = VCard.Pad * scale;
        var contentLeft = cardMin.X + pad;
        var contentRight = cardMax.X - pad;
        var headerTop = cardMin.Y + pad;
        VCard.Header(drawList, new Vector2(contentLeft, headerTop), contentRight - contentLeft, PhoneIcons.Quote,
            VelvetTheme.Rose, Loc.T(L.Velvet.YourIntro), scale, IntroCounter());
        var hairlineY = headerTop + (VCard.HeaderTile + IntroHeaderGap) * scale;
        FeedCell.Hairline(drawList, contentLeft, contentRight, hairlineY, VelvetTheme.Hairline);
        DrawIntroField(new Rect(new Vector2(contentLeft, hairlineY + IntroHeaderGap * scale),
            new Vector2(contentRight, cardMax.Y - pad)), scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight));
    }

    private void DrawIntroField(Rect field, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, field.Min, field.Max, Metrics.Radius.Md * scale,
            VelvetTheme.Alpha(VelvetTheme.Sunken, IntroWellAlpha).Packed());
        var padding = ImGui.GetStyle().FramePadding;
        var textLeft = field.Min.X + IntroFieldPad * scale;
        var wrapWidth = MathF.Max(1f, field.Width - (IntroFieldPad * 2f) * scale - padding.X * 2f - 4f * scale);
        ImGui.SetCursorScreenPos(new Vector2(textLeft, field.Min.Y + IntroFieldPad * scale));
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, VelvetTheme.TitleInk))
        {
            SoftWrapField.Multiline("##introText", ref introText, IntroLimit,
                new Vector2(field.Width - IntroFieldPad * 2f * scale, field.Height - IntroFieldPad * 2f * scale),
                wrapWidth);
        }

        if (introText.Length > 0)
        {
            return;
        }

        Typography.DrawWrappedLeft(new Vector2(textLeft + padding.X, field.Min.Y + IntroFieldPad * scale + padding.Y),
            IntroPrompt(), VelvetTheme.MutedInk, TextStyles.Body, wrapWidth);
    }

    private string IntroPrompt()
    {
        if (introPrompt.Length > 0 && ReferenceEquals(introPromptLanguage, Loc.Current))
        {
            return introPrompt;
        }

        introPromptLanguage = Loc.Current;
        introPrompt = Loc.T(L.Velvet.IntroduceYourselfTo, introName);
        return introPrompt;
    }

    private string IntroCounter()
    {
        if (introCounterLength == introText.Length)
        {
            return introCounter;
        }

        introCounterLength = introText.Length;
        introCounter = introCounterLength.ToString(Loc.Culture) + "/" + IntroLimit.ToString(Loc.Culture);
        return introCounter;
    }

    private void SendIntro(string userId)
    {
        store.SendIntro(userId, introText.Trim(), _ => { });
        introText = string.Empty;
        router.Pop();
    }

    private static string IntroLineOf(VelvetConnectionDto request) =>
        string.IsNullOrWhiteSpace(request.Intro) ? Loc.T(L.Velvet.WantsToConnect) : request.Intro;
}
