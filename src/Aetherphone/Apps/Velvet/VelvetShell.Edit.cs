using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private string editDisplayName = string.Empty;
    private string editHandle = string.Empty;
    private string editIntro = string.Empty;
    private string editPronouns = string.Empty;
    private int editGender;
    private int editIntent;
    private int editRelationship;
    private readonly List<string> editRole = new();
    private readonly List<string> editTags = new();
    private readonly List<string> editLimits = new();
    private volatile bool editBusy;
    private bool avatarEditing;

    private void BeginEditProfile()
    {
        var me = store.Me;
        if (me is null)
        {
            return;
        }

        editDisplayName = me.DisplayName;
        editHandle = me.Handle;
        editIntro = me.Intro;
        editPronouns = me.Pronouns;
        editGender = VelvetGender.Sanitize(me.Gender);
        editIntent = VelvetIntent.Sanitize(me.LookingFor);
        editRelationship = me.RelationshipStatus;
        editRole.Clear();
        editRole.AddRange(VelvetTags.Parse(me.Dynamic));
        editTags.Clear();
        editTags.AddRange(me.Tags);
        editLimits.Clear();
        editLimits.AddRange(me.Limits);
        avatarEditing = false;
    }

    private void DrawEditProfile(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (avatarEditing)
        {
            var context = new PhoneContext(area, theme, navigation);
            if (avatar.Draw(area, context, ui.Accent))
            {
                avatarEditing = false;
            }

            return;
        }

        if (VHeader.Push(area, Loc.T(L.Velvet.EditProfile), theme))
        {
            router.Pop();
            return;
        }

        if (ui.HeaderAction(area, editBusy ? Loc.T(L.Velvet.Saving) : Loc.T(L.Velvet.Save), !editBusy))
        {
            SaveProfile();
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            Gap(8f);
            DrawEditAvatar();
            Gap(14f);

            VSectionHeader.Card(FontAwesomeIcon.User, Loc.T(L.Velvet.CardIdentity));
            Gap(4f);
            ui.Field(Loc.T(L.Velvet.DisplayNameLabel), "##ed_name", ref editDisplayName, 40, false);
            ui.Field(Loc.T(L.Velvet.HandleLabel), "##ed_handle", ref editHandle, 15, false);
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.Feather, Loc.T(L.Velvet.CardAbout));
            Gap(4f);
            ui.Field(Loc.T(L.Velvet.IntroduceYourself), "##ed_intro", ref editIntro, 400, true);
            ui.Field(Loc.T(L.Velvet.PronounsLabel), "##ed_pronouns", ref editPronouns, 40, false);
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.VenusMars, Loc.T(L.Velvet.CardGender));
            Gap(6f);
            DrawGenderPicker(ref editGender);
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.Compass, Loc.T(L.Velvet.CardIntent));
            Gap(6f);
            DrawIntentEditor();
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.Heart, Loc.T(L.Velvet.CardRole));
            Gap(6f);
            DrawCategoryPicker(VelvetSuggestions.DynamicCategories, editRole);
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.HandHoldingHeart, Loc.T(L.Velvet.CardRelationship));
            Gap(6f);
            DrawRelationshipEditor();
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.Hashtag, Loc.T(L.Velvet.CardTags));
            Gap(6f);
            DrawCategoryPicker(VelvetSuggestions.TagCategories, editTags);
            Gap(16f);

            VSectionHeader.Card(FontAwesomeIcon.ShieldAlt, Loc.T(L.Velvet.CardLimits));
            Gap(6f);
            DrawTokenEditor(editLimits, VelvetSuggestions.Limits, VelvetTheme.Gold);
            Gap(16f);

            Gap(40f);
        }
    }

    private void DrawEditAvatar()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var block = Reserve(160f);
        var drawList = ImGui.GetWindowDrawList();
        var radius = 46f * scale;
        var center = new Vector2(block.Center.X, block.Min.Y + 8f * scale + radius);
        var me = store.Me;
        VAvatar.Draw(drawList, center, radius, theme, DisplayNameOf(editDisplayName, editHandle),
            me?.World ?? string.Empty, me?.AvatarUrl, images, lodestone, -1, VelvetTheme.Rose);

        var badgeCenter = new Vector2(center.X + radius * 0.70f, center.Y + radius * 0.70f);
        drawList.AddCircleFilled(badgeCenter, 14f * scale, VelvetTheme.GroundBottom.Packed(), 24);
        drawList.AddCircleFilled(badgeCenter, 12f * scale, VelvetTheme.Rose.Packed(), 24);
        AppSkin.Icon(badgeCenter, FontAwesomeIcon.Camera.ToIconString(), VelvetTheme.OnAccent, 0.58f);

        var avatarMin = new Vector2(center.X - radius, center.Y - radius);
        var avatarMax = new Vector2(center.X + radius, center.Y + radius);
        var avatarHovered = UiInteract.Hover(avatarMin, avatarMax);
        if (avatarHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pillWidth = 150f * scale;
        var pillTop = center.Y + radius + 16f * scale;
        var changeRect = new Rect(new Vector2(block.Center.X - pillWidth * 0.5f, pillTop),
            new Vector2(block.Center.X + pillWidth * 0.5f, pillTop + 34f * scale));
        var pillClicked = ui.GhostButton(changeRect, Loc.T(L.Velvet.ChangePhoto));
        if (pillClicked || (avatarHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left)))
        {
            avatar.Open();
            avatarEditing = true;
        }
    }

    private void DrawIntentEditor()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var models = new VChipModel[VelvetIntent.All.Length];
        for (var index = 0; index < models.Length; index++)
        {
            var def = VelvetIntent.All[index];
            var selected = VelvetIntent.Has(editIntent, def.Flag);
            models[index] = new VChipModel(Loc.T(def.Label), selected ? VChipStyle.Solid : VChipStyle.Ghost,
                selected ? def.Hue : VelvetTheme.Moonlight, def.Icon);
        }

        var clicked = VChipFlow.Draw(models, width, scale);
        if (clicked >= 0)
        {
            editIntent = VelvetIntent.Toggle(editIntent, VelvetIntent.All[clicked].Flag);
        }
    }

    private void DrawGenderPicker(ref int gender)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var options = VelvetGender.All;
        var models = new VChipModel[options.Length];
        for (var index = 0; index < options.Length; index++)
        {
            var value = options[index];
            var selected = gender == value;
            models[index] = new VChipModel(VelvetGender.Label(value), selected ? VChipStyle.Solid : VChipStyle.Ghost,
                selected ? VelvetTheme.Rose : VelvetTheme.Moonlight);
        }

        var clicked = VChipFlow.Draw(models, width, scale);
        if (clicked >= 0)
        {
            var value = options[clicked];
            gender = gender == value ? VelvetGender.None : value;
        }
    }

    private void DrawRelationshipEditor()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var options = VelvetRelationship.All;
        var models = new VChipModel[options.Length];
        for (var index = 0; index < options.Length; index++)
        {
            var value = options[index];
            var selected = editRelationship == value;
            models[index] = new VChipModel(VelvetRelationship.Label(value), selected ? VChipStyle.Solid : VChipStyle.Ghost,
                selected ? VelvetTheme.Rose : VelvetTheme.Moonlight);
        }

        var clicked = VChipFlow.Draw(models, width, scale);
        if (clicked >= 0)
        {
            editRelationship = options[clicked];
        }
    }

    private void SaveProfile()
    {
        if (editBusy)
        {
            return;
        }

        editBusy = true;
        var me = store.Me;
        var identityChanged = me is not null &&
            (editDisplayName.Trim() != me.DisplayName || editHandle.Trim() != me.Handle);
        var dynamic = VelvetTags.Join(editRole.ToArray());
        var request = new UpdateVelvetProfileRequest(editIntro.Trim(), editPronouns.Trim(), dynamic, editTags.ToArray(),
            editLimits.ToArray(), VelvetIntent.Sanitize(editIntent), editRelationship, null,
            Gender: VelvetGender.Sanitize(editGender));
        if (identityChanged)
        {
            store.UpdateIdentity(editDisplayName.Trim(), editHandle.Trim(),
                _ => store.UpdateProfile(request, _ => editBusy = false));
        }
        else
        {
            store.UpdateProfile(request, _ => editBusy = false);
        }
    }
}
