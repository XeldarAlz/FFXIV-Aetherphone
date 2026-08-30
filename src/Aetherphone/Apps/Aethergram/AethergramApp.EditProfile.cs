using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float EditAvatarRadius = 46f;
    private const float EditRowHeight = 46f;
    private const float EditLabelWidth = 104f;
    private const float EditInnerPad = 14f;
    private const float EditBioMinHeight = 64f;
    private const float EditCardRounding = 14f;

    private static readonly TextStyle EditWordStyle = new(1.03f, FontWeight.SemiBold);
    private static readonly TextStyle EditLabelStyle = new(0.93f, FontWeight.Regular);
    private static readonly TextStyle EditValueStyle = new(1f, FontWeight.Regular);
    private static readonly TextStyle EditFootStyle = new(0.83f, FontWeight.Regular);
    private static readonly TextStyle EditLinkStyle = TextStyles.SubheadlineEmphasized;
    private static readonly Vector4 EditCardFill = new(1f, 1f, 1f, 0.045f);
    private static readonly Vector4 EditCardStroke = new(1f, 1f, 1f, 0.07f);

    private string editDisplay = string.Empty;
    private string editHandle = string.Empty;
    private string editBio = string.Empty;
    private string editStatus = string.Empty;
    private string? editLoadedFor;
    private volatile bool editBusy;
    private volatile int editOutcome;

    private void DrawEditProfile(Rect area)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var me = store.Me ?? (store.ProfileUser is { IsMe: true } self ? self : null);
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        if (me is null)
        {
            store.EnsureMe();
            DrawScreenHeader(area, Loc.T(L.Aethergram.EditProfile));
            Typography.DrawCentered(area.Center, Loc.T(L.Common.Loading), Ink.MutedInk);
            return;
        }

        if (editOutcome == 1)
        {
            editOutcome = 0;
            store.ReloadProfile();
            back();
            return;
        }

        if (editOutcome == 2)
        {
            editOutcome = 0;
            editStatus = Loc.T(L.Aethergram.HandleTaken);
        }

        if (editLoadedFor != me.Id)
        {
            editLoadedFor = me.Id;
            editDisplay = me.DisplayName;
            editHandle = me.Handle;
            editBio = me.Bio;
            editStatus = string.Empty;
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

        var handleValid = SocialProfilePages.IsHandleValid(editHandle);
        var canSave = !editBusy && !string.IsNullOrWhiteSpace(editDisplay) && handleValid;
        var doneLabel = editBusy ? Loc.T(L.Aethergram.Saving) : Loc.T(L.Aethergram.Done);
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
            SaveProfile();
        }

        var reserve = MathF.Max(cancelMax.X - area.Min.X, area.Max.X - doneMin.X);
        var titleFitted = Typography.FitText(Loc.T(L.Aethergram.EditProfile),
            MathF.Max(1f, area.Width - reserve * 2f - 8f * scale), ScreenTitleStyle);
        Typography.DrawCentered(drawList, new Vector2(area.Center.X, rowCenterY), titleFitted, Ink.TitleInk,
            ScreenTitleStyle);

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            var listDrawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var avatarRadius = EditAvatarRadius * scale;
            var avatarCenter = new Vector2(origin.X + width * 0.5f, origin.Y + 14f * scale + avatarRadius);
            DrawAvatar(avatarCenter, avatarRadius, me.Name, me.World, me.AvatarUrl, 1.4f, 64, Frames.Of(me.FrameId));
            var avatarExtent = new Vector2(avatarRadius, avatarRadius);
            if (UiInteract.HoverClick(avatarCenter - avatarExtent, avatarCenter + avatarExtent))
            {
                StartCompose(true);
            }

            var linkLabel = Loc.T(L.Aethergram.EditPicture);
            var linkSize = Typography.Measure(linkLabel, EditLinkStyle);
            var linkTop = avatarCenter.Y + avatarRadius + 10f * scale;
            var linkMin = new Vector2(avatarCenter.X - linkSize.X * 0.5f - 8f * scale, linkTop - 4f * scale);
            var linkMax = new Vector2(avatarCenter.X + linkSize.X * 0.5f + 8f * scale, linkTop + linkSize.Y + 4f * scale);
            var linkHovered = UiInteract.Hover(linkMin, linkMax);
            Typography.Draw(listDrawList, new Vector2(avatarCenter.X - linkSize.X * 0.5f, linkTop), linkLabel,
                linkHovered ? Ink.TitleInk : Ink.AccentLink, EditLinkStyle);
            if (linkHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(linkMin, linkMax, linkHovered))
            {
                StartCompose(true);
            }

            var cardTop = linkMax.Y + 18f * scale;
            var cardMin = new Vector2(origin.X, cardTop);
            var cardRight = origin.X + width;
            var rowHeight = EditRowHeight * scale;
            var labelWidth = EditLabelWidth * scale;
            var innerPad = EditInnerPad * scale;
            var bioLabelHeight = Typography.LineHeight(EditLabelStyle);
            var bioFieldHeight = EditBioMinHeight * scale;
            var bioRowHeight = 12f * scale + bioLabelHeight + 5f * scale + bioFieldHeight + 12f * scale;
            var cardMax = new Vector2(cardRight, cardTop + rowHeight * 2f + bioRowHeight);
            var rounding = EditCardRounding * scale;
            Squircle.Fill(listDrawList, cardMin, cardMax, rounding, ImGui.GetColorU32(EditCardFill));
            Squircle.Stroke(listDrawList, cardMin, cardMax, rounding, ImGui.GetColorU32(EditCardStroke), 1f);

            var nameRowTop = cardTop;
            DrawEditLabel(listDrawList, cardMin.X + innerPad, nameRowTop, rowHeight, labelWidth,
                Loc.T(L.Aethergram.DisplayNameLabel));
            DrawEditInput("##aethergramEditName", cardMin.X + innerPad + labelWidth, cardRight - innerPad, nameRowTop,
                rowHeight, ref editDisplay, SocialProfilePages.DisplayNameMax, ImGuiInputTextFlags.None, Ink.TitleInk);
            DrawHairline(listDrawList, cardMin.X, cardRight, nameRowTop + rowHeight);

            var handleRowTop = nameRowTop + rowHeight;
            DrawEditLabel(listDrawList, cardMin.X + innerPad, handleRowTop, rowHeight, labelWidth,
                Loc.T(L.Aethergram.HandleLabel));
            var atSize = Typography.Measure("@", EditValueStyle);
            Typography.Draw(listDrawList,
                new Vector2(cardMin.X + innerPad + labelWidth, handleRowTop + (rowHeight - atSize.Y) * 0.5f), "@",
                Ink.FaintInk, EditValueStyle);
            var checkReserve = handleValid ? 22f * scale : 0f;
            if (DrawEditInput("##aethergramEditHandle", cardMin.X + innerPad + labelWidth + atSize.X + 2f * scale,
                    cardRight - innerPad - checkReserve, handleRowTop, rowHeight, ref editHandle,
                    SocialProfilePages.HandleMax, ImGuiInputTextFlags.CharsNoBlank,
                    handleValid ? Ink.TitleInk : Ink.Danger))
            {
                editHandle = editHandle.ToLowerInvariant();
            }

            if (handleValid)
            {
                PhoneIcon.Draw(listDrawList, new Vector2(cardRight - innerPad - 7f * scale, handleRowTop + rowHeight * 0.5f),
                    PhoneIcons.Check, Ink.PresenceGreen, 14f * scale);
            }

            DrawHairline(listDrawList, cardMin.X, cardRight, handleRowTop + rowHeight);

            var bioRowTop = handleRowTop + rowHeight;
            var bioLabelTop = bioRowTop + 12f * scale;
            Typography.Draw(listDrawList, new Vector2(cardMin.X + innerPad, bioLabelTop), Loc.T(L.Aethergram.BioLabel),
                Ink.MutedInk, EditLabelStyle);
            var counter = $"{editBio.Length.ToString(Loc.Culture)}/{SocialProfilePages.BioMax.ToString(Loc.Culture)}";
            var counterSize = Typography.Measure(counter, EditFootStyle);
            Typography.Draw(listDrawList,
                new Vector2(cardRight - innerPad - counterSize.X, bioLabelTop + (bioLabelHeight - counterSize.Y) * 0.5f),
                counter, Ink.FaintInk, EditFootStyle);
            var bioFieldTop = bioLabelTop + bioLabelHeight + 5f * scale;
            var bioFieldWidth = cardRight - innerPad - (cardMin.X + innerPad);
            ImGui.SetCursorScreenPos(new Vector2(cardMin.X + innerPad, bioFieldTop));
            using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
            using (ImRaii.PushColor(ImGuiCol.Text, Ink.TitleInk))
            {
                var wrapWidth = bioFieldWidth - ImGui.GetStyle().FramePadding.X * 2f - 4f * scale;
                SoftWrapField.Multiline("##aethergramEditBio", ref editBio, SocialProfilePages.BioMax,
                    new Vector2(bioFieldWidth, bioFieldHeight), wrapWidth);
            }

            var footTop = cardMax.Y + 12f * scale;
            var footText = editStatus.Length > 0 ? editStatus : Loc.T(L.Aethergram.HandleRules);
            var footHeight = Typography.DrawWrappedLeft(new Vector2(cardMin.X + 4f * scale, footTop), footText,
                editStatus.Length > 0 ? Ink.Danger : Ink.FaintInk, EditFootStyle, cardRight - cardMin.X - 8f * scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, footTop + footHeight + 40f * scale - origin.Y));
        }
    }

    private static void DrawEditLabel(ImDrawListPtr drawList, float left, float rowTop, float rowHeight,
        float maxWidth, string label)
    {
        var fitted = Typography.FitText(label, MathF.Max(1f, maxWidth - 8f * UiScale.Current), EditLabelStyle);
        var size = Typography.Measure(fitted, EditLabelStyle);
        Typography.Draw(drawList, new Vector2(left, rowTop + (rowHeight - size.Y) * 0.5f), fitted, Ink.MutedInk,
            EditLabelStyle);
    }

    private static bool DrawEditInput(string id, float left, float right, float rowTop, float rowHeight,
        ref string value, int maxLength, ImGuiInputTextFlags flags, Vector4 ink)
    {
        ImGui.SetCursorScreenPos(new Vector2(left, rowTop + rowHeight * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(MathF.Max(1f, right - left));
        Plugin.Fonts.NoticeText(value);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ink))
        {
            return ImGui.InputText(id, ref value, maxLength, flags);
        }
    }

    private void SaveProfile()
    {
        if (!store.IsSignedIn || editBusy)
        {
            return;
        }

        if (!SocialProfilePages.IsHandleValid(editHandle) || string.IsNullOrWhiteSpace(editDisplay))
        {
            editStatus = Loc.T(L.Aethergram.HandleRules);
            return;
        }

        editBusy = true;
        editStatus = string.Empty;
        store.UpdateProfile(editDisplay.Trim(), editHandle.Trim(), editBio.Trim(), (ok, _) =>
        {
            editBusy = false;
            editOutcome = ok ? 1 : 2;
        });
    }
}
