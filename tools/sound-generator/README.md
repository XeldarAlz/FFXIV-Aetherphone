# Sound generator

Rebuilds every bundled clip in `src/Aetherphone/Sounds/`:

- **Synthesizes** the original interface and game clips: glass taps from short decaying partials
  plus a filtered noise transient, dry keyboard clicks from a band-passed noise snap over a faint
  low thump, app and sheet transitions from band-passed
  noise swept up or down, marimba and bell chimes from tuned inharmonic partials, and a small
  synthetic room on most synthesized clips for polish.
- **Downloads** the third-party clips from pinned sources into `.cache/` (ignored by git): Material
  Design sounds (CC-BY 4.0, including the `Ui/win.wav` celebration), Android Open Source Project
  ringtones, notifications and the two Clock alarm loops (`Ui/alarm.wav`, `Ui/timer.wav`) at a fixed
  commit (Apache-2.0), and Kenney Casino and Interface packs (CC0).
- **Masters** everything the same way: trims leading silence to 3 ms before the onset, cuts the
  tail at -54 dB with a fade, resamples to 48 kHz and normalizes loudness per category (`tick`,
  `key`, `air`, `event`, `chime`, `game`, `loop`, `notification`) under a -1 dBFS peak ceiling.
  Ringtones and the Clock loops go to -16 LUFS integrated instead; they, and the `ringback` loop,
  keep their tails so they loop cleanly.

## Run

```bash
pip install numpy scipy soundfile pyloudnorm lameenc
python generate-sounds.py
```

Writes into the plugin's `Sounds` folder. Pass another output directory as the first argument to
render somewhere else for auditioning. `python generate-sounds.py --casino` renders only the Gamba
cues in `Sounds/Casino/` (all synthesized, nothing downloaded); they carry their own seeds, so the
rest of the set is unaffected. `Ui/shutter.wav` is a CC0 recording that is re-mastered in
place, so the output folder needs a copy of it. Every synthesized clip is seeded, so re-running
produces the same audio.

## Adding a sound

- Interface or game clip: add an entry to `ui_sounds()` or `game_sounds()` with its loudness
  category, then wire the file name in `UiSoundCatalog` (`src/Aetherphone/Core/Notifications/UiSound.cs`).
- Ringtone or notification: add a display name and source to `RINGTONES` or `NOTIFICATIONS`. The
  file name is the name players see in Settings.
- Clock loop: add it to `ALARM_TONES`. `SoundService` plays the loops by file name (`AlarmFile`,
  `TimerFile` in `src/Aetherphone/Core/Notifications/SoundService.cs`), so a new name needs a code
  change there.
- Credit any third-party source in `THIRD-PARTY-NOTICES.md`.
