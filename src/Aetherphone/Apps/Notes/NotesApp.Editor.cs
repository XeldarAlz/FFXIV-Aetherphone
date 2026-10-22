using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notes;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Notes;

internal sealed partial class NotesApp
{
    private const string EditorFieldId = "##noteBody";
    private const int NoteMaxLength = 8000;
    private const float StampTop = 4f;
    private const float StampGapBelow = 10f;
    private const float ComposeGlyphSize = 20f;
    private const float ComposeGap = 10f;
    private const float SelectionAlpha = 0.32f;
    private const float BackAnchorFraction = 0.32f;
    private const double AutosaveSeconds = 2.0;

    private readonly SoftWrapEditor noteEditor = new(SoftWrapLines.BreakOnReturn);
    private readonly NavBarButton[] editorButtons = new NavBarButton[2];
    private readonly Action closeNoteEditor;
    private PhoneNote? editingNote;
    private PhoneNote? shownNote;
    private Guid? pendingNoteId;
    private bool pendingNewNote;
    private bool noteDirty;
    private bool focusEditor;
    private double lastEditTime;
    private long stampKey = -1;
    private string stampText = string.Empty;

    private void StartNewNote()
    {
        CommitNoteBuffer();
        var note = new PhoneNote();
        configuration.Notes.Insert(0, note);
        OpenEditor(note);
        focusEditor = true;
        if (router.Current == NotesScreen.EditNote)
        {
            router.Replace(NotesScreen.EditNote);
            return;
        }

        router.Push(NotesScreen.EditNote);
    }

    private void StartEditNote(PhoneNote note)
    {
        CommitNoteBuffer();
        OpenEditor(note);
        router.Push(NotesScreen.EditNote);
    }

    private void OpenEditor(PhoneNote note)
    {
        editingNote = note;
        shownNote = note;
        noteEditor.Adopt(note.Body);
        noteDirty = false;
        focusEditor = false;
    }

