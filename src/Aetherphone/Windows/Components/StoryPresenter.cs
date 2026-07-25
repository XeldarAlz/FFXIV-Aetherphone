using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Windows.Components;

internal readonly record struct StoryConfirmLabels(LocString Confirm, LocString Cancel, LocString Busy);

internal readonly record struct StoryReplyHooks(
    LocString ReplyHint,
    Action<string, string, string> Send,
    Action<string> OpenThread);

internal sealed class StoryPresenter : IDisposable
{
    private readonly StoryStore stories;
    private readonly StoryTrayRow tray;
    private readonly StoryViewerOverlay viewer;
    private readonly StoryRingPainter painter;
    private readonly AppPalette palette;
    private readonly StoryConfirmLabels labels;
    private readonly ConfirmService confirm;
    private readonly Action onCompose;
    private readonly Func<StoryDto, StoryViewers> viewersSource;
    private readonly Func<bool> nextGroup;
    private readonly Func<bool> previousGroup;
    private readonly StoryReplyHooks? replyHooks;
    private readonly StoryReplyPrompt? replyPrompt;
    private StoryRingDto? pendingRing;
    private bool pendingOpenAtEnd;
    private bool pendingFromViewer;
    private StoryDto[]? viewerItems;

    public StoryPresenter(AethernetSession session, GramClient client, MediaClient media, RemoteImageCache images,
        LodestoneService lodestone, StoryRingPainter painter, AppPalette palette, StoryConfirmLabels labels,
        ConfirmService confirm, string logTag, Action onCompose, StoryReplyHooks? reply = null)
    {
        stories = new StoryStore(session, client, media, logTag);
        tray = new StoryTrayRow(images, lodestone);
        viewer = new StoryViewerOverlay(images, lodestone);
        this.painter = painter;
        this.palette = palette;
        this.labels = labels;
        this.confirm = confirm;
        this.onCompose = onCompose;
        viewersSource = ViewersFor;
        nextGroup = TryAdvanceGroup;
        previousGroup = TryRetreatGroup;
        replyHooks = reply;
        if (reply is { } hooks)
        {
            replyPrompt = new StoryReplyPrompt(hooks.ReplyHint, SendReply);
        }
    }

    public bool Active => viewer.Active;

    public bool Posting => stories.Posting;

    public void RefreshTray() => stories.RefreshTray();

    public bool TryRing(string authorId, out StoryRingDto ring) => stories.TryRing(authorId, out ring);

    public void CreateStory(string sourcePath, WallpaperCrop crop, string caption, Action<bool> onComplete) =>
        stories.CreateStory(sourcePath, crop, caption, onComplete);

    public void DrawTray(PhoneTheme theme) =>
        tray.Draw(theme, palette, stories.Rings, stories.HasOwnRing, painter, onCompose, OpenRing);

    public void DrawViewer(Rect screen, PhoneTheme theme) =>
        viewer.Draw(screen, theme, confirm.Active is not null);

    public void OpenRing(StoryRingDto ring)
    {
        pendingRing = ring;
        pendingOpenAtEnd = false;
        pendingFromViewer = false;
        stories.OpenAuthor(ring.AuthorId);
    }

    public void Close()
    {
        viewer.Reset();
        stories.CloseAuthor();
        stories.ClearViewers();
        pendingRing = null;
        pendingOpenAtEnd = false;
        pendingFromViewer = false;
        viewerItems = null;
    }

    public void Advance()
    {
        if (!viewer.Active && stories.OpenAuthorId is not null && (pendingRing is null || pendingFromViewer))
        {
            stories.CloseAuthor();
            viewerItems = null;
            pendingRing = null;
            pendingOpenAtEnd = false;
            pendingFromViewer = false;
        }

        if (pendingRing is not { } ring)
        {
            SyncViewerItems();
            return;
        }

        var items = stories.OpenStories;
        if (items.Length == 0)
        {
            if (!stories.GroupLoading)
            {
                pendingRing = null;
                pendingOpenAtEnd = false;
                pendingFromViewer = false;
                viewer.CancelGroupWait();
            }

            return;
        }

        pendingRing = null;
        var openAtEnd = pendingOpenAtEnd;
        pendingOpenAtEnd = false;
        pendingFromViewer = false;
        viewerItems = items;
        stories.ClearViewers();
        var label = ring.IsMe
            ? Loc.T(L.Story.YourStory)
            : SocialIdentity.Name(ring.AuthorDisplayName, ring.AuthorHandle);
        viewer.Open(items, label, ring.AuthorAvatarUrl, stories.MarkSeen, ring.IsMe, AskDelete, viewersSource,
            null, nextGroup, previousGroup, openAtEnd, ring.IsMe ? null : replyPrompt);
    }

    private bool TryAdvanceGroup() => TryOpenAdjacentRing(1);

    private bool TryRetreatGroup() => TryOpenAdjacentRing(-1);

    private bool TryOpenAdjacentRing(int direction)
    {
        var authorId = stories.OpenAuthorId;
        if (authorId is null)
        {
            return false;
        }

        var snapshot = stories.Rings;
        for (var ringIndex = 0; ringIndex < snapshot.Length; ringIndex++)
        {
            if (snapshot[ringIndex].AuthorId != authorId)
            {
                continue;
            }

            var target = ringIndex + direction;
            if (target < 0 || target >= snapshot.Length)
            {
                return false;
            }

            pendingRing = snapshot[target];
            pendingOpenAtEnd = direction < 0;
            pendingFromViewer = true;
            stories.OpenAuthor(snapshot[target].AuthorId);
            return true;
        }

        return false;
    }

    private void SendReply(StoryDto story, string text)
    {
        if (replyHooks is not { } hooks)
        {
            return;
        }

        var authorId = story.AuthorId;
        hooks.Send(authorId, story.Id, text);
        Close();
        hooks.OpenThread(authorId);
    }

    private StoryViewers ViewersFor(StoryDto story)
    {
        stories.LoadViewers(story.Id);
        return new StoryViewers(stories.Viewers, stories.ViewersTotal, stories.ViewersLoading);
    }

    private void SyncViewerItems()
    {
        if (!viewer.Active)
        {
            return;
        }

        var items = stories.OpenStories;
        if (ReferenceEquals(items, viewerItems))
        {
            return;
        }

        viewerItems = items;
        viewer.Replace(items);
    }

    private void AskDelete(StoryDto story)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Story.DeleteMessage),
            ConfirmLabel = Loc.T(labels.Confirm),
            CancelLabel = Loc.T(labels.Cancel),
            BusyLabel = Loc.T(labels.Busy),
            FailedMessage = Loc.T(L.Story.DeleteFailed),
            ConfirmAsync = done => stories.DeleteStory(story.Id, done),
        });
    }

    public void Dispose()
    {
        stories.Dispose();
    }
}
