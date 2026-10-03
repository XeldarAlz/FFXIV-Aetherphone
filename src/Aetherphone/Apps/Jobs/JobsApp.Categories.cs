using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Jobs;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Jobs;

internal sealed partial class JobsApp
{
    private const float EditorWidth = 252f;
    private const float EditorTitleHeight = 18f;
    private const float EditorFieldHeight = 30f;
    private const int CategoryNameMaxLength = 24;

    private static readonly List<JobsCategory> NoCategories = new();

    private int categoryEditorIndex = -1;
    private int categoryEditorGearsetId = -1;
    private int categoryEditorOpenedFrame;
    private string categoryEditorName = string.Empty;
    private bool focusCategoryField;

    private int categoryMoveIndex = -1;
    private int categoryMoveDelta;
    private int gearsetMoveCategoryIndex = -1;
    private int gearsetMoveId = -1;
    private int gearsetMoveNeighbourId = -1;

    private List<JobsCategory> CurrentCategories()
    {
        var contentId = characterWatch.CurrentContentId;
        return contentId != 0 && configuration.JobsCategoriesByCharacter.TryGetValue(contentId, out var categories)
            ? categories
            : NoCategories;
    }

    private List<JobsCategory> CategoriesForWrite()
    {
        var contentId = characterWatch.CurrentContentId;
        if (contentId == 0)
        {
            return NoCategories;
        }

        if (!configuration.JobsCategoriesByCharacter.TryGetValue(contentId, out var categories))
        {
            categories = new List<JobsCategory>();
            configuration.JobsCategoriesByCharacter[contentId] = categories;
        }

        return categories;
    }

    private void DrawCategoriesMenu(Rect content, PhoneTheme theme)
    {
        if (!menu.IsOpenFor(CategoryMenuId))
        {
            return;
        }

        UiAnchors.Report("jobs.categories.menu", categoriesButtonRect);
        var categories = CurrentCategories();
        var items = new DropdownMenu.Item[categories.Count + 1];
        for (var index = 0; index < categories.Count; index++)
        {
            items[index] = new DropdownMenu.Item(categories[index].Name, CanEdit: true, CanDelete: true);
        }

        items[categories.Count] = new DropdownMenu.Item(Loc.T(L.Jobs.NewCategory),
            Glyph: IconGlyph.Of(FontAwesomeIcon.FolderPlus));

        var picked = menu.Draw(content, theme, items, out var rowAction);
        if (picked < 0)
        {
            return;
        }

        if (picked == categories.Count)
        {
            OpenCategoryEditor(-1, -1);
            return;
        }

        if (rowAction == DropdownMenu.RowAction.Delete)
        {
            DeleteCategory(picked);
            return;
        }

        OpenCategoryEditor(picked, -1);
    }

    // Queued rather than applied at the click: both moves call Rebuild, which swaps the snapshot
    // the draw loop is still walking.
    private void ApplyPendingReorder()
    {
        if (categoryMoveIndex >= 0)
        {
            MoveCategory(categoryMoveIndex, categoryMoveDelta);
            categoryMoveIndex = -1;
            categoryMoveDelta = 0;
        }

        if (gearsetMoveId < 0)
        {
            return;
        }

        MoveGearsetInCategory(gearsetMoveCategoryIndex, gearsetMoveId, gearsetMoveNeighbourId);
        ResetPendingGearsetMove();
    }

    private void ResetPendingReorder()
    {
        categoryMoveIndex = -1;
        categoryMoveDelta = 0;
        ResetPendingGearsetMove();
    }

    private void ResetPendingGearsetMove()
    {
        gearsetMoveCategoryIndex = -1;
        gearsetMoveId = -1;
        gearsetMoveNeighbourId = -1;
    }

    private void MoveCategory(int categoryIndex, int delta)
    {
        var categories = CategoriesForWrite();
        var targetIndex = categoryIndex + delta;
        if (categoryIndex < 0 || categoryIndex >= categories.Count || targetIndex < 0 ||
            targetIndex >= categories.Count)
        {
            return;
        }

        var moved = categories[categoryIndex];
        categories[categoryIndex] = categories[targetIndex];
        categories[targetIndex] = moved;
        configuration.Save();
        Rebuild();
    }

