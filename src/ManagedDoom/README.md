# ManagedDoom (vendored)

This project is the Doom engine of [managed-doom](https://github.com/sinshu/managed-doom) by Nobuaki Tanaka,
a C# port of the original id Software source, licensed GPL-2.0-or-later (see `LICENSE_ManagedDoom.txt`).
It is compiled into Aetherphone as the engine behind the Doom mini-game. `Aetherphone.csproj` copies
`LICENSE_ManagedDoom.txt` to the output root, so the license ships in the release zip next to
`THIRD-PARTY-NOTICES.md`.

- Upstream commit: `9365696eb44326a3aab72c4bab217f7db8a87c96`
- Taken verbatim: `src/Doom`, `src/Video`, `src/Audio`, `src/UserInput`, `ApplicationInfo.cs`,
  `CommandLineArgs.cs`, `Config.cs`, `ConfigUtilities.cs`
- Dropped: `src/Silk` (the desktop window, OpenGL, OpenAL and keyboard host) and its packages. Aetherphone
  supplies its own video, sound, music and input backends in `src/Aetherphone/Apps/Games/Doom`.
- One modification: `ConfigUtilities.DataDirectoryOverride`, consulted by `GetExeDirectory()`, so the
  engine's config file and save games land in Aetherphone's own data folder instead of next to the
  game client's executable.

The project disables nullable analysis and style enforcement so the upstream code compiles untouched.
No game data is included. On demand, the Doom mini-game downloads the shareware episode or Freedoom
Phase 1 and 2, plus the TimGM6mb General MIDI soundfont, each checked against a pinned SHA-256
(`src/Aetherphone/Apps/Games/Doom/DoomAssets.cs`). It also plays `doom.wad`, `doom2.wad`,
`plutonia.wad` or `tnt.wad` when the player places their own copy in the `doom` folder inside the plugin config directory.
