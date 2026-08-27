using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddMediaTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "music", 2,
            new[]
            {
                GuideStep.Note(L.Onboarding.MusicTitle, L.Onboarding.MusicBody),
                GuideStep.Point(L.Onboarding.MusicSearchTitle, L.Onboarding.MusicSearchBody, "music.search"),
                GuideStep.Point(L.Onboarding.MusicRadioTitle, L.Onboarding.MusicRadioBody, "music.categories"),
                GuideStep.Note(L.Onboarding.MusicNowPlayingTitle, L.Onboarding.MusicNowPlayingBody),
            });
        Add(tours, "photos", 2,
            new[]
            {
                GuideStep.Point(L.Onboarding.PhotosTitle, L.Onboarding.PhotosBody, "photos.grid"),
                GuideStep.Note(L.Onboarding.PhotosEmptyTitle, L.Onboarding.PhotosEmptyBody),
            });
        Add(tours, "camera", 3,
            new[]
            {
                GuideStep.Note(L.Onboarding.CameraTitle, L.Onboarding.CameraBody),
                GuideStep.Point(L.Onboarding.CameraModesTitle, L.Onboarding.CameraModesBody, "camera.modes"),
                GuideStep.Point(L.Onboarding.CameraFlashTitle, L.Onboarding.CameraFlashBody, "camera.flash"),
                GuideStep.Point(L.Onboarding.CameraShowUiTitle, L.Onboarding.CameraShowUiBody, "camera.showUi"),
                GuideStep.Point(L.Onboarding.CameraShutterTitle, L.Onboarding.CameraShutterBody, "camera.shutter"),
            });
        Add(tours, "aetherstream", 1,
            new[]
            {
                GuideStep.Note(L.Apps.AetherStream, L.Onboarding.AetherStreamBody),
                GuideStep.Point(L.Onboarding.AetherStreamPlayerTitle, L.Onboarding.AetherStreamPlayerBody,
                    "aetherstream.hero"),
                GuideStep.Point(L.Onboarding.AetherStreamAddTitle, L.Onboarding.AetherStreamAddBody,
                    "aetherstream.composer"),
                GuideStep.Point(L.Onboarding.AetherStreamTransportTitle, L.Onboarding.AetherStreamTransportBody,
                    "aetherstream.transport"),
                GuideStep.Point(L.Onboarding.AetherStreamActionsTitle, L.Onboarding.AetherStreamActionsBody,
                    "aetherstream.actions"),
                GuideStep.Note(L.Onboarding.AetherStreamPartyTitle, L.Onboarding.AetherStreamPartyBody),
                GuideStep.Point(L.Onboarding.AetherStreamSettingsTitle, L.Onboarding.AetherStreamSettingsBody,
                    "aetherstream.settings"),
            });
        Add(tours, "notes", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Notes, L.Onboarding.NotesBody),
                GuideStep.Point(L.Onboarding.NotesNewTitle, L.Onboarding.NotesNewBody, "notes.new"),
                GuideStep.Tap(L.Notes.TabReminders, L.Onboarding.NotesRemindersBody, "notes.tab.reminders",
                    "notes.tab.reminders"),
                GuideStep.Point(L.Onboarding.NotesReminderTitle, L.Onboarding.NotesReminderBody, "notes.new"),
            });
    }
}
