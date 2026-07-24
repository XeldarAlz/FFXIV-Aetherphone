using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private void DrawInbox(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Aethergram.InboxTitle), back);
        var scale = ImGuiHelpers.GlobalScale;
        if (!dmStore.ThreadsLoaded && !dmStore.LoadingThreads)
        {
            dmStore.RefreshThreads();
        }

        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var threads = dmStore.Threads;
        using (AppSurface.Begin(listRect))
        {
            if (threads.Length == 0)
            {
                if (dmStore.LoadingThreads)
                {
                    Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 80f * scale),
                        Loc.T(L.Common.Loading), AppPalettes.Aethergram.MutedInk);
                    return;
                }

                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 80f * scale),
                    Loc.T(L.Aethergram.InboxEmpty), AppPalettes.Aethergram.TitleInk, TextStyles.Headline);
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 106f * scale),
                    Loc.T(L.Aethergram.InboxEmptyHint), AppPalettes.Aethergram.MutedInk, TextStyles.Subheadline);
                return;
            }

            ImGui.Dummy(new Vector2(0f, 6f * scale));
            for (var index = 0; index < threads.Length; index++)
            {
                DrawInboxRow(threads[index]);
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
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
            : thread.LastMessagePreview;
        var previewWidth = textRight - textLeft - (unread > 0 ? 22f * scale : 0f);
        Typography.Draw(new Vector2(textLeft, origin.Y + 35f * scale),
            Typography.FitText(preview, previewWidth, TextStyles.Subheadline.Scale, TextStyles.Subheadline.Weight),
            unread > 0 ? AppPalettes.Aethergram.BodyInk : AppPalettes.Aethergram.MutedInk, TextStyles.Subheadline);
        if (unread > 0)
        {
            ActivityBadge.Draw(new Vector2(textRight - 7f * scale, origin.Y + 42f * scale), unread, theme, scale);
        }

        if (UiInteract.HoverClick(origin, rowMax))
        {
            OpenThread(thread.OtherUserId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
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
