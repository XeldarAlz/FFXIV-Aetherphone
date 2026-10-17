# Bundled sounds

Every file in these folders is written by tools/sound-generator/generate-sounds.py. Edit the generator and re-run it rather than editing clips by hand, so trims, fades and loudness stay consistent. The one local input is Ui/shutter.wav, a recording the generator reads from this folder and re-masters in place.

## Folders

| Folder | What it holds | How it plays |
|---|---|---|
| Ringtones/ | The Ringtone options, 192 kbps stereo MP3 | Listed in Settings > Sounds > Ringtone; loops until the call is answered or missed |
| Notifications/ | The Notification Sound options, 192 kbps stereo MP3 | Listed in Settings > Sounds > Notification Sound and the per-app overrides; plays once |
| Ui/ | Interface clips (taps, keyboard, transitions, sheets, send and receive, call cues), mono 48 kHz 16-bit WAV | Wired to events in Core/Notifications/UiSound.cs; never listed in Settings |
| Games/ | The mini-game palette (hits, pops, chimes, cards, Simon tones), same WAV format | Wired in UiSound.cs on the Game channel, muted by the Game Sounds toggle |
| Casino/ | The Gamba cues (reels, wheel, chips, wins, race horn), same WAV format | Wired in UiSound.cs on the Game channel, muted by the Game Sounds toggle |

A few Ui/ clips are loops that `SoundService` plays by name: ringback.wav for an outgoing call, and alarm.wav and timer.wav for the Clock app. The `halloween_*.wav` clips are the seasonal sounds of Chirper, Aethergram, Velvet and the treat hunt they share. Those apps ask for them only while the Halloween theme is on. With Seasonal Sounds off (Settings > Sounds), the usual sound plays in their place where one exists, and nothing plays otherwise.

Per-event gain lives in the catalog in UiSound.cs, not in the files. `UiSoundCatalogTests` fails when a wired file is missing, and `UiSoundDecodeTests` checks that every wired clip reaches its onset within 30 ms and ends without a click. Sources and licenses are listed in THIRD-PARTY-NOTICES.md.

## Rules

- Every `.mp3` and `.wav` in these folders ships. The phone plays only its own audio, never the game's system sounds, so these folders plus a player's imports are the whole catalog. A picker folder with no files leaves only **Silent**.
- Fresh installs default to Ringtones/Signal.mp3 and Notifications/Chime.mp3. Both names live in `SoundLibrary.BundledRingtoneToken` and `SoundLibrary.BundledNotificationToken`, so rename a default file and its constant together. When a saved choice does not resolve to a file, the first bundled file of its kind in alphabetical order takes over.
- To remove or rename a ringtone or notification sound, add the retired name and its replacement to Core/Notifications/RetiredSounds.cs, so saved choices move to the replacement instead of the alphabetical fallback.
- Keep to `.mp3` and `.wav`. Both play through managed decoders that work under Wine; any other extension, or MP3 bytes in a file named `.wav`, falls back to Windows Media Foundation and can fail under Wine.
- A file's display name is its file name with underscores and hyphens turned into spaces (soft_bell.mp3 shows as "soft bell").
- Ship only audio you have the right to distribute, credit it in THIRD-PARTY-NOTICES.md when its license asks, and never ship AI-generated audio ([AI usage](../../../docs/ai-usage.md)).
- Players can add their own ringtones and notification sounds with Import from PC. The file is copied into the plugin config folder's Sounds/Ringtones or Sounds/Notifications and is never bundled.

## Delivering an original sound

The generator has no input for a finished recording: it synthesizes clips or downloads them from pinned Material, AOSP and Kenney sources, and its only local file is the shutter. A sound designer delivers a lossless 48 kHz WAV as the [Art guide](../../../docs/art-guide.md) describes. To land it, commit the designer's master in a folder under tools/sound-generator and add a source kind to generate-sounds.py that reads it, next to the Material and AOSP kinds. Give the entry a loudness category, or list it in `RINGTONES` or `NOTIFICATIONS`, so it goes through the same trim, fade and loudness pass as every other clip. Never read a shipped MP3 back as a source, since each run would encode it again and lose quality. Then wire the file like any other sound and credit the designer.
