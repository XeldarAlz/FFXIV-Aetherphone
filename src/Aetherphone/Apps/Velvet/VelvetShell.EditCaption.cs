using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Translation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float EditCaptionFieldHeight = 140f;
    private const float EditCaptionCardPad = 14f;
    private const int VelvetCaptionLimit = 500;

    private readonly MentionAutocomplete editCaptionMentions;
    private string editCaptionPostId = string.Empty;
    private string editCaptionText = string.Empty;
    private string editCaptionOriginal = string.Empty;
    private string editCaptionStatus = string.Empty;
    private bool editCaptionFocus;
    private volatile bool editCaptionBusy;
    private volatile int editCaptionOutcome;

    private void OpenEditCaption(VelvetPostDto post)
    {
        editCaptionPostId = post.Id;
        editCaptionText = post.Caption;
        editCaptionOriginal = post.Caption;
        editCaptionStatus = string.Empty;
        editCaptionOutcome = 0;
        editCaptionFocus = true;
        editCaptionMentions.Close();
        router.Push(VelvetView.EditCaption(post.Id));
    }

    private void DrawEditCaption(Rect area)
    {
        var scale = UiScale.Current;
        if (editCaptionOutcome == 1)
        {
            editCaptionOutcome = 0;
            translation.Forget(new TranslationKey(TranslationSurface.Post, editCaptionPostId));
            router.Pop();
            return;
        }

        if (editCaptionOutcome == 2)
        {
            editCaptionOutcome = 0;
            editCaptionStatus = Loc.T(L.Velvet.EditCaptionFailed);
        }

        var saveLabel = editCaptionBusy ? Loc.T(L.Velvet.Saving) : Loc.T(L.Velvet.Save);
        if (VHeader.Push(area, Loc.T(L.Velvet.EditCaption), 2))
        {
            router.Pop();
            return;
        }

        if (ui.HeaderAction(area, saveLabel, !editCaptionBusy))
        {
            SaveCaption();
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var innerPad = EditCaptionCardPad * scale;
            var fieldHeight = EditCaptionFieldHeight * scale;
            var counterHeight = Typography.LineHeight(TextStyles.Footnote);
            var cardMin = new Vector2(origin.X, origin.Y + 12f * scale);
            var cardMax = new Vector2(origin.X + width,
                cardMin.Y + innerPad * 2f + fieldHeight + 6f * scale + counterHeight);
            var rounding = Metrics.Radius.Lg * scale;
            Squircle.Fill(drawList, cardMin, cardMax, rounding, VelvetTheme.Card.Packed());
            Squircle.Stroke(drawList, cardMin, cardMax, rounding, VelvetTheme.CardStroke.Packed(), 1f * scale);

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
            using (ImRaii.PushColor(ImGuiCol.Text, VelvetTheme.TitleInk))
            {
                SoftWrapField.Multiline("##velvetEditCaption", ref editCaptionText, VelvetCaptionLimit, field.Size,
                    wrapWidth, editCaptionMentions);
            }

            var picked = mentionPopup.Draw(editCaptionMentions, area, theme, images, lodestone);
            if (picked >= 0)
            {
                editCaptionMentions.Pick(picked);
            }

            mentionPopup.Gate(editCaptionMentions);
            if (editCaptionText.Length == 0)
            {
                Typography.Draw(drawList, field.Min + ImGui.GetStyle().FramePadding,
                    Typography.FitText(Loc.T(L.Velvet.CaptionHint),
                        field.Width - ImGui.GetStyle().FramePadding.X * 2f, TextStyles.Body), VelvetTheme.MutedInk,
                    TextStyles.Body);
            }

            var counter = editCaptionText.Length.ToString(Loc.Culture) + "/" + VelvetCaptionLimit.ToString(Loc.Culture);
            var counterSize = Typography.Measure(counter, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(cardMax.X - innerPad - counterSize.X, field.Max.Y + 6f * scale),
                counter, editCaptionText.Length >= VelvetCaptionLimit - 50 ? VelvetTheme.Danger : VelvetTheme.Faint,
                TextStyles.Footnote);

            var footTop = cardMax.Y + 12f * scale;
            var footHeight = editCaptionStatus.Length > 0
                ? Typography.DrawWrappedLeft(new Vector2(cardMin.X + 4f * scale, footTop), editCaptionStatus,
                    VelvetTheme.Danger, TextStyles.Footnote, width - 8f * scale)
                : 0f;
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
            router.Pop();
            return;
        }

        editCaptionBusy = true;
        editCaptionStatus = string.Empty;
        store.EditCaption(editCaptionPostId, trimmed, succeeded =>
        {
            editCaptionBusy = false;
            editCaptionOutcome = succeeded ? 1 : 2;
        });
    }
}