    private void DrawNoteEditor(Rect area, bool live)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        var inlineHeight = NavBarMetrics.InlineHeight * scale;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + inlineHeight), area.Max);
        var frame = new NavBarFrame(area, body, scale, inlineHeight);
        var note = shownNote;
        if (note is not null)
        {
            DrawEditorBody(body, note, live && ReferenceEquals(note, editingNote), scale);
        }

        UiAnchors.Report("notes.editor.back", new Rect(area.Min,
            new Vector2(area.Min.X + area.Width * BackAnchorFraction, area.Min.Y + inlineHeight)));
        editorButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Copy), Loc.T(L.Notes.CopyNote));
        editorButtons[1] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.EllipsisH), Loc.T(L.Notes.More));
        var pressed = AppHeader.EndLargeTitle(in frame, context, "notes.editor.nav", string.Empty,
            NavBarStyle.From(ui), live ? editorButtons : ReadOnlySpan<NavBarButton>.Empty, Loc.T(L.Notes.TabNotes),
            closeNoteEditor);
        if (note is null || !live)
        {
            return;
        }

        if (pressed == 0)
        {
            CopyNote(note);
        }
        else if (pressed == 1)
        {
            OpenNoteSheet(note, true);
        }
    }

    private void DrawEditorBody(Rect body, PhoneNote note, bool editable, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var stamp = EditorStamp(note.UpdatedAt);
        var stampHeight = Typography.LineHeight(TextStyles.Footnote);
        var stampCenterY = body.Min.Y + StampTop * scale + stampHeight * 0.5f;
        Typography.DrawCentered(drawList, new Vector2(body.Center.X, stampCenterY),
            Typography.FitText(stamp, body.Width, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);

        var composeDiameter = TabBarLayout.ActionDiameter * scale;
        var composeCenter = new Vector2(body.Max.X - composeDiameter * 0.5f,
            body.Max.Y - TabBarLayout.BottomInset * scale - composeDiameter * 0.5f);
        var framePadding = ImGui.GetStyle().FramePadding;
        var fieldTop = stampCenterY + stampHeight * 0.5f + StampGapBelow * scale;
        var fieldBottom = composeCenter.Y - composeDiameter * 0.5f - ComposeGap * scale;
        var fieldMin = new Vector2(body.Min.X - framePadding.X, fieldTop);
        var fieldSize = new Vector2(body.Width + framePadding.X * 2f, MathF.Max(1f, fieldBottom - fieldTop));
        UiAnchors.Report("notes.editor", new Rect(new Vector2(body.Min.X, fieldTop),
            new Vector2(body.Max.X, fieldBottom)));

        if (editable)
        {
            DrawEditorField(note, fieldMin, fieldSize, framePadding, scale);
            DrawComposeButton(drawList, composeCenter, composeDiameter, scale);
            return;
        }

        drawList.PushClipRect(new Vector2(body.Min.X, fieldTop), new Vector2(body.Max.X, fieldBottom), true);
        Typography.DrawWrappedLeft(fieldMin + framePadding, note.Body, ui.TitleInk, TextStyles.Body, body.Width);
        drawList.PopClipRect();
    }

    private void DrawEditorField(PhoneNote note, Vector2 fieldMin, Vector2 fieldSize, Vector2 framePadding,
        float scale)
    {
        ImGui.SetCursorScreenPos(fieldMin);
        if (focusEditor && !router.IsTransitioning)
        {
            ImGui.SetKeyboardFocusHere();
            focusEditor = false;
        }

        using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.Text, ui.TitleInk))
        using (UiScope.PushColor(ImGuiCol.TextSelectedBg, Palette.WithAlpha(ui.Accent, SelectionAlpha)))
        using (UiScope.PushColor(ImGuiCol.ScrollbarBg, AppSkin.Transparent))
        using (Plugin.Fonts.Push(TextStyles.Body.Scale, TextStyles.Body.Weight))
        {
            noteEditor.Rewrap(fieldSize.X - framePadding.X * 2f - Metrics.Space.Xxs * scale);
            noteEditor.Draw(EditorFieldId, fieldSize, NoteMaxLength, 0);
        }

        if (noteEditor.Edited)
        {
            note.Body = noteEditor.Text;
            note.UpdatedAt = DateTime.Now;
            noteDirty = true;
            lastEditTime = ImGui.GetTime();
        }

        if (noteEditor.Text.Length == 0)
        {
            Typography.DrawWrappedLeft(fieldMin + framePadding, Loc.T(L.Notes.EditorPlaceholder), ui.MutedInk,
                TextStyles.Body, fieldSize.X - framePadding.X * 2f);
        }
    }

    private void DrawComposeButton(ImDrawListPtr drawList, Vector2 center, float diameter, float scale)
    {
        var radius = diameter * 0.5f;
        var hit = new Vector2(radius, radius);
        var hovered = UiInteract.Hover(center - hit, center + hit);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale("notes.editor.compose", down, PressFx.IconPressedScale);
        var drawn = hit * grow;
        Material.ThemedGlass(drawList, center - drawn, center + drawn, radius * grow, scale, theme);
        PhoneIcon.Draw(drawList, center, PhoneIcons.Edit, ui.Accent, ComposeGlyphSize * scale * grow);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(center - hit, center + hit), Loc.T(L.Notes.NewNote), HoverLabelSide.Above);
        if (UiInteract.Click(center - hit, center + hit, hovered))
        {
            StartNewNote();
        }
    }

    private string EditorStamp(DateTime updatedAt)
    {
        var key = updatedAt.Ticks / TimeSpan.TicksPerMinute ^ (long)TimeText.FormatVersion << 56;
        if (key == stampKey && stampText.Length > 0)
        {
            return stampText;
        }

        stampKey = key;
        stampText = Loc.T(L.Notes.EditedStamp, updatedAt.ToString("D", Loc.Culture), TimeText.Clock(updatedAt));
        return stampText;
    }

    private void CloseNoteEditor()
    {
        CommitNoteBuffer();
        router.Pop();
    }

    private void AutosaveNote()
    {
        if (!noteDirty || editingNote is null || ImGui.GetTime() - lastEditTime < AutosaveSeconds ||
            !noteEditor.HasContent)
        {
            return;
        }

        noteDirty = false;
        configuration.Save();
    }

    private void CommitNoteBuffer()
    {
        if (editingNote is not { } note)
        {
            return;
        }

        editingNote = null;
        if (!noteEditor.HasContent)
        {
            configuration.Notes.Remove(note);
            configuration.Save();
        }
        else if (noteDirty)
        {
            note.Body = noteEditor.Text;
            configuration.Save();
        }

        noteDirty = false;
    }
}
