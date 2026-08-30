using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Report;
using Aetherphone.Core.Social;

namespace Aetherphone.Windows.Components;

internal sealed class SocialProfileStyle
{
    public required LocString Saving { get; init; }
    public required LocString DeleteConfirmMessage { get; init; }
    public required LocString DeleteConfirm { get; init; }
    public required LocString DeleteCancel { get; init; }
    public required LocString DeleteFailed { get; init; }
    public required LocString DeleteCommentConfirmMessage { get; init; }
    public required LocString DeleteCommentFailed { get; init; }
    public required LocString RemoveCommentConfirmMessage { get; init; }
}

internal sealed class SocialProfilePages
{
    public const int DisplayNameMax = 40;
    public const int HandleMax = 15;
    public const int BioMax = 200;

    private readonly SocialFeedStore store;
    private readonly SocialProfileStyle style;
    private readonly ConfirmService confirm;
    private readonly ReportService report;

    public SocialProfilePages(SocialFeedStore store, SocialProfileStyle style, ConfirmService confirm,
        ReportService report)
    {
        this.store = store;
        this.style = style;
        this.confirm = confirm;
        this.report = report;
    }

    public void EnsureLoaded(SocialFeedScope scope)
    {
        if (store.Feed(scope).Length == 0 && !store.IsLoading(scope))
        {
            store.RefreshFeed(scope);
        }
    }

    public static bool IsHandleValid(string handle)
    {
        var value = handle.Trim();
        if (value.Length < 3 || value.Length > HandleMax)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            var ok = character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    public static string FollowersLabel(int count) => TrailingWord(Loc.Plural(L.Account.Followers, count));

    private static string TrailingWord(string plural)
    {
        var parts = plural.Split(' ', 2);
        return parts.Length > 1 ? parts[1] : plural;
    }

    public static string UserListTitle(UserListKind kind) => kind switch
    {
        UserListKind.Followers => Loc.T(L.Social.FollowersTitle),
        UserListKind.Following => Loc.T(L.Social.FollowingTitle),
        UserListKind.Mutuals => Loc.T(L.Social.MutualsTitle),
        _ => Loc.T(L.Social.LikedByTitle),
    };

    internal static string FollowedByLine(UserDto user)
    {
        if (user.IsMe || user.FollowedByCount <= 0 || user.FollowedByPreview is not { Length: > 0 } preview)
        {
            return string.Empty;
        }

        var others = user.FollowedByCount - preview.Length;
        if (others <= 0)
        {
            return preview.Length == 1
                ? string.Format(Loc.Culture, Loc.T(L.Social.FollowedByOne), preview[0])
                : string.Format(Loc.Culture, Loc.T(L.Social.FollowedByTwo), preview[0], preview[1]);
        }

        if (preview.Length == 1)
        {
            return others == 1
                ? string.Format(Loc.Culture, Loc.T(L.Social.FollowedByOneMoreOne), preview[0])
                : string.Format(Loc.Culture, Loc.T(L.Social.FollowedByOneMoreMany), preview[0], others);
        }

        return others == 1
            ? string.Format(Loc.Culture, Loc.T(L.Social.FollowedByTwoMoreOne), preview[0], preview[1])
            : string.Format(Loc.Culture, Loc.T(L.Social.FollowedByTwoMoreMany), preview[0], preview[1], others);
    }

    public void OpenReport(string targetType, string targetId, string title)
    {
        report.Open(new ReportPrompt
        {
            Title = title,
            Submit = (reason, done) => store.Report(targetType, targetId, reason, done),
        });
    }

    public void AskBlock(string authorDisplayName, string authorHandle, string authorId)
    {
        var name = SocialIdentity.Name(authorDisplayName, authorHandle);
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Social.BlockConfirmTitle, name),
            Message = Loc.T(L.Social.BlockConfirm),
            ConfirmLabel = Loc.T(L.Social.BlockAction),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            ConfirmAsync = done => store.Block(authorId, done, confirm.ReportFailure),
        });
    }

    public void AskDeletePost(string postId, Action? deleted = null)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(style.DeleteConfirmMessage),
            ConfirmLabel = Loc.T(style.DeleteConfirm),
            CancelLabel = Loc.T(style.DeleteCancel),
            Sheet = true,
            BusyLabel = Loc.T(style.Saving),
            FailedMessage = Loc.T(style.DeleteFailed),
            ConfirmAsync = done => store.DeletePost(postId, ok =>
            {
                if (ok)
                {
                    deleted?.Invoke();
                }

                done(ok);
            }),
        });
    }

    public void AskDeleteComment(string postId, string commentId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(style.DeleteCommentConfirmMessage),
            ConfirmLabel = Loc.T(style.DeleteConfirm),
            CancelLabel = Loc.T(style.DeleteCancel),
            Sheet = true,
            BusyLabel = Loc.T(style.Saving),
            FailedMessage = Loc.T(style.DeleteCommentFailed),
            ConfirmAsync = done => store.DeleteComment(postId, commentId, done),
        });
    }

    public void AskRemoveComment(string postId, string commentId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(style.RemoveCommentConfirmMessage),
            ConfirmLabel = Loc.T(style.DeleteConfirm),
            CancelLabel = Loc.T(style.DeleteCancel),
            Sheet = true,
            BusyLabel = Loc.T(style.Saving),
            FailedMessage = Loc.T(style.DeleteCommentFailed),
            ConfirmAsync = done => store.DeleteComment(postId, commentId, done),
        });
    }
}
