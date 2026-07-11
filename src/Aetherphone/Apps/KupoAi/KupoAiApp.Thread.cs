using System.Numerics;
using System.Text;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.KupoAi;

internal sealed partial class KupoAiApp
{
    private const string AssistantSenderId = "kupoai";
    private const string MySenderId = "me";

    private void DrawThread(Rect area, KupoAiConversation conversation)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var title = conversation.Title.Length > 0 ? conversation.Title : Loc.T(L.Apps.KupoAi);
        AppHeader.Draw(new PhoneContext(area, theme, navigation), title, back);

        var composerHeight = 52f * scale;
        var footnoteHeight = 20f * scale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var gateText = GateText();
        var listBottom = gateText is null
            ? area.Max.Y - composerHeight - footnoteHeight
            : area.Max.Y - composerHeight;
        var listRect = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, listBottom));

        RebuildTranscriptIfNeeded(conversation);
        onMessageContext ??= OpenMessageMenu;
        var model = new ChatTranscriptModel(conversation.Id, transcriptCache, MySenderId, ui.Accent, theme,
            AppPalettes.KupoAi.MutedInk, AppPalettes.KupoAi.BodyInk, store.Asking, false, false, images, mediaUrl,
            onImageClick, Loc.T(L.KupoAi.ThreadEmpty), Loc.T(L.Common.Loading), onMessageContext);
        transcript.Draw(listRect, model);

        if (gateText is not null)
        {
            Typography.DrawWrappedCentered(new Vector2(area.Center.X, area.Max.Y - composerHeight + 8f * scale),
                gateText, ui.MutedInk, TextStyles.Footnote, area.Width - 48f * scale);
            return;
        }

        DrawQuotaFootnote(new Rect(new Vector2(area.Min.X, listBottom),
            new Vector2(area.Max.X, listBottom + footnoteHeight)));
        DrawComposer(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), conversation);
        DrawMessageMenu(area, conversation);
    }

    private string? GateText()
    {
        if (!store.IsSignedIn)
        {
            return Loc.T(L.KupoAi.SignedOut);
        }

        if (!store.Ready)
        {
            return Loc.T(L.KupoAi.Indexing);
        }

        if (store.RemainingToday == 0)
        {
            return Loc.T(L.KupoAi.QuotaExhausted, store.DailyLimit);
        }

        return null;
    }

    private void DrawQuotaFootnote(Rect rect)
    {
        if (store.RemainingToday < 0)
        {
            return;
        }

        var color = store.RemainingToday <= 3 ? theme.Danger : ui.MutedInk;
        Typography.DrawCentered(new Vector2(rect.Center.X, rect.Center.Y),
            Loc.T(L.KupoAi.QuotaLeft, store.RemainingToday, store.DailyLimit), color, 0.72f);
    }

    private void RebuildTranscriptIfNeeded(KupoAiConversation conversation)
    {
        var version = store.Version;
        if (version == transcriptVersion && conversation.Id == transcriptConversationId)
        {
            return;
        }

        transcriptVersion = version;
        transcriptConversationId = conversation.Id;
        var messages = store.SnapshotMessages(conversation);
        var cache = new TranscriptMessage[messages.Length];
        for (var index = 0; index < messages.Length; index++)
        {
            var message = messages[index];
            var id = $"{conversation.Id}:{index}";
            cache[index] = message.Role switch
            {
                KupoAiRoles.User => new TranscriptMessage(id, MySenderId, message.Text, 0, message.AtUnix, 0, 0,
                    null, string.Empty, default),
                KupoAiRoles.Assistant => new TranscriptMessage(id, AssistantSenderId, AssistantBody(message), 0,
                    message.AtUnix, 0, 0, null, Loc.T(L.Apps.KupoAi), default),
                _ => new TranscriptMessage(id, AssistantSenderId, MapNote(message.Text), 2, message.AtUnix, 0, 0,
                    null, string.Empty, default),
            };
        }

        transcriptCache = cache;
    }

    private static string AssistantBody(KupoAiMessage message)
    {
        if (message.SourceTitles.Length == 0)
        {
            return message.Text;
        }

        var builder = new StringBuilder(message.Text.Length + 64);
        builder.Append(message.Text).Append("\n\n").Append(Loc.T(L.KupoAi.SourcesLabel));
        for (var index = 0; index < message.SourceTitles.Length; index++)
        {
            builder.Append(index == 0 ? " " : "   ");
            builder.Append('[').Append(index + 1).Append("] ").Append(message.SourceTitles[index]);
        }

        return builder.ToString();
    }

    private string MapNote(string note)
    {
        return note switch
        {
            KupoAiNotes.Quota => Loc.T(L.KupoAi.QuotaExhausted, store.DailyLimit),
            KupoAiNotes.GlobalQuota => Loc.T(L.KupoAi.GlobalQuota),
            KupoAiNotes.Indexing => Loc.T(L.KupoAi.Indexing),
            KupoAiNotes.NoMatch => Loc.T(L.KupoAi.NoMatch),
            KupoAiNotes.Offline => Loc.T(L.KupoAi.Offline),
            KupoAiNotes.RateLimited => Loc.T(L.KupoAi.RateLimited),
            KupoAiNotes.Error => Loc.T(L.KupoAi.Error),
            _ => note,
        };
    }

    private void OpenMessageMenu(string messageId)
    {
        menuMessageId = messageId;
        var pos = ImGui.GetMousePos();
        messageMenu.Toggle(messageId, new Rect(pos, pos + new Vector2(1f, 1f)));
    }

    private void DrawMessageMenu(Rect area, KupoAiConversation conversation)
    {
        if (menuMessageId is not { } id || !messageMenu.IsOpenFor(id))
        {
            return;
        }

        var message = FindMessage(conversation, id);
        if (message is null)
        {
            return;
        }

        var items = new DropdownMenu.Item[1 + message.SourceTitles.Length];
        items[0] = new DropdownMenu.Item(Loc.T(L.KupoAi.CopyAnswer), FontAwesomeIcon.Copy.ToIconString());
        for (var index = 0; index < message.SourceTitles.Length; index++)
        {
            items[1 + index] = new DropdownMenu.Item(Loc.T(L.KupoAi.OpenSource, message.SourceTitles[index]),
                FontAwesomeIcon.ExternalLinkAlt.ToIconString());
        }

        var clicked = messageMenu.Draw(area, theme, items);
        if (clicked == 0)
        {
            ImGui.SetClipboardText(message.Text);
        }
        else if (clicked > 0 && clicked - 1 < message.SourceUrls.Length)
        {
            UrlActions.OpenInBrowser(message.SourceUrls[clicked - 1]);
        }
    }

    private KupoAiMessage? FindMessage(KupoAiConversation conversation, string messageId)
    {
        var separator = messageId.LastIndexOf(':');
        if (separator < 0 || !int.TryParse(messageId[(separator + 1)..], out var index))
        {
            return null;
        }

        var messages = store.SnapshotMessages(conversation);
        return index >= 0 && index < messages.Length ? messages[index] : null;
    }

    private void DrawComposer(Rect area, KupoAiConversation conversation)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(area.Min, new Vector2(area.Max.X, area.Min.Y), ImGui.GetColorU32(theme.Separator), 1f);

        var sendWidth = 40f * scale;
        var pillMin = new Vector2(area.Min.X + 12f * scale, area.Min.Y + 8f * scale);
        var pillMax = new Vector2(area.Max.X - sendWidth - 12f * scale, area.Max.Y - 8f * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 14f * scale,
            (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 24f * scale);
        if (composerFocus)
        {
            ImGui.SetKeyboardFocusHere();
            composerFocus = false;
        }

        var submitted = false;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##kupoaiAsk", Loc.T(L.KupoAi.AskHint), ref draft,
                    KupoAiStore.MaxQuestionChars, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                submitted = true;
            }
        }

        var canSend = draft.Trim().Length > 0 && !store.Asking;
        var sendCenter = new Vector2(area.Max.X - sendWidth * 0.5f - 8f * scale, area.Center.Y);
        drawList.AddCircleFilled(sendCenter, 16f * scale,
            ImGui.GetColorU32(canSend ? ui.Accent : theme.SurfaceMuted), 24);
        AppSkin.Icon(sendCenter, FontAwesomeIcon.PaperPlane.ToIconString(), White, 0.9f);
        var sendRect = new Rect(sendCenter - new Vector2(16f * scale, 16f * scale),
            sendCenter + new Vector2(16f * scale, 16f * scale));
        if (UiInteract.Hover(sendRect.Min, sendRect.Max))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && canSend)
            {
                submitted = true;
            }
        }

        if (submitted && canSend)
        {
            store.Ask(conversation, draft);
            draft = string.Empty;
            transcript.RequestSnapToBottom();
            composerFocus = true;
        }
    }
}
