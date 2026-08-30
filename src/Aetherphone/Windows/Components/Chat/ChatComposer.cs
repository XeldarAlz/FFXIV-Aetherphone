using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal enum ChatComposerStyle : byte
{
    Bar,
    Pill,
}

internal struct ChatComposerModel
{
    public AppSkin Ui;
    public ChatComposerStyle Style;
    public string Hint;
    public string ConversationId;
    public int MaxLength;
    public bool Sending;
    public bool CanImage;
    public bool CanVoice;
    public bool CanLocation;
    public bool CanHandleEscape;
    public bool Blocked;
    public string BlockedNotice;
    public Action OnBlockedTap;
    public Func<int> ResolveVoiceInput;
    public Action<string> OnPickImage;
    public Action<string> OnShareLocation;
    public Action<string, string, string?> OnSendText;
    public Action<string, string, string> OnEditText;
    public Action<string, byte[], int> OnSendVoice;
}

internal sealed class ChatComposer : IDisposable
{
    private const int TextKind = 0;
    private const float AccessoryBarHeight = 46f;
    private const float BarHeight = 56f;
    private const float PillComposerHeight = 66f;
    private const float PillEdgePad = 10f;
    private const float PillInsetY = 8f;
    private const float PillCameraInset = 4f;
    private const float PillCameraGlyph = 22f;
    private const float PillTextGap = 8f;
    private const float PillIconEdge = 6f;
    private const float PillIconHit = 30f;
    private const float PillIconGlyph = 24f;
    private const float PillIconGap = 0f;
    private const float PillSendPad = 8f;
    private const float PillIdleAlpha = 0.9f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 FieldFill = new(1f, 1f, 1f, 0.10f);
    private static readonly Vector4 BarFill = new(1f, 1f, 1f, 0.05f);
    private static readonly Vector4 PillFill = new(1f, 1f, 1f, 0.08f);
    private static readonly Vector4 PillStroke = new(1f, 1f, 1f, 0.10f);
    private static readonly TextStyle SendStyle = TextStyles.BodyEmphasized;

    private readonly VoiceNoteRecorder recorder = new();
    private readonly EmojiPicker emojiPicker = new();
    private string draft = string.Empty;
    private bool focus;
    private bool emojiOpen;
    private int emojiOpenedFrame = -1;
    private int emojiClosedFrame = -1;
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
        ? AccessoryBarHeight * UiScale.Current
        : 0f;

    public static float Height(ChatComposerStyle style) =>
        (style == ChatComposerStyle.Pill ? PillComposerHeight : BarHeight) * UiScale.Current;

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

        var surface = PaintSurface(composerRect, model);
        if (model.Blocked)
        {
            DrawBlockedComposer(surface, model);
            return;
        }

        if (recorder.Recording)
        {
            DrawRecordingComposer(surface, model);
            return;
        }

        if (model.Style == ChatComposerStyle.Pill)
        {
            DrawPillComposer(composerRect, surface, model);
            return;
        }

