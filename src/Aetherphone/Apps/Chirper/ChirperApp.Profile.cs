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
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Chirper;

// Profile screens: a user's profile header/stats, the edit-profile form (avatar, handle, bio), the
// discover/search-people list and the home top bar, plus report/delete moderation. Split from the
// main feed for readability.
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
            : (string.IsNullOrEmpty(user.DisplayName) ? user.Name : user.DisplayName);
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
            DrawProfileHeader(user);
            var posts = store.ProfilePosts;
            ui.SectionHeading(Loc.T(L.Chirper.ChirpsTitle));
            if (posts.Length == 0)
            {
                Typography.DrawCentered(new Vector2(body.Center.X, ImGui.GetCursorScreenPos().Y + 40f * scale),
                    Loc.T(L.Chirper.Empty), AppPalettes.Chirper.MutedInk);
            }
            else
            {
                for (var index = 0; index < posts.Length; index++)
                {
                    DrawPost(posts[index]);
                }

                ImGui.Dummy(new Vector2(0f, 24f * scale));
            }
        }
    }

    private void DrawProfileHeader(UserDto user)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = 16f * scale;
        var innerLeft = origin.X + pad;
        var innerWidth = width - pad * 2f;
        var displayName = string.IsNullOrEmpty(user.DisplayName) ? user.Name : user.DisplayName;
        var avatarRadius = 40f * scale;
        var handleLine = user.Handle.Length > 0 ? $"@{user.Handle}" : string.Empty;
        var worldLine = $"{user.Name} · {user.World}";
        var lineGap = 3f * scale;
        var nameH = Typography.Measure(displayName, 1.4f, FontWeight.Bold).Y;
        var handleH = handleLine.Length > 0 ? Typography.Measure(handleLine, 0.95f).Y + lineGap : 0f;
        var worldH = Typography.Measure(worldLine, 0.95f).Y;
        var bioH = user.Bio.Length > 0 ? 8f * scale + MeasureWrapped(user.Bio, innerWidth, 1f) : 0f;
        var textTop = origin.Y + pad + avatarRadius * 2f + 14f * scale;
        var cardBottom = textTop + nameH + lineGap + handleH + worldH + bioH + pad;
        ui.Card(drawList, origin, new Vector2(origin.X + width, cardBottom), 20f * scale);
        var avatarCenter = new Vector2(innerLeft + avatarRadius, origin.Y + pad + avatarRadius);
        drawList.AddCircleFilled(avatarCenter, avatarRadius + 2.5f * scale,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.14f)), 64);
        DrawAvatar(drawList, avatarCenter, avatarRadius, user.Name, user.World, user.AvatarUrl, 1.5f, 64);
        var buttonHeight = 34f * scale;
        var buttonWidth = 122f * scale;
        var buttonMax = new Vector2(origin.X + width - pad, avatarCenter.Y + buttonHeight * 0.5f);
        var buttonRect = new Rect(new Vector2(buttonMax.X - buttonWidth, buttonMax.Y - buttonHeight), buttonMax);
        var reportShown = false;
        if (user.IsMe)
        {
            if (DrawPillButton(buttonRect, Loc.T(L.Chirper.EditProfile), false))
            {
                editLoadedFor = null;
                router.Push(ChirperRoute.EditProfile);
            }
        }
        else
        {
            var reportCenter = new Vector2(buttonRect.Min.X - buttonHeight * 0.5f - 10f * scale, avatarCenter.Y);
            reportShown = DrawReportToggle(reportCenter, buttonHeight * 0.5f, "user", user.Id);
            if (DrawPillButton(buttonRect, user.IsFollowing ? Loc.T(L.Chirper.Following) : Loc.T(L.Chirper.Follow),
                    !user.IsFollowing))
            {
                store.SetFollow(user.Id, !user.IsFollowing);
            }
        }

        Typography.Draw(new Vector2(innerLeft, textTop), displayName, theme.TextStrong, 1.4f, FontWeight.Bold);
        var textY = textTop + nameH + lineGap;
        if (handleLine.Length > 0)
        {
            Typography.Draw(new Vector2(innerLeft, textY), handleLine, AppPalettes.Chirper.MutedInk, 0.95f);
            textY += handleH;
        }

        Typography.Draw(new Vector2(innerLeft, textY), worldLine, AppPalettes.Chirper.MutedInk, 0.95f);
        textY += worldH;
        if (user.Bio.Length > 0)
        {
            ImGui.SetCursorScreenPos(new Vector2(innerLeft, textY + 8f * scale));
            var bioWrapPos = (innerLeft + innerWidth) - ImGui.GetWindowPos().X;
            ImGui.PushTextWrapPos(bioWrapPos);
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Chirper.BodyInk))
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
            AppPalettes.Chirper.MutedInk, 0.85f);
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
        var dividerColor = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f));
        for (var index = 1; index < 3; index++)
        {
            var x = origin.X + third * index;
            drawList.AddLine(new Vector2(x, origin.Y + 14f * scale), new Vector2(x, origin.Y + height - 14f * scale),
                dividerColor, 1f);
        }

        var followersLabel = Loc.Plural(L.Account.Followers, user.Followers).Split(' ', 2)[^1];
        DrawStatColumn(origin.X + third * 0f, third, centerY, user.Following.ToString(Loc.Culture),
            Loc.T(L.Chirper.Following));
        DrawStatColumn(origin.X + third * 1f, third, centerY, user.Followers.ToString(Loc.Culture), followersLabel);
        DrawStatColumn(origin.X + third * 2f, third, centerY, user.Posts.ToString(Loc.Culture), PostsLabel(user.Posts));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawStatColumn(float left, float columnWidth, float centerY, string value, string label)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var center = left + columnWidth * 0.5f;
        Typography.DrawCentered(new Vector2(center, centerY - 10f * scale), value, theme.TextStrong, 1.25f,
            FontWeight.Bold);
        Typography.DrawCentered(new Vector2(center, centerY + 13f * scale), label, AppPalettes.Chirper.MutedInk, 0.8f);
    }

    private bool DrawReportToggle(Vector2 center, float radius, string targetType, string targetId)
    {
        var active = reportTargetType == targetType && reportTargetId == targetId;
        var background = Palette.WithAlpha(theme.Danger, active ? 0.32f : 0.16f);
        if (DrawIconButton(center, radius, FontAwesomeIcon.Flag.ToIconString(), theme.Danger, background, 0.9f))
        {
            if (active)
            {
                reportTargetType = null;
                reportTargetId = null;
                active = false;
            }
            else
            {
                reportTargetType = targetType;
                reportTargetId = targetId;
                reportReasonDraft = string.Empty;
                reportStatus = string.Empty;
                active = true;
            }
        }

        return active;
    }

    private void DrawReportComposer(float left, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var buttonWidth = 84f * scale;
        var buttonHeight = 28f * scale;
        ImGui.SetCursorScreenPos(new Vector2(left, origin.Y));
        ImGui.SetNextItemWidth(width - buttonWidth - 8f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppPalettes.Chirper.FieldSurface))
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Chirper.TitleInk))
        {
            ImGui.InputTextWithHint("##reportReason", Loc.T(L.Chirper.ReportReasonHint), ref reportReasonDraft,
                MaxReportReasonLength);
        }

        var buttonRect = new Rect(new Vector2(left + width - buttonWidth, origin.Y - 2f * scale),
            new Vector2(left + width, origin.Y - 2f * scale + buttonHeight));
        var canSubmit = !reportSubmitting;
        if (DrawPillButton(buttonRect, reportSubmitting ? Loc.T(L.Chirper.Saving) : Loc.T(L.Chirper.ReportSubmit),
                canSubmit) && canSubmit)
        {
            SubmitReport();
        }

        ImGui.SetCursorScreenPos(new Vector2(left, origin.Y + buttonHeight + 2f * scale));
        if (reportStatus.Length > 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Chirper.MutedInk))
            {
                ImGui.TextUnformatted(reportStatus);
            }

            ImGui.Dummy(new Vector2(0f, 4f * scale));
        }
    }

    private void SubmitReport()
    {
        if (reportSubmitting || reportTargetType is not { } targetType || reportTargetId is not { } targetId)
        {
            return;
        }

        reportSubmitting = true;
        var reason = reportReasonDraft.Trim();
        store.Report(targetType, targetId, reason.Length > 0 ? reason : null, ok =>
        {
            reportSubmitting = false;
            reportStatus = Loc.T(ok ? L.Chirper.ReportSent : L.Chirper.ReportFailed);
            if (ok)
            {
                reportTargetType = null;
                reportTargetId = null;
            }
        });
    }

    private void AskDeleteComment(string postId, string commentId)
    {
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Chirper.DeleteCommentConfirmMessage),
            ConfirmLabel = Loc.T(L.Chirper.DeleteConfirm),
            CancelLabel = Loc.T(L.Chirper.DeleteCancel),
            BusyLabel = Loc.T(L.Chirper.Saving),
            FailedMessage = Loc.T(L.Chirper.DeleteCommentFailed),
            ConfirmAsync = done => store.DeleteComment(postId, commentId, done),
        });
    }

    private void DrawDeleteCommentTooltip(Vector2 iconCenter, float hitRadius, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var tooltipText = Loc.T(L.Chirper.DeleteComment);
        var textSize = Typography.Measure(tooltipText, 0.78f, FontWeight.Medium);
        var padX = 9f * scale;
        var padY = 5f * scale;
        var bubbleSize = new Vector2(textSize.X + padX * 2f, textSize.Y + padY * 2f);
        var gap = 9f * scale;
        var windowMin = ImGui.GetWindowPos();
        var windowMax = windowMin + ImGui.GetWindowSize();
        var minX = Math.Clamp(iconCenter.X - bubbleSize.X * 0.5f, windowMin.X + 4f * scale,
            windowMax.X - bubbleSize.X - 4f * scale);
        var minY = iconCenter.Y - hitRadius - gap - bubbleSize.Y;
        if (minY < windowMin.Y + 4f * scale)
        {
            minY = iconCenter.Y + hitRadius + gap;
        }

        var min = new Vector2(minX, minY);
        var max = min + bubbleSize;
        var bubble = Palette.WithAlpha(Palette.Mix(theme.AppBackground, theme.TextStrong, 0.9f), 0.97f);
        Squircle.Fill(dl, min, max, bubbleSize.Y * 0.5f, ImGui.GetColorU32(bubble));
        Typography.Draw(dl, new Vector2(min.X + padX, min.Y + padY), tooltipText, theme.AppBackground, 0.78f,
            FontWeight.Medium);
    }

    private void AskDeletePost(string postId)
    {
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Chirper.DeleteConfirmMessage),
            ConfirmLabel = Loc.T(L.Chirper.DeleteConfirm),
            CancelLabel = Loc.T(L.Chirper.DeleteCancel),
            BusyLabel = Loc.T(L.Chirper.Saving),
            FailedMessage = Loc.T(L.Chirper.DeleteFailed),
            ConfirmAsync = done => store.DeletePost(postId, done),
        });
    }

    private void DrawEditProfile(Rect area)
    {
        var me = store.Me ?? (store.ProfileUser is { IsMe: true } self ? self : null);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Chirper.EditProfile), back);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        if (me is null)
        {
            store.EnsureMe();
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), AppPalettes.Chirper.MutedInk);
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
            editStatus = Loc.T(L.Chirper.HandleTaken);
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
        if (DrawHeaderAction(area, editBusy ? Loc.T(L.Chirper.Saving) : Loc.T(L.Chirper.Save), canSave))
        {
            SaveProfile();
        }

        using (AppSurface.Begin(body))
        {
            var avatarRadius = 34f * scale;
            var avatarOrigin = ImGui.GetCursorScreenPos();
            var avatarCenter = new Vector2(avatarOrigin.X + ImGui.GetContentRegionAvail().X * 0.5f,
                avatarOrigin.Y + avatarRadius);
            DrawAvatar(ImGui.GetWindowDrawList(), avatarCenter, avatarRadius, me.Name, me.World, me.AvatarUrl, 1.3f,
                48);
            ImGui.SetCursorScreenPos(new Vector2(avatarOrigin.X, avatarCenter.Y + avatarRadius + 8f * scale));
            var changeWidth = 150f * scale;
            var changeTop = ImGui.GetCursorScreenPos().Y;
            var changeRect = new Rect(new Vector2(avatarCenter.X - changeWidth * 0.5f, changeTop),
                new Vector2(avatarCenter.X + changeWidth * 0.5f, changeTop + 30f * scale));
            if (DrawPillButton(changeRect, Loc.T(L.Chirper.ChangePhoto), false))
            {
                OpenAvatarComposer();
            }

            ImGui.SetCursorScreenPos(new Vector2(avatarOrigin.X, changeRect.Max.Y + 16f * scale));
            DrawField(Loc.T(L.Chirper.DisplayNameLabel), "##editDisplay", ref editDisplay, DisplayNameMax, false);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            DrawHandleField();
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            DrawField(Loc.T(L.Chirper.BioLabel), "##editBio", ref editBio, BioMax, true);
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

    private void DrawHandleField()
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Chirper.MutedInk))
        {
            ImGui.TextUnformatted(Loc.T(L.Chirper.HandleLabel));
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 34f * scale;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 9f * scale,
            ImGui.GetColorU32(AppPalettes.Chirper.FieldSurface));
        Typography.Draw(new Vector2(origin.X + 12f * scale, origin.Y + height * 0.5f - 8f * scale), "@",
            AppPalettes.Chirper.MutedInk, 1f);
        ImGui.SetCursorScreenPos(new Vector2(origin.X + 26f * scale,
            origin.Y + height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(width - 38f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, IsHandleValid(editHandle) ? AppPalettes.Chirper.TitleInk : theme.Danger))
        {
            if (ImGui.InputText("##editHandle", ref editHandle, HandleMax, ImGuiInputTextFlags.CharsNoBlank))
            {
                editHandle = editHandle.ToLowerInvariant();
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        Typography.Draw(new Vector2(origin.X + 2f * scale, origin.Y + height + 3f * scale),
            Loc.T(L.Chirper.HandleRules), AppPalettes.Chirper.MutedInk, 0.78f);
        ImGui.Dummy(new Vector2(width, 16f * scale));
    }

    private void DrawField(string label, string id, ref string value, int maxLength, bool multiline)
    {
        ui.Field(label, id, ref value, maxLength, multiline);
    }

    private void SaveProfile()
    {
        if (!store.IsSignedIn || editBusy)
        {
            return;
        }

        if (!IsHandleValid(editHandle) || editDisplay.Trim().Length == 0)
        {
            editStatus = Loc.T(L.Chirper.HandleRules);
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
        AppHeader.Draw(context, Loc.T(L.Chirper.FindPeople), back);
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
                    store.Searching ? Loc.T(L.Common.Searching) : Loc.T(L.Chirper.SearchByName), AppPalettes.Chirper.MutedInk);
            }
            else
            {
                ImGui.Dummy(new Vector2(0f, 4f * scale));
                for (var index = 0; index < snapshot.Length; index++)
                {
                    DrawUserRow(snapshot[index]);
                }
            }
        }
    }

    private void DrawUserRow(UserDto user)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowHeight = 58f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + rowHeight), 16f * scale);
        var pad = 12f * scale;
        var radius = 20f * scale;
        var avatarCenter = new Vector2(origin.X + pad + radius, origin.Y + rowHeight * 0.5f);
        DrawAvatar(drawList, avatarCenter, radius, user.Name, user.World, user.AvatarUrl, 0.95f, 32);
        var textLeft = avatarCenter.X + radius + 12f * scale;
        var displayName = string.IsNullOrEmpty(user.DisplayName) ? user.Name : user.DisplayName;
        Typography.Draw(new Vector2(textLeft, origin.Y + 12f * scale), displayName, theme.TextStrong, 1f,
            FontWeight.SemiBold);
        var sub = user.Handle.Length > 0 ? $"@{user.Handle} · {user.World}" : $"{user.Name} · {user.World}";
        Typography.Draw(new Vector2(textLeft, origin.Y + 33f * scale), sub, AppPalettes.Chirper.MutedInk, 0.85f);
        var buttonWidth = 96f * scale;
        var buttonHeight = 30f * scale;
        var buttonRect =
            new Rect(
                new Vector2(origin.X + width - pad - buttonWidth, origin.Y + rowHeight * 0.5f - buttonHeight * 0.5f),
                new Vector2(origin.X + width - pad, origin.Y + rowHeight * 0.5f + buttonHeight * 0.5f));
        if (DrawPillButton(buttonRect, user.IsFollowing ? Loc.T(L.Chirper.Following) : Loc.T(L.Chirper.Follow),
                !user.IsFollowing))
        {
            store.SetFollow(user.Id, !user.IsFollowing);
        }

        var rowMin = origin;
        var rowMax = new Vector2(origin.X + width - buttonWidth - pad - 6f * scale, origin.Y + rowHeight);
        if (HoverClick(rowMin, rowMax))
        {
            OpenProfile(user.Id);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }

    private void DrawSearchBar(Rect bar)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var pillMin = new Vector2(bar.Min.X + 12f * scale, bar.Min.Y + 9f * scale);
        var pillMax = new Vector2(bar.Max.X - 12f * scale, bar.Max.Y - 9f * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f,
            ImGui.GetColorU32(AppPalettes.Chirper.FieldSurface));
        AppSkin.Icon(new Vector2(pillMin.X + 16f * scale, (pillMin.Y + pillMax.Y) * 0.5f),
            FontAwesomeIcon.Search.ToIconString(), AppPalettes.Chirper.MutedInk, 0.85f);
        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 32f * scale,
            (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 44f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Chirper.TitleInk))
        {
            if (ImGui.InputTextWithHint("##chirperSearch", Loc.T(L.Chirper.NameOrWorld), ref searchDraft, 64,
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
            AppPalettes.Chirper.TitleInk, 1.3f, FontWeight.Bold);
        var me = store.Me;
        var searchCenter = new Vector2(area.Max.X - 22f * scale, rowCenterY);
        if (me is not null)
        {
            var radius = 14f * scale;
            var center = new Vector2(area.Max.X - 52f * scale, rowCenterY);
            DrawAvatar(ImGui.GetWindowDrawList(), center, radius, me.Name, me.World, me.AvatarUrl, 0.85f, 24);
            if (HoverClick(center - new Vector2(radius, radius), center + new Vector2(radius, radius)))
            {
                OpenProfile(me.Id);
            }
        }

        if (DrawIconButton(searchCenter, 14f * scale, FontAwesomeIcon.Search.ToIconString(), AppPalettes.Chirper.BodyInk,
                new Vector4(0f, 0f, 0f, 0f), 0.95f) && store.IsSignedIn)
        {
            store.ClearDiscover();
            searchDraft = string.Empty;
            router.Push(ChirperRoute.Discover);
        }
    }
}
