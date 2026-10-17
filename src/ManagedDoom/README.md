# ManagedDoom (vendored)

This project is the Doom engine of [managed-doom](https://github.com/sinshu/managed-doom) by Nobuaki Tanaka,
a C# port of the original id Software source, licensed GPL-2.0-or-later (see LICENSE_ManagedDoom.txt).
It runs the Doom mini-game. Aetherphone.csproj copies the license into the release zip next to
THIRD-PARTY-NOTICES.md. Paths below are relative to this folder unless they start with src/Aetherphone.

- Upstream: Managed Doom v2.1a, commit `9365696eb44326a3aab72c4bab217f7db8a87c96`.
- Kept: src/Doom, src/Video, src/Audio, src/UserInput, and src/ApplicationInfo.cs, src/CommandLineArgs.cs,
  src/Config.cs and src/ConfigUtilities.cs, verbatim except the one change below.
- Dropped: upstream's src/Silk desktop host (window, OpenGL, OpenAL, keyboard) and its packages.
  Aetherphone supplies its own backends in src/Aetherphone/Apps/Games/Doom.
- Changed: `ConfigUtilities.DataDirectoryOverride`, read by `GetExeDirectory()`, so config and saved
  games land in Aetherphone's data folder instead of next to the game client's executable.

## Build and CI exemptions

ManagedDoom.csproj turns off nullable analysis, analyzers, code-style enforcement and warnings as errors,
so the upstream code compiles untouched. The CI namespace guard skips this folder, and the underscore
field, byte order mark and LINQ guards scan only the plugin and its tests. The em dash, async void, UI scale
and clock guards still apply.
Dependabot does not update this project.

## Game data

None is included. The Doom mini-game downloads the shareware episode or Freedoom, plus a General MIDI
soundfont, each pinned by SHA-256 in src/Aetherphone/Apps/Games/Doom/DoomAssets.cs, and plays a commercial
IWAD the player puts in the doom folder of the plugin config directory.

## Updating from upstream

1. Copy the kept folders and files from the new upstream commit over src.
2. Add `DataDirectoryOverride` back to ConfigUtilities.cs, and make `GetExeDirectory()` return it when set.
3. Build Release, play a level in game, and update the commit here and in THIRD-PARTY-NOTICES.md.
