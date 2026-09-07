using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Report;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private static Rect Reserve(float heightUnscaled)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + heightUnscaled * scale));
        ImGui.Dummy(new Vector2(width, heightUnscaled * scale));
        return rect;
    }

    private readonly List<VChipModel> chipModels = new();

    private int DrawChipFlow(float width, float scale) =>
        VChipFlow.Draw(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(chipModels), width, scale);

    private float MeasureChipFlow(float width, float scale) =>
        VChipFlow.Measure(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(chipModels), width, scale);

    private static void DrawInsetHelpText(string text)
    {
        var scale = UiScale.Current;
        var pad = SocialChrome.CellPadX * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y), text, VelvetTheme.MutedInk,
            TextStyles.Footnote, MathF.Max(1f, width - pad * 2f));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private const float EmptyStateTop = 76f;
    private const float EmptyStateGap = 8f;

    private static float DrawEmpty(Rect area, string title, string body)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var maxWidth = MathF.Max(1f, area.Width - FeedCell.PadX * 2f * scale);
        var top = area.Min.Y + EmptyStateTop * scale;
        var titleBottom = Typography.DrawWrappedCentered(drawList, title, TextStyles.Headline, VelvetTheme.TitleInk,
            new Vector2(area.Center.X, top), maxWidth);
        if (body.Length == 0)
        {
            return titleBottom;
        }

        return Typography.DrawWrappedCentered(drawList, body, TextStyles.Subheadline, VelvetTheme.MutedInk,
            new Vector2(area.Center.X, titleBottom + EmptyStateGap * scale), maxWidth);
    }

    private void FillWhoLabels()
    {
        whoLabels[0] = Loc.T(L.Velvet.WhoEveryone);
        whoLabels[1] = Loc.T(L.Velvet.WhoFriends);
        whoLabels[2] = Loc.T(L.Velvet.WhoNoOne);
    }

    private static void Gap(float pixels)
    {
        ImGui.Dummy(new Vector2(0f, pixels * UiScale.Current));
    }

    private static Rect AnchorBox(Vector2 center, float half)
    {
        var offset = new Vector2(half, half);
        return new Rect(center - offset, center + offset);
    }

    private static void WrapText(string text, Vector4 color, in TextStyle style)
    {
        ImGui.PushTextWrapPos(0f);
        using (ImRaii.PushColor(ImGuiCol.Text, color))
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Typography.Wrapped(text);
        }

        ImGui.PopTextWrapPos();
    }

    private void OpenReport(string targetType, string targetId, string title)
    {
        report.Open(new ReportPrompt
        {
            Title = title,
            Submit = (reason, done) => store.Report(targetType, targetId, reason, succeeded =>
            {
                if (succeeded)
                {
                    reportedTargets.Add(targetId);
                }

                done(succeeded);
            }),
        });
    }

    private bool AlreadyReported(string targetId) => reportedTargets.Contains(targetId);

    private void OpenPostSheet(VelvetPostDto post, bool inFeed)
    {
        sheetPost = post;
        sheetPostInFeed = inFeed;
        postSheetTitle = DisplayNameOf(post.OwnerDisplayName, post.OwnerHandle);
        postSheetCount = 0;
        if (inFeed)
        {
            AddPostSheetItem(PostSheetAction.View, Loc.T(L.Velvet.ViewPost), false);
        }

        if (store.Me is { } me && me.UserId == post.OwnerId)
        {
            AddPostSheetItem(PostSheetAction.Edit, Loc.T(L.Velvet.EditCaption), false);
            AddPostSheetItem(PostSheetAction.Audience,
                Loc.T(post.Audience == VelvetPostAudience.Public ? L.Velvet.MakeConnections : L.Velvet.MakePublic),
                false);
            AddPostSheetItem(PostSheetAction.Delete, Loc.T(L.Velvet.DeleteConfirm), true);
        }
        else
        {
            AddPostSheetItem(PostSheetAction.Report, Loc.T(L.Velvet.Report), true);
            AddPostSheetItem(PostSheetAction.Block, Loc.T(L.Velvet.Block), true);
        }

        postSheet.Open();
    }

    private void AddPostSheetItem(PostSheetAction action, string label, bool danger)
    {
        postSheetActions[postSheetCount] = action;
        postSheetItems[postSheetCount] = new ActionSheet.Item(label, string.Empty, danger);
        postSheetCount++;
    }

    private void DrawPostSheet(Rect screen)
    {
        if (!postSheet.CapturesPointer)
        {
            return;
        }

        var picked = postSheet.Draw(screen, ActionSheetStyle.From(ui), postSheetItems.AsSpan(0, postSheetCount),
            Loc.T(L.Common.Cancel), false, postSheetTitle);
        if (picked < 0 || sheetPost is not { } post)
        {
            return;
        }

        switch (postSheetActions[picked])
        {
            case PostSheetAction.View:
                OpenPostDetail(post.Id);
                break;
            case PostSheetAction.Edit:
                OpenEditCaption(post);
                break;
            case PostSheetAction.Audience:
                store.SetPostAudience(post, post.Audience == VelvetPostAudience.Public
                    ? VelvetPostAudience.Connections
                    : VelvetPostAudience.Public);
                break;
            case PostSheetAction.Delete:
                AskDeletePost(post.Id, sheetPostInFeed ? null : back);
                break;
            case PostSheetAction.Report:
                OpenReport("velvet_post", post.Id, Loc.T(L.Velvet.ReportPost));
                break;
            case PostSheetAction.Block:
                AskBlock(post.OwnerId, DisplayNameOf(post.OwnerDisplayName, post.OwnerHandle));
                break;
        }
    }

    private void OpenProfileMenu(VelvetProfileDto user)
    {
        profileMenuUserId = user.UserId;
        profileMenuName = DisplayNameOf(user.DisplayName, user.Handle);
        profileMenuCount = 0;
        if (store.Me?.UserId == user.UserId)
        {
            AddProfileMenuItem(ProfileMenuAction.Settings, Loc.T(L.Velvet.Settings), false);
            AddProfileMenuItem(ProfileMenuAction.Rules, Loc.T(L.Conduct.Eyebrow), false);
        }
        else
        {
            if (user.ConnectionState == VelvetConnectionState.Connected)
            {
                AddProfileMenuItem(ProfileMenuAction.Disconnect, Loc.T(L.Velvet.Disconnect), false);
            }

            AddProfileMenuItem(ProfileMenuAction.NotInterested, Loc.T(L.Velvet.NotInterested), false);
            if (!AlreadyReported(user.UserId))
            {
                AddProfileMenuItem(ProfileMenuAction.Report, Loc.T(L.Velvet.Report), true);
            }

            AddProfileMenuItem(ProfileMenuAction.Block, Loc.T(L.Velvet.Block), true);
        }

        profileMenu.Open();
    }

    private void AddProfileMenuItem(ProfileMenuAction action, string label, bool danger)
    {
        profileMenuActions[profileMenuCount] = action;
        profileMenuItems[profileMenuCount] = new ActionSheet.Item(label, string.Empty, danger);
        profileMenuCount++;
    }

    private void DrawProfileMenu(Rect screen)
    {
        if (!profileMenu.CapturesPointer)
        {
            return;
        }

        var picked = profileMenu.Draw(screen, ActionSheetStyle.From(ui),
            profileMenuItems.AsSpan(0, profileMenuCount), Loc.T(L.Common.Cancel), false, profileMenuName);
        if (picked < 0)
        {
            return;
        }

        switch (profileMenuActions[picked])
        {
            case ProfileMenuAction.Settings:
                settingsLoaded = false;
                router.Push(VelvetView.Settings);
                break;
            case ProfileMenuAction.Rules:
                conduct.ShowRules(Id);
                break;
            case ProfileMenuAction.Report:
                OpenReport("velvet_profile", profileMenuUserId, Loc.T(L.Velvet.ReportProfile));
                break;
            case ProfileMenuAction.NotInterested:
                store.HideFromDiscover(profileMenuUserId);
                router.Pop();
                break;
            case ProfileMenuAction.Disconnect:
                AskDisconnect(profileMenuUserId);
                break;
            case ProfileMenuAction.Block:
                AskBlock(profileMenuUserId, profileMenuName);
                break;
        }
    }

    private void AskBlock(string userId, string displayName)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Social.BlockConfirmTitle, displayName),
            Message = Loc.T(L.Velvet.BlockConfirm),
            ConfirmLabel = Loc.T(L.Velvet.Block),
            CancelLabel = Loc.T(L.Velvet.DeleteCancel),
            Danger = true,
            ConfirmAsync = done => store.Block(userId, done, confirm.ReportFailure),
        });
    }
}
