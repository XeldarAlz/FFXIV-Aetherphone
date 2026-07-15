using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private void StartStoryCompose()
    {
        post.Open(true);
        router.Push(VelvetView.Compose);
    }

    private void OpenStoryRing(StoryRingDto ring)
    {
        pendingStoryRing = ring;
        stories.OpenAuthor(ring.AuthorId);
    }

    private void CloseStories()
    {
        storyViewer.Reset();
        stories.CloseAuthor();
        stories.ClearViewers();
        pendingStoryRing = null;
        viewerItems = null;
    }

    private void AdvanceStoryOpen()
    {
        if (!storyViewer.Active && stories.OpenAuthorId is not null && pendingStoryRing is null)
        {
            stories.CloseAuthor();
            viewerItems = null;
        }

        if (pendingStoryRing is not { } ring)
        {
            SyncViewerItems();
            return;
        }

        var items = stories.OpenStories;
        if (items.Length == 0)
        {
            if (!stories.GroupLoading)
            {
                pendingStoryRing = null;
            }

            return;
        }

        pendingStoryRing = null;
        viewerItems = items;
        stories.ClearViewers();
        var label = ring.IsMe
            ? Loc.T(L.Story.YourStory)
            : SocialIdentity.Name(ring.AuthorDisplayName, ring.AuthorHandle);
        storyViewer.Open(items, label, ring.AuthorAvatarUrl, stories.MarkSeen, ring.IsMe, AskDeleteStory,
            storyViewers);
    }

    private StoryViewers StoryViewersFor(StoryDto story)
    {
        stories.LoadViewers(story.Id);
        return new StoryViewers(stories.Viewers, stories.ViewersTotal, stories.ViewersLoading);
    }

    private void SyncViewerItems()
    {
        if (!storyViewer.Active)
        {
            return;
        }

        var items = stories.OpenStories;
        if (ReferenceEquals(items, viewerItems))
        {
            return;
        }

        viewerItems = items;
        storyViewer.Replace(items);
    }

    private void AskDeleteStory(StoryDto story)
    {
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Story.DeleteMessage),
            ConfirmLabel = Loc.T(L.Velvet.DeleteConfirm),
            CancelLabel = Loc.T(L.Velvet.DeleteCancel),
            BusyLabel = Loc.T(L.Velvet.Saving),
            FailedMessage = Loc.T(L.Story.DeleteFailed),
            ConfirmAsync = done => stories.DeleteStory(story.Id, done),
        });
    }
}
