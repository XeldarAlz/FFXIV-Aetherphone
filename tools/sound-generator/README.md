# Sound generator

Rebuilds every bundled clip in src/Aetherphone/Sounds/:

- **Synthesizes** the original interface, game and Gamba clips from code: decaying partials, filtered noise and tuned bell and marimba tones, with a small synthetic room on most of them. Every synthesized clip is seeded, so re-running produces the same audio.
- **Downloads** the third-party clips from pinned sources into .cache/ (ignored by git): Material Design sounds (CC-BY 4.0, including the Ui/win.wav celebration), Android Open Source Project ringtones, notifications and the two Clock loops (Ui/alarm.wav, Ui/timer.wav) at a fixed commit (Apache-2.0), and the Kenney Casino and Interface packs (CC0).
- **Masters** everything the same way: trims leading silence to 3 ms before the onset, cuts the tail at -54 dB with a fade, resamples to 48 kHz, and sets the loudness for the clip's category under a -1 dBFS peak ceiling.

## Loudness targets

Short clips are measured on their loudest 30 ms window after a weighting filter (an 80 Hz high-pass and a high-shelf lift), so a click and a chime land at the same perceived level. Ringtones and the Clock loops use integrated loudness instead and keep their tails so they loop cleanly; the ringback loop keeps its tail too.

| Category | Target | Used for |
|---|---|---|
| `tick` | -24 dB | Taps and the refresh tick |
| `key` | -23 dB | Keyboard clicks |
| `air` | -27 dB | App open and close, sheets, the island |
| `event` | -20 dB | Lock, send, receive, toggles, the shutter |
| `chime` | -19 dB | Success, coin and call cues, big wins and fanfares |
| `game` | -19 dB | Mini-game and most Gamba cues |
| `loop` | -24 dB | The outgoing-call ringback |
| `notification` | -15 dB | Notification sounds |
| Ringtones and Clock loops | -16 LUFS integrated | Ringtones, alarm.wav, timer.wav |

The targets live in `LOUDNESS_TARGETS` and `RINGTONE_LUFS` at the top of generate-sounds.py. Per-event playback gain is separate and lives in the plugin's catalog (src/Aetherphone/Core/Notifications/UiSound.cs).

## Run

```bash
pip install numpy scipy soundfile pyloudnorm lameenc
python generate-sounds.py
```

This writes into the plugin's Sounds folder, Sounds/Casino/ included. Pass another output directory as the first argument to render somewhere else for auditioning. `python generate-sounds.py --casino` renders only the Gamba cues in Sounds/Casino/ (all synthesized, nothing downloaded); they carry their own seeds, so the rest of the set is unaffected. Ui/shutter.wav is a CC0 recording that the script reads from the output folder and re-masters in place, so an output folder other than the plugin's needs a copy of it.

## Adding a sound

- Interface or game clip: add an entry to `ui_sounds()` or `game_sounds()` with its loudness category, then add a `UiSound` member and wire the file name in `UiSoundCatalog` (both in src/Aetherphone/Core/Notifications/UiSound.cs). Gamba cues go in `casino_sounds()` and seasonal clips in `halloween_sounds()`.
- Ringtone or notification: add a display name and source to `RINGTONES` or `NOTIFICATIONS`. The file name is the name players see in Settings.
- Clock loop: add it to `ALARM_TONES`. `SoundService` plays the loops by file name (src/Aetherphone/Core/Notifications/SoundService.cs), so a new name needs a code change there.
- A sound designer's finished recording: the script has no source kind for one yet. src/Aetherphone/Sounds/README.md says how to add it.
- Credit any third-party source in THIRD-PARTY-NOTICES.md.
