using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float TopBarIconSize = 26f;
    private const float LogoSize = 30f;
    private const float LogoGap = 10f;
    private const float TitleIconGap = 8f;
    private const float TitleHitPad = 6f;
    private const float TitleHitPadY = 4f;
    private const float TitleHitRounding = 8f;
    private const float SpinnerGap = 12f;
    private const float SpinnerRadius = 7f;
    private const float HeaderAnchorHalf = 18f;
    private const float FeedTabRowHeight = 44f;
    private const float FeedTabUnderline = 2f;
    private const float FeedTabSmoothTime = 0.09f;

    private static readonly TextStyle WordmarkStyle = new(1.4f, FontWeight.Bold);
    private static readonly TextStyle FeedTabStyle = new(1.07f, FontWeight.SemiBold);
    private static readonly TextStyle FeedTabIdleStyle = new(1.07f, FontWeight.Medium);
    private static readonly UnderlineTabStyle FeedTabsStyle = new(FeedTabStyle, FeedTabIdleStyle,
        VelvetTheme.TitleInk, VelvetTheme.MutedInk, VelvetTheme.Rose, FeedTabUnderline, SocialChrome.CellPadX,
        FeedTabSmoothTime);

    private Spring feedTabSlide;

    private void DrawRootTopBar(Rect area)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + VHeader.Height * scale * 0.5f;
        var logoSize = LogoSize * scale;
        var logoCenter = new Vector2(area.Min.X + SocialChrome.CellPadX * scale + logoSize * 0.5f, rowCenterY);
        if (!AppIconTextures.TryDrawArtwork(drawList, Id, logoCenter, logoSize, VelvetTheme.RoseInk))
        {
            PhoneIcon.Draw(drawList, logoCenter, PhoneIcons.Moon, VelvetTheme.RoseInk, logoSize);
        }

        var titleLeft = logoCenter.X + logoSize * 0.5f + LogoGap * scale;
        var titleRight = SocialChrome.HeaderSlot(area, RootTrailingSlots(activeTab) - 1).X
            - SocialChrome.HeaderIconRadius * scale - TitleIconGap * scale;
        var titleHeight = Typography.LineHeight(WordmarkStyle);
        var title = Typography.FitText(DisplayName, MathF.Max(1f, titleRight - titleLeft), WordmarkStyle);
        var titleSize = Typography.Measure(title, WordmarkStyle);
        var titleMin = new Vector2(titleLeft - TitleHitPad * scale, rowCenterY - titleHeight * 0.5f - TitleHitPadY * scale);
        var titleMax = new Vector2(titleLeft + titleSize.X + TitleHitPad * scale,
            rowCenterY + titleHeight * 0.5f + TitleHitPadY * scale);
        UiInteract.HoverHighlight(drawList, titleMin, titleMax, TitleHitRounding * scale);
        Typography.Draw(drawList, new Vector2(titleLeft, rowCenterY - titleHeight * 0.5f), title, VelvetTheme.TitleInk,
            WordmarkStyle);
        if (UiInteract.HoverClick(titleMin, titleMax))
        {
            RefreshRootTab();
        }

        if (RootTabLoading())
        {
            LoadingPulse.Spinner(new Vector2(titleMax.X + SpinnerGap * scale, rowCenterY), SpinnerRadius * scale,
                VelvetTheme.RoseInk);
        }

        DrawRootIcons(area, drawList);
    }

    private static int RootTrailingSlots(VelvetPage tab) =>
        tab switch
        {
            VelvetPage.Discover => 3,
            VelvetPage.Feed => 2,
            VelvetPage.Me => 2,
            VelvetPage.Messages => 2,
            _ => 1,
        };

    private bool RootTabLoading() =>
        activeTab switch
        {
            VelvetPage.Feed => store.LoadingFeed,
            VelvetPage.Discover => store.LoadingDiscover,
            _ => false,
        };

    private void RefreshRootTab()
    {
        switch (activeTab)
        {
            case VelvetPage.Feed:
                RefreshFeed();
                break;
            case VelvetPage.Discover:
                ApplyDiscoverFilters();
                break;
        }
    }

    private void DrawRootIcons(Rect area, ImDrawListPtr drawList)
    {
        var scale = UiScale.Current;
        var radius = SocialChrome.HeaderIconRadius * scale;
        var activityCenter = SocialChrome.HeaderSlot(area, 0);
        UiAnchors.Report("velvet.activity", AnchorBox(activityCenter, HeaderAnchorHalf * scale));
        if (SocialChrome.DrawHeaderIcon(drawList, activityCenter, radius, PhoneIcons.Bell, TopBarIconSize,
                Loc.T(L.Velvet.Activity), VelvetInk.Shared, VelvetTheme.TitleInk, false, social.UnseenCount(Id)))
        {
            activityFeed.Invalidate();
            router.Push(VelvetView.Activity);
        }

        switch (activeTab)
        {
            case VelvetPage.Discover:
                DrawFilterIcon(area, drawList, radius, VelvetPage.Discover, "velvet.discover.filter");
                if (SocialChrome.DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 2), radius, PhoneIcons.Search,
                        TopBarIconSize, Loc.T(L.Common.Search), VelvetInk.Shared, VelvetTheme.TitleInk))
                {
                    OpenSearch();
                }

                break;
            case VelvetPage.Feed:
                DrawFilterIcon(area, drawList, radius, VelvetPage.Feed, null);
                break;
            case VelvetPage.Messages:
                if (messagesTab == VelvetMessagesTab.Chats && SocialChrome.DrawHeaderIcon(drawList,
                        SocialChrome.HeaderSlot(area, 1), radius, PhoneIcons.Search, TopBarIconSize,
                        Loc.T(L.Common.Search), VelvetInk.Shared, VelvetTheme.TitleInk, chatsSearchOpen))
                {
                    ToggleChatsSearch();
                }

                break;
            case VelvetPage.Me:
                if (store.Me is { } profile && SocialChrome.DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 1),
                        radius, PhoneIcons.Dots, TopBarIconSize, Loc.T(L.Velvet.More), VelvetInk.Shared,
                        VelvetTheme.TitleInk))
                {
                    OpenProfileMenu(profile);
                }

                break;
        }
    }

    private Rect DrawFeedScopeTabs(Rect area)
    {
        var scale = UiScale.Current;
        var row = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + FeedTabRowHeight * scale));
        var picked = UnderlineTabs.Draw(row, Loc.T(L.Velvet.FeedScopeAll), Loc.T(L.Velvet.FeedScopeConnections),
            store.FeedScope == VelvetFeedScope.Connections, ref feedTabSlide, VelvetInk.Shared, FeedTabsStyle);
        if (picked >= 0)
        {
            var scope = picked == 1 ? VelvetFeedScope.Connections : VelvetFeedScope.All;
            if (scope != store.FeedScope)
            {
                store.SetFeedScope(scope);
                feedScrollTopPending = true;
            }
        }

        return new Rect(new Vector2(area.Min.X, row.Max.Y), area.Max);
    }

    private void DrawFilterIcon(Rect area, ImDrawListPtr drawList, float radius, VelvetPage surface, string? anchorKey)
    {
        var center = SocialChrome.HeaderSlot(area, 1);
        if (anchorKey is not null)
        {
            UiAnchors.Report(anchorKey, AnchorBox(center, HeaderAnchorHalf * UiScale.Current));
        }

        if (SocialChrome.DrawHeaderIcon(drawList, center, radius, PhoneIcons.AdjustmentsHorizontal, TopBarIconSize,
                Loc.T(L.Velvet.FiltersTitle), VelvetInk.Shared, VelvetTheme.TitleInk,
                IncludeFor(surface).Any || mutes.Any))
        {
            OpenFilters(surface);
        }
    }
}
