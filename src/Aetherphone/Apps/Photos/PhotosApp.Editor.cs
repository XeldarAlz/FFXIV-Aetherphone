using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp
{
    private const float EditorTopBarHeight = 44f;
    private const float EditorRowCenter = 22f;
    private const float EditorEdgeInset = 12f;
    private const float EditorNoticeOffset = 44f;
    private const float EditorResetLift = 18f;
    private const float EditorTopScrimExtra = 52f;
    private const float EditorSavingRadius = 13f;
    private const float DisabledSaveAlpha = 0.4f;

    private readonly PhotoEditSession editSession = new();

    private void OpenEditor(string path)
    {
        editSession.Open(path);
        router.Push(PhotoView.Editor());
    }

    private void CloseEditor()
    {
        editSession.Close();
        router.Pop();
    }

    private void DrawEditor(Rect screen)
    {
        var scale = UiScale.Current;
        if (!editSession.IsOpen)
        {
            router.Pop(false);
            return;
        }

        var safe = ContentWithin(screen);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(ViewerBackdrop));
        var panelTop = safe.Max.Y - (PhotoEditPanel.Height * scale);
        var panel = new Rect(new Vector2(screen.Min.X, panelTop), screen.Max);
        var stage = new Rect(new Vector2(screen.Min.X, safe.Min.Y + (EditorTopBarHeight * scale)),
            new Vector2(screen.Max.X, panelTop));
        PhotoEditPanel.DrawStage(editSession, stage, ui, scale, ImGui.GetTime());
        PhotoEditPanel.DrawTools(editSession, panel, safe.Max.Y, ui, scale);
        DrawEditorTopBar(screen, safe, scale);
        if (editSession.IsDirty && !editSession.Saving)
        {
            var resetCenter = new Vector2(screen.Center.X, panelTop - (EditorResetLift * scale));
            if (TextButton.Draw(resetCenter, Loc.T(L.Photos.Reset), WhiteMuted, scale))
            {
                editSession.Reset();
            }
        }

        if (editSession.Saving)
        {
            Material.Veil(drawList, stage.Min, stage.Max, 0.45f, 0f);
            LoadingPulse.Draw(stage.Center, EditorSavingRadius * scale, ui.Accent, WhiteMuted,
                Loc.T(L.Photos.Save));
        }
    }

    private void DrawEditorTopBar(Rect screen, Rect safe, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        PhotosChrome.TopScrim(drawList, screen.Min, screen.Max, (frameTheme.TopZoneHeight + EditorTopScrimExtra) * scale);
        var rowCenterY = safe.Min.Y + (EditorRowCenter * scale);
        Typography.DrawCentered(drawList, new Vector2(screen.Center.X, rowCenterY), Loc.T(L.Photos.Edit), White,
            TextStyles.Headline);

        var cancelLabel = Loc.T(L.Common.Cancel);
        var cancelWidth = TextButton.Width(cancelLabel, scale);
        var cancelCenter = new Vector2(safe.Min.X + (EditorEdgeInset * scale) + (cancelWidth * 0.5f), rowCenterY);
        if (TextButton.Draw(cancelCenter, cancelLabel, White, scale) && !editSession.Saving)
        {
            CloseEditor();
            return;
        }

        var canSave = editSession.Preview.Ready && editSession.IsDirty && !editSession.Saving;
        var saveLabel = Loc.T(L.Photos.Save);
        var saveWidth = TextButton.Width(saveLabel, scale);
        var saveCenter = new Vector2(safe.Max.X - (EditorEdgeInset * scale) - (saveWidth * 0.5f), rowCenterY);
        var saveColor = canSave ? ui.Accent : Palette.WithAlpha(White, DisabledSaveAlpha);
        if (TextButton.Draw(saveCenter, saveLabel, saveColor, scale) && canSave)
        {
            SaveEdit();
        }

        if (editSession.Notice.Length > 0)
        {
            Typography.DrawCentered(drawList, new Vector2(screen.Center.X, safe.Min.Y + (EditorNoticeOffset * scale)),
                editSession.Notice, frameTheme.Danger, TextStyles.Footnote);
        }
    }

    private void SaveEdit()
    {
        editSession.Saving = true;
        editSession.Notice = string.Empty;
        _ = SaveEditAsync(editSession.Snapshot());
    }

    private async Task SaveEditAsync(PhotoSaveRequest request)
    {
        string? saved = null;
        try
        {
            var rendered = await Task.Run(() => PhotoEditSession.Render(request)).ConfigureAwait(false);
            saved = await Task.Run(() => library.SaveEdited(rendered.Pixels, rendered.Width, rendered.Height))
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Photos] saving the edit of {Path.GetFileName(request.Path)} failed");
        }

        await Plugin.Framework.RunOnFrameworkThread(() => FinishSave(saved)).ConfigureAwait(false);
    }

    private void FinishSave(string? saved)
    {
        editSession.Saving = false;
        if (saved is null)
        {
            editSession.Notice = Loc.T(L.Photos.EditFailed);
            return;
        }

        Refresh();
        var insertAt = Math.Clamp(viewerIndex, 0, viewerPaths.Length);
        var expanded = new string[viewerPaths.Length + 1];
        for (var index = 0; index < insertAt; index++)
        {
            expanded[index] = viewerPaths[index];
        }

        expanded[insertAt] = saved;
        for (var index = insertAt; index < viewerPaths.Length; index++)
        {
            expanded[index + 1] = viewerPaths[index];
        }

        viewerPaths = expanded;
        viewerIndex = insertAt;
        zoomView.Reset();
        CloseEditor();
    }
}
