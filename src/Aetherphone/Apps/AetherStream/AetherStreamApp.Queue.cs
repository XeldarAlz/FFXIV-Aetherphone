using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    // Deliberately not a reuse of HomeInteractionController - that solves a 2D grid/folder/dock
    // problem far beyond a flat list reorder. Borrows its core shape (threshold-triggered drag
    // start, tracked position, commit on release) at a scale that fits one column of rows.
    private const float QueueDragThreshold = 7f;
    private const float QueueRowHeight = 56f;

    private int queueDragIndex = -1;
    private Vector2 queueDragStart;
    private float queueDragY;
    private bool queueDragActive;

    private void DrawQueueTab(Rect body, float scale)
    {
        var margin = Metrics.Space.Lg * scale;
        var content = new Rect(new Vector2(body.Min.X + margin, body.Min.Y), new Vector2(body.Max.X - margin, body.Max.Y));

        var controlsHeight = 84f * scale;
        DrawQueueControls(new Rect(content.Min, new Vector2(content.Max.X, content.Min.Y + controlsHeight)), scale);

        var listTop = content.Min.Y + controlsHeight + 8f * scale;

        // See WatchAlongSession.PendingQueueSuggestions.
        if (watchAlong.IsHosting && watchAlong.PendingQueueSuggestions.Count > 0)
        {
            listTop = DrawQueueSuggestions(content, listTop, scale);
        }

        var listRect = new Rect(new Vector2(content.Min.X, listTop), content.Max);
        DrawQueueList(listRect, scale);
    }

    private float DrawQueueSuggestions(Rect content, float top, float scale)
    {
        Typography.Draw(new Vector2(content.Min.X, top), Loc.T(L.AetherStream.QueueSuggestionsHeader), ui.MutedInk,
            TextStyles.Caption1);
        top += 20f * scale;

        var rowHeight = 40f * scale;
        foreach (var suggestion in watchAlong.PendingQueueSuggestions)
        {
            var rowRect = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + rowHeight));
            DrawQueueSuggestionRow(rowRect, suggestion, scale);
            top = rowRect.Max.Y + 6f * scale;
        }

        return top + 6f * scale;
    }

    private void DrawQueueSuggestionRow(Rect rect, QueueSuggestion suggestion, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, rect.Min, rect.Max, 8f * scale, ImGui.GetColorU32(ui.FieldSurface));

        var dismissRect = new Rect(new Vector2(rect.Max.X - 76f * scale, rect.Min.Y + 5f * scale),
            new Vector2(rect.Max.X - 6f * scale, rect.Max.Y - 5f * scale));
        var addRect = new Rect(new Vector2(dismissRect.Min.X - 66f * scale, rect.Min.Y + 5f * scale),
            new Vector2(dismissRect.Min.X - 6f * scale, rect.Max.Y - 5f * scale));

        var textLeft = rect.Min.X + 10f * scale;
        var textRight = addRect.Min.X - 10f * scale;
        Typography.Draw(new Vector2(textLeft, rect.Center.Y - 14f * scale),
            Ellipsize(suggestion.DisplayName, textRight - textLeft), ui.TitleInk, TextStyles.Body);
        Typography.Draw(new Vector2(textLeft, rect.Center.Y + 2f * scale),
            Ellipsize(suggestion.Url, textRight - textLeft), ui.MutedInk, TextStyles.Caption1);

        if (SmallButton(dismissRect, Loc.T(L.AetherStream.QueueSuggestionDismiss), true, scale, danger: true))
        {
            watchAlong.DenyQueueSuggestion(suggestion.SuggestionId);
        }

        if (SmallButton(addRect, Loc.T(L.AetherStream.QueueSuggestionAdd), true, scale))
        {
            watchAlong.ApproveQueueSuggestion(suggestion.SuggestionId);
        }
    }

    private void DrawQueueControls(Rect rect, float scale)
    {
        var fieldHeight = 34f * scale;
        var fieldRect = new Rect(rect.Min, new Vector2(rect.Max.X, rect.Min.Y + fieldHeight));
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, fieldRect.Min, fieldRect.Max, 9f * scale, ImGui.GetColorU32(ui.FieldSurface));
        ImGui.SetCursorScreenPos(new Vector2(fieldRect.Min.X + 10f * scale,
            fieldRect.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(fieldRect.Width - 20f * scale);
        using (Dalamud.Interface.Utility.Raii.ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (Dalamud.Interface.Utility.Raii.ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputTextWithHint("##aetherstreamQueueUrl", Loc.T(L.AetherStream.UrlHint), ref urlInput, 2000,
                ImGuiInputTextFlags.None);
        }

        var buttonTop = fieldRect.Max.Y + 8f * scale;
        var buttonRow = new Rect(new Vector2(rect.Min.X, buttonTop), new Vector2(rect.Max.X, buttonTop + 32f * scale));
        var gap = 8f * scale;
        var third = (buttonRow.Width - gap * 2f) / 3f;
        var enabled = urlInput.Trim().Length > 0;

        var addRect = new Rect(buttonRow.Min, new Vector2(buttonRow.Min.X + third, buttonRow.Max.Y));
        var nextRect = new Rect(new Vector2(addRect.Max.X + gap, buttonRow.Min.Y),
            new Vector2(addRect.Max.X + gap + third, buttonRow.Max.Y));
        var clearRect = new Rect(new Vector2(nextRect.Max.X + gap, buttonRow.Min.Y), buttonRow.Max);

        if (SmallButton(addRect, Loc.T(L.AetherStream.AddToQueue), enabled, scale) && enabled)
        {
            queue.Add(MakeEntry(urlInput.Trim()));
            urlInput = string.Empty;
        }

        if (SmallButton(nextRect, Loc.T(L.AetherStream.PlayNext), enabled, scale) && enabled)
        {
            queue.PlayNext(MakeEntry(urlInput.Trim()));
            urlInput = string.Empty;
        }

        var hasEntries = queue.Entries.Count > 0 || queue.Current is not null;
        if (SmallButton(clearRect, Loc.T(L.AetherStream.ClearQueue), hasEntries, scale, danger: true) && hasEntries)
        {
            confirm.Ask(new ConfirmRequest
            {
                Message = Loc.T(L.AetherStream.ClearQueueConfirm),
                ConfirmLabel = Loc.T(L.AetherStream.Stop),
                CancelLabel = Loc.T(L.AetherStream.Keep),
                Confirm = () => queue.Clear(),
            });
        }
    }

    private static VideoQueueEntry MakeEntry(string url) => new(url, url, string.Empty, null, null);

    private bool SmallButton(Rect rect, string label, bool enabled, float scale, bool danger = false)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = enabled && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var accent = danger ? theme.Danger : ui.Accent;
        var fill = !enabled ? Palette.WithAlpha(ui.MutedInk, 0.14f) :
            hovered ? Palette.WithAlpha(accent, 0.26f) : Palette.WithAlpha(accent, 0.16f);
        Squircle.Fill(drawList, rect.Min, rect.Max, 8f * scale, ImGui.GetColorU32(fill));
        var ink = enabled ? accent : ui.MutedInk;
        Typography.DrawCentered(rect.Center, label, ink, TextStyles.Caption1.Scale, FontWeight.SemiBold);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawQueueList(Rect rect, float scale)
    {
        var entries = queue.Entries;
        if (entries.Count == 0)
        {
            Typography.DrawWrappedCentered(new Vector2(rect.Center.X, rect.Min.Y + 60f * scale),
                Loc.T(L.AetherStream.QueueEmpty), ui.MutedInk, TextStyles.Subheadline, rect.Width - 40f * scale);
            queueDragIndex = -1;
            queueDragActive = false;
            return;
        }

        var rowHeight = QueueRowHeight * scale;
        UpdateDrag(entries.Count, rowHeight, rect);

        using (AppSurface.Begin(rect))
        {
            for (var index = 0; index < entries.Count; index++)
            {
                var displayIndex = queueDragActive && index == queueDragIndex ? -1 : index;
                var origin = ImGui.GetCursorScreenPos();
                var rowMin = origin;
                var rowMax = new Vector2(origin.X + ImGui.GetContentRegionAvail().X, origin.Y + rowHeight);
                if (queueDragActive && index == queueDragIndex)
                {
                    rowMin = new Vector2(rowMin.X, rowMin.Y + queueDragY);
                    rowMax = new Vector2(rowMax.X, rowMax.Y + queueDragY);
                }

                DrawQueueRow(new Rect(rowMin, rowMax), entries[index], index, scale);
                ImGui.SetCursorScreenPos(origin);
                ImGui.Dummy(new Vector2(rowMax.X - rowMin.X, rowHeight));
            }
        }
    }

    private void UpdateDrag(int count, float rowHeight, Rect listRect)
    {
        if (!queueDragActive)
        {
            return;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var targetIndex = Math.Clamp(queueDragIndex + (int)MathF.Round(queueDragY / rowHeight), 0, count - 1);
            if (targetIndex != queueDragIndex)
            {
                queue.Reorder(queueDragIndex, targetIndex);
            }

            queueDragActive = false;
            queueDragIndex = -1;
            return;
        }

        queueDragY = ImGui.GetMousePos().Y - queueDragStart.Y;
    }

    private void DrawQueueRow(Rect row, VideoQueueEntry entry, int index, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(row.Min, row.Max);
        if (hovered || (queueDragActive && index == queueDragIndex))
        {
            Squircle.Fill(drawList, row.Min, row.Max, 8f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, queueDragActive && index == queueDragIndex ? 0.10f : 0.05f)));
        }

        var artSize = row.Height - 12f * scale;
        var artMin = new Vector2(row.Min.X + 6f * scale, row.Min.Y + 6f * scale);
        var artMax = artMin + new Vector2(artSize, artSize);
        Squircle.Fill(drawList, artMin, artMax, 6f * scale, ImGui.GetColorU32(ui.FieldSurface));
        var thumbnail = VideoThumbnailResolver.Get(remoteImages, http, entry.Url, entry.ThumbnailUrl);
        if (thumbnail is not null)
        {
            drawList.AddImageRounded(thumbnail.Handle, artMin, artMax, Vector2.Zero, Vector2.One, 0xFFFFFFFFu,
                6f * scale, ImDrawFlags.RoundCornersAll);
        }
        else
        {
            AppSkin.Icon((artMin + artMax) * 0.5f, FontAwesomeIcon.Play.ToIconString(), ui.MutedInk, 0.7f);
        }

        var textLeft = artMax.X + 10f * scale;
        var textRight = row.Max.X - 78f * scale;
        Typography.Draw(new Vector2(textLeft, row.Min.Y + 8f * scale), Ellipsize(entry.Title, textRight - textLeft),
            ui.TitleInk, TextStyles.Body);
        var secondLine = entry.Duration is { } duration
            ? $"{entry.Source}  ·  {TimeText.MinutesSeconds((int)duration.TotalSeconds)}"
            : entry.Source;
        Typography.Draw(new Vector2(textLeft, row.Min.Y + 30f * scale), Ellipsize(secondLine, textRight - textLeft),
            ui.MutedInk, TextStyles.Footnote);

        // Drag handle - press-and-hold past the threshold starts a reorder; a plain click on the
        // rest of the row plays it now.
        var handleCenter = new Vector2(row.Max.X - 52f * scale, row.Center.Y);
        AppSkin.Icon(handleCenter, FontAwesomeIcon.GripLines.ToIconString(), ui.MutedInk, 0.6f);
        var handleHit = ImGui.IsMouseHoveringRect(handleCenter - new Vector2(14f * scale), handleCenter + new Vector2(14f * scale));
        if (handleHit && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !queueDragActive)
        {
            queueDragIndex = index;
            queueDragStart = ImGui.GetMousePos();
            queueDragY = 0f;
        }

        if (queueDragIndex == index && !queueDragActive && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (Vector2.Distance(ImGui.GetMousePos(), queueDragStart) > QueueDragThreshold * scale)
            {
                queueDragActive = true;
            }
        }

        var removeCenter = new Vector2(row.Max.X - 16f * scale, row.Center.Y);
        if (ui.IconButton(removeCenter, 12f * scale, FontAwesomeIcon.Times.ToIconString(), ui.MutedInk,
                AppSkin.Transparent, 0.55f, Loc.T(L.AetherStream.Remove)))
        {
            queue.Remove(entry);
        }

        if (hovered && !queueDragActive && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !handleHit &&
            ImGui.GetMousePos().X < removeCenter.X - 16f * scale)
        {
            queue.PlayNow(entry);
        }
    }

    private static string Ellipsize(string text, float maxWidth)
    {
        if (maxWidth <= 0f || Typography.Measure(text, TextStyles.Body).X <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "…";
        for (var length = text.Length - 1; length > 0; length--)
        {
            var candidate = text.Substring(0, length).TrimEnd() + ellipsis;
            if (Typography.Measure(candidate, TextStyles.Body).X <= maxWidth)
            {
                return candidate;
            }
        }

        return ellipsis;
    }
}
