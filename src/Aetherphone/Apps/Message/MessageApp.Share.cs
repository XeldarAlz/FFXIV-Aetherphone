using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Sharing;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private string sharePhotoFilter = string.Empty;
    private string? pendingSharedPhoto;

    public ShareKindSet AcceptedShares => session.IsSignedIn ? ShareKindSet.Photo : ShareKindSet.None;

    public void OnShare(in ShareItem item)
    {
        if (item.Kind != ShareKind.Photo)
        {
            return;
        }

        pendingSharedPhoto = item.LocalPath;
    }

    private void ConsumeSharedPhoto()
    {
        var path = pendingSharedPhoto;
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        pendingSharedPhoto = null;
        if (!session.IsSignedIn)
        {
            return;
        }

        activeTab = MessageTab.Chats;
        sharePhotoFilter = string.Empty;
        router.Reset();
        router.Push(MessageRoute.SharePhoto(path));
    }

    private void DrawSharePhotoPicker(Rect area, string path)
    {
        if (DrawConversationPicker(area, Loc.T(L.Common.SendPhoto), ref sharePhotoFilter) is not { } target)
        {
            return;
        }

        store.SendImageMessage(target.Id, path, string.Empty, _ => { });
        forwardOpenPending = target.Id;
    }
}