    // Swaps the two ids' own slots instead of stepping one index in GearsetIds: a category can hold
    // an id whose gearset no longer exists, and those are skipped when the rows are built, so list
    // neighbours and row neighbours are not the same thing.
    private void MoveGearsetInCategory(int categoryIndex, int gearsetId, int neighbourGearsetId)
    {
        var categories = CategoriesForWrite();
        if (categoryIndex < 0 || categoryIndex >= categories.Count)
        {
            return;
        }

        var gearsetIds = categories[categoryIndex].GearsetIds;
        var fromIndex = gearsetIds.IndexOf(gearsetId);
        var toIndex = gearsetIds.IndexOf(neighbourGearsetId);
        if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex)
        {
            return;
        }

        gearsetIds[fromIndex] = neighbourGearsetId;
        gearsetIds[toIndex] = gearsetId;
        configuration.Save();
        Rebuild();
    }

    private void DrawRowMenu(Rect content, PhoneTheme theme)
    {
        if (!menu.IsOpenFor(RowMenuId) || menuGearsetId < 0)
        {
            return;
        }

        var categories = CurrentCategories();
        var assignedIndex = -1;
        for (var index = 0; index < categories.Count; index++)
        {
            if (categories[index].GearsetIds.Contains(menuGearsetId))
            {
                assignedIndex = index;
                break;
            }
        }

        var earlierId = -1;
        var laterId = -1;
        if (assignedIndex >= 0 && assignedIndex < snapshot.Shelves.Length)
        {
            ShelfNeighbours(snapshot.Shelves[assignedIndex], menuGearsetId, out earlierId, out laterId);
        }

        var next = categories.Count;
        var earlierIndex = earlierId >= 0 ? next++ : -1;
        var laterIndex = laterId >= 0 ? next++ : -1;
        var removeIndex = assignedIndex >= 0 ? next++ : -1;
        var newIndex = next;
        var items = new DropdownMenu.Item[newIndex + 1];
        for (var index = 0; index < categories.Count; index++)
        {
            items[index] = new DropdownMenu.Item(categories[index].Name, Selected: index == assignedIndex);
        }

        if (earlierIndex >= 0)
        {
            items[earlierIndex] = new DropdownMenu.Item(Loc.T(L.Jobs.MoveEarlier),
                Glyph: IconGlyph.Of(FontAwesomeIcon.ArrowLeft));
        }

        if (laterIndex >= 0)
        {
            items[laterIndex] = new DropdownMenu.Item(Loc.T(L.Jobs.MoveLater),
                Glyph: IconGlyph.Of(FontAwesomeIcon.ArrowRight));
        }

        if (removeIndex >= 0)
        {
            items[removeIndex] = new DropdownMenu.Item(Loc.T(L.Jobs.RemoveFromCategory),
                Glyph: IconGlyph.Of(FontAwesomeIcon.FolderMinus));
        }

        items[newIndex] = new DropdownMenu.Item(Loc.T(L.Jobs.NewCategory),
            Glyph: IconGlyph.Of(FontAwesomeIcon.FolderPlus));

        var picked = menu.Draw(content, theme, items);
        if (picked < 0)
        {
            return;
        }

        if (picked == newIndex)
        {
            OpenCategoryEditor(-1, menuGearsetId);
            return;
        }

        if (picked == earlierIndex || picked == laterIndex)
        {
            gearsetMoveCategoryIndex = assignedIndex;
            gearsetMoveId = menuGearsetId;
            gearsetMoveNeighbourId = picked == earlierIndex ? earlierId : laterId;
            return;
        }

        if (picked == removeIndex)
        {
            RemoveGearsetFromCategory(menuGearsetId);
            return;
        }

        AssignGearsetToCategory(menuGearsetId, picked);
    }

    private void ShelfNeighbours(JobShelf shelf, int gearsetId, out int earlierId, out int laterId)
    {
        earlierId = -1;
        laterId = -1;
        var indices = shelf.GearsetIndices;
        for (var position = 0; position < indices.Length; position++)
        {
            if (snapshot.Gearsets[indices[position]].Id != gearsetId)
            {
                continue;
            }

            if (position > 0)
            {
                earlierId = snapshot.Gearsets[indices[position - 1]].Id;
            }

            if (position < indices.Length - 1)
            {
                laterId = snapshot.Gearsets[indices[position + 1]].Id;
            }

            return;
        }
    }

    private void AssignGearsetToCategory(int gearsetId, int categoryIndex)
    {
        if (characterWatch.CurrentContentId == 0)
        {
            return;
        }

        var categories = CategoriesForWrite();
        if (categoryIndex < 0 || categoryIndex >= categories.Count)
        {
            return;
        }

        RemoveGearsetFromCategories(categories, gearsetId);
        categories[categoryIndex].GearsetIds.Add(gearsetId);
        configuration.Save();
        Rebuild();
    }

    private void RemoveGearsetFromCategory(int gearsetId)
    {
        if (characterWatch.CurrentContentId == 0)
        {
            return;
        }

        RemoveGearsetFromCategories(CategoriesForWrite(), gearsetId);
        configuration.Save();
        Rebuild();
    }

    private static void RemoveGearsetFromCategories(List<JobsCategory> categories, int gearsetId)
    {
        for (var index = 0; index < categories.Count; index++)
        {
            categories[index].GearsetIds.Remove(gearsetId);
        }
    }

    private void DeleteCategory(int index)
    {
        var categories = CurrentCategories();
        if (index < 0 || index >= categories.Count)
        {
            return;
        }

        var category = categories[index];
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Jobs.DeleteCategoryConfirm, category.Name),
            ConfirmLabel = Loc.T(L.Jobs.DeleteCategory),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Danger = true,
            Confirm = () =>
            {
                CurrentCategories().Remove(category);
                configuration.Save();
                Rebuild();
            },
        });
    }

    private void OpenCategoryEditor(int categoryIndex, int gearsetId)
    {
        var categories = CurrentCategories();
        categoryEditorIndex = categoryIndex >= 0 && categoryIndex < categories.Count ? categoryIndex : -1;
        categoryEditorGearsetId = gearsetId;
        categoryEditorName = categoryEditorIndex >= 0 ? categories[categoryEditorIndex].Name : string.Empty;
        categoryEditorOpenedFrame = ImGui.GetFrameCount();
        focusCategoryField = true;
        categoryEditorOpen = true;
    }

    private void CloseCategoryEditor()
    {
        categoryEditorOpen = false;
        categoryEditorIndex = -1;
        categoryEditorGearsetId = -1;
    }

    private bool CategoryEditorClicked() =>
        categoryEditorOpenedFrame != ImGui.GetFrameCount() && ImGui.IsMouseClicked(ImGuiMouseButton.Left);

    private void DrawCategoryEditor(Rect content, float scale)
    {
        var theme = ui.Theme;
        var pad = Metrics.Space.Md * scale;
        var gap = Metrics.Space.Md * scale;
        var width = EditorWidth * scale;
        var titleHeight = EditorTitleHeight * scale;
        var fieldHeight = EditorFieldHeight * scale;
        var height = pad * 2f + titleHeight + gap + fieldHeight + gap + fieldHeight;
        var min = new Vector2(content.Center.X - width * 0.5f, content.Min.Y + 96f * scale);
        var max = min + new Vector2(width, height);

        var titleTop = min.Y + pad;
        var fieldTop = titleTop + titleHeight + gap;
        var buttonTop = fieldTop + fieldHeight + gap;
        var nameRect = new Rect(new Vector2(min.X + pad, fieldTop), new Vector2(max.X - pad, fieldTop + fieldHeight));
        var saveRect = new Rect(new Vector2(max.X - pad - PickerButtonWidth * scale, buttonTop),
            new Vector2(max.X - pad, buttonTop + fieldHeight));

        DrawCategoryFieldHost(nameRect, scale);

        var drawList = ImGui.GetForegroundDrawList();
        var screen = SceneChrome.ScreenFrom(content, theme, scale);
        Material.Veil(drawList, screen.Min, screen.Max, PickerScrim, theme.ScreenRounding * scale);
        PopoverSurface.Draw(drawList, min, max, PickerRounding * scale, theme, scale);
        var title = Loc.T(categoryEditorIndex >= 0 ? L.Jobs.RenameCategory : L.Jobs.NewCategoryTitle);
        Typography.Draw(drawList, new Vector2(min.X + pad, titleTop), title, theme.TextStrong,
            TextStyles.SubheadlineEmphasized);

        DrawPickerField(drawList, nameRect, theme, scale);
        var named = categoryEditorName.Length > 0;
        var nameText = Typography.FitText(named ? categoryEditorName : Loc.T(L.Jobs.CategoryNamePlaceholder),
            nameRect.Width - Metrics.Space.Md * 2f * scale, TextStyles.Body);
        Typography.Draw(drawList, FieldTextOrigin(nameRect, nameText, TextStyles.Body, scale), nameText,
            named ? theme.TextStrong : theme.TextMuted, TextStyles.Body);

        DrawCategorySaveButton(drawList, saveRect, theme, scale);
        if (categoryEditorOpenedFrame == ImGui.GetFrameCount())
        {
            return;
        }

        var clickedOutside = UiInteract.ClickedOutside(min, max, false);
        if (ImGui.IsKeyPressed(ImGuiKey.Escape) || clickedOutside)
        {
            CloseCategoryEditor();
            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Enter))
        {
            SaveCategoryEditor();
        }
    }

    private void DrawCategoryFieldHost(Rect nameRect, float scale)
    {
        var inset = Metrics.Space.Md * scale;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, default(Vector4))
                   .Push(ImGuiCol.FrameBgHovered, default(Vector4))
                   .Push(ImGuiCol.FrameBgActive, default(Vector4))
                   .Push(ImGuiCol.Text, default(Vector4))
                   .Push(ImGuiCol.Border, default(Vector4)))
        {
            ImGui.SetCursorScreenPos(new Vector2(nameRect.Min.X + inset, nameRect.Min.Y));
            using (ImRaii.Child("##jobsCategoryNameHost", new Vector2(nameRect.Width - inset, nameRect.Height), false,
                       HostFlags))
            {
                if (focusCategoryField)
                {
                    focusCategoryField = false;
                    ImGui.SetKeyboardFocusHere();
                }

                ImGui.SetNextItemWidth(-1f);
                ImGui.InputText("##jobsCategoryNameField", ref categoryEditorName, CategoryNameMaxLength);
            }
        }
    }

    private void DrawCategorySaveButton(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, float scale)
    {
        var enabled = categoryEditorName.Trim().Length > 0;
        var hovered = enabled && UiInteract.HoverWindowOnly(rect.Min, rect.Max);
        var fill = !enabled
            ? Palette.WithAlpha(theme.TextMuted, 0.2f)
            : hovered
                ? Palette.Mix(ui.Accent, PickerInkOnDark, 0.14f)
                : ui.Accent;
        Squircle.Fill(drawList, rect.Min, rect.Max, rect.Height * 0.5f, ImGui.GetColorU32(fill));
        var ink = enabled
            ? Palette.Luminance(fill) > 0.62f ? PickerInkOnLight : PickerInkOnDark
            : theme.TextMuted;
        Typography.DrawCentered(drawList, rect.Center, Loc.T(L.Jobs.SaveCategory), ink,
            TextStyles.SubheadlineEmphasized);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (CategoryEditorClicked())
        {
            SaveCategoryEditor();
        }
    }

    private void SaveCategoryEditor()
    {
        var name = categoryEditorName.Trim();
        if (name.Length == 0 || characterWatch.CurrentContentId == 0)
        {
            return;
        }

        var categories = CategoriesForWrite();
        if (categoryEditorIndex >= 0 && categoryEditorIndex < categories.Count)
        {
            categories[categoryEditorIndex].Name = name;
        }
        else
        {
            var category = new JobsCategory { Name = name };
            if (categoryEditorGearsetId >= 0)
            {
                RemoveGearsetFromCategories(categories, categoryEditorGearsetId);
                category.GearsetIds.Add(categoryEditorGearsetId);
            }

            categories.Add(category);
        }

        configuration.Save();
        CloseCategoryEditor();
        Rebuild();
    }
}
