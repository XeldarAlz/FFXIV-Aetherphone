using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Aethergram;

// Profile screens: another user's profile header/stats/grid, the edit-profile form, and the
// discover/search-people list. Split from the main feed for readability.
internal sealed partial class AethergramApp
{
    private void DrawProfileHeader(UserDto user)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = 16f * scale;
        var innerLeft = origin.X + pad;
        var innerWidth = width - pad * 2f;
        var displayName = SocialIdentity.Name(user.DisplayName, user.Handle);
        var avatarRadius = 40f * scale;
        var regionCode = user.IsMe ? SocialRegion.EffectiveCode(configuration, gameData) : gameData.RegionCodeForWorld(user.World);
        var metaLine = SocialIdentity.ProfileMeta(user.Handle, regionCode);
        var lineGap = 3f * scale;
        var nameH = Typography.Measure(displayName, 1.4f, FontWeight.Bold).Y;
        var metaH = metaLine.Length > 0 ? Typography.Measure(metaLine, 0.95f).Y : 0f;
        var bioH = user.Bio.Length > 0 ? 8f * scale + MeasureWrapped(user.Bio, innerWidth, 1f) : 0f;
        var textTop = origin.Y + pad + avatarRadius * 2f + 14f * scale;
        var cardBottom = textTop + nameH + lineGap + metaH + bioH + pad;
        ui.Card(drawList, origin, new Vector2(origin.X + width, cardBottom), 20f * scale);
        var avatarCenter = new Vector2(innerLeft + avatarRadius, origin.Y + pad + avatarRadius);
        drawList.AddCircleFilled(avatarCenter, avatarRadius + 2.5f * scale,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.14f)), 64);
        DrawAvatar(avatarCenter, avatarRadius, user.Name, user.World, user.AvatarUrl, 1.5f, 64);
        var buttonHeight = 34f * scale;
        var buttonWidth = 122f * scale;
        var buttonMax = new Vector2(origin.X + width - pad, avatarCenter.Y + buttonHeight * 0.5f);
        var buttonRect = new Rect(new Vector2(buttonMax.X - buttonWidth, buttonMax.Y - buttonHeight), buttonMax);
        var reportShown = false;
        if (user.IsMe)
        {
            if (DrawPillButton(buttonRect, Loc.T(L.Aethergram.EditProfile), false))
            {
                editLoadedFor = null;
                router.Push(AethergramRoute.EditProfile);
            }
        }
        else
        {
            var reportCenter = new Vector2(buttonRect.Min.X - buttonHeight * 0.5f - 10f * scale, avatarCenter.Y);
            reportShown = DrawReportToggle(reportCenter, buttonHeight * 0.5f, "user", user.Id);
            if (DrawPillButton(buttonRect,
                    user.IsFollowing ? Loc.T(L.Aethergram.Following) : Loc.T(L.Aethergram.Follow), !user.IsFollowing))
            {
                store.SetFollow(user.Id, !user.IsFollowing);
            }
        }

        Typography.Draw(new Vector2(innerLeft, textTop), displayName, theme.TextStrong, 1.4f, FontWeight.Bold);
        var textY = textTop + nameH + lineGap;
        if (metaLine.Length > 0)
        {
            Typography.Draw(new Vector2(innerLeft, textY), metaLine, AppPalettes.Aethergram.MutedInk, 0.95f);
            textY += metaH;
        }

        if (user.Bio.Length > 0)
        {
            ImGui.SetCursorScreenPos(new Vector2(innerLeft, textY + 8f * scale));
            ImGui.PushTextWrapPos(innerLeft + innerWidth);
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Aethergram.BodyInk))
            {
                ImGui.TextWrapped(user.Bio);
            }

            ImGui.PopTextWrapPos();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardBottom - origin.Y + 10f * scale));
        if (reportShown)
        {
            DrawReportComposer(innerLeft, innerWidth);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }

        DrawProfileStats(user);
        DrawProfileTimeZone(user);
        ImGui.Dummy(new Vector2(0f, 14f * scale));
    }

    private void DrawProfileTimeZone(UserDto user)
    {
        if (user.UtcOffsetMinutes is not { } offset)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        var text = $"{Loc.T(L.Profile.LocalTimeLabel)}  {SocialTimeZone.Describe(offset)}";
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var textSize = Typography.Measure(text, 0.85f);
        Typography.DrawCentered(new Vector2(origin.X + width * 0.5f, origin.Y + textSize.Y * 0.5f), text,
            AppPalettes.Aethergram.MutedInk, 0.85f);
        ImGui.Dummy(new Vector2(width, textSize.Y));
    }

    private void DrawProfileStats(UserDto user)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 64f * scale;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 18f * scale);
        var third = width / 3f;
        var centerY = origin.Y + height * 0.5f;
        DrawStatHover(drawList, origin, third * 1f, third, height, scale);
        DrawStatHover(drawList, origin, third * 2f, third, height, scale);
        var dividerColor = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f));
        for (var index = 1; index < 3; index++)
        {
            var x = origin.X + third * index;
            drawList.AddLine(new Vector2(x, origin.Y + 14f * scale), new Vector2(x, origin.Y + height - 14f * scale),
                dividerColor, 1f);
        }

        var followersLabel = FollowersLabel(user.Followers);
        DrawStatColumn(origin.X + third * 0f, third, centerY, user.Grams.ToString(Loc.Culture), PostsLabel(user.Grams));
        DrawStatColumn(origin.X + third * 1f, third, centerY, user.Followers.ToString(Loc.Culture), followersLabel);
        DrawStatColumn(origin.X + third * 2f, third, centerY, user.Following.ToString(Loc.Culture),
            Loc.T(L.Aethergram.Following));
        if (HoverClick(new Vector2(origin.X + third, origin.Y), new Vector2(origin.X + third * 2f, origin.Y + height)))
        {
            OpenUserList(user.Id, UserListKind.Followers);
        }

        if (HoverClick(new Vector2(origin.X + third * 2f, origin.Y), new Vector2(origin.X + width, origin.Y + height)))
        {
            OpenUserList(user.Id, UserListKind.Following);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawStatColumn(float left, float columnWidth, float centerY, string value, string label)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var center = left + columnWidth * 0.5f;
        Typography.DrawCentered(new Vector2(center, centerY - 10f * scale), value, theme.TextStrong, 1.25f,
            FontWeight.Bold);
        Typography.DrawCentered(new Vector2(center, centerY + 13f * scale), label, AppPalettes.Aethergram.MutedInk, 0.8f);
    }

    private static void DrawStatHover(ImDrawListPtr drawList, Vector2 origin, float columnOffset, float columnWidth,
        float height, float scale)
    {
        var padX = 6f * scale;
        var padY = 8f * scale;
        var min = new Vector2(origin.X + columnOffset + padX, origin.Y + padY);
        var max = new Vector2(origin.X + columnOffset + columnWidth - padX, origin.Y + height - padY);
        UiInteract.HoverHighlight(drawList, min, max, 12f * scale);
    }

    private void DrawProfileGrid()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var posts = store.ProfilePosts;
        if (posts.Length == 0)
        {
            Typography.DrawCentered(
                new Vector2(ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X * 0.5f,
                    ImGui.GetCursorScreenPos().Y + 40f * scale), Loc.T(L.Aethergram.Empty), AppPalettes.Aethergram.MutedInk);
            return;
        }

        var gap = 3f * scale;
        var cell = (ImGui.GetContentRegionAvail().X - gap * (GridColumns - 1)) / GridColumns;
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap)))
        {
            for (var index = 0; index < posts.Length; index++)
            {
                using (ImRaii.PushId(index))
                {
                    var clicked = ImGui.InvisibleButton("gram", new Vector2(cell, cell));
                    DrawGridThumbnail(posts[index], ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
                    if (clicked)
                    {
                        OpenDetail(posts[index]);
                    }
                }

                if (index % GridColumns != GridColumns - 1)
                {
                    ImGui.SameLine();
                }
            }
        }

        ImGui.Dummy(new Vector2(0f, 24f * scale));
    }

    private void DrawGridThumbnail(PostDto post, Vector2 min, Vector2 max)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 8f * ImGuiHelpers.GlobalScale;
        var texture = images.Get(post.MediaUrl);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(AppPalettes.Aethergram.FieldSurface));
            return;
        }

        var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
        drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        if (ImGui.IsItemHovered())
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.1f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private void DrawEditProfile(Rect area)
    {
        var me = store.Me ?? (store.ProfileUser is { IsMe: true } self ? self : null);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Aethergram.EditProfile), back);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        if (me is null)
        {
            store.EnsureMe();
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), AppPalettes.Aethergram.MutedInk);
            return;
        }

        if (editOutcome == 1)
        {
            editOutcome = 0;
            store.ReloadProfile();
            router.Pop();
            return;
        }

        if (editOutcome == 2)
        {
            editOutcome = 0;
            editStatus = Loc.T(L.Aethergram.HandleTaken);
        }

        if (editLoadedFor != me.Id)
        {
            editLoadedFor = me.Id;
            editDisplay = me.DisplayName;
            editHandle = me.Handle;
            editBio = me.Bio;
            editStatus = string.Empty;
        }

        var handleValid = IsHandleValid(editHandle);
        var canSave = !editBusy && editDisplay.Trim().Length > 0 && handleValid;
        if (DrawHeaderAction(area, editBusy ? Loc.T(L.Aethergram.Saving) : Loc.T(L.Aethergram.Save), canSave))
        {
            SaveProfile();
        }

        using (AppSurface.Begin(body))
        {
            var origin = ImGui.GetCursorScreenPos();
            var avatarRadius = 34f * scale;
            var avatarCenter = new Vector2(origin.X + ImGui.GetContentRegionAvail().X * 0.5f, origin.Y + avatarRadius);
            DrawAvatar(avatarCenter, avatarRadius, me.Name, me.World, me.AvatarUrl, 1.3f, 48);
            ImGui.SetCursorScreenPos(new Vector2(origin.X, avatarCenter.Y + avatarRadius + 8f * scale));
            var changeWidth = 150f * scale;
            var changeRect = new Rect(new Vector2(avatarCenter.X - changeWidth * 0.5f, ImGui.GetCursorScreenPos().Y),
                new Vector2(avatarCenter.X + changeWidth * 0.5f, ImGui.GetCursorScreenPos().Y + 30f * scale));
            if (DrawPillButton(changeRect, Loc.T(L.Aethergram.ChangePhoto), false))
            {
                StartCompose(true);
            }

            ImGui.SetCursorScreenPos(new Vector2(origin.X, changeRect.Max.Y + 16f * scale));
            DrawField(Loc.T(L.Aethergram.DisplayNameLabel), "##editDisplay", ref editDisplay, DisplayNameMax, false);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            DrawHandleField();
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            DrawField(Loc.T(L.Aethergram.BioLabel), "##editBio", ref editBio, BioMax, true);
            if (editStatus.Length > 0)
            {
                ImGui.Dummy(new Vector2(0f, 10f * scale));
                using (ImRaii.PushColor(ImGuiCol.Text, theme.Danger))
                {
                    ImGui.TextWrapped(editStatus);
                }
            }
        }
    }

    private void SaveProfile()
    {
        var me = store.Me;
        if (me is null || editBusy)
        {
            return;
        }

        if (!IsHandleValid(editHandle) || editDisplay.Trim().Length == 0)
        {
            editStatus = Loc.T(L.Aethergram.HandleRules);
            return;
        }

        editBusy = true;
        editStatus = string.Empty;
        store.UpdateProfile(editDisplay.Trim(), editHandle.Trim(), editBio.Trim(), (ok, _) =>
        {
            editBusy = false;
            editOutcome = ok ? 1 : 2;
        });
    }

    private void DrawDiscover(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Aethergram.FindPeople), back);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var searchHeight = 52f * scale;
        DrawSearchBar(new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + searchHeight)));
        var listRect = new Rect(new Vector2(area.Min.X, top + searchHeight), area.Max);
        var snapshot = store.DiscoverResults;
        using (AppSurface.Begin(listRect))
        {
            if (snapshot.Length == 0)
            {
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale),
                    store.Searching ? Loc.T(L.Common.Searching) : Loc.T(L.Aethergram.SearchByName),
                    AppPalettes.Aethergram.MutedInk);
            }
            else
            {
                for (var index = 0; index < snapshot.Length; index++)
                {
                    DrawUserRow(snapshot[index]);
                }
            }
        }
    }

    private void DrawUserList(Rect area, string sourceId, UserListKind kind)
    {
        store.OpenUserList(sourceId, kind);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, UserListTitle(kind), back);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var listRect = new Rect(new Vector2(area.Min.X, top), area.Max);
        var snapshot = store.UserListResults;
        using (AppSurface.Begin(listRect))
        {
            if (snapshot.Length == 0)
            {
                var message = store.UserListLoading ? Loc.T(L.Common.Loading)
                    : store.UserListFailed ? Loc.T(L.Aethergram.ProfileError)
                    : Loc.T(L.Social.ListEmpty);
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale), message,
                    AppPalettes.Aethergram.MutedInk);
            }
            else
            {
                ImGui.Dummy(new Vector2(0f, 4f * scale));
                for (var index = 0; index < snapshot.Length; index++)
                {
                    DrawUserRow(snapshot[index]);
                }

                ImGui.Dummy(new Vector2(0f, 12f * scale));
            }
        }
    }

    private static string UserListTitle(UserListKind kind) => kind switch
    {
        UserListKind.Followers => Loc.T(L.Social.FollowersTitle),
        UserListKind.Following => Loc.T(L.Social.FollowingTitle),
        _ => Loc.T(L.Social.LikedByTitle),
    };

    private void DrawUserRow(UserDto user)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowHeight = 58f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var radius = 20f * scale;
        var avatarCenter = new Vector2(origin.X + radius, origin.Y + rowHeight * 0.5f);
        DrawAvatar(avatarCenter, radius, user.Name, user.World, user.AvatarUrl, 0.95f, 32);
        var textLeft = origin.X + radius * 2f + 12f * scale;
        var displayName = SocialIdentity.Name(user.DisplayName, user.Handle);
        Typography.Draw(new Vector2(textLeft, origin.Y + 9f * scale), displayName, theme.TextStrong, 1f,
            FontWeight.SemiBold);
        var regionCode = user.IsMe ? SocialRegion.EffectiveCode(configuration, gameData) : gameData.RegionCodeForWorld(user.World);
        var sub = SocialIdentity.ProfileMeta(user.Handle, regionCode);
        Typography.Draw(new Vector2(textLeft, origin.Y + 31f * scale), sub, AppPalettes.Aethergram.MutedInk, 0.85f);
        var buttonWidth = 96f * scale;
        var buttonHeight = 30f * scale;
        var buttonRect =
            new Rect(new Vector2(origin.X + width - buttonWidth, origin.Y + rowHeight * 0.5f - buttonHeight * 0.5f),
                new Vector2(origin.X + width, origin.Y + rowHeight * 0.5f + buttonHeight * 0.5f));
        if (DrawPillButton(buttonRect, user.IsFollowing ? Loc.T(L.Aethergram.Following) : Loc.T(L.Aethergram.Follow),
                !user.IsFollowing))
        {
            store.SetFollow(user.Id, !user.IsFollowing);
        }

        if (HoverClick(origin, new Vector2(origin.X + width - buttonWidth - 6f * scale, origin.Y + rowHeight)))
        {
            OpenProfile(user.Id);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }

    private void DrawSearchBar(Rect bar)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var pillMin = new Vector2(bar.Min.X + 12f * scale, bar.Min.Y + 9f * scale);
        var pillMax = new Vector2(bar.Max.X - 12f * scale, bar.Max.Y - 9f * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f,
            ImGui.GetColorU32(AppPalettes.Aethergram.FieldSurface));
        AppSkin.Icon(new Vector2(pillMin.X + 16f * scale, (pillMin.Y + pillMax.Y) * 0.5f),
            FontAwesomeIcon.Search.ToIconString(), AppPalettes.Aethergram.MutedInk, 0.85f);
        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 32f * scale,
            (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 44f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##aethergramSearch", Loc.T(L.Aethergram.NameOrWorld), ref searchDraft, 64,
                    ImGuiInputTextFlags.EnterReturnsTrue))
            {
                store.Search(searchDraft);
            }
        }
    }

    private void DrawHomeTopBar(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var logoSize = Typography.Measure(DisplayName, 1.3f, FontWeight.Bold);
        Typography.Draw(new Vector2(area.Min.X + 16f * scale, rowCenterY - logoSize.Y * 0.5f), DisplayName,
            AppPalettes.Aethergram.TitleInk, 1.3f, FontWeight.Bold);
    }
}
