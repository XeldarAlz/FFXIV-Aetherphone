using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal enum VelvetEditSection
{
    Race,
    Gender,
    Sexuality,
    Languages,
    Intent,
    Role,
    Kinks,
    Limits,
    Relationship,
    Tags,
}

internal sealed partial class VelvetShell
{
    private const float EditGroupGap = 10f;
    private const float EditGroupHeaderHeight = 28f;
    private const float EditHelpGap = 8f;
    private const int EditIntroMaxLength = 400;

    private static readonly VelvetEditSection[] EditSections =
    {
        VelvetEditSection.Race,
        VelvetEditSection.Gender,
        VelvetEditSection.Sexuality,
        VelvetEditSection.Languages,
        VelvetEditSection.Intent,
        VelvetEditSection.Role,
        VelvetEditSection.Kinks,
        VelvetEditSection.Limits,
        VelvetEditSection.Relationship,
        VelvetEditSection.Tags,
    };

    private string editDisplayName = string.Empty;
    private string editHandle = string.Empty;
    private string editIntro = string.Empty;
    private string editPronouns = string.Empty;
    private int editGender;
    private int editSexuality;
    private int editLanguages;
    private int editIntent;
    private int editRelationship;
    private int editRace;
    private readonly List<string> editRole = new();
    private readonly List<string> editKinks = new();
    private readonly List<string> editTags = new();
    private readonly List<string> editLimits = new();
    private readonly Spring[] editReveal = new Spring[EditSections.Length];
    private readonly string[] editSummaries = new string[EditSections.Length];
    private readonly List<string> editSummaryLabels = new();
    private int expandedEditSections;
    private bool editSummariesDirty;
    private LanguageInfo? editSummaryLanguage;
    private int editSummaryRaceId;
    private string editRaceDetected = string.Empty;
    private volatile bool editBusy;
    private volatile bool editSaveSucceeded;
    private volatile bool editSaveFailed;
    private bool avatarEditing;
    private bool photoEditing;

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
        editSexuality = VelvetSexuality.Sanitize(me.Sexuality);
        editLanguages = VelvetLanguages.Sanitize(me.Languages);
        editIntent = VelvetIntent.Sanitize(me.LookingFor);
        editRelationship = me.RelationshipStatus;
        editRace = me.RaceOverride;
        editRole.Clear();
        editRole.AddRange(VelvetTags.Parse(me.Dynamic));
        editKinks.Clear();
        editKinks.AddRange(me.Kinks ?? Array.Empty<string>());
        editTags.Clear();
        editTags.AddRange(me.Tags);
        editLimits.Clear();
        editLimits.AddRange(me.Limits);
        expandedEditSections = 0;
        for (var index = 0; index < editReveal.Length; index++)
        {
            editReveal[index].SnapTo(0f);
        }

