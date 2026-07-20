using System.Text;
using Aetherphone.Core;
using Aetherphone.Core.Emoji;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal struct ChatComposerModel
{
    public AppSkin Ui;
    public string ConversationId;
    public int MaxLength;
    public bool Sending;
    public bool CanImage;
    public bool CanVoice;
    public bool CanHandleEscape;
    public Func<int> ResolveVoiceInput;
    public Action<string> OnPickImage;
    public Action<string, string, string?> OnSendText;
    public Action<string, string, string> OnEditText;
    public Action<string, byte[], int> OnSendVoice;
}

internal sealed class ChatComposer : IDisposable
{
    private const int TextKind = 0;
    private const float AccessoryBarHeight = 46f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 FieldFill = new(1f, 1f, 1f, 0.10f);
    private static readonly Vector4 BarFill = new(1f, 1f, 1f, 0.05f);

    private readonly VoiceNoteRecorder recorder = new();
    private readonly EmojiPicker emojiPicker = new();
    private string draft = string.Empty;
    private bool focus;
    private bool emojiOpen;
    private string? replyTargetId;
    private string replyBarName = string.Empty;
    private string replyBarPreview = string.Empty;
    private string? editTargetId;
    private string editBarPreview = string.Empty;

    public string Draft
    {
        get => draft;
        set => draft = value;
    }

    public bool IsEditing => editTargetId is not null;

    public bool HasReplyTarget => replyTargetId is not null;

    public bool Recording => recorder.Recording;

    public float AccessoryHeight => replyTargetId is not null || editTargetId is not null
        ? AccessoryBarHeight * ImGuiHelpers.GlobalScale
        : 0f;

    public void BeginReply(string messageId, string senderName, string preview)
    {
        ClearEdit();
        replyTargetId = messageId;
        replyBarName = senderName;
        replyBarPreview = preview;
        focus = true;
    }

    public void BeginEdit(string messageId, string body)
    {
        ClearReply();
        editTargetId = messageId;
        editBarPreview = ChatText.QuotePreview(body, TextKind);
        draft = body;
        focus = true;
    }

    public void ClearReply()
    {
        replyTargetId = null;
        replyBarName = string.Empty;
        replyBarPreview = string.Empty;
    }

    public void ClearEdit()
    {
        if (editTargetId is null)
        {
            return;
        }

        editTargetId = null;
        draft = string.Empty;
    }

    public void ClearTargets()
    {
        replyTargetId = null;
        replyBarName = string.Empty;
        replyBarPreview = string.Empty;
        editTargetId = null;
    }

    public void Clear()
    {
        ClearTargets();
        draft = string.Empty;
    }

    public void CancelVoice()
    {
        recorder.Cancel();
    }

    public void Dispose()
    {
        recorder.Dispose();
    }

    public void Draw(Rect composerRect, in ChatComposerModel model)
    {
        var accessory = AccessoryHeight;
        if (accessory > 0f)
        {
            var barRect = new Rect(new Vector2(composerRect.Min.X, composerRect.Min.Y - accessory),
                new Vector2(composerRect.Max.X, composerRect.Min.Y));
            if (editTargetId is not null)
            {
                DrawEditBar(barRect, model);
            }
            else
            {
                DrawReplyBar(barRect, model);
            }
        }

        if (recorder.Recording)
        {
            DrawRecordingComposer(composerRect, model);
            return;
        }

        DrawInputComposer(composerRect, model);
    }

    private void DrawInputComposer(Rect area, in ChatComposerModel model)
    {
        var ui = model.Ui;
        var theme = ui.Theme;
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(area.Min, new Vector2(area.Max.X, area.Min.Y), ImGui.GetColorU32(theme.Separator), 1f);
        var buttonRadius = 16f * scale;
        var pillLeftAnchor = area.Min.X + 12f * scale;
        if (model.CanImage)
        {
            var pictureCenter = new Vector2(area.Min.X + 12f * scale + buttonRadius, area.Center.Y);
            var pictureMin = pictureCenter - new Vector2(buttonRadius, buttonRadius);
            var pictureMax = pictureCenter + new Vector2(buttonRadius, buttonRadius);
            var pictureHovered = ImGui.IsMouseHoveringRect(pictureMin, pictureMax);
            drawList.AddCircleFilled(pictureCenter, buttonRadius,
                ImGui.GetColorU32(pictureHovered ? Palette.Mix(ui.Accent, theme.TextStrong, 0.12f) : ui.Accent), 24);
            AppSkin.Icon(pictureCenter, FontAwesomeIcon.Image.ToIconString(), White, 0.85f);
            HoverTooltip.Show(new Rect(pictureMin, pictureMax), Loc.T(L.Velvet.SendPicture), HoverLabelSide.Above);
            if (pictureHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    model.OnPickImage(model.ConversationId);
                }
            }

            pillLeftAnchor = pictureMax.X + 10f * scale;
        }

