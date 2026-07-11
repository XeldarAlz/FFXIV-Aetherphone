using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.KupoAi;

internal sealed partial class KupoAiApp
{
    private void DrawList(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        AppHeader.Draw(new PhoneContext(area, theme, navigation), Loc.T(L.Apps.KupoAi), navigation.Back);
        var headerCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        if (ui.IconButton(new Vector2(area.Max.X - 24f * scale, headerCenterY), 16f * scale,
                FontAwesomeIcon.Plus.ToIconString(), ui.BodyInk, Transparent, 1.1f, Loc.T(L.KupoAi.NewChat),
                HoverLabelSide.Below) && store.IsSignedIn)
        {
            StartNewChat();
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        if (!store.IsSignedIn)
        {
            DrawCenteredNotice(body, Loc.T(L.KupoAi.SignedOut));
            return;
        }

        if (!store.Loaded)
        {
            DrawCenteredNotice(body, Loc.T(L.Common.Loading));
            return;
        }

        var conversations = store.Conversations;
        if (conversations.Count == 0)
        {
            DrawEmptyState(body, scale);
            return;
        }

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            for (var index = 0; index < conversations.Count; index++)
            {
                DrawConversationRow(conversations[index], scale);
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));
        }
    }

    private void DrawEmptyState(Rect body, float scale)
    {
        var centerX = body.Center.X;
        var top = body.Min.Y + body.Height * 0.30f;
        Typography.DrawCentered(new Vector2(centerX, top), Loc.T(L.KupoAi.EmptyTitle), ui.TitleInk, 1.15f,
            FontWeight.Bold);
        var hintTop = top + 20f * scale;
        var hintHeight = Typography.DrawWrappedCentered(new Vector2(centerX, hintTop), Loc.T(L.KupoAi.EmptyHint),
            ui.MutedInk, TextStyles.Subheadline, body.Width - 56f * scale);
        var cursor = hintTop + hintHeight + 8f * scale;
        if (store.RemainingToday >= 0)
        {
            Typography.DrawCentered(new Vector2(centerX, cursor + 8f * scale),
                Loc.T(L.KupoAi.QuotaLeft, store.RemainingToday, store.DailyLimit), ui.MutedInk, 0.78f);
            cursor += 24f * scale;
        }

        var buttonWidth = 180f * scale;
        var buttonTop = cursor + 18f * scale;
        var buttonRect = new Rect(new Vector2(centerX - buttonWidth * 0.5f, buttonTop),
            new Vector2(centerX + buttonWidth * 0.5f, buttonTop + 42f * scale));
        if (ui.PillButton(buttonRect, Loc.T(L.KupoAi.NewChat), true))
        {
            StartNewChat();
        }
    }

    private void DrawCenteredNotice(Rect body, string text)
    {
        var scale = ImGuiHelpers.GlobalScale;
        Typography.DrawWrappedCentered(new Vector2(body.Center.X, body.Min.Y + body.Height * 0.32f), text,
            ui.MutedInk, TextStyles.Subheadline, body.Width - 56f * scale);
    }

    private void DrawConversationRow(KupoAiConversation conversation, float scale)
    {
        var rowHeight = 56f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + rowHeight), 16f * scale);
        var pad = 14f * scale;
        var title = conversation.Title.Length > 0 ? conversation.Title : Loc.T(L.KupoAi.NewChat);
        var trashCenter = new Vector2(origin.X + width - 24f * scale, origin.Y + rowHeight * 0.5f);
        var titleWidth = trashCenter.X - 20f * scale - (origin.X + pad);
        Typography.Draw(new Vector2(origin.X + pad, origin.Y + rowHeight * 0.5f - 16f * scale),
            Typography.FitText(title, titleWidth, 0.95f, FontWeight.SemiBold), theme.TextStrong, 0.95f,
            FontWeight.SemiBold);
        Typography.Draw(new Vector2(origin.X + pad, origin.Y + rowHeight * 0.5f + 3f * scale),
            TimeText.Ago(DateTimeOffset.FromUnixTimeSeconds(conversation.UpdatedAtUnix)), ui.MutedInk, 0.75f);
        var deleteClicked = ui.IconButton(trashCenter, 13f * scale, FontAwesomeIcon.Trash.ToIconString(),
            ui.MutedInk, Transparent, 0.9f, Loc.T(L.KupoAi.Delete), HoverLabelSide.Above);
        if (deleteClicked)
        {
            ConfirmDelete(conversation);
        }
        else if (UiInteract.HoverClick(origin, new Vector2(trashCenter.X - 20f * scale, origin.Y + rowHeight)))
        {
            OpenConversation(conversation);
        }

        ImGui.Dummy(new Vector2(width, rowHeight));
        ImGui.Dummy(new Vector2(0f, 8f * scale));
    }

    private void ConfirmDelete(KupoAiConversation conversation)
    {
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.KupoAi.Delete),
            Message = Loc.T(L.KupoAi.DeleteConfirm),
            ConfirmLabel = Loc.T(L.KupoAi.Delete),
            CancelLabel = Loc.T(L.Common.Cancel),
            Confirm = () =>
            {
                store.DeleteConversation(conversation.Id);
                if (activeConversation?.Id == conversation.Id)
                {
                    activeConversation = null;
                }
            },
        });
    }

    private void StartNewChat()
    {
        activeConversation = store.NewConversation();
        draft = string.Empty;
        composerFocus = true;
        router.Push(KupoAiView.Thread);
    }

    private void OpenConversation(KupoAiConversation conversation)
    {
        activeConversation = conversation;
        draft = string.Empty;
        composerFocus = true;
        router.Push(KupoAiView.Thread);
    }
}
