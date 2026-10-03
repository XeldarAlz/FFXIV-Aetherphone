using Aetherphone.Apps.Music.Components;
using Aetherphone.Apps.Music.Library;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const int ImportLinkLimit = 2048;
    private const int ImportPreviewRows = 8;
    private const float ImportCoverSide = 150f;
    private const float ImportButtonWidth = 96f;
    private const float ImportSpinnerRadius = 13f;
    private const float ImportFailureGlyphScale = 1.6f;
    private const string ImportContext = "import.preview";

    private PlaylistImporter? importer;
    private string importDraft = string.Empty;
    private string importName = string.Empty;
    private ImportPreview? importPreviewFor;
    private Song[] importPreviewSongs = Array.Empty<Song>();
    private CoverArt importArt = CoverArt.None;
    private string importMeta = string.Empty;
    private bool focusImportLink;

    private PlaylistImporter Importer => importer ??= new PlaylistImporter(songResolver);

    public bool TryHandlePastedLink(string text)
    {
        if (!PlaylistImporter.Classify(text).IsValid)
        {
            return false;
        }

        OpenImport(text);
        return true;
    }

    private void OpenImport(string text)
    {
        importDraft = (text ?? string.Empty).Trim();
        importPreviewFor = null;
        if (importDraft.Length > 0)
        {
            Importer.Start(importDraft);
        }
        else
        {
            Importer.Reset();
            focusImportLink = true;
        }

        if (Router.Current.Screen != MusicScreen.Import)
        {
            Push(MusicRoute.Of(MusicScreen.Import));
        }
    }

    private void DrawImport(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var frame = BeginPage(context);
        using (AppSurface.BeginEdgeToEdge(frame.Body))
        {
            var textWidth = ScrollLayout.StableContentWidth() - MusicUi.Inset * 2f * scale;
            LibraryKit.Gap(Metrics.Space.Sm);
            LibraryKit.CenteredText(Loc.T(L.Music.Library.ImportHint), TextStyles.Subheadline, ui.MutedInk, textWidth);
            LibraryKit.Gap(Metrics.Space.Lg);
            DrawImportField(scale);
            LibraryKit.Gap(Metrics.Space.Xl);
            switch (Importer.Phase)
            {
                case ImportPhase.Fetching:
                    DrawImportFetching(scale, textWidth);
                    break;
                case ImportPhase.Failed:
                    DrawImportFailure(scale, textWidth);
                    break;
                case ImportPhase.Ready:
                    DrawImportPreview(scale, textWidth);
                    break;
            }

            LibraryKit.Gap(LibraryBottomGap);
        }

        EndPage(in frame, context, Loc.T(L.Music.ImportPlaylist));
    }

    private void DrawImportField(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = MusicUi.Inset * scale;
        var height = LibraryKit.FieldHeight * scale;
        var buttonWidth = ImportButtonWidth * scale;
        var gap = Metrics.Space.Sm * scale;
        var field = new Rect(new Vector2(origin.X + inset, origin.Y),
            new Vector2(origin.X + width - inset - buttonWidth - gap, origin.Y + height));
        var submitted = LibraryKit.TextField(field, "##musicImportLink", Loc.T(L.Music.Library.LinkHint),
            ref importDraft, ImportLinkLimit, focusImportLink, ui, out _);
        focusImportLink = false;
        var button = new Rect(new Vector2(field.Max.X + gap, origin.Y), new Vector2(origin.X + width - inset,
            origin.Y + height));
        var empty = importDraft.Trim().Length == 0;
        var tapped = ui.PillButton(button, Loc.T(empty ? L.Music.Library.Paste : L.Music.Library.LookUp), true,
            "##musicImportGo");
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        if (tapped && empty)
        {
            importDraft = (ImGui.GetClipboardText() ?? string.Empty).Trim();
            submitted = importDraft.Length > 0;
        }
        else if (tapped)
        {
            submitted = true;
        }

        if (!submitted || importDraft.Trim().Length == 0 || Importer.Busy)
        {
            return;
        }

        importPreviewFor = null;
        Importer.Start(importDraft);
    }

    private void DrawImportFetching(float scale, float textWidth)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var radius = ImportSpinnerRadius * scale;
        LoadingPulse.Spinner(new Vector2(origin.X + width * 0.5f, origin.Y + radius), radius, ui.Accent, 1f,
            ImGui.GetWindowDrawList());
        ImGui.Dummy(new Vector2(width, radius * 2f));
        LibraryKit.Gap(Metrics.Space.Md);
        LibraryKit.CenteredText(Loc.T(L.Music.Library.Fetching), TextStyles.Headline, ui.TitleInk, textWidth);
        LibraryKit.Gap(Metrics.Space.Xxs);
        LibraryKit.CenteredText(Loc.T(L.Music.Library.FetchingSub), TextStyles.Footnote, ui.MutedInk, textWidth);
    }

    private void DrawImportFailure(float scale, float textWidth)
    {
        var failure = Importer.Failure;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var glyphHeight = Typography.LineHeight(TextStyles.Title1);
        AppSkin.Icon(ImGui.GetWindowDrawList(), new Vector2(origin.X + width * 0.5f, origin.Y + glyphHeight * 0.5f),
            IconGlyph.Of(FontAwesomeIcon.ExclamationCircle), ui.MutedInk, ImportFailureGlyphScale);
        ImGui.Dummy(new Vector2(width, glyphHeight));
        LibraryKit.Gap(Metrics.Space.Md);
        LibraryKit.CenteredText(Loc.T(FailureText(failure)), TextStyles.Subheadline, ui.BodyInk, textWidth);
        if (failure == ImportFailure.InvalidLink)
        {
            return;
        }

        LibraryKit.Gap(Metrics.Space.Lg);
        var buttonOrigin = ImGui.GetCursorScreenPos();
        var buttonWidth = MathF.Min(textWidth, ImportButtonWidth * 2f * scale);
        var button = new Rect(new Vector2(buttonOrigin.X + (width - buttonWidth) * 0.5f, buttonOrigin.Y),
            new Vector2(buttonOrigin.X + (width + buttonWidth) * 0.5f, buttonOrigin.Y + AppSkin.PillHeight * scale));
        var notInstalled = failure == ImportFailure.NotInstalled;
        var tapped = ui.PillButton(button, Loc.T(notInstalled ? L.Music.Library.SetUp : L.Common.Retry), true,
            "##musicImportRetry");
        ImGui.SetCursorScreenPos(buttonOrigin);
        ImGui.Dummy(new Vector2(width, button.Height));
        if (!tapped)
        {
            return;
        }

        if (notInstalled)
        {
            setupDismissed = false;
            Importer.Reset();
            return;
        }

        importPreviewFor = null;
        Importer.Start(Importer.Link);
    }

    private static LocString FailureText(ImportFailure failure)
    {
        return failure switch
        {
            ImportFailure.InvalidLink => L.Music.Library.InvalidLink,
            ImportFailure.NotInstalled => L.Music.Library.NotInstalled,
            ImportFailure.Empty => L.Music.Library.EmptyImport,
            _ => L.Music.Library.Unavailable,
        };
    }

    private static LocString KindText(PlaylistLinkKind kind)
    {
        return kind switch
        {
            PlaylistLinkKind.Mix => L.Music.Library.KindMix,
            PlaylistLinkKind.Channel => L.Music.Library.KindChannel,
            PlaylistLinkKind.Video => L.Music.Library.KindVideo,
            _ => L.Music.Library.KindPlaylist,
        };
    }

    private void EnsureImportPreview(ImportPreview preview)
    {
        if (ReferenceEquals(importPreviewFor, preview))
        {
            return;
        }

        importPreviewFor = preview;
        var rows = Math.Min(ImportPreviewRows, preview.Songs.Length);
        importPreviewSongs = preview.Songs.AsSpan(0, rows).ToArray();
        importArt = CoverArt.Of(preview.Songs);
        importName = preview.Title.Length > 0 ? preview.Title : Loc.T(L.Music.Library.ImportedPlaylist);
        importMeta = string.Format(Loc.Culture, Loc.T(L.Music.Library.Summary), MusicUi.SongCount(preview.Songs.Length),
            MusicUi.LongDuration(preview.TotalSeconds));
    }

    private void DrawImportPreview(float scale, float textWidth)
    {
        if (Importer.Preview is not { } preview)
        {
            return;
        }

        EnsureImportPreview(preview);
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var side = ImportCoverSide * scale;
        LibraryArt.DrawCover(ImGui.GetWindowDrawList(), images, wallpaperImages,
            new Vector2(origin.X + (width - side) * 0.5f, origin.Y), side, importArt, importName);
        ImGui.Dummy(new Vector2(width, side));
        LibraryKit.Gap(Metrics.Space.Md);
        LibraryKit.CenteredText(Loc.T(KindText(preview.Link.Kind)), TextStyles.FootnoteEmphasized, ui.Accent,
            textWidth);
        LibraryKit.CenteredText(preview.Title.Length > 0 ? preview.Title : importName, TextStyles.Title3, ui.TitleInk,
            textWidth);
        if (preview.Author.Length > 0)
        {
            LibraryKit.CenteredText(preview.Author, TextStyles.Subheadline, ui.MutedInk, textWidth);
        }

        LibraryKit.CenteredText(importMeta, TextStyles.Footnote, ui.MutedInk, textWidth);
        LibraryKit.Gap(Metrics.Space.Lg);
        if (preview.Link.IsCollection)
        {
            DrawImportCommit(preview, scale);
        }
        else
        {
            DrawSingleSongCommit(preview, scale);
        }

        LibraryKit.Gap(Metrics.Space.Lg);
        DrawSongRows(importPreviewSongs, ImportContext, importName);
    }

    private void DrawImportCommit(ImportPreview preview, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = MusicUi.Inset * scale;
        var field = new Rect(new Vector2(origin.X + inset, origin.Y),
            new Vector2(origin.X + width - inset, origin.Y + LibraryKit.FieldHeight * scale));
        LibraryKit.TextField(field, "##musicImportName", Loc.T(L.Music.PlaylistNameHint), ref importName,
            LibraryStore.NameLimit, false, ui, out _);
        var buttonTop = field.Max.Y + Metrics.Space.Md * scale;
        var button = new Rect(new Vector2(field.Min.X, buttonTop),
            new Vector2(field.Max.X, buttonTop + AppSkin.PillHeight * scale));
        var tapped = ui.PillButton(button, Loc.T(L.Music.Library.ImportButton), true, "##musicImportCommit");
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, button.Max.Y - origin.Y));
        if (!tapped)
        {
            return;
        }

        var name = importName.Trim();
        if (name.Length == 0)
        {
            name = Loc.T(L.Music.Library.ImportedPlaylist);
        }

        var id = library.CreatePlaylist(name, string.Empty, preview.Link.Url);
        library.AddRangeToPlaylist(id, preview.Songs);
        ShellToast.Show(string.Format(Loc.Culture, Loc.T(L.Music.Library.Imported), name));
        Importer.Reset();
        importDraft = string.Empty;
        importPreviewFor = null;
        Router.Replace(MusicRoute.Playlist(id));
    }

    private void DrawSingleSongCommit(ImportPreview preview, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = MusicUi.Inset * scale;
        var gap = Metrics.Space.Md * scale;
        var buttonWidth = MathF.Max(1f, (width - inset * 2f - gap) * 0.5f);
        var height = AppSkin.PillHeight * scale;
        var song = preview.Songs[0];
        var libraryRect = new Rect(new Vector2(origin.X + inset, origin.Y),
            new Vector2(origin.X + inset + buttonWidth, origin.Y + height));
        var playlistRect = new Rect(new Vector2(libraryRect.Max.X + gap, origin.Y),
            new Vector2(libraryRect.Max.X + gap + buttonWidth, origin.Y + height));
        var inLibrary = library.InLibrary(song.VideoId);
        var addLibrary = ui.PillButton(libraryRect, Loc.T(inLibrary ? L.Music.Library.AddedToLibrary :
            L.Music.AddToLibrary), !inLibrary, "##musicImportLibrary");
        var addPlaylist = ui.PillButton(playlistRect, Loc.T(L.Music.AddToPlaylist), false, "##musicImportPlaylist");
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        if (addLibrary && !inLibrary)
        {
            library.AddToLibrary(song);
            ShellToast.Show(Loc.T(L.Music.Library.AddedToLibrary));
        }
        else if (addPlaylist)
        {
            playlistPicker.Open(song);
        }
    }
}
