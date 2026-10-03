using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddMediaTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "music", 4,
            new[]
            {
                GuideStep.Intro(L.Apps.Music, L.Onboarding.MusicIntroBody),
                GuideStep.TryTap(L.Onboarding.MusicFindTitle, L.Onboarding.MusicFindBody, "music.tab.search"),
                GuideStep.Point(L.Onboarding.MusicScopesTitle, L.Onboarding.MusicScopesBody, "music.search.scopes",
                    GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.MusicRadioTabTitle, L.Onboarding.MusicRadioTabBody, "music.tab.radio"),
                GuideStep.TryTap(L.Onboarding.MusicLibraryTitle, L.Onboarding.MusicLibraryBody, "music.tab.library"),
                GuideStep.Point(L.Onboarding.MusicPlaylistTitle, L.Onboarding.MusicPlaylistBody,
                    "music.library.playlists", GuideGesture.Tap),
                GuideStep.TryTap(L.Onboarding.MusicJamTitle, L.Onboarding.MusicJamBody, "music.tab.home"),
            });
        Add(tours, "photos", 3,
            new[]
            {
                GuideStep.TryTap(L.Onboarding.PhotosLibraryTitle, L.Onboarding.PhotosLibraryBody,
                    "photos.tab.library"),
                GuideStep.Point(L.Onboarding.PhotosOpenTitle, L.Onboarding.PhotosOpenBody, "photos.grid",
                    GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.PhotosAlbumsTitle, L.Onboarding.PhotosAlbumsBody, "photos.tab.albums"),
                GuideStep.Point(L.Onboarding.PhotosNewAlbumTitle, L.Onboarding.PhotosNewAlbumBody, "photos.albums.new",
                    GuideGesture.Tap),
                GuideStep.Point(L.Onboarding.PhotosTrashTitle, L.Onboarding.PhotosTrashBody, "photos.albums.trash",
                    GuideGesture.None),
            });
        Add(tours, "camera", 4,
            new[]
            {
                GuideStep.Point(L.Onboarding.CameraFrameTitle, L.Onboarding.CameraFrameBody, "camera.viewfinder",
                    GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.CameraSquareTitle, L.Onboarding.CameraSquareBody, "camera.mode.square"),
                GuideStep.Point(L.Onboarding.CameraShootTitle, L.Onboarding.CameraShootBody, "camera.shutter",
                    GuideGesture.Tap),
                GuideStep.Point(L.Onboarding.CameraShowUiTitle, L.Onboarding.CameraHudBody, "camera.showUi",
                    GuideGesture.Tap),
                GuideStep.Point(L.Onboarding.CameraRotateTitle, L.Onboarding.CameraRotateBody, "camera.rotate",
                    GuideGesture.Tap),
                GuideStep.Point(L.Onboarding.CameraLastShotTitle, L.Onboarding.CameraLastShotBody, "camera.lastShot",
                    GuideGesture.Tap),
            });
        Add(tours, "aetherstream", 3,
            new[]
            {
                GuideStep.Intro(L.Apps.AetherStream, L.Onboarding.AetherStreamIntroBody),
                GuideStep.Point(L.Onboarding.AetherStreamPasteTitle, L.Onboarding.AetherStreamPasteBody,
                    "aetherstream.composer", GuideGesture.None),
                GuideStep.Point(L.Onboarding.AetherStreamScreenTitle, L.Onboarding.AetherStreamScreenBody,
                    "aetherstream.screen", GuideGesture.Tap),
                GuideStep.TryTap(L.Onboarding.AetherStreamPartyTitle, L.Onboarding.AetherStreamPartyTabBody,
                    "aetherstream.tab.party"),
                GuideStep.Point(L.Onboarding.AetherStreamStartTitle, L.Onboarding.AetherStreamStartBody,
                    "aetherstream.party.start", GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.AetherStreamLibraryTitle, L.Onboarding.AetherStreamLibraryBody,
                    "aetherstream.tab.library"),
            });
        Add(tours, "notes", 3,
            new[]
            {
                GuideStep.TryTap(L.Onboarding.NotesNewTitle, L.Onboarding.NotesStartBody, "notes.new"),
                GuideStep.Point(L.Onboarding.NotesWriteTitle, L.Onboarding.NotesWriteBody, "notes.editor",
                    GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.NotesSaveTitle, L.Onboarding.NotesSaveBody, "notes.editor.back"),
                GuideStep.TryTap(L.Onboarding.NotesTodoTitle, L.Onboarding.NotesTodoBody, "notes.tab.reminders"),
                GuideStep.TryTap(L.Onboarding.NotesReminderTitle, L.Onboarding.NotesAddReminderBody, "notes.new"),
                GuideStep.TryTap(L.Onboarding.NotesNudgeTitle, L.Onboarding.NotesNudgeBody,
                    "notes.reminder.remind"),
            });
    }
}
