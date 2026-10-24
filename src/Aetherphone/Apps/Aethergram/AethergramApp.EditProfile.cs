using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float EditAvatarRadius = 46f;
    private const float EditRowHeight = 46f;
    private const float EditLabelWidth = 104f;
    private const float EditInnerPad = 14f;
    private const float EditBioMinHeight = 64f;
    private const float EditHeaderGap = 8f;

    private static readonly TextStyle EditLabelStyle = TextStyles.Subheadline;
    private static readonly TextStyle EditValueStyle = TextStyles.Body;
    private static readonly TextStyle EditFootStyle = TextStyles.Footnote;
    private static readonly TextStyle EditLinkStyle = TextStyles.SubheadlineEmphasized;

    private string editDisplay = string.Empty;
    private string editHandle = string.Empty;
    private string editBio = string.Empty;
    private string editStatus = string.Empty;
    private string? editLoadedFor;
    private volatile bool editBusy;
    private volatile int editOutcome;

    private bool DrawEditHeader(Rect area, string title, bool canSave, bool busy)
    {
        var scale = UiScale.Current;
        var doneLabel = busy ? Loc.T(L.Aethergram.Saving) : Loc.T(L.Aethergram.Done);
        var height = Button.SmallHeight * scale;
        var width = Button.WidthFor(doneLabel, ButtonSize.Small);
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var right = area.Max.X - CellPadX * scale;
        var done = new Rect(new Vector2(right - width, rowCenterY - height * 0.5f),
            new Vector2(right, rowCenterY + height * 0.5f));
        SocialChrome.DrawScreenHeader(area, title, Ink, back, ScreenTitleStyle, width / scale + EditHeaderGap,
            centered: true);
        return Button.Draw(done, doneLabel, ui.Ink, ButtonStyle.Prominent, enabled: canSave,
            id: "aethergram.edit.done");
    }
    private void DrawEditProfile(Rect area)
    {
        var scale = UiScale.Current;
        var me = store.Me ?? (store.ProfileUser is { IsMe: true } self ? self : null);
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

        var handleValid = SocialProfilePages.IsHandleValid(editHandle);
        var canSave = !editBusy && !string.IsNullOrWhiteSpace(editDisplay) && handleValid;
        if (DrawEditHeader(area, Loc.T(L.Aethergram.EditProfile), canSave, editBusy))
        {
            SaveProfile();
        }

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
            ui.Card(listDrawList, cardMin, cardMax, Metrics.Radius.Grouped * scale);

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
            using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
            using (UiScope.PushColor(ImGuiCol.Text, Ink.TitleInk))
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
        using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.Text, ink))
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
