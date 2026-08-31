using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Translation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float EditCaptionFieldMinHeight = 140f;

    private readonly MentionAutocomplete editCaptionMentions;
    private string editCaptionPostId = string.Empty;
    private string editCaptionText = string.Empty;
    private string editCaptionOriginal = string.Empty;
    private string editCaptionStatus = string.Empty;
    private bool editCaptionFocus;
    private volatile bool editCaptionBusy;
    private volatile int editCaptionOutcome;

    private void OpenEditCaption(PostDto post)
    {
        editCaptionPostId = post.Id;
        editCaptionText = post.Text;
        editCaptionOriginal = post.Text;
        editCaptionStatus = string.Empty;
        editCaptionOutcome = 0;
        editCaptionFocus = true;
        editCaptionMentions.Close();
        router.Push(AethergramRoute.EditCaption(post.Id));
    }

    private void DrawEditCaption(Rect area)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        if (editCaptionOutcome == 1)
        {
            editCaptionOutcome = 0;
            translation.Forget(new TranslationKey(TranslationSurface.Post, editCaptionPostId));
            back();
            return;
        }

        if (editCaptionOutcome == 2)
        {
            editCaptionOutcome = 0;
            editCaptionStatus = Loc.T(L.Aethergram.EditCaptionFailed);
        }

        var cancelLabel = Loc.T(L.Common.Cancel);
        var cancelSize = Typography.Measure(cancelLabel, EditWordStyle);
        var cancelMin = new Vector2(area.Min.X, area.Min.Y);
        var cancelMax = new Vector2(area.Min.X + CellPadX * scale + cancelSize.X + 12f * scale,
            area.Min.Y + AppHeader.Height * scale);
        var cancelHovered = UiInteract.Hover(cancelMin, cancelMax);
        Typography.Draw(drawList, new Vector2(area.Min.X + CellPadX * scale, rowCenterY - cancelSize.Y * 0.5f),
            cancelLabel, cancelHovered ? Ink.TitleInk : Ink.BodyInk, EditWordStyle);
        if (cancelHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(cancelMin, cancelMax, cancelHovered))
        {
            back();
        }

        var canSave = !editCaptionBusy;
        var doneLabel = editCaptionBusy ? Loc.T(L.Aethergram.Saving) : Loc.T(L.Aethergram.Done);
        var doneSize = Typography.Measure(doneLabel, EditWordStyle);
        var doneMax = new Vector2(area.Max.X, area.Min.Y + AppHeader.Height * scale);
        var doneMin = new Vector2(area.Max.X - CellPadX * scale - doneSize.X - 12f * scale, area.Min.Y);
        var doneHovered = canSave && UiInteract.Hover(doneMin, doneMax);
        Typography.Draw(drawList, new Vector2(area.Max.X - CellPadX * scale - doneSize.X, rowCenterY - doneSize.Y * 0.5f),
            doneLabel, !canSave ? Ink.FaintInk : doneHovered ? Ink.TitleInk : Ink.AccentLink, EditWordStyle);
        if (doneHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(doneMin, doneMax, doneHovered))
        {
            SaveCaption();
        }

        var reserve = MathF.Max(cancelMax.X - area.Min.X, area.Max.X - doneMin.X);
        var titleFitted = Typography.FitText(Loc.T(L.Aethergram.EditCaption),
            MathF.Max(1f, area.Width - reserve * 2f - 8f * scale), ScreenTitleStyle);
        Typography.DrawCentered(drawList, new Vector2(area.Center.X, rowCenterY), titleFitted, Ink.TitleInk,
            ScreenTitleStyle);

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            var listDrawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var innerPad = EditInnerPad * scale;
            var cardMin = new Vector2(origin.X, origin.Y + 14f * scale);
            var fieldHeight = EditCaptionFieldMinHeight * scale;
            var counterHeight = Typography.LineHeight(EditFootStyle);
            var cardMax = new Vector2(origin.X + width,
                cardMin.Y + innerPad + fieldHeight + 6f * scale + counterHeight + innerPad);
            var rounding = EditCardRounding * scale;
            Squircle.Fill(listDrawList, cardMin, cardMax, rounding, ImGui.GetColorU32(EditCardFill));
            Squircle.Stroke(listDrawList, cardMin, cardMax, rounding, ImGui.GetColorU32(EditCardStroke), 1f);

            var field = new Rect(new Vector2(cardMin.X + innerPad, cardMin.Y + innerPad),
                new Vector2(cardMax.X - innerPad, cardMin.Y + innerPad + fieldHeight));
            ImGui.SetCursorScreenPos(field.Min);
            if (editCaptionFocus)
            {
                ImGui.SetKeyboardFocusHere();
                editCaptionFocus = false;
            }

            var wrapWidth = field.Width - ImGui.GetStyle().FramePadding.X * 2f - 4f * scale;
            using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
            using (ImRaii.PushColor(ImGuiCol.Text, Ink.TitleInk))
            {
                SoftWrapField.Multiline("##aethergramEditCaption", ref editCaptionText, MaxCaptionLength, field.Size,
                    wrapWidth, editCaptionMentions);
            }

            var pickedMention = mentionPopup.Draw(editCaptionMentions, area, theme, images, lodestone);
            if (pickedMention >= 0)
            {
                editCaptionMentions.Pick(pickedMention);
            }

            mentionPopup.Gate(editCaptionMentions);
            if (editCaptionText.Length == 0)
            {
                var hint = Typography.FitText(Loc.T(L.Aethergram.CaptionHint),
                    field.Width - ImGui.GetStyle().FramePadding.X * 2f, TextStyles.Body);
                Typography.Draw(listDrawList, field.Min + ImGui.GetStyle().FramePadding, hint, Ink.MutedInk,
                    TextStyles.Body);
            }

            var counter = $"{editCaptionText.Length.ToString(Loc.Culture)}/{MaxCaptionLength.ToString(Loc.Culture)}";
            var counterSize = Typography.Measure(counter, EditFootStyle);
            var counterInk = editCaptionText.Length >= MaxCaptionLength - 50 ? Ink.Danger : Ink.FaintInk;
            Typography.Draw(listDrawList,
                new Vector2(cardMax.X - innerPad - counterSize.X, field.Max.Y + 6f * scale), counter, counterInk,
                EditFootStyle);

            var footTop = cardMax.Y + 12f * scale;
            var footHeight = 0f;
            if (editCaptionStatus.Length > 0)
            {
                footHeight = Typography.DrawWrappedLeft(new Vector2(cardMin.X + 4f * scale, footTop),
                    editCaptionStatus, Ink.Danger, EditFootStyle, width - 8f * scale);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, footTop + footHeight + 40f * scale - origin.Y));
        }
    }

    private void SaveCaption()
    {
        if (editCaptionBusy || editCaptionPostId.Length == 0)
        {
            return;
        }

        var trimmed = editCaptionText.Trim();
        if (string.Equals(trimmed, editCaptionOriginal, StringComparison.Ordinal))
        {
            back();
            return;
        }

        editCaptionBusy = true;
        editCaptionStatus = string.Empty;
        store.EditCaption(editCaptionPostId, trimmed, ok =>
        {
            editCaptionBusy = false;
            editCaptionOutcome = ok ? 1 : 2;
        });
    }
}