        DrawInputComposer(composerRect, model);
    }

    private static Rect PaintSurface(Rect area, in ChatComposerModel model)
    {
        var drawList = ImGui.GetWindowDrawList();
        if (model.Style != ChatComposerStyle.Pill)
        {
            drawList.AddLine(area.Min, new Vector2(area.Max.X, area.Min.Y),
                ImGui.GetColorU32(model.Ui.Theme.Separator), 1f);
            return area;
        }

        var scale = UiScale.Current;
        var pill = new Rect(new Vector2(area.Min.X + PillEdgePad * scale, area.Min.Y + PillInsetY * scale),
            new Vector2(area.Max.X - PillEdgePad * scale, area.Max.Y - PillInsetY * scale));
        var rounding = pill.Height * 0.5f;
        Squircle.Fill(drawList, pill.Min, pill.Max, rounding, ImGui.GetColorU32(PillFill));
        Squircle.Stroke(drawList, pill.Min, pill.Max, rounding, ImGui.GetColorU32(PillStroke), 1f * scale);
        return pill;
    }

    private static void DrawBlockedComposer(Rect area, in ChatComposerModel model)
    {
        var ui = model.Ui;
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var edgePad = 14f * scale;
        var iconRadius = 13f * scale;
        var iconCenter = new Vector2(area.Min.X + edgePad + iconRadius, area.Center.Y);
        AppSkin.Icon(iconCenter, IconGlyph.Of(FontAwesomeIcon.Lock), ui.MutedInk, 0.9f);

        var textLeft = iconCenter.X + iconRadius + 9f * scale;
        var label = Typography.FitText(model.BlockedNotice, area.Max.X - edgePad - textLeft, TextStyles.Footnote);
        var labelSize = Typography.Measure(label, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, area.Center.Y - labelSize.Y * 0.5f), label, ui.MutedInk,
            TextStyles.Footnote);

        if (UiInteract.Hover(area.Min, area.Max))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.HoverClick(area.Min, area.Max))
        {
            model.OnBlockedTap?.Invoke();
        }
    }

    private void DrawInputComposer(Rect area, in ChatComposerModel model)
    {
        var ui = model.Ui;
        var theme = ui.Theme;
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var buttonRadius = 18f * scale;
        var iconRadius = 15f * scale;
        var edgePad = 10f * scale;
        var sendCenter = new Vector2(area.Max.X - edgePad - buttonRadius, area.Center.Y);
        var pillMin = new Vector2(area.Min.X + edgePad, area.Min.Y + 7f * scale);
        var pillMax = new Vector2(sendCenter.X - buttonRadius - 8f * scale, area.Max.Y - 7f * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f, ImGui.GetColorU32(FieldFill));

        var emojiCenter = new Vector2(pillMin.X + iconRadius + 5f * scale, area.Center.Y);
        var emojiMin = emojiCenter - new Vector2(iconRadius, iconRadius);
        var emojiMax = emojiCenter + new Vector2(iconRadius, iconRadius);
        var emojiHovered = UiInteract.Hover(emojiMin, emojiMax);
        var emojiColor = emojiOpen ? ui.Accent : emojiHovered ? theme.TextStrong : ui.MutedInk;
        AppSkin.Icon(emojiCenter, IconGlyph.Of(FontAwesomeIcon.Smile), emojiColor, 0.95f);
        HoverTooltip.Show(new Rect(emojiMin, emojiMax), Loc.T(L.Common.Emoji), HoverLabelSide.Above);
        if (emojiHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                ToggleEmoji();
            }
        }

        var textRight = pillMax.X - 14f * scale;
        var trailingIconX = pillMax.X - iconRadius - 5f * scale;
        if (model.CanImage)
        {
            var pictureCenter = new Vector2(trailingIconX, area.Center.Y);
            var pictureMin = pictureCenter - new Vector2(iconRadius, iconRadius);
            var pictureMax = pictureCenter + new Vector2(iconRadius, iconRadius);
            var pictureHovered = UiInteract.Hover(pictureMin, pictureMax);
            AppSkin.Icon(pictureCenter, IconGlyph.Of(FontAwesomeIcon.Image),
                pictureHovered ? theme.TextStrong : ui.MutedInk, 0.95f);
            HoverTooltip.Show(new Rect(pictureMin, pictureMax), Loc.T(L.Velvet.SendPicture), HoverLabelSide.Above);
            if (pictureHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    model.OnPickImage(model.ConversationId);
                }
            }

            trailingIconX = pictureMin.X - iconRadius - 4f * scale;
            textRight = pictureMin.X - 6f * scale;
        }

        if (model.CanLocation)
        {
            var locationCenter = new Vector2(trailingIconX, area.Center.Y);
            var locationMin = locationCenter - new Vector2(iconRadius, iconRadius);
            var locationMax = locationCenter + new Vector2(iconRadius, iconRadius);
            var locationHovered = UiInteract.Hover(locationMin, locationMax);
            AppSkin.Icon(locationCenter, IconGlyph.Of(FontAwesomeIcon.MapMarkerAlt),
                locationHovered ? theme.TextStrong : ui.MutedInk, 0.95f);
            HoverTooltip.Show(new Rect(locationMin, locationMax), Loc.T(L.Message.ShareLocation),
                HoverLabelSide.Above);
            if (locationHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    model.OnShareLocation(model.ConversationId);
                }
            }

            textRight = locationMin.X - 6f * scale;
        }

        var textLeft = emojiMax.X + 4f * scale;
        ImGui.SetCursorScreenPos(new Vector2(textLeft,
            (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(MathF.Max(1f, textRight - textLeft));
        if (focus)
        {
            ImGui.SetKeyboardFocusHere();
            focus = false;
        }

        var submitted = false;
        Plugin.Fonts.NoticeText(draft);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##chatComposerInput", model.Hint, ref draft, model.MaxLength,
                    ImGuiInputTextFlags.EnterReturnsTrue))
            {
                submitted = true;
            }
        }

        var hasDraft = draft.Trim().Length > 0;
        var canSend = hasDraft && !model.Sending;
        var sendRect = new Rect(sendCenter - new Vector2(buttonRadius, buttonRadius),
            sendCenter + new Vector2(buttonRadius, buttonRadius));
        if (hasDraft)
        {
            drawList.AddCircleFilled(sendCenter, buttonRadius,
                ImGui.GetColorU32(canSend ? ui.Accent : theme.SurfaceMuted), 24);
            AppSkin.Icon(sendCenter, IconGlyph.Of(FontAwesomeIcon.PaperPlane), White, 0.9f);
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
            drawList.AddCircleFilled(sendCenter, buttonRadius, ImGui.GetColorU32(ui.Accent), 24);
            AppSkin.Icon(sendCenter, IconGlyph.Of(FontAwesomeIcon.Microphone), White, 0.9f);
            HoverTooltip.Show(sendRect, Loc.T(L.Message.RecordVoiceHint), HoverLabelSide.Above);
            if (UiInteract.Hover(sendRect.Min, sendRect.Max))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !model.Sending)
                {
                    recorder.Start(model.ResolveVoiceInput());
                    UiFeedback.Play(UiSound.RecordStart);
                }
            }
        }
        else
        {
            drawList.AddCircleFilled(sendCenter, buttonRadius, ImGui.GetColorU32(theme.SurfaceMuted), 24);
            AppSkin.Icon(sendCenter, IconGlyph.Of(FontAwesomeIcon.PaperPlane), White, 0.9f);
        }

        if (submitted && canSend)
        {
            Submit(model);
        }

        if (emojiOpen)
        {
            DrawEmojiPanel(area, model);
        }
    }

    private void DrawPillComposer(Rect area, Rect pill, in ChatComposerModel model)
    {
        var ui = model.Ui;
        var theme = ui.Theme;
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var centerY = pill.Center.Y;
        var textLeft = pill.Min.X + PillTextGap * scale;
        if (model.CanImage)
        {
            var cameraRadius = pill.Height * 0.5f - PillCameraInset * scale;
            var cameraCenter = new Vector2(pill.Min.X + PillCameraInset * scale + cameraRadius, centerY);
            var cameraExtent = new Vector2(cameraRadius, cameraRadius);
            var cameraHovered = UiInteract.Hover(cameraCenter - cameraExtent, cameraCenter + cameraExtent);
            drawList.AddCircleFilled(cameraCenter, cameraRadius,
                ImGui.GetColorU32(cameraHovered ? Palette.Lighten(ui.Accent, 0.12f) : ui.Accent), 32);
            PhoneIcon.Draw(drawList, cameraCenter, PhoneIcons.Camera, White, PillCameraGlyph * scale);
            HoverTooltip.Show(new Rect(cameraCenter - cameraExtent, cameraCenter + cameraExtent),
                Loc.T(L.Velvet.SendPicture), HoverLabelSide.Above);
            if (cameraHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    model.OnPickImage(model.ConversationId);
                }
            }

            textLeft = cameraCenter.X + cameraRadius + PillTextGap * scale;
        }

        var idleInk = Palette.WithAlpha(theme.TextStrong, PillIdleAlpha);
        var hasDraft = draft.Trim().Length > 0;
        var submitted = false;
        var rightEdge = pill.Max.X - PillIconEdge * scale;
        if (hasDraft)
        {
            var label = Loc.T(L.Velvet.Send);
            var labelSize = Typography.Measure(label, SendStyle);
            var sendPad = PillSendPad * scale;
            var sendMin = new Vector2(rightEdge - labelSize.X - sendPad * 2f, pill.Min.Y);
            var sendMax = new Vector2(rightEdge, pill.Max.Y);
            var canSend = !model.Sending;
            var sendHovered = canSend && UiInteract.Hover(sendMin, sendMax);
            var sendInk = !canSend ? ui.MutedInk : sendHovered ? Palette.Lighten(ui.Accent, 0.18f) : ui.Accent;
            Typography.Draw(drawList, new Vector2(sendMin.X + sendPad, centerY - labelSize.Y * 0.5f), label, sendInk,
                SendStyle);
            HoverTooltip.Show(new Rect(sendMin, sendMax), label, HoverLabelSide.Above);
            if (sendHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    submitted = true;
                }
            }

            rightEdge = sendMin.X - PillIconGap * scale;
            if (DrawPillIcon(drawList, ref rightEdge, centerY, PhoneIcons.MoodSmile,
                    emojiOpen ? ui.Accent : idleInk, theme.TextStrong, Loc.T(L.Common.Emoji), scale))
            {
                ToggleEmoji();
            }
        }
        else
        {
            if (model.CanLocation && DrawPillIcon(drawList, ref rightEdge, centerY, PhoneIcons.MapPin, idleInk,
                    theme.TextStrong, Loc.T(L.Message.ShareLocation), scale))
            {
                model.OnShareLocation(model.ConversationId);
            }

            if (DrawPillIcon(drawList, ref rightEdge, centerY, PhoneIcons.MoodSmile,
                    emojiOpen ? ui.Accent : idleInk, theme.TextStrong, Loc.T(L.Common.Emoji), scale))
            {
                ToggleEmoji();
            }

            if (model.CanVoice && DrawPillIcon(drawList, ref rightEdge, centerY, PhoneIcons.Microphone, idleInk,
                    theme.TextStrong, Loc.T(L.Message.RecordVoiceHint), scale) && !model.Sending)
            {
                recorder.Start(model.ResolveVoiceInput());
                UiFeedback.Play(UiSound.RecordStart);
            }
        }

        var textRight = rightEdge - PillTextGap * scale;
        ImGui.SetCursorScreenPos(new Vector2(textLeft, centerY - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(MathF.Max(1f, textRight - textLeft));
        if (focus)
        {
            ImGui.SetKeyboardFocusHere();
            focus = false;
        }

        Plugin.Fonts.NoticeText(draft);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##chatComposerInput", model.Hint, ref draft, model.MaxLength,
                    ImGuiInputTextFlags.EnterReturnsTrue))
            {
                submitted = true;
            }
        }

        if (submitted && draft.Trim().Length > 0 && !model.Sending)
        {
            Submit(model);
        }

        if (emojiOpen)
        {
            DrawEmojiPanel(area, model);
        }
    }

    private static bool DrawPillIcon(ImDrawListPtr drawList, ref float rightEdge, float centerY, string glyph,
        Vector4 idleInk, Vector4 hoverInk, string tooltip, float scale)
    {
        var half = PillIconHit * 0.5f * scale;
        var center = new Vector2(rightEdge - half, centerY);
        var extent = new Vector2(half, half);
        var min = center - extent;
        var max = center + extent;
        var hovered = UiInteract.Hover(min, max);
        PhoneIcon.Draw(drawList, center, glyph, hovered ? hoverInk : idleInk, PillIconGlyph * scale);
        HoverTooltip.Show(new Rect(min, max), tooltip, HoverLabelSide.Above);
        rightEdge = min.X - PillIconGap * scale;
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void ToggleEmoji()
    {
        if (ImGui.GetFrameCount() == emojiClosedFrame)
        {
            return;
        }

        emojiOpen = !emojiOpen;
        emojiOpenedFrame = ImGui.GetFrameCount();
    }

    private void Submit(in ChatComposerModel model)
    {
        if (editTargetId is { } editId)
        {
            model.OnEditText(model.ConversationId, editId, draft);
            ClearEdit();
        }
        else
        {
            model.OnSendText(model.ConversationId, draft, replyTargetId);
            UiFeedback.Play(UiSound.MessageSent);
            draft = string.Empty;
            ClearReply();
        }

        emojiOpen = false;
        focus = true;
    }

    private void DrawEmojiPanel(Rect composerArea, in ChatComposerModel model)
    {
        var scale = UiScale.Current;
        var height = 250f * scale;
        var bottom = composerArea.Min.Y - AccessoryHeight;
        var panel = new Rect(new Vector2(composerArea.Min.X, bottom - height),
            new Vector2(composerArea.Max.X, bottom));
        var picked = emojiPicker.Draw(panel, model.Ui);
        if (picked is null)
        {
            DismissEmojiOnOutsideClick(panel);
            return;
        }

        if (draft.Length + picked.Length < model.MaxLength)
        {
            draft += picked;
            Plugin.Fonts.NoticeText(draft);
        }
    }

    private void DismissEmojiOnOutsideClick(Rect panel)
    {
        var frame = ImGui.GetFrameCount();
        if (frame == emojiOpenedFrame || !UiInteract.ClickedOutside(panel.Min, panel.Max))
        {
            return;
        }

        emojiOpen = false;
        emojiClosedFrame = frame;
    }

    private void DrawRecordingComposer(Rect area, in ChatComposerModel model)
    {
        var ui = model.Ui;
        var theme = ui.Theme;
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var cancelCenter = new Vector2(area.Min.X + 28f * scale, area.Center.Y);
        if (ui.IconButton(cancelCenter, 16f * scale, IconGlyph.Of(FontAwesomeIcon.TrashAlt), theme.Danger,
                AppSkin.Transparent, 1f, Loc.T(L.Common.Cancel), HoverLabelSide.Above))
        {
            recorder.Cancel();
            UiFeedback.Play(UiSound.RecordCancel);
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
        AppSkin.Icon(sendCenter, IconGlyph.Of(FontAwesomeIcon.PaperPlane), White, 0.9f);
        var sendRect = new Rect(sendCenter - new Vector2(16f * scale, 16f * scale),
            sendCenter + new Vector2(16f * scale, 16f * scale));
        HoverTooltip.Show(sendRect, Loc.T(L.Velvet.Send), HoverLabelSide.Above);
        var sendClicked = UiInteract.HoverClick(sendRect.Min, sendRect.Max);
        if (sendClicked || recorder.AtCapacity)
        {
            if (recorder.Stop(out var wavBytes, out var durationSecs))
            {
                model.OnSendVoice(model.ConversationId, wavBytes, durationSecs);
                UiFeedback.Play(UiSound.MessageSent);
            }
        }
    }

    private void DrawReplyBar(Rect area, in ChatComposerModel model)
    {
        var ui = model.Ui;
        var theme = ui.Theme;
        var scale = UiScale.Current;
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
        if (ui.IconButton(closeCenter, closeRadius, IconGlyph.Of(FontAwesomeIcon.Times), ui.MutedInk,
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
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(BarFill));
        drawList.AddLine(area.Min, new Vector2(area.Max.X, area.Min.Y), ImGui.GetColorU32(theme.Separator), 1f);
        var iconCenter = new Vector2(area.Min.X + 22f * scale, area.Center.Y);
        AppSkin.Icon(iconCenter, IconGlyph.Of(FontAwesomeIcon.Pen), ui.Accent, 0.9f);
        var textLeft = iconCenter.X + 16f * scale;
        var closeRadius = 13f * scale;
        var textWidth = area.Max.X - 20f * scale - closeRadius * 2f - textLeft;
        Typography.Draw(new Vector2(textLeft, area.Min.Y + 7f * scale),
            Typography.FitText(Loc.T(L.Message.EditingLabel), textWidth, 0.78f, FontWeight.SemiBold),
            ui.Accent, 0.78f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(textLeft, area.Min.Y + 24f * scale),
            Typography.FitText(editBarPreview, textWidth, 0.82f, FontWeight.Regular), ui.MutedInk, 0.82f);
        var closeCenter = new Vector2(area.Max.X - 14f * scale - closeRadius, area.Center.Y);
        if (ui.IconButton(closeCenter, closeRadius, IconGlyph.Of(FontAwesomeIcon.Times), ui.MutedInk,
                AppSkin.Transparent, 0.9f, Loc.T(L.Common.Cancel))
            || (model.CanHandleEscape && ImGui.IsKeyPressed(ImGuiKey.Escape)))
        {
            ClearEdit();
        }
    }
}