        editSummariesDirty = true;
        editSaveSucceeded = false;
        editSaveFailed = false;
        avatarEditing = false;
        photoEditing = false;
    }

    private void DrawEditProfile(Rect area)
    {
        var scale = UiScale.Current;
        if (avatarEditing)
        {
            var context = new PhoneContext(area, theme, navigation);
            if (avatar.Draw(area, context, ui.Accent))
            {
                avatarEditing = false;
            }

            return;
        }

        if (photoEditing)
        {
            var context = new PhoneContext(area, theme, navigation);
            if (cardPhotos.Draw(area, context, ui.Accent))
            {
                photoEditing = false;
            }

            return;
        }

        if (editSaveSucceeded)
        {
            editSaveSucceeded = false;
            router.Pop(false);
            return;
        }

        if (VHeader.Push(area, Loc.T(L.Velvet.EditProfile)))
        {
            if (!HasUnsavedEdits())
            {
                router.Pop();
                return;
            }

            confirm.Ask(new ConfirmRequest
            {
                Message = Loc.T(L.Velvet.DiscardEdits),
                ConfirmLabel = Loc.T(L.Velvet.DiscardEditsConfirm),
                CancelLabel = Loc.T(L.Velvet.KeepEditing),
                Sheet = true,
                Confirm = () => router.Pop(),
            });
            return;
        }

        if (ui.HeaderAction(area, editBusy ? Loc.T(L.Velvet.Saving) : Loc.T(L.Velvet.Save), !editBusy))
        {
            SaveProfile();
        }

        var side = Metrics.Space.Sm * scale;
        var body = new Rect(new Vector2(area.Min.X + side, area.Min.Y + VHeader.Height * scale),
            new Vector2(area.Max.X - side, area.Max.Y));
        using (AppSurface.Begin(body))
        {
            if (editSaveFailed)
            {
                Gap(10f);
                WrapText(Loc.T(L.Velvet.SaveFailed), VelvetTheme.Danger, TextStyles.Callout);
                Gap(4f);
            }

            Gap(8f);
            DrawEditAvatar();
            Gap(10f);
            DrawEditPhotos();
            Gap(14f);

            VSectionHeader.Card(PhoneIcons.User, Loc.T(L.Velvet.CardIdentity));
            Gap(4f);
            ui.Field(Loc.T(L.Velvet.DisplayNameLabel), "##ed_name", ref editDisplayName, 40, false);
            ui.Field(Loc.T(L.Velvet.HandleLabel), "##ed_handle", ref editHandle, 15, false);
            Gap(16f);

            VSectionHeader.Card(PhoneIcons.Feather, Loc.T(L.Velvet.CardAbout));
            Gap(4f);
            ui.Field(Loc.T(L.Velvet.IntroduceYourself), "##ed_intro", ref editIntro, EditIntroMaxLength, true);
            ui.Field(Loc.T(L.Velvet.PronounsLabel), "##ed_pronouns", ref editPronouns, 40, false);
            Gap(16f);

            VSectionHeader.Overline(Loc.T(L.Velvet.EditDetailsHeader));
            SyncEditSummaries();
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
            {
                for (var index = 0; index < EditSections.Length; index++)
                {
                    DrawEditSection(EditSections[index], index);
                }
            }

            Gap(40f);
        }
    }

    private void DrawEditSection(VelvetEditSection section, int slot)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var open = (expandedEditSections & (1 << slot)) != 0;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var reveal = editReveal[slot].Step(open ? 1f : 0f, VDisclosure.RevealSmoothTime, delta);
        Gap(VCard.Gap);
        var header = Reserve(VDisclosure.HeaderHeight);
        var inset = VDisclosure.PanelPadX * scale;
        var innerWidth = MathF.Max(1f, header.Width - inset * 2f);
        var visible = reveal > 0.001f
            ? (EditSectionContentHeight(section, innerWidth, scale) + VDisclosure.PanelPadY * 2f * scale) * reveal
            : 0f;
        if (VDisclosure.Card(drawList, header, visible, EditSectionGlyph(section), EditSectionTone(section),
                Loc.T(EditSectionTitle(section)), editSummaries[slot], reveal, scale))
        {
            expandedEditSections ^= 1 << slot;
        }

        if (visible <= 0f)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        ImGui.PushClipRect(new Vector2(header.Min.X, origin.Y), new Vector2(header.Max.X, origin.Y + visible), true);
        ImGui.SetCursorScreenPos(new Vector2(header.Min.X + inset, origin.Y + VDisclosure.PanelPadY * scale));
        DrawEditSectionContent(section, header.Min.X + inset, innerWidth, scale);
        ImGui.PopClipRect();
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(header.Width, visible));
    }

    private static Vector4 EditSectionTone(VelvetEditSection section) =>
        section switch
        {
            VelvetEditSection.Race => VelvetTheme.Moonlight,
            VelvetEditSection.Languages => VelvetTheme.RegionAccent,
            VelvetEditSection.Role => RoleTone,
            VelvetEditSection.Kinks => KinkTone,
            VelvetEditSection.Limits => VelvetTheme.Gold,
            _ => VelvetTheme.Rose,
        };

    private static LocString EditSectionTitle(VelvetEditSection section) =>
        section switch
        {
            VelvetEditSection.Race => L.Velvet.CardRace,
            VelvetEditSection.Gender => L.Velvet.CardGender,
            VelvetEditSection.Sexuality => L.Velvet.CardSexuality,
            VelvetEditSection.Languages => L.Velvet.CardLanguages,
            VelvetEditSection.Intent => L.Velvet.CardIntent,
            VelvetEditSection.Role => L.Velvet.CardRole,
            VelvetEditSection.Kinks => L.Velvet.CardKinks,
            VelvetEditSection.Limits => L.Velvet.CardLimits,
            VelvetEditSection.Relationship => L.Velvet.CardRelationship,
            _ => L.Velvet.CardTags,
        };

    private static string EditSectionGlyph(VelvetEditSection section) =>
        section switch
        {
            VelvetEditSection.Race => PhoneIcons.Sparkles,
            VelvetEditSection.Gender => PhoneIcons.Gender,
            VelvetEditSection.Sexuality => PhoneIcons.Rainbow,
            VelvetEditSection.Languages => PhoneIcons.Language,
            VelvetEditSection.Intent => PhoneIcons.Compass,
            VelvetEditSection.Role => PhoneIcons.Heart,
            VelvetEditSection.Kinks => PhoneIcons.Flame,
            VelvetEditSection.Limits => PhoneIcons.Shield,
            VelvetEditSection.Relationship => PhoneIcons.HeartHandshake,
            _ => PhoneIcons.Hash,
        };

    private float EditSectionContentHeight(VelvetEditSection section, float width, float scale)
    {
        switch (section)
        {
            case VelvetEditSection.Race:
                return EditRaceContentHeight(width, scale);
            case VelvetEditSection.Tags:
                return EditTagCategoriesHeight(width, scale);
        }

        FillEditChips(section);
        return MeasureChipFlow(width, scale);
    }

    private void DrawEditSectionContent(VelvetEditSection section, float left, float width, float scale)
    {
        switch (section)
        {
            case VelvetEditSection.Race:
                DrawEditRace(left, width, scale);
                return;
            case VelvetEditSection.Tags:
                DrawEditTagCategories(left, width, scale);
                return;
        }

        FillEditChips(section);
        AlignEditCursor(left);
        var clicked = DrawChipFlow(width, scale);
        if (clicked >= 0)
        {
            ToggleEditOption(section, clicked);
        }
    }

    private static void AlignEditCursor(float left)
    {
        ImGui.SetCursorScreenPos(new Vector2(left, ImGui.GetCursorScreenPos().Y));
    }

    private float EditRaceContentHeight(float width, float scale)
    {
        var height = Typography.MeasureWrappedBlock(Loc.T(L.Velvet.RaceHelp), TextStyles.Footnote, width).Y
                     + EditHelpGap * scale;
        FillRaceChips();
        height += MeasureChipFlow(width, scale);
        if (editRaceDetected.Length > 0)
        {
            height += EditHelpGap * scale
                      + Typography.MeasureWrappedBlock(editRaceDetected, TextStyles.Footnote, width).Y;
        }

        return height;
    }

    private void DrawEditRace(float left, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var helpHeight = Typography.DrawWrappedLeft(new Vector2(left, origin.Y), Loc.T(L.Velvet.RaceHelp),
            VelvetTheme.MutedInk, TextStyles.Footnote, width);
        ImGui.SetCursorScreenPos(new Vector2(left, origin.Y + helpHeight + EditHelpGap * scale));
        FillRaceChips();
        var clicked = DrawChipFlow(width, scale);
        if (clicked >= 0)
        {
            editRace = clicked == 0 ? 0 : VelvetRace.All[clicked - 1];
            editSummariesDirty = true;
        }

        if (editRaceDetected.Length == 0)
        {
            return;
        }

        var detectedTop = ImGui.GetCursorScreenPos().Y + EditHelpGap * scale;
        var detectedHeight = Typography.DrawWrappedLeft(new Vector2(left, detectedTop), editRaceDetected,
            VelvetTheme.MutedInk, TextStyles.Footnote, width);
        ImGui.SetCursorScreenPos(new Vector2(left, detectedTop));
        ImGui.Dummy(new Vector2(width, detectedHeight));
    }

    private void FillRaceChips()
    {
        chipModels.Clear();
        AddOptionChip(Loc.T(L.Velvet.RaceMatchCharacter), editRace == 0, VelvetTheme.Rose, null);
        for (var index = 0; index < VelvetRace.All.Length; index++)
        {
            var raceId = VelvetRace.All[index];
            AddOptionChip(VelvetRace.Label(gameData, raceId), editRace == raceId, VelvetTheme.Rose, null);
        }
    }

    private float EditTagCategoriesHeight(float width, float scale)
    {
        var categories = VelvetSuggestions.TagCategories;
        var height = 0f;
        for (var index = 0; index < categories.Length; index++)
        {
            if (index > 0)
            {
                height += EditGroupGap * scale;
            }

            height += EditGroupHeaderHeight * scale;
            FillTokenChips(categories[index].Tags, editTags, categories[index].Hue);
            height += MeasureChipFlow(width, scale);
        }

        var union = CategoryUnion(categories);
        if (!HasUnlisted(editTags, union))
        {
            return height;
        }

        chipModels.Clear();
        AppendOrphanChips(editTags, union);
        return height + (EditGroupGap + EditGroupHeaderHeight) * scale + MeasureChipFlow(width, scale);
    }

    private void DrawEditTagCategories(float left, float width, float scale)
    {
        var categories = VelvetSuggestions.TagCategories;
        for (var index = 0; index < categories.Length; index++)
        {
            if (index > 0)
            {
                Gap(EditGroupGap);
            }

            var category = categories[index];
            DrawEditGroupHeader(Loc.T(category.Title), category.Hue, left, width);
            FillTokenChips(category.Tags, editTags, category.Hue);
            AlignEditCursor(left);
            var clicked = DrawChipFlow(width, scale);
            if (clicked >= 0)
            {
                ToggleToken(category.Tags, editTags, clicked);
            }
        }

        var union = CategoryUnion(categories);
        if (!HasUnlisted(editTags, union))
        {
            return;
        }

        Gap(EditGroupGap);
        DrawEditGroupHeader(Loc.T(L.Velvet.CatOther), VelvetTheme.Moonlight, left, width);
        chipModels.Clear();
        AppendOrphanChips(editTags, union);
        AlignEditCursor(left);
        var removed = DrawChipFlow(width, scale);
        if (removed >= 0)
        {
            RemoveOrphan(editTags, union, removed);
        }
    }

    private static void DrawEditGroupHeader(string label, Vector4 hue, float left, float width)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var height = EditGroupHeaderHeight * scale;
        Typography.Draw(ImGui.GetWindowDrawList(),
            new Vector2(left, origin.Y + height * 0.5f - Typography.LineHeight(TextStyles.FootnoteEmphasized) * 0.5f),
            Loc.Upper(label), VelvetTheme.Lerp(hue, VelvetTheme.OnAccent, 0.3f), TextStyles.FootnoteEmphasized);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void FillEditChips(VelvetEditSection section)
    {
        chipModels.Clear();
        switch (section)
        {
            case VelvetEditSection.Gender:
                for (var index = 0; index < VelvetGender.All.Length; index++)
                {
                    var value = VelvetGender.All[index];
                    AddOptionChip(VelvetGender.Label(value), VelvetGender.Has(editGender, value), VelvetTheme.Rose,
                        null);
                }

                break;
            case VelvetEditSection.Sexuality:
                for (var index = 0; index < VelvetSexuality.All.Length; index++)
                {
                    var value = VelvetSexuality.All[index];
                    AddOptionChip(VelvetSexuality.Label(value), VelvetSexuality.Has(editSexuality, value),
                        VelvetTheme.Rose, null);
                }

                break;
            case VelvetEditSection.Languages:
                for (var index = 0; index < VelvetLanguages.All.Length; index++)
                {
                    var value = VelvetLanguages.All[index];
                    AddOptionChip(VelvetLanguages.Label(value), VelvetLanguages.Has(editLanguages, value),
                        VelvetTheme.RegionAccent, null);
                }

                break;
            case VelvetEditSection.Intent:
                for (var index = 0; index < VelvetIntent.All.Length; index++)
                {
                    var intent = VelvetIntent.All[index];
                    AddOptionChip(Loc.T(intent.Label), VelvetIntent.Has(editIntent, intent.Flag), intent.Hue,
                        intent.Glyph);
                }

                break;
            case VelvetEditSection.Role:
                FillTokenChips(VelvetRoles.Tokens, editRole, RoleTone);
                AppendOrphanChips(editRole, VelvetRoles.Tokens);
                break;
            case VelvetEditSection.Kinks:
                FillTokenChips(VelvetKinks.Tokens, editKinks, KinkTone);
                AppendOrphanChips(editKinks, VelvetKinks.Tokens);
                break;
            case VelvetEditSection.Limits:
                FillTokenChips(VelvetLimits.Tokens, editLimits, VelvetTheme.Gold);
                AppendOrphanChips(editLimits, VelvetLimits.Tokens);
                break;
            case VelvetEditSection.Relationship:
                for (var index = 0; index < VelvetRelationship.All.Length; index++)
                {
                    var value = VelvetRelationship.All[index];
                    AddOptionChip(VelvetRelationship.Label(value), editRelationship == value, VelvetTheme.Rose, null);
                }

                break;
        }
    }

    private void FillTokenChips(string[] options, List<string> selected, Vector4 tone)
    {
        chipModels.Clear();
        for (var index = 0; index < options.Length; index++)
        {
            var token = options[index];
            AddOptionChip(VelvetTokenLabels.Of(token), selected.Contains(token), tone, null);
        }
    }

    private void AppendOrphanChips(List<string> selected, string[] known)
    {
        for (var index = 0; index < selected.Count; index++)
        {
            var token = selected[index];
            if (ContainsTag(known, token))
            {
                continue;
            }

            chipModels.Add(new VChipModel(VelvetTokenLabels.Of(token), VChipStyle.Solid, VelvetTheme.Moonlight, null,
                true));
        }
    }

    private void AddOptionChip(string label, bool selected, Vector4 tone, string? glyph)
    {
        chipModels.Add(selected
            ? new VChipModel(label, VChipStyle.Solid, tone, PhoneIcons.Check)
            : new VChipModel(label, VChipStyle.Ghost, VelvetTheme.Moonlight, glyph));
    }

    private void ToggleEditOption(VelvetEditSection section, int clicked)
    {
        switch (section)
        {
            case VelvetEditSection.Gender:
                editGender = VelvetGender.Toggle(editGender, VelvetGender.All[clicked]);
                break;
            case VelvetEditSection.Sexuality:
                editSexuality = VelvetSexuality.Toggle(editSexuality, VelvetSexuality.All[clicked]);
                break;
            case VelvetEditSection.Languages:
                editLanguages = VelvetLanguages.Toggle(editLanguages, VelvetLanguages.All[clicked]);
                break;
            case VelvetEditSection.Intent:
                editIntent = VelvetIntent.Toggle(editIntent, VelvetIntent.All[clicked].Flag);
                break;
            case VelvetEditSection.Role:
                ToggleToken(VelvetRoles.Tokens, editRole, clicked);
                break;
            case VelvetEditSection.Kinks:
                ToggleToken(VelvetKinks.Tokens, editKinks, clicked);
                break;
            case VelvetEditSection.Limits:
                ToggleToken(VelvetLimits.Tokens, editLimits, clicked);
                break;
            case VelvetEditSection.Relationship:
                editRelationship = VelvetRelationship.All[clicked];
                break;
        }

        editSummariesDirty = true;
    }

    private void ToggleToken(string[] options, List<string> selected, int clicked)
    {
        editSummariesDirty = true;
        if (clicked >= options.Length)
        {
            RemoveOrphan(selected, options, clicked - options.Length);
            return;
        }

        var token = options[clicked];
        if (!selected.Remove(token))
        {
            selected.Add(token);
        }
    }

    private void RemoveOrphan(List<string> selected, string[] known, int orphanIndex)
    {
        editSummariesDirty = true;
        var seen = 0;
        for (var index = 0; index < selected.Count; index++)
        {
            if (ContainsTag(known, selected[index]))
            {
                continue;
            }

            if (seen == orphanIndex)
            {
                selected.RemoveAt(index);
                return;
            }

            seen++;
        }
    }

    private void SyncEditSummaries()
    {
        if (!editSummariesDirty && ReferenceEquals(editSummaryLanguage, Loc.Current) &&
            editSummaryRaceId == localRaceId)
        {
            return;
        }

        editSummariesDirty = false;
        editSummaryLanguage = Loc.Current;
        editSummaryRaceId = localRaceId;
        editRaceDetected = localRaceId > 0
            ? Loc.T(L.Velvet.RaceDetected, VelvetRace.Label(gameData, localRaceId))
            : string.Empty;
        for (var index = 0; index < EditSections.Length; index++)
        {
            editSummaries[index] = EditSectionSummary(EditSections[index]);
        }
    }

    private string EditSectionSummary(VelvetEditSection section)
    {
        editSummaryLabels.Clear();
        switch (section)
        {
            case VelvetEditSection.Race:
                return editRace > 0 ? VelvetRace.Label(gameData, editRace) : Loc.T(L.Velvet.RaceMatchCharacter);
            case VelvetEditSection.Relationship:
                return VelvetRelationship.Label(editRelationship);
            case VelvetEditSection.Gender:
                editSummaryLabels.AddRange(VelvetGender.Labels(editGender));
                break;
            case VelvetEditSection.Sexuality:
                editSummaryLabels.AddRange(VelvetSexuality.Labels(editSexuality));
                break;
            case VelvetEditSection.Languages:
                editSummaryLabels.AddRange(VelvetLanguages.Labels(editLanguages));
                break;
            case VelvetEditSection.Intent:
                for (var index = 0; index < VelvetIntent.All.Length; index++)
                {
                    if (VelvetIntent.Has(editIntent, VelvetIntent.All[index].Flag))
                    {
                        editSummaryLabels.Add(Loc.T(VelvetIntent.All[index].Label));
                    }
                }

                break;
            case VelvetEditSection.Role:
                AddTokenLabels(editRole);
                break;
            case VelvetEditSection.Kinks:
                AddTokenLabels(editKinks);
                break;
            case VelvetEditSection.Limits:
                AddTokenLabels(editLimits);
                break;
            default:
                AddTokenLabels(editTags);
                break;
        }

        return editSummaryLabels.Count switch
        {
            0 => Loc.T(L.Velvet.EditNotSet),
            1 => editSummaryLabels[0],
            2 => string.Concat(editSummaryLabels[0], ", ", editSummaryLabels[1]),
            _ => Loc.Plural(L.Velvet.EditChosenCount, editSummaryLabels.Count),
        };
    }

    private void AddTokenLabels(List<string> tokens)
    {
        for (var index = 0; index < tokens.Count; index++)
        {
            editSummaryLabels.Add(VelvetTokenLabels.Of(tokens[index]));
        }
    }

    private const int MaxCardPhotos = 6;
    private const int CardPhotoColumns = 3;
    private const float CardPhotoAspect = 0.8f;
    private const float CardPhotoGap = 8f;
    private const float CardPhotoRounding = 12f;
    private const float CardPhotoBadgeHeight = 20f;
    private const float CardPhotoBadgePad = 8f;
    private const float CardPhotoBadgeInset = 8f;
    private const float CardPhotoSpinnerRadius = 8f;
    private const float PreviewButtonHeight = 38f;
    private const int PhotoSheetMaxItems = 2;

    private readonly ActionSheet photoSheet = new();
    private readonly ActionSheet.Item[] photoSheetItems = new ActionSheet.Item[PhotoSheetMaxItems];
    private int photoSheetCount;
    private string photoSheetPhotoId = string.Empty;
    private bool photoSheetCanCover;

    private void DrawEditPhotos()
    {
        var scale = UiScale.Current;
        var photos = store.Me is { } me ? CardPhotos(me) : NoCardPhotos;
        VSectionHeader.Card(PhoneIcons.Photo, Loc.T(L.Velvet.PhotosSection));
        Gap(6f);
        var width = ScrollLayout.StableContentWidth();
        var gap = CardPhotoGap * scale;
        var cell = (width - gap * (CardPhotoColumns - 1)) / CardPhotoColumns;
        var cellHeight = cell / CardPhotoAspect;
        var rows = (MaxCardPhotos + CardPhotoColumns - 1) / CardPhotoColumns;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var busy = store.CardPhotoBusy;
        for (var slot = 0; slot < MaxCardPhotos; slot++)
        {
            var row = slot / CardPhotoColumns;
            var column = slot % CardPhotoColumns;
            var min = new Vector2(origin.X + column * (cell + gap), origin.Y + row * (cellHeight + gap));
            var max = new Vector2(min.X + cell, min.Y + cellHeight);
            if (slot < photos.Length)
            {
                if (DrawCardPhotoTile(drawList, min, max, photos[slot], slot == 0))
                {
                    OpenPhotoSheet(photos[slot].Id, slot);
                }

                continue;
            }

            var next = slot == photos.Length;
            if (DrawEmptyPhotoSlot(drawList, min, max, next, next && busy) && !busy)
            {
                cardPhotos.Open();
                photoEditing = true;
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * cellHeight + (rows - 1) * gap));
        Gap(8f);
        ui.HelpText(Loc.T(L.Velvet.PhotosHint));
        Gap(8f);
        if (ui.GhostButton(Reserve(PreviewButtonHeight), Loc.T(L.Velvet.PreviewCard)))
        {
            OpenCardPreview();
        }
    }

    private bool DrawCardPhotoTile(ImDrawListPtr drawList, Vector2 min, Vector2 max, VelvetCardPhotoDto photo,
        bool cover)
    {
        var scale = UiScale.Current;
        var rounding = CardPhotoRounding * scale;
        DrawCoverImage(drawList, min, max, photo.Url, rounding, string.Empty);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            Squircle.Stroke(drawList, min, max, rounding, VelvetTheme.RoseInk.Packed(), Metrics.Stroke.Thin * scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (cover)
        {
            var label = Loc.T(L.Velvet.CoverBadge);
            var textSize = Typography.Measure(label, TextStyles.Caption2);
            var pad = CardPhotoBadgePad * scale;
            var badgeMin = new Vector2(min.X + CardPhotoBadgeInset * scale,
                max.Y - CardPhotoBadgeInset * scale - CardPhotoBadgeHeight * scale);
            var badgeMax = new Vector2(badgeMin.X + textSize.X + pad * 2f, max.Y - CardPhotoBadgeInset * scale);
            Squircle.Fill(drawList, badgeMin, badgeMax, CardPhotoBadgeHeight * scale * 0.5f, VelvetTheme.Rose.Packed());
            Typography.Draw(drawList,
                new Vector2(badgeMin.X + pad, (badgeMin.Y + badgeMax.Y) * 0.5f - textSize.Y * 0.5f), label,
                VelvetTheme.OnAccent, TextStyles.Caption2);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private static bool DrawEmptyPhotoSlot(ImDrawListPtr drawList, Vector2 min, Vector2 max, bool next, bool busy)
    {
        var scale = UiScale.Current;
        var rounding = CardPhotoRounding * scale;
        var hovered = next && !busy && UiInteract.Hover(min, max);
        Squircle.Fill(drawList, min, max, rounding, (hovered ? VelvetTheme.CardHi : VelvetTheme.PlumWell).Packed());
        Squircle.Stroke(drawList, min, max, rounding,
            (next ? VelvetTheme.Alpha(VelvetTheme.RoseInk, 0.55f) : VelvetTheme.Hairline).Packed(),
            Metrics.Stroke.Hairline * scale);
        var center = (min + max) * 0.5f;
        if (busy)
        {
            LoadingPulse.Spinner(center, CardPhotoSpinnerRadius * scale, VelvetTheme.RoseInk);
        }
        else
        {
            PhoneIcon.Draw(drawList, center, PhoneIcons.Plus, next ? VelvetTheme.RoseInk : VelvetTheme.Faint,
                VIcon.CardAction * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private void OpenPhotoSheet(string photoId, int index)
    {
        photoSheetPhotoId = photoId;
        photoSheetCanCover = index > 0;
        photoSheetCount = 0;
        if (photoSheetCanCover)
        {
            photoSheetItems[photoSheetCount] = new ActionSheet.Item(Loc.T(L.Velvet.MakeCover));
            photoSheetCount++;
        }

        photoSheetItems[photoSheetCount] = new ActionSheet.Item(Loc.T(L.Velvet.RemovePhoto), string.Empty, true);
        photoSheetCount++;
        photoSheet.Open();
    }

    private void DrawPhotoSheet(Rect screen)
    {
        if (!photoSheet.CapturesPointer)
        {
            return;
        }

        var picked = photoSheet.Draw(screen, ActionSheetStyle.From(ui), photoSheetItems.AsSpan(0, photoSheetCount),
            Loc.T(L.Common.Cancel), false, string.Empty);
        if (picked < 0)
        {
            return;
        }

        if (photoSheetCanCover && picked == 0)
        {
            store.MakeCardPhotoCover(photoSheetPhotoId);
            return;
        }

        store.RemoveCardPhoto(photoSheetPhotoId);
    }

    private void DrawEditAvatar()
    {
        var scale = UiScale.Current;
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
        PhoneIcon.Draw(drawList, badgeCenter, PhoneIcons.Camera, VelvetTheme.OnAccent, VIcon.Small * scale);

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

    private bool HasUnsavedEdits()
    {
        if (store.Me is not { } me)
        {
            return false;
        }

        return !string.Equals(editDisplayName, me.DisplayName, StringComparison.Ordinal)
               || !string.Equals(editHandle, me.Handle, StringComparison.Ordinal)
               || !string.Equals(editIntro, me.Intro, StringComparison.Ordinal)
               || !string.Equals(editPronouns, me.Pronouns, StringComparison.Ordinal)
               || editGender != VelvetGender.Sanitize(me.Gender)
               || editSexuality != VelvetSexuality.Sanitize(me.Sexuality)
               || editLanguages != VelvetLanguages.Sanitize(me.Languages)
               || editIntent != VelvetIntent.Sanitize(me.LookingFor)
               || editRelationship != me.RelationshipStatus
               || editRace != me.RaceOverride
               || Differs(editRole, VelvetTags.Parse(me.Dynamic))
               || Differs(editKinks, me.Kinks)
               || Differs(editTags, me.Tags)
               || Differs(editLimits, me.Limits);
    }

    private static bool Differs(List<string> edited, IReadOnlyList<string>? saved)
    {
        var count = saved?.Count ?? 0;
        if (edited.Count != count)
        {
            return true;
        }

        for (var index = 0; index < edited.Count; index++)
        {
            if (!string.Equals(edited[index], saved![index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void SaveProfile()
    {
        if (editBusy)
        {
            return;
        }

        editBusy = true;
        editSaveFailed = false;
        var me = store.Me;
        var identityChanged = me is not null &&
            (editDisplayName.Trim() != me.DisplayName || editHandle.Trim() != me.Handle);
        var request = BuildEditRequest(null, null);
        if (identityChanged)
        {
            store.UpdateIdentity(editDisplayName.Trim(), editHandle.Trim(),
                identitySaved => store.UpdateProfile(request,
                    profileSaved => CompleteSave(identitySaved && profileSaved)));
        }
        else
        {
            store.UpdateProfile(request, CompleteSave);
        }
    }

    private UpdateVelvetProfileRequest BuildEditRequest(bool? discoverable, int? whoCanMessage) =>
        new(editIntro.Trim(), editPronouns.Trim(), VelvetTags.Join(editRole.ToArray()), editTags.ToArray(),
            editLimits.ToArray(), VelvetIntent.Sanitize(editIntent), editRelationship, discoverable, whoCanMessage,
            VelvetGender.Sanitize(editGender), VelvetSexuality.Sanitize(editSexuality), editKinks.ToArray(), editRace,
            VelvetLanguages.Sanitize(editLanguages));

    private void CompleteSave(bool succeeded)
    {
        if (succeeded)
        {
            editSaveSucceeded = true;
        }
        else
        {
            editSaveFailed = true;
        }

        editBusy = false;
    }
}
