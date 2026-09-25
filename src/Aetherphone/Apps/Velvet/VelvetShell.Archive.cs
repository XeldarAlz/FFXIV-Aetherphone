using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private enum PostNotice
    {
        None,
        Replace,
        PinFailed,
        UnpinFailed,
        Pinned,
        Unpinned,
        Archived,
        Unarchived,
        ArchiveFailed,
        UnarchiveFailed,
    }

    private const int MaxPinnedPosts = 3;
    private const float ArchiveTopGap = 6f;
    private const float ArchiveBottomPad = 40f;
    private const float PinGlyphSize = 12f;
    private const float PinGlyphGap = 3f;

    private readonly ScreenToast toast = new();
    private volatile PostNotice pendingPostNotice;
    private string pendingNoticePostId = string.Empty;
    private bool pendingNoticeLeavesDetail;

    private void OpenArchive()
    {
        store.RefreshArchived();
        router.Push(VelvetView.Archive);
    }

    private void DrawArchive(Rect area)
    {
        var scale = UiScale.Current;
        if (VHeader.Push(area, Loc.T(L.Social.ArchiveTitle)))
        {
            router.Pop();
            return;
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        var posts = store.ArchivedPosts;
        using (AppSurface.BeginEdgeToEdge(body))
        {
            if (posts.Length == 0)
            {
                if (store.ArchivedLoaded)
                {
                    DrawEmpty(body, Loc.T(L.Social.ArchiveEmpty), Loc.T(L.Social.ArchiveEmptyHint));
                }
                else
                {
                    DrawEmpty(body, Loc.T(L.Common.Loading), string.Empty);
                }

                return;
            }

            var width = ScrollLayout.StableContentWidth();
            Gap(ArchiveTopGap);
            DrawArchiveGrid(posts, width);
            if (store.ArchivedLoadingMore)
            {
                InfiniteScroll.DrawLoadingRow(body.Center.X, VelvetTheme.MutedInk);
            }

            Gap(ArchiveBottomPad);
            if (store.HasMoreArchived && !store.ArchivedLoadingMore && InfiniteScroll.ReachedBottom())
            {
                store.LoadMoreArchived();
            }
        }
    }

    private void DrawArchiveGrid(VelvetPostDto[] posts, float width)
    {
        var scale = UiScale.Current;
        var cellGap = ProfileGridGap * scale;
        var cell = (width - cellGap * (ProfileColumns - 1)) / ProfileColumns;
        var rows = (posts.Length + ProfileColumns - 1) / ProfileColumns;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < posts.Length; index++)
        {
            var post = posts[index];
            var row = index / ProfileColumns;
            var column = index % ProfileColumns;
            var min = new Vector2(origin.X + column * (cell + cellGap), origin.Y + row * (cell + cellGap));
            var max = new Vector2(min.X + cell, min.Y + cell);
            var veiled = SensitiveReveals.ShouldVeil(post.Sensitive, post.Id, configuration.ShowSensitiveContent);
            DrawMedia(drawList, min, max, post.MediaUrl, 0f, veiled: veiled);
            if (UiInteract.Click(min, max))
            {
                OpenPostDetail(post.Id);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * cell + (rows - 1) * cellGap));
    }

    private static float DrawPinnedGlyph(ImDrawListPtr drawList, float left, float top, float lineHeight)
    {
        var scale = UiScale.Current;
        var glyphSize = PinGlyphSize * scale;
        PhoneIcon.Draw(drawList, new Vector2(left + glyphSize * 0.5f, top + lineHeight * 0.5f), PhoneIcons.PinFilled,
            VelvetTheme.MutedInk, glyphSize);
        return glyphSize + PinGlyphGap * scale;
    }

    private void PinPost(string postId)
    {
        store.PinPost(postId, false, outcome => QueuePostNotice(outcome switch
        {
            PinOutcome.Pinned => PostNotice.Pinned,
            PinOutcome.LimitReached => PostNotice.Replace,
            _ => PostNotice.PinFailed,
        }, postId, false));
    }

    private void UnpinPost(string postId)
    {
        store.UnpinPost(postId, ok => QueuePostNotice(ok ? PostNotice.Unpinned : PostNotice.UnpinFailed, postId, false));
    }

    private void ArchivePost(string postId, bool fromDetail)
    {
        store.ArchivePost(postId,
            ok => QueuePostNotice(ok ? PostNotice.Archived : PostNotice.ArchiveFailed, postId, fromDetail));
    }

    private void RestorePost(string postId, bool fromDetail)
    {
        store.UnarchivePost(postId,
            ok => QueuePostNotice(ok ? PostNotice.Unarchived : PostNotice.UnarchiveFailed, postId, fromDetail));
    }

    private void QueuePostNotice(PostNotice notice, string postId, bool leavesDetail)
    {
        pendingNoticePostId = postId;
        pendingNoticeLeavesDetail = leavesDetail;
        pendingPostNotice = notice;
    }

    private void DrainPostNotices()
    {
        var notice = pendingPostNotice;
        if (notice == PostNotice.None)
        {
            return;
        }

        pendingPostNotice = PostNotice.None;
        switch (notice)
        {
            case PostNotice.Replace:
                AskReplacePinnedPost(pendingNoticePostId);
                break;
            case PostNotice.PinFailed:
                confirm.Alert(null, Loc.T(L.Social.PinFailed), Loc.T(L.Common.Close));
                break;
            case PostNotice.UnpinFailed:
                confirm.Alert(null, Loc.T(L.Social.UnpinFailed), Loc.T(L.Common.Close));
                break;
            case PostNotice.Pinned:
                toast.Show(Loc.T(L.Social.PinnedToast));
                break;
            case PostNotice.Unpinned:
                toast.Show(Loc.T(L.Social.UnpinnedToast));
                break;
            case PostNotice.Archived:
                toast.Show(Loc.T(L.Social.ArchivedToast));
                LeaveDetailAfterNotice();
                break;
            case PostNotice.Unarchived:
                toast.Show(Loc.T(L.Social.UnarchivedToast));
                LeaveDetailAfterNotice();
                break;
            case PostNotice.ArchiveFailed:
                confirm.Alert(null, Loc.T(L.Social.ArchiveFailed), Loc.T(L.Common.Close));
                break;
            case PostNotice.UnarchiveFailed:
                confirm.Alert(null, Loc.T(L.Social.UnarchiveFailed), Loc.T(L.Common.Close));
                break;
        }
    }

    private void LeaveDetailAfterNotice()
    {
        if (pendingNoticeLeavesDetail && router.Current.Screen == VelvetScreenId.PostDetail)
        {
            back();
        }
    }

    private void AskReplacePinnedPost(string postId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Social.PinReplaceTitle),
            Message = Loc.T(L.Social.PinReplaceMessage, MaxPinnedPosts),
            ConfirmLabel = Loc.T(L.Social.PinReplaceConfirm),
            CancelLabel = Loc.T(L.Velvet.DeleteCancel),
            Danger = false,
            Sheet = true,
            BusyLabel = Loc.T(L.Velvet.Saving),
            FailedMessage = Loc.T(L.Social.PinFailed),
            ConfirmAsync = done => store.PinPost(postId, true, outcome =>
            {
                var pinned = outcome == PinOutcome.Pinned;
                if (pinned)
                {
                    QueuePostNotice(PostNotice.Pinned, postId, false);
                }

                done(pinned);
            }),
        });
    }
}
