using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float TabBarHeight = 58f;
    private const float TabBarIconSize = 25f;
    private const float TabBarHoverRadius = 20f;
    private const float TabBarAvatarRadius = 13f;
    private const float TabBarAvatarRingGap = 3f;
    private const float TabBarAnchorHalf = 20f;
    private const int TabCount = 4;

    private void DrawTabBar(Rect bar)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        SocialChrome.PaintBarBackdrop(ui, drawList, bar, screenRect);
        FeedCell.Hairline(drawList, bar.Min.X, bar.Max.X, bar.Min.Y + 1f, VelvetTheme.Hairline);
        var slot = bar.Width / TabCount;
        var anchorHalf = new Vector2(TabBarAnchorHalf * scale, TabBarAnchorHalf * scale);
        for (var index = 0; index < TabCount; index++)
        {
            var tab = (VelvetPage)index;
            var cell = new Rect(new Vector2(bar.Min.X + slot * index, bar.Min.Y),
                new Vector2(bar.Min.X + slot * (index + 1), bar.Max.Y));
            var center = new Vector2(cell.Center.X, bar.Center.Y);
            UiAnchors.Report(AnchorFor(tab), new Rect(center - anchorHalf, center + anchorHalf));
            if (DrawTabSlot(drawList, cell, center, tab))
            {
                SelectTab(tab);
            }
        }
    }

    private bool DrawTabSlot(ImDrawListPtr drawList, Rect cell, Vector2 center, VelvetPage tab)
    {
        var scale = UiScale.Current;
        var active = activeTab == tab;
        var hovered = UiInteract.Hover(cell.Min, cell.Max);
        if (hovered)
        {
            drawList.AddCircleFilled(center, TabBarHoverRadius * scale, ImGui.GetColorU32(VelvetInk.Shared.FieldFill),
                32);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var ink = active ? VelvetTheme.RoseInk : hovered ? VelvetTheme.TitleInk : VelvetTheme.MutedInk;
        var iconSize = TabBarIconSize * scale;
        string label;
        switch (tab)
        {
            case VelvetPage.Feed:
                PhoneIcon.Draw(drawList, center, PhoneIcons.Photo, ink, iconSize);
                label = Loc.T(L.Velvet.TabFeed);
                break;
            case VelvetPage.Messages:
                PhoneIcon.Draw(drawList, center,
                    active ? PhoneIcons.MessageCircleFilled : PhoneIcons.MessageCircle, ink, iconSize);
                SocialChrome.DrawCountBadge(drawList, center + new Vector2(11f * scale, -10f * scale),
                    store.UnreadCount + store.RequestCount, VelvetInk.Shared);
                label = Loc.T(L.Velvet.Messages);
                break;
            case VelvetPage.Me:
                DrawTabAvatar(drawList, center, active, ink, iconSize);
                label = Loc.T(L.Velvet.TabMe);
                break;
            default:
                PhoneIcon.Draw(drawList, center, PhoneIcons.Compass, ink, iconSize);
                label = Loc.T(L.Velvet.TabDiscover);
                break;
        }

        HoverTooltip.Show(cell, label, HoverLabelSide.Above);
        return UiInteract.Click(cell.Min, cell.Max, hovered);
    }

    private void DrawTabAvatar(ImDrawListPtr drawList, Vector2 center, bool active, Vector4 ink, float iconSize)
    {
        if (store.Me is not { } me)
        {
            store.EnsureMe();
            PhoneIcon.Draw(drawList, center, active ? PhoneIcons.UserFilled : PhoneIcons.User, ink, iconSize);
            return;
        }

        var scale = UiScale.Current;
        var radius = TabBarAvatarRadius * scale;
        VAvatar.Draw(drawList, center, radius, theme, DisplayNameOf(me.DisplayName, me.Handle), me.World,
            me.AvatarUrl, images, lodestone, -1, null, Frames.Of(me.FrameId));
        if (active)
        {
            drawList.AddCircle(center, radius + TabBarAvatarRingGap * scale,
                ImGui.GetColorU32(VelvetTheme.RoseInk), 32, 1.6f * scale);
        }
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
