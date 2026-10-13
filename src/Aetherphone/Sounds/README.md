# Bundled sounds

Every file in these folders is written by
`tools/sound-generator/generate-sounds.py`. Edit the generator and re-run it
instead of hand-editing clips, so trims, fades and loudness stay consistent.

Ringtone and notification audio live in separate subfolders, and each picker in
Settings only lists its own kind:

- `Ringtones/` ships the **Ringtone** options (plays on incoming calls).
- `Notifications/` ships the **Notification Sound** options (plays on
  notifications, including per-app overrides).
- `Ui/` ships the **Interface Sounds** clips (lock, app transitions, sheets,
  taps, keyboard, toggles, send and receive, call cues and friends). This
  folder is not a picker: the file names are wired to events in
  `Core/Notifications/UiSound.cs` and never appear in Settings lists, and
  `UiSoundCatalogTests` fails the build server if a wired name goes missing.
  `ringback.wav` is the outgoing-call loop, and `alarm.wav` and `timer.wav`
  are the Clock loops, all played by `SoundService`. The `halloween_*.wav`
  clips are the seasonal Chirper and Aethergram sounds, played only while the
  Halloween theme is on.
- `Games/` ships the **Game Sounds** palette for the mini-games and the
  Casino (hits, pops, chimes, cards, chips, Simon tones), wired through
  the same catalog on the Game channel and gated by the Game Sounds toggle in
  Settings.

Interface and game clips are mono 48 kHz 16-bit PCM WAV. Ringtones and
notification sounds are 192 kbps stereo MP3. The generator normalizes each
clip to a loudness target for its category; per-event gain lives in the
catalog, not in the files. `UiSoundDecodeTests` checks that every wired clip
reaches its onset within 30 ms and ends without a click. Sources and licenses
are listed in `THIRD-PARTY-NOTICES.md`.

Every `.mp3` and `.wav` file in these folders ships with the plugin. The phone
plays its own audio only. Game system sounds are never used, so these folders
plus the user's imported files are the entire sound catalog: with no files in a
folder the only option left in that picker is **Silent**.

Notes:

- Fresh installs (and configs migrated off the old game sounds) default to
  `Ringtones/Signal.mp3` for calls and `Notifications/Chime.mp3` for
  notifications. Those two names live in `SoundLibrary.BundledRingtoneToken`
  and `SoundLibrary.BundledNotificationToken`, so rename the files and the
  constants together. Whenever a saved choice no longer resolves, the first
  file in alphabetical order of its kind takes over.
- Removing or renaming a bundled ringtone or notification sound: add the old
  name and its replacement to `Core/Notifications/RetiredSounds.cs`, so saved
  choices move to the replacement instead of the alphabetical fallback.
- Playback is dispatched by file extension: `.mp3` and `.wav` play through
  managed decoders (Wine-safe), everything else falls back to Windows Media
  Foundation. A misnamed file (for example MP3 bytes named `.wav`) is
  rejected by the managed reader and falls back to Media Foundation too, so
  it plays on Windows but can fail under Wine.
- A file's display name is its file name with `_`/`-` turned into spaces
  (`soft_bell.mp3` shows as "soft bell").
- Ringtones loop until the call is answered or missed, so keep them seamless.
  Notification sounds play once.
- Ship only audio you have the rights to distribute, and add attribution to
  `THIRD-PARTY-NOTICES.md` when a file requires it.
- Users can add their own files from Settings ("Import from PC"); importing on
  the Ringtone page copies into the plugin config directory's
  `Sounds/Ringtones` folder, importing on a notification sound page copies into
  `Sounds/Notifications`. Imported files are not bundled.
