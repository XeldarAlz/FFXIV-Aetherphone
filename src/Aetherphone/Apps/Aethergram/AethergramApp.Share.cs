using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private readonly HashSet<string> shareSentUserIds = new(StringComparer.Ordinal);
    private string shareSearchDraft = string.Empty;

    private void OpenShare(string postId)
    {
        shareSentUserIds.Clear();
        shareSearchDraft = string.Empty;
        store.ClearDiscover();
        router.Push(AethergramRoute.Share(postId));
    }

    private void DrawShare(Rect area, string postId)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Aethergram.SendTo), back);
        var scale = ImGuiHelpers.GlobalScale;
        if (!dmStore.ThreadsLoaded && !dmStore.LoadingThreads)
        {
            dmStore.RefreshThreads();
        }

        var searchTop = area.Min.Y + AppHeader.Height * scale;
        var searchRect = new Rect(new Vector2(area.Min.X, searchTop),
            new Vector2(area.Max.X, searchTop + 52f * scale));
        if (SearchField.DrawSubmit(searchRect, "##aethergramShareSearch", Loc.T(L.Aethergram.NameOrWorld),
                ref shareSearchDraft, AppPalettes.Aethergram))
        {
            store.Search(shareSearchDraft);
        }

        var listRect = new Rect(new Vector2(area.Min.X, searchRect.Max.Y + 4f * scale), area.Max);
        using (AppSurface.Begin(listRect))
        {
            if (shareSearchDraft.Trim().Length > 0)
            {
                DrawShareSearchResults(listRect, postId, scale);
            }
            else
            {
                DrawShareThreads(listRect, postId, scale);
            }
        }
    }

    private void DrawShareSearchResults(Rect listRect, string postId, float scale)
    {
        var results = store.DiscoverResults;
        if (results.Length == 0)
        {
            Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale),
                Loc.T(store.Searching ? L.Common.Searching : L.Aethergram.SearchByName),
                AppPalettes.Aethergram.MutedInk);
            return;
        }

        ImGui.Dummy(new Vector2(0f, 6f * scale));
        var myId = store.Me?.Id;
        for (var index = 0; index < results.Length; index++)
        {
            var user = results[index];
            if (user.Id == myId)
            {
                continue;
            }

            DrawShareRow(postId, user.Id, SocialIdentity.Name(user.DisplayName, user.Handle),
                Monogram(user.DisplayName, user.Handle), user.AvatarUrl, user.CanMessage);
        }

        ImGui.Dummy(new Vector2(0f, 24f * scale));
    }

    private void DrawShareThreads(Rect listRect, string postId, float scale)
    {
        var threads = dmStore.Threads;
        var visibleCount = 0;
        for (var index = 0; index < threads.Length; index++)
        {
            if (!threads[index].Pending)
            {
                visibleCount++;
            }
        }

        if (visibleCount == 0)
        {
            Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale),
                Loc.T(dmStore.LoadingThreads ? L.Common.Loading : L.Aethergram.InboxEmpty),
                AppPalettes.Aethergram.MutedInk);
            return;
        }

        ImGui.Dummy(new Vector2(0f, 6f * scale));
        for (var index = 0; index < threads.Length; index++)
        {
            var thread = threads[index];
            if (thread.Pending)
            {
                continue;
            }

            DrawShareRow(postId, thread.OtherUserId,
                SocialIdentity.Name(thread.OtherDisplayName, thread.OtherHandle),
                Monogram(thread.OtherDisplayName, thread.OtherHandle), thread.OtherAvatarUrl, true);
        }

        ImGui.Dummy(new Vector2(0f, 24f * scale));
    }

    private void DrawShareRow(string postId, string userId, string title, string monogram, string? avatarUrl,
        bool canMessage)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var rowHeight = 56f * scale;
        var rowMax = new Vector2(origin.X + width, origin.Y + rowHeight);
        ui.Card(drawList, origin, rowMax, 16f * scale);
        var pad = 12f * scale;
        var avatarRadius = 19f * scale;
        var avatarCenter = new Vector2(origin.X + pad + avatarRadius, origin.Y + rowHeight * 0.5f);
        AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent, monogram, 0.95f,
            lodestone.Remote(userId, ToUri(avatarUrl)), 32);
        var buttonWidth = 76f * scale;
        var buttonHeight = 30f * scale;
        var buttonRect = new Rect(
            new Vector2(rowMax.X - pad - buttonWidth, origin.Y + (rowHeight - buttonHeight) * 0.5f),
            new Vector2(rowMax.X - pad, origin.Y + (rowHeight + buttonHeight) * 0.5f));
        var textLeft = avatarCenter.X + avatarRadius + 12f * scale;
        var titleWidth = buttonRect.Min.X - 10f * scale - textLeft;
        Typography.Draw(new Vector2(textLeft, origin.Y + rowHeight * 0.5f - 9f * scale),
            Typography.FitText(title, titleWidth, TextStyles.Headline), theme.TextStrong,
            TextStyles.Headline.Scale, TextStyles.Headline.Weight);
        var sent = shareSentUserIds.Contains(userId);
        if (sent || !canMessage)
        {
            AppSkin.PillButton(buttonRect, Loc.T(sent ? L.Aethergram.Sent : L.Aethergram.Send), sent, false, theme);
        }
        else if (ui.PillButton(buttonRect, Loc.T(L.Aethergram.Send), true))
        {
            dmStore.SendPostShare(userId, postId);
            shareSentUserIds.Add(userId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }
}
