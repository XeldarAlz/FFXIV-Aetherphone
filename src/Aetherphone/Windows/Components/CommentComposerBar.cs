using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal readonly record struct CommentComposerStyle(
    Vector4 Hairline,
    Vector4 FieldFill,
    Vector4 TextInk,
    Vector4 SendEnabled,
    Vector4 SendDisabled,
    Vector4 SendIconInk,
    bool CircleSend,
    float PillPadY,
    float PillRightInset,
    float SendIconScale);

internal static class CommentComposerBar
{
    public static bool Draw(Rect bar, Rect screen, AppSkin ui, PhoneTheme theme, in CommentComposerStyle style,
        string inputId, string hint, ref string draft, int maxLength, MentionAutocomplete mentions,
        MentionPopup mentionPopup, RemoteImageCache images, LodestoneService lodestone, bool busy,
        ref bool focusPending, EmojiComposer emoji)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(bar.Min, new Vector2(bar.Max.X, bar.Min.Y), ImGui.GetColorU32(style.Hairline), 1f);
        var pillMin = new Vector2(bar.Min.X + 12f * scale, bar.Min.Y + style.PillPadY * scale);
        var pillMax = new Vector2(bar.Max.X - style.PillRightInset * scale, bar.Max.Y - style.PillPadY * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f, ImGui.GetColorU32(style.FieldFill));
        var emojiRadius = 14f * scale;
        var emojiCenter = new Vector2(pillMin.X + 11f * scale + emojiRadius, bar.Center.Y);
        emoji.DrawToggle(ui, emojiCenter, emojiRadius, style.SendEnabled,
            Palette.WithAlpha(style.TextInk, 0.5f), Loc.T(L.Common.Emoji));
        var textLeft = emojiCenter.X + emojiRadius + 6f * scale;
        ImGui.SetCursorScreenPos(new Vector2(textLeft,
            (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - textLeft - 10f * scale);
        if (focusPending)
        {
            ImGui.SetKeyboardFocusHere();
            focusPending = false;
        }

        var submitted = false;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, style.TextInk))
        {
            submitted = MentionField.SingleLineWithHint(inputId, hint, ref draft, maxLength, mentions);
        }

        var pickedMention = mentionPopup.Draw(mentions, screen, theme, images, lodestone);
        if (pickedMention >= 0)
        {
            mentions.Pick(pickedMention);
        }

        mentionPopup.Gate(mentions);

        var canSend = draft.Trim().Length > 0 && !busy;
        if (style.CircleSend)
        {
            var sendRadius = 15f * scale;
            var sendCenter = new Vector2(pillMax.X + 6f * scale + sendRadius, bar.Center.Y);
            drawList.AddCircleFilled(sendCenter, sendRadius,
                ImGui.GetColorU32(canSend ? style.SendEnabled : style.SendDisabled), 24);
            AppSkin.Icon(sendCenter, FontAwesomeIcon.PaperPlane.ToIconString(), style.SendIconInk,
                style.SendIconScale);
            if (UiInteract.HoverClick(sendCenter - new Vector2(sendRadius, sendRadius),
                    sendCenter + new Vector2(sendRadius, sendRadius)))
            {
                submitted = true;
            }
        }
        else
        {
            var sendCenter = new Vector2(bar.Max.X - 28f * scale, bar.Center.Y);
            if (ui.IconButton(sendCenter, 16f * scale, FontAwesomeIcon.PaperPlane.ToIconString(),
                    canSend ? style.SendEnabled : style.SendDisabled, new Vector4(0f, 0f, 0f, 0f),
                    style.SendIconScale))
            {
                submitted = true;
            }
        }

        var panelHeight = emoji.PanelHeight(scale);
        if (panelHeight > 0f)
        {
            emoji.DrawPanel(new Rect(new Vector2(bar.Min.X, bar.Min.Y - panelHeight),
                new Vector2(bar.Max.X, bar.Min.Y)), ui, ref draft, maxLength);
        }

        return submitted && canSend;
    }
}
