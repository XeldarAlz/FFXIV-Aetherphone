using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private const float AddSheetHeight = 0.5f;
    private const float AddSheetViewerHeight = 0.34f;
    private const float FieldRowHeight = 44f;
    private const float ClipboardPollSeconds = 1f;
    private const int MaxLinkLength = 2000;
    private const int PlaylistActionOne = 0;
    private const int PlaylistActionAll = 1;

    private readonly SheetSurface addSheet = new("aetherstream.add");
    private readonly string[] addModeLabels = new string[3];
    private readonly Action<Rect> drawAddContent;

    private string linkInput = string.Empty;
    private string copiedLink = string.Empty;
    private string copiedLinkHost = string.Empty;
    private string dismissedCopiedLink = string.Empty;
    private QueueAddMode addMode = QueueAddMode.PlayNow;
    private QueueAddMode pendingPlaylistMode = QueueAddMode.PlayNow;
    private RefreshCadence clipboardCadence;
    private uint clipboardSequence = ClipboardWatch.UnseenSequence;

    private void OpenAddSheet()
    {
        addMode = video.HasMedia || queue.Current is not null ? QueueAddMode.AddToQueue : QueueAddMode.PlayNow;
        tracksSheet.Close();
        addSheet.Open();
    }

    private void DrawAddSheet(Rect area, float scale)
    {
        var viewing = watchAlong.IsViewing;
        var title = viewing
            ? Loc.T(watchAlong.CanAddDirectly ? L.AetherStream.AddToPartyTitle : L.AetherStream.SuggestTitle)
            : Loc.T(L.AetherStream.AddVideoTitle);
        addSheet.Draw(area, SheetSkin.From(ui.Palette, Ink), title, viewing ? AddSheetViewerHeight : AddSheetHeight,
            drawAddContent);
    }

    private void DrawAddContent(Rect content)
    {
        var scale = UiScale.Current;
        var viewing = watchAlong.IsViewing;
        var top = content.Min.Y;
        var submitted = DrawLinkField(new Rect(new Vector2(content.Min.X, top),
            new Vector2(content.Max.X, top + FieldRowHeight * scale)), "##aetherstreamAddLink",
            "aetherstream.add.paste", scale);
        top += FieldRowHeight * scale + Metrics.Space.Md * scale;

        if (!viewing)
        {
            addModeLabels[0] = Loc.T(L.AetherStream.PlayNow);
            addModeLabels[1] = Loc.T(L.AetherStream.PlayNext);
            addModeLabels[2] = Loc.T(L.AetherStream.AddToQueue);
            var strip = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + 36f * scale));
            addMode = (QueueAddMode)SegmentStrip.Draw("aetherstream.addMode", strip, addModeLabels, (int)addMode,
                ui.Palette);
            top += 36f * scale + Metrics.Space.Md * scale;
        }

        var canSubmit = MediaInput.Normalize(linkInput).Length > 0;
        var primary = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + ButtonHeight * scale));
        var primaryLabel = viewing
            ? Loc.T(watchAlong.CanAddDirectly ? L.AetherStream.AddToQueue : L.AetherStream.SuggestAction)
            : Loc.T(addMode == QueueAddMode.PlayNow ? L.AetherStream.PlayAction : L.AetherStream.QueueSuggestionAdd);
        if (PrimaryButton(primary, primaryLabel, canSubmit) || (submitted && canSubmit))
        {
            SubmitInput(linkInput, addMode);
            return;
        }

        if (viewing)
        {
            return;
        }

        top += ButtonHeight * scale + Metrics.Space.Sm * scale;
        var browse = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + ButtonHeight * scale));
        if (QuietButton(browse, Loc.T(L.AetherStream.BrowseLocalFile)))
        {
            PickLocalFile();
        }
    }

    private bool DrawLinkField(Rect row, string imguiId, string pasteId, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pasteRadius = 17f * scale;
        var field = new Rect(row.Min, new Vector2(row.Max.X - pasteRadius * 2f - Metrics.Space.Sm * scale, row.Max.Y));
        var hint = watchAlong.IsViewing ? Loc.T(L.AetherStream.SuggestHint) : Loc.T(L.AetherStream.UrlHint);
        var submitted = SubmitField.Draw(field, imguiId, hint, ref linkInput, accentedTheme, MaxLinkLength,
            FontAwesomeIcon.Link);
        var pasteCenter = new Vector2(row.Max.X - pasteRadius, row.Center.Y);
        if (HoverButton.Circle(drawList, pasteId, pasteCenter, pasteRadius,
                FontAwesomeIcon.Paste, Ink.ButtonFill, Ink.TitleInk, ImGui.GetIO().DeltaTime, 1f, true,
                Loc.T(L.AetherStream.PasteClipboard), HoverLabelSide.Above))
        {
            var clipboard = ImGui.GetClipboardText();
            if (!string.IsNullOrWhiteSpace(clipboard))
            {
                linkInput = MediaInput.Normalize(clipboard);
            }
        }

        return submitted;
    }

    private void PickLocalFile()
    {
        FilePicker.PickVideo(Loc.T(L.AetherStream.BrowseLocalFile),
            path => Interlocked.Exchange(ref pendingLocalFile, path));
    }

    private void SubmitInput(string raw) => SubmitInput(raw, addMode);

    private void SubmitInput(string raw, QueueAddMode mode)
    {
        var input = MediaInput.Normalize(raw);
        if (input.Length == 0)
        {
            return;
        }

        var localPath = MediaInput.LooksLikeLocalPath(input);
        if (!localPath && !VideoEngine.ValidateURL(input, out _))
        {
            ShellToast.Show(Loc.T(L.AetherStream.NotALink));
            return;
        }

        linkInput = string.Empty;
        if (watchAlong.IsViewing)
        {
            if (localPath)
            {
                ShellToast.Show(Loc.T(L.AetherStream.SuggestLinksOnly));
                return;
            }

            watchAlong.SuggestQueueItem(input);
            ShellToast.Show(Loc.T(watchAlong.CanAddDirectly
                ? L.AetherStream.AddedToQueueToast
                : L.AetherStream.SuggestionSent));
            addSheet.Close();
            return;
        }

        if (!localPath && VideoUrlResolver.IsPlaylistUrl(input))
        {
            if (VideoUrlResolver.NamesOneVideo(input))
            {
                actionLink = input;
                linkInput = input;
                pendingPlaylistMode = mode;
                BeginActions(SheetPurpose.PlaylistLink, Loc.T(L.AetherStream.PlaylistLinkTitle));
                AddAction(PlaylistActionOne, Loc.T(L.AetherStream.PlaylistJustThis));
                AddAction(PlaylistActionAll, Loc.T(L.AetherStream.PlaylistWhole));
                addSheet.Close();
                actions.Open();
                return;
            }

            StartPlaylistImport(input, mode);
            return;
        }

        AddSingle(input, mode);
    }

    private void AddSingle(string input, QueueAddMode mode)
    {
        if (mode == QueueAddMode.PlayNow)
        {
            CancelPendingJoin();
        }

        queue.Insert(queue.CreateDisplayEntry(input), mode);
        addSheet.Close();
        if (mode == QueueAddMode.PlayNow)
        {
            activeTab = StreamTab.Watch;
            return;
        }

        ShellToast.Show(Loc.T(mode == QueueAddMode.PlayNext
            ? L.AetherStream.PlayingNextToast
            : L.AetherStream.AddedToQueueToast));
    }

    private void StartPlaylistImport(string input, QueueAddMode mode)
    {
        if (mode == QueueAddMode.PlayNow)
        {
            CancelPendingJoin();
        }

        if (!queue.ImportPlaylist(input, mode, true))
        {
            linkInput = input;
            ShellToast.Show(Loc.T(L.AetherStream.PlaylistImporting));
            return;
        }

        addSheet.Close();
        ShellToast.Show(Loc.T(L.AetherStream.PlaylistImporting));
        if (mode == QueueAddMode.PlayNow)
        {
            activeTab = StreamTab.Watch;
        }
    }

    private void HandlePlaylistLinkAction(int code)
    {
        if (actionLink.Length == 0)
        {
            return;
        }

        var link = actionLink;
        actionLink = string.Empty;
        linkInput = string.Empty;
        if (code == PlaylistActionAll)
        {
            StartPlaylistImport(link, pendingPlaylistMode);
            return;
        }

        AddSingle(link, pendingPlaylistMode);
    }

    private void PollCopiedLink()
    {
        if (!clipboardCadence.Advance(ImGui.GetIO().DeltaTime, ClipboardPollSeconds))
        {
            return;
        }

        clipboardCadence.Reset();
        if (!ClipboardWatch.HasChanged(ref clipboardSequence))
        {
            return;
        }

        var clipboard = MediaInput.Normalize(ImGui.GetClipboardText());
        if (string.Equals(clipboard, copiedLink, StringComparison.Ordinal))
        {
            return;
        }

        copiedLink = string.Empty;
        copiedLinkHost = string.Empty;
        if (clipboard.Length is 0 or > MaxLinkLength
            || !clipboard.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            || !VideoEngine.ValidateURL(clipboard, out var parsed) || parsed is null)
        {
            return;
        }

        copiedLink = clipboard;
        copiedLinkHost = parsed.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? parsed.Host[4..]
            : parsed.Host;
    }
}
