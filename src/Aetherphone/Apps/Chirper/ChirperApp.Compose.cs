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
            profile.ForceRefreshSoon();
            router.Pop();
            return;
        }

        if (composeOutcome == 2)
        {
            composeOutcome = 0;
            composeStatus = Loc.T(L.Account.CannotReach);
        }

        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Chirper.NewChirp), back);
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
            var cardMin = origin;
            var cardMax = new Vector2(origin.X + width, area.Max.Y - footerHeight);
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
            var inputHeight = cardMax.Y - inputTop - pad;
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

            var footerY = area.Max.Y - footerHeight * 0.5f;
            if (composeStatus.Length > 0)
            {
                Typography.Draw(
                    new Vector2(origin.X + 2f * scale, footerY - Typography.Measure(composeStatus, 0.85f).Y * 0.5f),
                    composeStatus, theme.Danger, 0.85f);
            }

            var remaining = MaxPostLength - draft.Length;
            var counterColor = remaining < 40
                ? (remaining < 0 ? theme.Danger : new Vector4(0.95f, 0.65f, 0.20f, 1f))
                : AppPalettes.Chirper.MutedInk;
            var counter = remaining.ToString(Loc.Culture);
            var counterSize = Typography.Measure(counter, 0.9f, FontWeight.Medium);
            Typography.Draw(new Vector2(area.Max.X - 4f * scale - counterSize.X, footerY - counterSize.Y * 0.5f),
                counter, counterColor, 0.9f, FontWeight.Medium);
        }
    }

    private void Submit()
    {
        if (string.IsNullOrWhiteSpace(draft) || store.Posting)
        {
            return;
        }

        composeStatus = string.Empty;
        store.Compose(draft, ok => composeOutcome = ok ? 1 : 2);
    }
}
