using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
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
    float SendIconScale,
    float SendRadius = 15f);

internal static class CommentComposerBar
{
    private const float LeadingInset = 6f;
    private const float LeadingButtonRadius = 12f;
    private const float LeadingButtonGap = 0f;
    private const float TextGap = 4f;

    public static bool Draw(Rect bar, Rect screen, AppSkin ui, PhoneTheme theme, in CommentComposerStyle style,
        string inputId, string hint, ref string draft, int maxLength, MentionAutocomplete mentions,
        MentionPopup mentionPopup, RemoteImageCache images, LodestoneService lodestone, bool busy,
        ref bool focusPending, EmojiComposer emoji, CommentAttachment? attachment = null,
        PhotoLibrary? library = null, WallpaperImageCache? wallpaperImages = null, float sendReveal = 1f)
    {
        var scale = UiScale.Current;
        var attachmentActive = attachment is not null && library is not null && wallpaperImages is not null;
        var overlayTop = bar.Min.Y;
        if (attachmentActive)
        {
            attachment!.ConsumePendingImport();
            if (emoji.Open)
            {
                attachment.ClosePanel();
            }

            var stripHeight = attachment.StripHeight(scale);
            if (stripHeight > 0f)
            {
                overlayTop = bar.Min.Y - stripHeight;
                attachment.DrawStrip(new Rect(new Vector2(bar.Min.X, overlayTop), new Vector2(bar.Max.X, bar.Min.Y)),
                    theme, wallpaperImages!);
            }
        }

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(bar.Min, new Vector2(bar.Max.X, bar.Min.Y), ImGui.GetColorU32(style.Hairline), 1f);
        var pillMin = new Vector2(bar.Min.X + 12f * scale, bar.Min.Y + style.PillPadY * scale);
        var pillRightInset = style.CircleSend
            ? Easing.Lerp(12f, style.PillRightInset, Math.Clamp(sendReveal, 0f, 1f))
            : style.PillRightInset;
        var pillMax = new Vector2(bar.Max.X - pillRightInset * scale, bar.Max.Y - style.PillPadY * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f, ImGui.GetColorU32(style.FieldFill));
        var emojiRadius = LeadingButtonRadius * scale;
        var emojiCenter = new Vector2(pillMin.X + LeadingInset * scale + emojiRadius, bar.Center.Y);
        emoji.DrawToggle(ui, emojiCenter, emojiRadius, style.SendEnabled,
            Palette.WithAlpha(style.TextInk, 0.5f), Loc.T(L.Common.Emoji));
        var textLeft = emojiCenter.X + emojiRadius + TextGap * scale;
        if (attachmentActive)
        {
            var photoRadius = LeadingButtonRadius * scale;
            var photoCenter = new Vector2(emojiCenter.X + emojiRadius + LeadingButtonGap * scale + photoRadius,
                bar.Center.Y);
            attachment!.DrawToggle(ui, photoCenter, photoRadius, style.SendEnabled,
                Palette.WithAlpha(style.TextInk, 0.5f), Loc.T(L.Common.AddPhoto), library!, emoji);
            textLeft = photoCenter.X + photoRadius + TextGap * scale;
        }
        ImGui.SetCursorScreenPos(new Vector2(textLeft,
            (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - textLeft - 10f * scale);
        if (focusPending && !InputShield.Active)
        {
            ImGui.SetKeyboardFocusHere();
            MentionField.CaretToEnd(inputId);
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

        var canSend = (draft.Trim().Length > 0 || (attachmentActive && attachment!.Path is not null)) && !busy;
        if (style.CircleSend && sendReveal > 0.01f)
        {
            var reveal = Math.Clamp(sendReveal, 0f, 1f);
            var sendRadius = style.SendRadius * scale * (0.6f + 0.4f * reveal);
            var sendCenter = new Vector2(pillMax.X + 6f * scale + style.SendRadius * scale, bar.Center.Y);
            var sendFill = canSend ? style.SendEnabled : style.SendDisabled;
            drawList.AddCircleFilled(sendCenter, sendRadius,
                ImGui.GetColorU32(Palette.WithAlpha(sendFill, sendFill.W * reveal)), 24);
            AppSkin.Icon(sendCenter, IconGlyph.Of(FontAwesomeIcon.PaperPlane),
                Palette.WithAlpha(style.SendIconInk, style.SendIconInk.W * reveal), style.SendIconScale * reveal);
            if (reveal > 0.5f && UiInteract.HoverClick(sendCenter - new Vector2(sendRadius, sendRadius),
                    sendCenter + new Vector2(sendRadius, sendRadius)))
            {
                submitted = true;
            }
        }
        else
        {
            var sendCenter = new Vector2(bar.Max.X - 28f * scale, bar.Center.Y);
            if (ui.IconButton(sendCenter, 16f * scale, IconGlyph.Of(FontAwesomeIcon.PaperPlane),
                    canSend ? style.SendEnabled : style.SendDisabled, new Vector4(0f, 0f, 0f, 0f),
                    style.SendIconScale))
            {
                submitted = true;
            }
        }

        var panelHeight = emoji.PanelHeight(scale);
        if (panelHeight > 0f)
        {
            emoji.DrawPanel(new Rect(new Vector2(bar.Min.X, overlayTop - panelHeight),
                new Vector2(bar.Max.X, overlayTop)), ui, ref draft, maxLength);
        }
        else if (attachmentActive)
        {
            var photoPanelHeight = attachment!.PanelHeight(scale);
            if (photoPanelHeight > 0f)
            {
                attachment.DrawPanel(new Rect(new Vector2(bar.Min.X, overlayTop - photoPanelHeight),
                    new Vector2(bar.Max.X, overlayTop)), ui, theme, wallpaperImages!);
            }
        }

        return submitted && canSend;
    }
}
