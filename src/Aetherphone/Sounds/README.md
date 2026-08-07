# Bundled sounds

Ringtone and notification audio live in separate subfolders, and each picker in
Settings only lists its own kind:

- `Ringtones/` ships the **Ringtone** options (plays on incoming calls).
- `Notifications/` ships the **Notification Sound** options (plays on
  notifications, including per-app overrides).

Every `.mp3` and `.wav` file in these folders ships with the plugin. The phone
plays its own audio only. Game system sounds are never used, so these folders
plus the user's imported files are the entire sound catalog: with no files in a
folder the only option left in that picker is **Silent**.

Notes:

- Fresh installs (and configs migrated off the old game sounds) default to
  `Ringtones/Ringtone_1.mp3` for calls and `Notifications/Notification_1.mp3`
  for notifications. Those two names live in `SoundLibrary.BundledRingtoneToken`
  and `SoundLibrary.BundledNotificationToken`, so rename the files and the
  constants together. Whenever a saved choice no longer resolves, the first
  file in alphabetical order of its kind takes over.
- Playback is dispatched by file extension: `.mp3` and `.wav` play through
  managed decoders (Wine-safe), everything else falls back to Windows Media
  Foundation. A misnamed file (for example MP3 bytes named `.wav`) plays
  through the wrong decoder and can fail, where content sniffing used to
  cover that case.
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
