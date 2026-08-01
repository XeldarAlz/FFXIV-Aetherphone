using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Jobs;
using Aetherphone.Core.Localization;
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
    private const float CategoryDragHandleRadius = 9f;

    // Matches AetherStreamQueue's own reorder drag threshold - the phone-appears-to-move bug
    // turned out to be DragScrollHost racing the handle for the same press (fixed via
    // surface.CancelDrag() in JobsApp.cs), not a timing race with the window lock, so there's no
    // need for a long-press dead zone here after all.
    private const float DragThreshold = 7f;

    private static readonly List<JobsCategory> NoCategories = new();

    private bool categoryEditorOpen;
    private int categoryEditorIndex = -1;
    private int categoryEditorGearsetId = -1;
    private int categoryEditorOpenedFrame;
    private string categoryEditorName = string.Empty;
    private bool focusCategoryField;

    // Rebuilt every frame from the on-screen custom section headers (see JobsApp.cs's main draw
    // loop) - used both to place each header's drag handle and, once a drag is active, to find
    // the nearest header to the cursor as the drop target. Header heights are fixed but section
    // card heights vary with gearset count, so target detection compares against real header
    // positions rather than assuming a uniform row height (contrast AetherStreamQueue.Reorder's
    // drag, where every row is the same height).
    private readonly List<(int CategoryIndex, Rect Rect)> categoryHeaderRects = new();
    private int categoryDragIndex = -1;
    private Vector2 categoryDragPressPos;
    private bool categoryDragActive;

    // Reordering the jobs/gearsets *within* one custom category - separate from the header drag
    // above, which reorders the categories themselves. Rows inside a section are uniform height
    // (RowHeight), so this can use AetherStreamQueue's simpler displaced-row drag math instead of
    // the nearest-header search the variable-height category headers need.
    private int jobDragCategoryIndex = -1;
    private int jobDragIndex = -1;
    private Vector2 jobDragStart;
    private float jobDragY;
    private bool jobDragActive;

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

        var categories = CurrentCategories();
        var items = new DropdownMenu.Item[categories.Count + 1];
        for (var index = 0; index < categories.Count; index++)
        {
            items[index] = new DropdownMenu.Item(categories[index].Name, CanEdit: true, CanDelete: true);
        }

        items[categories.Count] = new DropdownMenu.Item(Loc.T(L.Jobs.NewCategory),
            Glyph: FontAwesomeIcon.FolderPlus.ToIconString());

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

    // Grip handle on a custom section's header - press-and-hold past the threshold starts a
    // drag, mirroring AetherStreamQueue's own reorder gesture. Also registers this header's rect
    // for this frame's drop-target search, so callers must invoke this for every custom section
    // before UpdateCategoryDrag runs.
    //
    // A real InvisibleButton, not just UiInteract.Hover's raw mouse-position check: Dear ImGui
    // starts its own native window-drag whenever a press lands on the window background with no
    // item active, regardless of LockPosition (that only adds NoMove). Without an actual active
    // item here, pressing the handle on an unlocked phone would drag the whole phone instead of
    // starting a reorder.
    private void DrawCategoryDragHandle(Rect headerRect, int categoryIndex, float scale)
    {
        categoryHeaderRects.Add((categoryIndex, headerRect));

        var drawList = ImGui.GetWindowDrawList();
        var radius = CategoryDragHandleRadius * scale;
        var center = new Vector2(headerRect.Max.X - radius - 2f * scale, headerRect.Center.Y);
        ImGui.SetCursorScreenPos(center - new Vector2(radius));
        ImGui.InvisibleButton($"##categoryDragHandle{categoryIndex}", new Vector2(radius * 2f));
        var hovered = ImGui.IsItemHovered();
        var activated = ImGui.IsItemActivated();
        var dragging = categoryDragIndex == categoryIndex;
        var tint = dragging || hovered
            ? ui.Accent
            : Palette.WithAlpha(ui.MutedInk, ui.MutedInk.W * 0.6f);
        AppSkin.Icon(drawList, center, FontAwesomeIcon.GripLinesVertical.ToIconString(), tint, 0.6f);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (categoryDragIndex < 0 && activated)
        {
            categoryDragIndex = categoryIndex;
            categoryDragPressPos = ImGui.GetMousePos();
            categoryDragActive = false;
        }
    }

    // Drives the drag state machine once per frame, after every custom header has registered
    // itself this frame via DrawCategoryDragHandle. Deliberately runs outside the section loop
    // (rather than inline per-row) since the drop-target search needs every header's rect, not
    // just the ones drawn so far this frame.
    private void UpdateCategoryDrag(float scale)
    {
        if (categoryDragIndex < 0)
        {
            return;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (categoryDragActive)
            {
                var targetIndex = ClosestCategoryHeader(ImGui.GetMousePos().Y);
                if (targetIndex >= 0)
                {
                    ReorderCategory(categoryDragIndex, targetIndex);
                }
            }

            categoryDragIndex = -1;
            categoryDragActive = false;
            return;
        }

        if (!categoryDragActive && Vector2.Distance(ImGui.GetMousePos(), categoryDragPressPos) >
            DragThreshold * scale)
        {
            categoryDragActive = true;
        }

        if (!categoryDragActive)
        {
            return;
        }

        var hoverIndex = ClosestCategoryHeader(ImGui.GetMousePos().Y);
        if (hoverIndex >= 0)
        {
            DrawCategoryDropHighlight(hoverIndex, scale);
        }
    }

    private int ClosestCategoryHeader(float mouseY)
    {
        var best = -1;
        var bestDistance = float.MaxValue;
        for (var index = 0; index < categoryHeaderRects.Count; index++)
        {
            var distance = MathF.Abs(categoryHeaderRects[index].Rect.Center.Y - mouseY);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = categoryHeaderRects[index].CategoryIndex;
            }
        }

        return best;
    }

    private void DrawCategoryDropHighlight(int categoryIndex, float scale)
    {
        for (var index = 0; index < categoryHeaderRects.Count; index++)
        {
            if (categoryHeaderRects[index].CategoryIndex != categoryIndex)
            {
                continue;
            }

            var rect = categoryHeaderRects[index].Rect;
            var drawList = ImGui.GetForegroundDrawList();
            drawList.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.16f)),
                6f * scale);
            return;
        }
    }

    // A true move (remove then re-insert), not a plain adjacent swap - a single drag gesture can
    // cross several other categories at once, unlike the old up/down-arrow version this replaced.
    private void ReorderCategory(int fromIndex, int toIndex)
    {
        var categories = CurrentCategories();
        if (fromIndex < 0 || fromIndex >= categories.Count || toIndex < 0 || toIndex >= categories.Count ||
            fromIndex == toIndex)
        {
            return;
        }

        var item = categories[fromIndex];
        categories.RemoveAt(fromIndex);
        categories.Insert(toIndex, item);
        configuration.Save();
        Rebuild();
    }

    // Grip handle on a job row inside a custom category's card - same long-press-and-hold-still
    // start as DrawCategoryDragHandle, but only one row (identified by categoryIndex+rowIndex) can
    // claim the drag.
    //
    // A real InvisibleButton for the same reason as DrawCategoryDragHandle - without an active
    // ImGui item under the press, Dear ImGui's own window-drag takes over instead, dragging the
    // whole phone rather than starting a reorder. Returns whether the handle is hovered, so
    // DrawJobRow can exclude that area from the row's own hover/equip-click handling.
    private bool DrawJobDragHandle(ImDrawListPtr drawList, Vector2 center, float radius, int categoryIndex,
        int rowIndex, float scale)
    {
        ImGui.SetCursorScreenPos(center - new Vector2(radius));
        ImGui.InvisibleButton($"##jobDragHandle{categoryIndex}_{rowIndex}", new Vector2(radius * 2f));
        var hovered = ImGui.IsItemHovered();
        var activated = ImGui.IsItemActivated();

        var dragging = jobDragCategoryIndex == categoryIndex && jobDragIndex == rowIndex;
        var tint = dragging || hovered ? ui.Accent : Palette.WithAlpha(ui.MutedInk, ui.MutedInk.W * 0.6f);
        AppSkin.Icon(drawList, center, FontAwesomeIcon.GripLines.ToIconString(), tint, 0.55f);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (jobDragIndex < 0 && activated)
        {
            jobDragCategoryIndex = categoryIndex;
            jobDragIndex = rowIndex;
            jobDragStart = ImGui.GetMousePos();
            jobDragY = 0f;
            jobDragActive = false;
        }

        return hovered;
    }

    // Called once per custom section's card, before its rows are drawn - a no-op unless this is
    // the specific category currently being dragged in.
    private void UpdateJobDrag(int categoryIndex, JobEntry[] entries, float rowHeight, float scale)
    {
        if (jobDragCategoryIndex != categoryIndex || jobDragIndex < 0)
        {
            return;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (jobDragActive && jobDragIndex < entries.Length)
            {
                var targetIndex = Math.Clamp(jobDragIndex + (int)MathF.Round(jobDragY / rowHeight), 0,
                    entries.Length - 1);
                if (targetIndex != jobDragIndex)
                {
                    ReorderGearsetInCategory(categoryIndex, entries[jobDragIndex].GearsetId,
                        entries[targetIndex].GearsetId);
                }
            }

            jobDragCategoryIndex = -1;
            jobDragIndex = -1;
            jobDragActive = false;
            return;
        }

        if (!jobDragActive && Vector2.Distance(ImGui.GetMousePos(), jobDragStart) > DragThreshold * scale)
        {
            jobDragActive = true;
        }

        if (jobDragActive)
        {
            jobDragY = ImGui.GetMousePos().Y - jobDragStart.Y;
        }
    }

    // Moves fromGearsetId to sit where toGearsetId currently is, within one category's own
    // GearsetIds list - matched by id rather than raw index so a stale id (a gearset removed from
    // the game but not yet cleaned out of GearsetIds) can't desync the drag from the display.
    private void ReorderGearsetInCategory(int categoryIndex, int fromGearsetId, int toGearsetId)
    {
        var categories = CurrentCategories();
        if (categoryIndex < 0 || categoryIndex >= categories.Count)
        {
            return;
        }

        var gearsetIds = categories[categoryIndex].GearsetIds;
        var fromIndex = gearsetIds.IndexOf(fromGearsetId);
        var toIndex = gearsetIds.IndexOf(toGearsetId);
        if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex)
        {
            return;
        }

        gearsetIds.RemoveAt(fromIndex);
        gearsetIds.Insert(toIndex, fromGearsetId);
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

        var removeIndex = assignedIndex >= 0 ? categories.Count : -1;
        var newIndex = categories.Count + (assignedIndex >= 0 ? 1 : 0);
        var items = new DropdownMenu.Item[newIndex + 1];
        for (var index = 0; index < categories.Count; index++)
        {
            items[index] = new DropdownMenu.Item(categories[index].Name, Selected: index == assignedIndex);
        }

        if (removeIndex >= 0)
        {
            items[removeIndex] = new DropdownMenu.Item(Loc.T(L.Jobs.RemoveFromCategory),
                Glyph: FontAwesomeIcon.FolderMinus.ToIconString());
        }

        items[newIndex] = new DropdownMenu.Item(Loc.T(L.Jobs.NewCategory),
            Glyph: FontAwesomeIcon.FolderPlus.ToIconString());

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

        if (picked == removeIndex)
        {
            RemoveGearsetFromCategory(menuGearsetId);
            return;
        }

        AssignGearsetToCategory(menuGearsetId, picked);
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