        var emojiCenter = new Vector2(pillLeftAnchor + buttonRadius, area.Center.Y);
        var emojiMin = emojiCenter - new Vector2(buttonRadius, buttonRadius);
        var emojiMax = emojiCenter + new Vector2(buttonRadius, buttonRadius);
        var emojiHovered = ImGui.IsMouseHoveringRect(emojiMin, emojiMax);
        var emojiColor = emojiOpen ? ui.Accent : emojiHovered ? theme.TextStrong : ui.MutedInk;
        AppSkin.Icon(emojiCenter, FontAwesomeIcon.Smile.ToIconString(), emojiColor, 0.95f);
        HoverTooltip.Show(new Rect(emojiMin, emojiMax), Loc.T(L.Common.Emoji), HoverLabelSide.Above);
        if (emojiHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                emojiOpen = !emojiOpen;
            }
        }

        pillLeftAnchor = emojiMax.X + 6f * scale;

        var sendWidth = 40f * scale;
        var pillMin = new Vector2(pillLeftAnchor, area.Min.Y + 8f * scale);
        var pillMax = new Vector2(area.Max.X - sendWidth - 12f * scale, area.Max.Y - 8f * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f, ImGui.GetColorU32(FieldFill));
        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 14f * scale,
            (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 24f * scale);
        if (focus)
        {
            ImGui.SetKeyboardFocusHere();
            focus = false;
        }

        var submitted = false;
        Plugin.Fonts.NoticeText(draft);
        var overlayEmoji = EmojiScanner.MightContain(draft);
        var inputTextColor = overlayEmoji ? new Vector4(0f, 0f, 0f, 0f) : theme.TextStrong;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, inputTextColor))
        {
            if (ImGui.InputTextWithHint("##chatComposerInput", Loc.T(L.Velvet.MessageHint), ref draft, model.MaxLength,
                    ImGuiInputTextFlags.EnterReturnsTrue))
            {
                submitted = true;
            }
        }

        if (overlayEmoji)
        {
            DrawComposerEmojiOverlay(pillMin, pillMax, scale, theme, ImGui.IsItemActive());
        }

        var hasDraft = draft.Trim().Length > 0;
        var canSend = hasDraft && !model.Sending;
        var sendCenter = new Vector2(area.Max.X - sendWidth * 0.5f - 8f * scale, area.Center.Y);
        var sendHitRadius = 16f * scale;
        var sendRect = new Rect(sendCenter - new Vector2(sendHitRadius, sendHitRadius),
            sendCenter + new Vector2(sendHitRadius, sendHitRadius));
        if (hasDraft)
        {
            drawList.AddCircleFilled(sendCenter, 16f * scale,
                ImGui.GetColorU32(canSend ? ui.Accent : theme.SurfaceMuted), 24);
            AppSkin.Icon(sendCenter, FontAwesomeIcon.PaperPlane.ToIconString(), White, 0.9f);
            HoverTooltip.Show(sendRect, Loc.T(L.Velvet.Send), HoverLabelSide.Above);
            if (UiInteract.Hover(sendRect.Min, sendRect.Max))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && canSend)
                {
                    submitted = true;
                }
            }
        }
        else if (model.CanVoice)
        {
            drawList.AddCircleFilled(sendCenter, 16f * scale, ImGui.GetColorU32(ui.Accent), 24);
            AppSkin.Icon(sendCenter, FontAwesomeIcon.Microphone.ToIconString(), White, 0.9f);
            HoverTooltip.Show(sendRect, Loc.T(L.Message.RecordVoiceHint), HoverLabelSide.Above);
            if (UiInteract.Hover(sendRect.Min, sendRect.Max))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !model.Sending)
                {
                    recorder.Start(model.ResolveVoiceInput());
                }
            }
        }
        else
        {
            drawList.AddCircleFilled(sendCenter, 16f * scale, ImGui.GetColorU32(theme.SurfaceMuted), 24);
            AppSkin.Icon(sendCenter, FontAwesomeIcon.PaperPlane.ToIconString(), White, 0.9f);
        }

        if (submitted && canSend)
        {
            if (editTargetId is { } editId)
            {
                model.OnEditText(model.ConversationId, editId, draft);
                ClearEdit();
            }
            else
            {
                model.OnSendText(model.ConversationId, draft, replyTargetId);
                draft = string.Empty;
                ClearReply();
            }

            emojiOpen = false;
            focus = true;
        }

        if (emojiOpen)
        {
            DrawEmojiPanel(area, model);
        }
    }

    private void DrawEmojiPanel(Rect composerArea, in ChatComposerModel model)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = 250f * scale;
        var bottom = composerArea.Min.Y - AccessoryHeight;
        var panel = new Rect(new Vector2(composerArea.Min.X, bottom - height),
            new Vector2(composerArea.Max.X, bottom));
        var picked = emojiPicker.Draw(panel, model.Ui);
        if (picked is null)
        {
            return;
        }

        if (Encoding.UTF8.GetByteCount(draft) + Encoding.UTF8.GetByteCount(picked) < model.MaxLength)
        {
            draft += picked;
            Plugin.Fonts.NoticeText(draft);
            focus = true;
        }
    }

    private void DrawComposerEmojiOverlay(Vector2 pillMin, Vector2 pillMax, float scale, PhoneTheme theme, bool active)
    {
        if (draft.Length == 0)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var fontSize = ImGui.GetFontSize();
        var textLeft = pillMin.X + 14f * scale;
        var fieldRight = pillMax.X - 10f * scale;
        var fieldWidth = MathF.Max(1f, fieldRight - textLeft);
        var textTop = (pillMin.Y + pillMax.Y) * 0.5f - fontSize * 0.5f;
        var width = EmojiText.MeasureWidth(draft, fontSize);
        var scrollX = MathF.Max(0f, width - fieldWidth);
        var ink = ImGui.GetColorU32(theme.TextStrong);
        drawList.PushClipRect(new Vector2(textLeft, pillMin.Y), new Vector2(fieldRight, pillMax.Y), true);
        var caretX = EmojiText.DrawLine(drawList, draft, new Vector2(textLeft, textTop), fontSize, ink, scrollX);
        if (active && ImGui.GetTime() % 1.0 < 0.5)
        {
            var cursorX = MathF.Min(caretX, fieldRight);
            drawList.AddLine(new Vector2(cursorX, textTop + 1f * scale),
                new Vector2(cursorX, textTop + fontSize - 1f * scale), ink, MathF.Max(1f, scale));
        }

        drawList.PopClipRect();
    }

    private void DrawRecordingComposer(Rect area, in ChatComposerModel model)
    {
        var ui = model.Ui;
        var theme = ui.Theme;
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(area.Min, new Vector2(area.Max.X, area.Min.Y), ImGui.GetColorU32(theme.Separator), 1f);
        var cancelCenter = new Vector2(area.Min.X + 28f * scale, area.Center.Y);
        if (ui.IconButton(cancelCenter, 16f * scale, FontAwesomeIcon.TrashAlt.ToIconString(), theme.Danger,
                AppSkin.Transparent, 1f, Loc.T(L.Common.Cancel), HoverLabelSide.Above))
        {
            recorder.Cancel();
            return;
        }

        var pulse = 0.55f + 0.45f * MathF.Sin((float)ImGui.GetTime() * 5f);
        var dotCenter = new Vector2(cancelCenter.X + 34f * scale, area.Center.Y);
        drawList.AddCircleFilled(dotCenter, 5f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Danger, 0.4f + 0.6f * pulse)), 16);
        var elapsed = TimeText.MinutesSeconds((int)recorder.ElapsedSeconds);
        Typography.Draw(new Vector2(dotCenter.X + 12f * scale, area.Center.Y
            - Typography.Measure(elapsed, 1f, FontWeight.SemiBold).Y * 0.5f), elapsed, theme.TextStrong, 1f,
            FontWeight.SemiBold);
        var meterLeft = dotCenter.X + 64f * scale;
        var meterRight = area.Max.X - 64f * scale;
        if (meterRight > meterLeft + 30f * scale)
        {
            var meterY = area.Center.Y;
            drawList.AddRectFilled(new Vector2(meterLeft, meterY - 2f * scale),
                new Vector2(meterRight, meterY + 2f * scale), ImGui.GetColorU32(FieldFill), 2f * scale);
            var level = Math.Clamp(recorder.Level * 6f, 0f, 1f);
            drawList.AddRectFilled(new Vector2(meterLeft, meterY - 2f * scale),
                new Vector2(meterLeft + (meterRight - meterLeft) * level, meterY + 2f * scale),
                ImGui.GetColorU32(ui.Accent), 2f * scale);
        }

        var sendCenter = new Vector2(area.Max.X - 28f * scale, area.Center.Y);
        drawList.AddCircleFilled(sendCenter, 16f * scale, ImGui.GetColorU32(ui.Accent), 24);
        AppSkin.Icon(sendCenter, FontAwesomeIcon.PaperPlane.ToIconString(), White, 0.9f);
        var sendRect = new Rect(sendCenter - new Vector2(16f * scale, 16f * scale),
            sendCenter + new Vector2(16f * scale, 16f * scale));
        HoverTooltip.Show(sendRect, Loc.T(L.Velvet.Send), HoverLabelSide.Above);
        var sendClicked = UiInteract.HoverClick(sendRect.Min, sendRect.Max);
        if (sendClicked || recorder.AtCapacity)
        {
            if (recorder.Stop(out var wavBytes, out var durationSecs))
            {
                model.OnSendVoice(model.ConversationId, wavBytes, durationSecs);
            }
        }
    }

    private void DrawReplyBar(Rect area, in ChatComposerModel model)
    {
        var ui = model.Ui;
        var theme = ui.Theme;
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(BarFill));
        drawList.AddLine(area.Min, new Vector2(area.Max.X, area.Min.Y), ImGui.GetColorU32(theme.Separator), 1f);
        var barMin = new Vector2(area.Min.X + 14f * scale, area.Min.Y + 8f * scale);
        var barMax = new Vector2(barMin.X + 3f * scale, area.Max.Y - 8f * scale);
        Squircle.Fill(drawList, barMin, barMax, 1.5f * scale, ImGui.GetColorU32(ui.Accent));
        var textLeft = barMax.X + 9f * scale;
        var closeRadius = 13f * scale;
        var textWidth = area.Max.X - 20f * scale - closeRadius * 2f - textLeft;
        Typography.Draw(new Vector2(textLeft, area.Min.Y + 7f * scale),
            Typography.FitText(Loc.T(L.Message.ReplyingTo, replyBarName), textWidth, 0.78f, FontWeight.SemiBold),
            ui.Accent, 0.78f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(textLeft, area.Min.Y + 24f * scale),
            Typography.FitText(replyBarPreview, textWidth, 0.82f, FontWeight.Regular), ui.MutedInk, 0.82f);
        var closeCenter = new Vector2(area.Max.X - 14f * scale - closeRadius, area.Center.Y);
        if (ui.IconButton(closeCenter, closeRadius, FontAwesomeIcon.Times.ToIconString(), ui.MutedInk,
                AppSkin.Transparent, 0.9f, Loc.T(L.Common.Cancel))
            || (model.CanHandleEscape && ImGui.IsKeyPressed(ImGuiKey.Escape)))
        {
            ClearReply();
        }
    }

    private void DrawEditBar(Rect area, in ChatComposerModel model)
    {
        var ui = model.Ui;
        var theme = ui.Theme;
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(BarFill));
        drawList.AddLine(area.Min, new Vector2(area.Max.X, area.Min.Y), ImGui.GetColorU32(theme.Separator), 1f);
        var iconCenter = new Vector2(area.Min.X + 22f * scale, area.Center.Y);
        AppSkin.Icon(iconCenter, FontAwesomeIcon.Pen.ToIconString(), ui.Accent, 0.9f);
        var textLeft = iconCenter.X + 16f * scale;
        var closeRadius = 13f * scale;
        var textWidth = area.Max.X - 20f * scale - closeRadius * 2f - textLeft;
        Typography.Draw(new Vector2(textLeft, area.Min.Y + 7f * scale),
            Typography.FitText(Loc.T(L.Message.EditingLabel), textWidth, 0.78f, FontWeight.SemiBold),
            ui.Accent, 0.78f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(textLeft, area.Min.Y + 24f * scale),
            Typography.FitText(editBarPreview, textWidth, 0.82f, FontWeight.Regular), ui.MutedInk, 0.82f);
        var closeCenter = new Vector2(area.Max.X - 14f * scale - closeRadius, area.Center.Y);
        if (ui.IconButton(closeCenter, closeRadius, FontAwesomeIcon.Times.ToIconString(), ui.MutedInk,
                AppSkin.Transparent, 0.9f, Loc.T(L.Common.Cancel))
            || (model.CanHandleEscape && ImGui.IsKeyPressed(ImGuiKey.Escape)))
        {
            ClearEdit();
        }
    }
}
