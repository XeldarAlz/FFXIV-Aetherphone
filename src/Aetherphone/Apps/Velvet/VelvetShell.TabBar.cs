using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const int TabCount = 4;

    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[TabCount];

    private void DrawTabBar(Rect area)
    {
        var hasAvatar = store.Me is not null;
        if (!hasAvatar)
        {
            store.EnsureMe();
        }

        tabItems[(int)VelvetPage.Discover] = new TabItem(Loc.T(L.Velvet.TabDiscover), PhoneIcons.Compass,
            AnchorKey: AnchorFor(VelvetPage.Discover));
        tabItems[(int)VelvetPage.Feed] = new TabItem(Loc.T(L.Velvet.TabFeed), PhoneIcons.Photo,
            AnchorKey: AnchorFor(VelvetPage.Feed));
        tabItems[(int)VelvetPage.Messages] = new TabItem(Loc.T(L.Velvet.Messages), PhoneIcons.MessageCircle,
            PhoneIcons.MessageCircleFilled, store.UnreadCount + store.RequestCount, AnchorFor(VelvetPage.Messages));
        tabItems[(int)VelvetPage.Me] = new TabItem(Loc.T(L.Velvet.TabMe), PhoneIcons.User, PhoneIcons.UserFilled,
            AnchorKey: AnchorFor(VelvetPage.Me), CustomIcon: hasAvatar);
        var result = tabBar.Draw(area, ui, tabItems, (int)activeTab, icons: this);
        if (result.Tapped < 0)
        {
            return;
        }

        SelectTab((VelvetPage)result.Tapped);
    }

    void ITabIconDrawer.DrawTabIcon(ImDrawListPtr drawList, int index, TabItemPose pose, bool active)
    {
        if (store.Me is not { } me)
        {
            return;
        }

        var scale = UiScale.Current;
        VAvatar.Draw(drawList, pose.IconCenter, pose.AvatarRadius(scale), theme, DisplayNameOf(me.DisplayName, me.Handle), me.World,
            me.AvatarUrl, images, lodestone, -1, null, Frames.Of(me.FrameId));
        if (!active)
        {
            return;
        }

        drawList.AddCircle(pose.IconCenter, pose.AvatarRingRadius(scale),
            ImGui.GetColorU32(VelvetTheme.RoseInk), 32, 1.6f * scale);
    }

    private void SelectTab(VelvetPage tab)
    {
        if (tab == VelvetPage.Feed && activeTab == VelvetPage.Feed)
        {
            RefreshFeed();
        }

        if (tab == activeTab)
        {
            return;
        }

        postSheet.Close();
        threadSheet.Close();
        profileMenu.Close();
        activeTab = tab;
    }

    private static string AnchorFor(VelvetPage tab) =>
        tab switch
        {
            VelvetPage.Feed => "velvet.tab.feed",
            VelvetPage.Messages => "velvet.tab.messages",
            VelvetPage.Me => "velvet.tab.me",
            _ => "velvet.tab.discover",
        };
}
