using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Report;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private static Rect Reserve(float heightUnscaled)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + heightUnscaled * scale));
        ImGui.Dummy(new Vector2(width, heightUnscaled * scale));
        return rect;
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

    private void DrawPostMenu(Rect area, bool inFeed)
    {
        if (menuPost is not { } post || !postMenu.IsOpenFor(post.Id))
        {
            return;
        }

        var mine = store.Me is { } me && me.UserId == post.OwnerId;
        var count = 0;
        if (inFeed)
        {
            postItems[count++] = new DropdownMenu.Item(Loc.T(L.Velvet.ViewPost),
                FontAwesomeIcon.Expand.ToIconString());
        }

        if (mine)
        {
            var isPublic = post.Audience == VelvetPostAudience.Public;
            postItems[count++] = new DropdownMenu.Item(Loc.T(isPublic ? L.Velvet.MakeConnections : L.Velvet.MakePublic),
                (isPublic ? FontAwesomeIcon.Lock : FontAwesomeIcon.Globe).ToIconString());
            postItems[count++] = new DropdownMenu.Item(Loc.T(L.Velvet.DeleteConfirm),
                FontAwesomeIcon.Trash.ToIconString(), true);
        }
        else
        {
            postItems[count++] = new DropdownMenu.Item(Loc.T(L.Velvet.Report),
                FontAwesomeIcon.Flag.ToIconString(), true);
            postItems[count++] = new DropdownMenu.Item(Loc.T(L.Velvet.Block),
                FontAwesomeIcon.Ban.ToIconString(), true);
        }

        var picked = postMenu.Draw(area, theme, postItems.AsSpan(0, count));
        if (picked < 0)
        {
            return;
        }

        var viewOffset = inFeed ? 1 : 0;
        if (inFeed && picked == 0)
        {
            OpenPostDetail(post.Id);
            return;
        }

        if (mine)
        {
            if (picked == viewOffset)
            {
                var nextAudience = post.Audience == VelvetPostAudience.Public
                    ? VelvetPostAudience.Connections
                    : VelvetPostAudience.Public;
                store.SetPostAudience(post, nextAudience);
                return;
            }

            AskDeletePost(post.Id, inFeed ? null : back);
            return;
        }

        if (picked == viewOffset)
        {
            OpenReport("velvet_post", post.Id, Loc.T(L.Velvet.ReportPost));
            return;
        }

        AskBlock(post.OwnerId, DisplayNameOf(post.OwnerDisplayName, post.OwnerHandle));
    }

    private void AskBlock(string userId, string displayName)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Velvet.BlockConfirm, displayName),
            ConfirmLabel = Loc.T(L.Velvet.Block),
            CancelLabel = Loc.T(L.Velvet.DeleteCancel),
            Danger = true,
            Confirm = () => store.Block(userId, _ => { }),
        });
    }
}
