using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private void DrawProfile(Rect area, string userId)
    {
        if (store.ProfileUserId != userId)
        {
            store.OpenProfile(userId);
        }

        var user = store.ProfileUser;
        var title = user is null
            ? Loc.T(L.Apps.Chirper)
            : SocialIdentity.Name(user.DisplayName, user.Handle);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, title, back);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        if (store.ProfileFailed)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Chirper.ProfileError), AppPalettes.Chirper.MutedInk);
            return;
        }

        if (user is null)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), AppPalettes.Chirper.MutedInk);
            return;
        }

        using (AppSurface.Begin(body))
        {
            profile.DrawProfileHeader(user, theme);
            var posts = store.ProfilePosts;
            ui.SectionHeading(Loc.T(L.Chirper.ChirpsTitle));
            if (posts.Length == 0)
            {
                Typography.DrawCentered(new Vector2(body.Center.X, ImGui.GetCursorScreenPos().Y + 40f * scale),
                    Loc.T(L.Chirper.Empty), AppPalettes.Chirper.MutedInk);
            }
            else
            {
                renderedUnderlyingIds.Clear();
                for (var index = 0; index < posts.Length; index++)
                {
                    if (!renderedUnderlyingIds.Add(posts[index].RepostOfId ?? posts[index].Id))
                    {
                        continue;
                    }

                    DrawPost(posts[index]);
                }

                ImGui.Dummy(new Vector2(0f, 24f * scale));
            }
        }
    }

    private void OpenUserList(string sourceId, UserListKind kind)
    {
        actions.Reset();
        store.OpenUserList(sourceId, kind);
        router.Push(ChirperRoute.UserList(sourceId, kind));
    }

    private void OpenAvatarComposer()
    {
        avatar.Open();
        router.Push(ChirperRoute.Avatar);
    }

    private void DrawAvatarCompose(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        if (avatar.Draw(area, context, Accent))
        {
            store.ReloadProfile();
            router.Pop();
        }
    }

    private void DrawDiscover(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Chirper.FindPeople), back);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var searchHeight = 52f * scale;
        profile.DrawSearchBar(new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + searchHeight)));
        profile.DrawSearchResults(new Rect(new Vector2(area.Min.X, top + searchHeight), area.Max), theme, true);
    }

    private void DrawHomeTopBar(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        const float titleScale = 1.3f;
        var titleCenter = new Vector2(area.Center.X, rowCenterY);
        var titleSize = Typography.Measure(DisplayName, titleScale, FontWeight.Bold);
        var titlePadding = new Vector2(12f * scale, 6f * scale);
        var titleMin = titleCenter - titleSize * 0.5f - titlePadding;
        var titleMax = titleCenter + titleSize * 0.5f + titlePadding;
        UiInteract.HoverHighlight(ImGui.GetWindowDrawList(), titleMin, titleMax, (titleMax.Y - titleMin.Y) * 0.5f);
        Typography.DrawCentered(titleCenter, DisplayName, AppPalettes.Chirper.TitleInk, titleScale, FontWeight.Bold);
        if (UiInteract.HoverClick(titleMin, titleMax))
        {
            RefreshActiveFeed();
        }
        if (store.Me is { } me)
        {
            var radius = 16f * scale;
            var center = new Vector2(area.Min.X + 16f * scale + radius, rowCenterY);
            DrawAvatar(ImGui.GetWindowDrawList(), center, radius, me.Name, me.World, me.AvatarUrl, 0.9f, 28);
            if (UiInteract.HoverClick(center - new Vector2(radius, radius), center + new Vector2(radius, radius)))
            {
                OpenProfile(me.Id);
            }
        }

        if (store.IsSignedIn)
        {
            var refreshCenter = new Vector2(area.Min.X + 68f * scale, rowCenterY);
            if (store.IsLoading(activeScope))
            {
                LoadingPulse.Spinner(refreshCenter, 8f * scale, ui.Accent);
            }
            else if (ui.IconButton(refreshCenter, 16f * scale, FontAwesomeIcon.Sync.ToIconString(),
                         AppPalettes.Chirper.BodyInk, new Vector4(0f, 0f, 0f, 0f), 1.1f, Loc.T(L.Common.Refresh),
                         HoverLabelSide.Below))
            {
                RefreshActiveFeed();
            }
        }

        var searchCenter = new Vector2(area.Max.X - 24f * scale, rowCenterY);
        UiAnchors.Report("chirper.search", new Rect(searchCenter - new Vector2(18f * scale, 18f * scale),
            searchCenter + new Vector2(18f * scale, 18f * scale)));
        if (ui.IconButton(searchCenter, 16f * scale, FontAwesomeIcon.Search.ToIconString(), AppPalettes.Chirper.BodyInk,
                new Vector4(0f, 0f, 0f, 0f), 1.2f, Loc.T(L.Chirper.FindPeople), HoverLabelSide.Below) &&
            store.IsSignedIn)
        {
            store.ClearDiscover();
            profile.SearchDraft = string.Empty;
            router.Push(ChirperRoute.Discover);
        }

        var bellCenter = new Vector2(area.Max.X - 60f * scale, rowCenterY);
        UiAnchors.Report("chirper.activity", new Rect(bellCenter - new Vector2(18f * scale, 18f * scale),
            bellCenter + new Vector2(18f * scale, 18f * scale)));
        if (ui.IconButton(bellCenter, 16f * scale, FontAwesomeIcon.Bell.ToIconString(), AppPalettes.Chirper.BodyInk,
                new Vector4(0f, 0f, 0f, 0f), 1.2f, Loc.T(L.Social.ActivityTitle), HoverLabelSide.Below) &&
            store.IsSignedIn)
        {
            OpenActivity();
        }

        ActivityBadge.Draw(bellCenter + new Vector2(10f * scale, -10f * scale), social.UnseenCount(Id), theme, scale);
    }
}
