using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private Action<NotificationDto>? activityActor;
    private Action<NotificationDto>? activityPost;

    private void DrawActivity(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (VHeader.Push(area, Loc.T(L.Velvet.Activity), theme))
        {
            router.Pop();
            return;
        }

        activityActor ??= item => OpenProfile(item.ActorId);
        activityPost ??= item =>
        {
            if (item.PostId is { } postId)
            {
                store.EnsurePost(postId);
                router.Push(VelvetView.PostDetail(postId));
            }
        };
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        activityFeed.EnsureFresh(social.Latest);
        SocialActivityList.Draw(body, ui, VelvetTheme.Palette, theme, activityFeed.Items, Id, images, lodestone,
            activityActor, activityPost, loadOlderActivity);
    }
}
