using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Chirper;

// The compose flow: the compose card behind the shared ComposeFab. The body field wraps and stays
// within its limit through the shared SoftWrapField. Split from the main feed for readability.
internal sealed partial class ChirperApp
{
    private void DrawCompose(Rect area)
    {
        if (composeOutcome == 1)
        {
            composeOutcome = 0;
            draft = string.Empty;
            composeStatus = string.Empty;
            composeEmoji.Close();
            quoteTarget = null;
            quoteTargetId = null;
            store.RefreshFeed(SocialFeedScope.ForYou);
            store.RefreshFeed(SocialFeedScope.Following);
            router.Pop();
            return;
        }

        if (composeOutcome == 2)
        {
            composeOutcome = 0;
            composeStatus = Loc.T(L.Account.CannotReach);
        }

        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(quoteTarget is not null ? L.Chirper.QuoteTitle : L.Chirper.NewChirp), back);
        var canPost = !string.IsNullOrWhiteSpace(draft) && !store.Posting;
        if (ui.HeaderAction(area, store.Posting ? Loc.T(L.Chirper.Saving) : Loc.T(L.Chirper.Post), canPost))
        {
            Submit();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var footerHeight = 40f * scale;
            var emojiHeight = composeEmoji.PanelHeight(scale);
            var panelTop = area.Max.Y - footerHeight - emojiHeight;
            var cardMin = origin;
            var cardMax = new Vector2(origin.X + width, emojiHeight > 0f ? panelTop - 8f * scale : panelTop);
            ui.Card(drawList, cardMin, cardMax, 18f * scale);
            var pad = 14f * scale;
            var radius = 20f * scale;
            var me = store.Me;
            var displayName = me is null ? string.Empty : SocialIdentity.Name(me.DisplayName, me.Handle);
            if (me is not null)
            {
                DrawAvatar(drawList, new Vector2(cardMin.X + pad + radius, cardMin.Y + pad + radius), radius, me.Name,
                    me.World, me.AvatarUrl, 0.95f, 48);
            }

            var inputLeft = pad + radius * 2f + 12f * scale;
            var inputX = cardMin.X + inputLeft;
            var nameMaxWidth = MathF.Max(1f, width - inputLeft - pad);
            displayName = displayName.Length > 0
                ? Typography.FitText(displayName, nameMaxWidth, 1.05f, FontWeight.SemiBold)
                : displayName;
            var nameSize = displayName.Length > 0
                ? Typography.Measure(displayName, 1.05f, FontWeight.SemiBold)
                : Vector2.Zero;
            if (displayName.Length > 0)
            {
                Typography.Draw(new Vector2(inputX, cardMin.Y + pad), displayName, theme.TextStrong, 1.05f,
                    FontWeight.SemiBold);
            }

            var inputTop = cardMin.Y + pad + nameSize.Y + 6f * scale;
            var inputWidth = width - inputLeft - pad;
            var quotePreviewHeight = quoteTarget is not null
                ? QuotedCardHeight(quoteTarget, inputWidth) + 8f * scale
                : 0f;
            var inputHeight = cardMax.Y - inputTop - pad - quotePreviewHeight;
            ImGui.SetCursorScreenPos(new Vector2(inputX, inputTop));
            ImGui.SetNextItemWidth(inputWidth);
            if (composeFocus)
            {
                ImGui.SetKeyboardFocusHere();
                composeFocus = false;
            }

            var framePadding = ImGui.GetStyle().FramePadding.X;
            var composeWrapWidth = inputWidth - framePadding * 2f - 4f * scale;
            using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Chirper.TitleInk))
            using (Plugin.Fonts.Push(1.15f))
            {
                SoftWrapField.Multiline("##chirpBody", ref draft, MaxPostLength,
                    new Vector2(inputWidth, inputHeight), composeWrapWidth, composeMentions);
            }

            var pickedMention = mentionPopup.Draw(composeMentions, area, theme, images, lodestone);
            if (pickedMention >= 0)
            {
                composeMentions.Pick(pickedMention);
            }

            mentionPopup.Gate(composeMentions);

            if (draft.Length == 0)
            {
                Typography.Draw(new Vector2(inputX + 4f * scale, inputTop + 2f * scale), Loc.T(L.Chirper.Compose),
                    AppPalettes.Chirper.MutedInk, 1.15f);
            }

            if (quoteTarget is not null)
            {
                var quoteMin = new Vector2(inputX, inputTop + inputHeight + 8f * scale);
                DrawQuotedCard(drawList, quoteMin, inputWidth, QuotedCardHeight(quoteTarget, inputWidth), quoteTarget,
                    false, "compose.quote");
            }

            if (composeEmoji.Open)
            {
                var panel = new Rect(new Vector2(area.Min.X, panelTop),
                    new Vector2(area.Max.X, area.Max.Y - footerHeight));
                composeEmoji.DrawPanel(panel, ui, ref draft, MaxPostLength);
            }

            var footerY = area.Max.Y - footerHeight * 0.5f;
            var emojiRadius = 15f * scale;
            var emojiCenter = new Vector2(origin.X + 4f * scale + emojiRadius, footerY);
            composeEmoji.DrawToggle(ui, emojiCenter, emojiRadius, Accent, AppPalettes.Chirper.MutedInk,
                Loc.T(L.Common.Emoji));
            var remaining = MaxPostLength - draft.Length;
            var counterColor = remaining < 40
                ? (remaining < 0 ? theme.Danger : new Vector4(0.95f, 0.65f, 0.20f, 1f))
                : AppPalettes.Chirper.MutedInk;
            var counter = remaining.ToString(Loc.Culture);
            var counterSize = Typography.Measure(counter, 0.9f, FontWeight.Medium);
            Typography.Draw(new Vector2(area.Max.X - 4f * scale - counterSize.X, footerY - counterSize.Y * 0.5f),
                counter, counterColor, 0.9f, FontWeight.Medium);
            if (composeStatus.Length > 0)
            {
                var statusLeft = emojiCenter.X + emojiRadius + 10f * scale;
                var statusMaxWidth = MathF.Max(1f, area.Max.X - 8f * scale - counterSize.X - statusLeft);
                var clippedStatus = Typography.FitText(composeStatus, statusMaxWidth, 0.85f, FontWeight.Regular);
                Typography.Draw(
                    new Vector2(statusLeft, footerY - Typography.Measure(clippedStatus, 0.85f).Y * 0.5f),
                    clippedStatus, theme.Danger, 0.85f);
            }
        }
    }

    private void Submit()
    {
        if (string.IsNullOrWhiteSpace(draft) || store.Posting)
        {
            return;
        }

        composeStatus = string.Empty;
        if (quoteTargetId is not null)
        {
            store.Quote(draft, quoteTargetId, ok => composeOutcome = ok ? 1 : 2);
        }
        else
        {
            store.Compose(draft, ok => composeOutcome = ok ? 1 : 2);
        }
    }
}
