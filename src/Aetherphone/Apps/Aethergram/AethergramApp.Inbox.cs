using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float InboxSegmentSmoothTime = 0.14f;

    private readonly DropdownMenu inboxRowMenu = new();
    private readonly DropdownMenu.Item[] inboxRowItems = new DropdownMenu.Item[1];
    private string? inboxMenuThreadId;
    private int inboxTab;
    private Spring inboxSegmentSpring;

    private void DrawInbox(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Aethergram.InboxTitle), back);
        var scale = ImGuiHelpers.GlobalScale;
        if (!dmStore.ThreadsLoaded && !dmStore.LoadingThreads)
        {
            dmStore.RefreshThreads();
        }

        var pad = 16f * scale;
        var segTop = area.Min.Y + AppHeader.Height * scale + 6f * scale;
        var segRect = new Rect(new Vector2(area.Min.X + pad, segTop),
            new Vector2(area.Max.X - pad, segTop + 32f * scale));
        var requestCount = dmStore.RequestCount;
        var requestsLabel = requestCount > 0
            ? Loc.T(L.Aethergram.RequestsCount, requestCount)
            : Loc.T(L.Aethergram.Requests);
        DrawInboxSegments(segRect, Loc.T(L.Aethergram.ChatsTab), requestsLabel);
        var listRect = new Rect(new Vector2(area.Min.X, segRect.Max.Y + 8f * scale), area.Max);
        var showRequests = inboxTab == 1;
        var threads = dmStore.Threads;
        var visibleCount = 0;
        for (var index = 0; index < threads.Length; index++)
        {
            if (threads[index].Pending == showRequests)
            {
                visibleCount++;
            }
        }

        using (AppSurface.Begin(listRect))
        {
            if (visibleCount == 0)
            {
                DrawInboxEmptyState(listRect, showRequests, threads.Length, scale);
            }
            else
            {
                ImGui.Dummy(new Vector2(0f, 6f * scale));
                for (var index = 0; index < threads.Length; index++)
                {
                    if (threads[index].Pending == showRequests)
                    {
                        DrawInboxRow(threads[index]);
                    }
                }

                ImGui.Dummy(new Vector2(0f, 24f * scale));
            }
        }

        DrawInboxRowMenu(area);
    }

    private void DrawInboxSegments(Rect rect, string chatsLabel, string requestsLabel)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, rect.Min, rect.Max, rect.Height * 0.5f);
        var segmentWidth = rect.Width * 0.5f;
        var pad = 3f * scale;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, NavHoverMaxFrameSeconds);
        var thumb = Math.Clamp(inboxSegmentSpring.Step(inboxTab, InboxSegmentSmoothTime, delta), 0f, 1f);
        var thumbMin = new Vector2(rect.Min.X + pad + thumb * segmentWidth, rect.Min.Y + pad);
        var thumbMax = new Vector2(thumbMin.X + segmentWidth - pad * 2f, rect.Max.Y - pad);
        Squircle.Fill(drawList, thumbMin, thumbMax, (rect.Height - pad * 2f) * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(Accent, 0.30f)));
        DrawInboxSegment(rect, 0, chatsLabel, segmentWidth);
        DrawInboxSegment(rect, 1, requestsLabel, segmentWidth);
    }

    private void DrawInboxSegment(Rect rect, int index, string label, float segmentWidth)
    {
        var min = new Vector2(rect.Min.X + index * segmentWidth, rect.Min.Y);
        var max = new Vector2(min.X + segmentWidth, rect.Max.Y);
        var active = inboxTab == index;
        Typography.DrawCentered(new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f), label,
            active ? AppPalettes.Aethergram.TitleInk : AppPalettes.Aethergram.MutedInk,
            TextStyles.FootnoteEmphasized);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(min, max, hovered))
        {
            inboxTab = index;
        }
    }

    private void DrawInboxEmptyState(Rect listRect, bool showRequests, int totalThreads, float scale)
    {
        if (dmStore.LoadingThreads && totalThreads == 0)
        {
            Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 80f * scale),
                Loc.T(L.Common.Loading), AppPalettes.Aethergram.MutedInk);
            return;
        }

        if (showRequests)
        {
            Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 80f * scale),
                Loc.T(L.Aethergram.RequestsEmpty), AppPalettes.Aethergram.TitleInk, TextStyles.Headline);
            return;
        }

        Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 80f * scale),
            Loc.T(L.Aethergram.InboxEmpty), AppPalettes.Aethergram.TitleInk, TextStyles.Headline);
        Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 106f * scale),
            Loc.T(L.Aethergram.InboxEmptyHint), AppPalettes.Aethergram.MutedInk, TextStyles.Subheadline);
    }

    private void DrawInboxRow(GramThreadDto thread)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var rowHeight = 64f * scale;
        var rowMax = new Vector2(origin.X + width, origin.Y + rowHeight);
        ui.Card(drawList, origin, rowMax, 16f * scale);
        var pad = 12f * scale;
        var avatarRadius = 22f * scale;
        var avatarCenter = new Vector2(origin.X + pad + avatarRadius, origin.Y + rowHeight * 0.5f);
        AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent,
            Monogram(thread.OtherDisplayName, thread.OtherHandle), 0.95f,
            lodestone.Remote(thread.OtherUserId, ToUri(thread.OtherAvatarUrl)), 32);
        PresenceDot(drawList, new Vector2(avatarCenter.X + avatarRadius - 4f * scale,
            avatarCenter.Y + avatarRadius - 4f * scale), thread.Presence);
        var textLeft = avatarCenter.X + avatarRadius + 12f * scale;
        var textRight = origin.X + width - pad;
        var timeText = thread.LastMessageAtUnix > 0 ? TimeText.Short(thread.LastMessageAtUnix) : string.Empty;
        var timeSize = timeText.Length > 0 ? Typography.Measure(timeText, TextStyles.Footnote) : Vector2.Zero;
        var title = SocialIdentity.Name(thread.OtherDisplayName, thread.OtherHandle);
        var titleWidth = textRight - textLeft - (timeSize.X > 0f ? timeSize.X + 8f * scale : 0f);
        Typography.Draw(new Vector2(textLeft, origin.Y + 12f * scale),
            Typography.FitText(title, titleWidth, 1f, FontWeight.SemiBold), theme.TextStrong, 1f,
            FontWeight.SemiBold);
        if (timeText.Length > 0)
        {
            Typography.Draw(new Vector2(textRight - timeSize.X, origin.Y + 13f * scale), timeText,
                AppPalettes.Aethergram.MutedInk, TextStyles.Footnote);
        }

        var unread = thread.UnreadCount;
        var preview = string.IsNullOrEmpty(thread.LastMessagePreview)
            ? Loc.T(L.Aethergram.ThreadEmpty)
            : ChatText.ListPreview(thread.LastMessagePreview);
        var previewWidth = textRight - textLeft - (unread > 0 ? 22f * scale : 0f);
        Typography.Draw(new Vector2(textLeft, origin.Y + 35f * scale),
            Typography.FitText(preview, previewWidth, TextStyles.Subheadline.Scale, TextStyles.Subheadline.Weight),
            unread > 0 ? AppPalettes.Aethergram.BodyInk : AppPalettes.Aethergram.MutedInk, TextStyles.Subheadline);
        if (unread > 0)
        {
            ActivityBadge.Draw(new Vector2(textRight - 7f * scale, origin.Y + 42f * scale), unread, theme, scale);
        }

        if (UiInteract.Hover(origin, rowMax) && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            OpenInboxRowMenu(thread.OtherUserId);
        }
        else if (UiInteract.HoverClick(origin, rowMax))
        {
            OpenThread(thread.OtherUserId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }

    private void OpenInboxRowMenu(string otherId)
    {
        inboxMenuThreadId = otherId;
        var position = ImGui.GetMousePos();
        inboxRowMenu.Toggle(otherId, new Rect(position, position + new Vector2(1f, 1f)));
    }

    private void DrawInboxRowMenu(Rect area)
    {
        if (inboxMenuThreadId is not { } otherId || !inboxRowMenu.IsOpenFor(otherId))
        {
            return;
        }

        inboxRowItems[0] = new DropdownMenu.Item(Loc.T(L.Aethergram.DeleteConversation),
            FontAwesomeIcon.Trash.ToIconString(), true);
        if (inboxRowMenu.Draw(area, theme, inboxRowItems) == 0)
        {
            AskDeleteConversation(otherId);
        }
    }

    private void AskDeleteConversation(string otherId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Aethergram.DeleteConversation),
            Message = Loc.T(L.Aethergram.DeleteConversationMessage),
            ConfirmLabel = Loc.T(L.Aethergram.DeleteConfirm),
            CancelLabel = Loc.T(L.Aethergram.DeleteCancel),
            Danger = true,
            Confirm = () => DeleteConversation(otherId),
        });
    }

    private void DeleteConversation(string otherId)
    {
        var current = router.Current;
        var threadOpen = current.Screen == AethergramScreen.Thread && current.Id == otherId;
        dmStore.DeleteThread(otherId);
        if (threadOpen)
        {
            router.Pop();
        }
    }

    private void OpenInbox()
    {
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
