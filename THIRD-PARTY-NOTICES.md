# Third-Party Notices

This project bundles or depends on third-party software. Their licenses and
required notices are reproduced below. This file is shipped inside every
release archive alongside the components it covers.

---

## AI-generated content

**No asset inside the phone is AI-generated.** The icons, sounds, and fonts in
the device UI come from licensed sources credited in the sections below, and
each phone case is the work of a named human artist. The built-in wallpapers
are original to Aetherphone, and so are most interface and game sounds,
which are synthesized from code in `tools/sound-generator`.

Phone case art is drawn by community artists and each case credits its artist
by name in Settings. App icons are derived from Phosphor Icons (the Rolladeck
partner mark aside, see below), in-app glyphs from Tabler Icons, emoji from
Twemoji, the remaining audio from Google's Material sounds, the Android Open
Source Project and CC0 sound packs. See the sections below for the licenses
covering each.

The plugin's eight translated language catalogs are AI-assisted with human
review, and in-game terminology is verified against the game's own data rather
than translated freely. Corrections from native speakers are always welcome:
https://github.com/XeldarAlz/FFXIV-Aetherphone/blob/dev/docs/translating.md

## Inter font family

The fonts under `src/Aetherphone/Fonts/` (Inter Regular, Medium, SemiBold,
Bold) are redistributed unmodified.

- Copyright (c) 2016 The Inter Project Authors (https://github.com/rsms/inter)
- License: SIL Open Font License 1.1
- Full license text: `src/Aetherphone/Fonts/Inter-OFL.txt`, shipped next to
  the fonts in every release archive.

## Pirata One font

`src/Aetherphone/Fonts/PirataOne-Regular.ttf`, the seasonal wordmark face for
Chirper and Aethergram, is redistributed unmodified.

- Copyright (c) 2012 Rodrigo Fuenzalida, Nicolas Massi
  (www.taip.com.ar / abc.taip.com.ar), with Reserved Font Name 'Pirata'
- Source: https://github.com/google/fonts/tree/main/ofl/pirataone
- License: SIL Open Font License 1.1
- Full license text: `src/Aetherphone/Fonts/PirataOne-OFL.txt`, shipped next
  to the fonts in every release archive.

## Phosphor Icons

The painted application icons under `src/Aetherphone/Icons/` (`<appid>.png`
and `<appid>.fg.png`) are derived from
[Phosphor Icons](https://phosphoricons.com) 2.1.1 fill glyphs (recolored,
composed on a tile and rasterized to PNG); see `tools/icon-generator/` for
the generator. The one file in that folder outside this pipeline is
`rolladeck.png`, the Rolladeck partner mark shown with its live DJ listings
in the Music app.

- Homepage: https://phosphoricons.com
- Source: https://github.com/phosphor-icons/core
- License: MIT (Copyright (c) 2023 Phosphor Icons); full text reproduced in
  the MIT section below.

## Tabler Icons

`src/Aetherphone/Fonts/TablerIcons.ttf` is a subset of the outline and filled
[Tabler Icons](https://tabler.io/icons) webfonts, remapped into a private
codepoint range; `tools/icon-font/generate-icon-font.py` builds it and lists
every glyph it keeps. The legacy stencil generator in
`tools/icon-generator/generate-app-icons.mjs` rasterizes Tabler outline icons
and is kept for reference.

- Homepage: https://tabler.io/icons
- Source: https://github.com/tabler/tabler-icons
- License: MIT (Copyright (c) 2020-2026 Paweł Kuna); full text reproduced in
  the MIT section below.

## Twemoji

The color emoji images under `src/Aetherphone/Emoji/` (3,512 PNGs, one per
emoji sequence) are the 72x72 assets of
[Twemoji](https://github.com/jdecked/twemoji) 15.1.0, redistributed
unmodified.

- Copyright Twitter, Inc and other contributors
- Source: https://github.com/jdecked/twemoji
- License (graphics): Creative Commons Attribution 4.0 International
  (CC-BY 4.0), https://creativecommons.org/licenses/by/4.0/

The emoji metadata in `src/Aetherphone/Emoji/catalog.json` (labels, groups,
search tags, shortcodes and skin-tone variants) is built from
[emojibase-data](https://github.com/milesj/emojibase) by Miles Johnson,
MIT License; full text reproduced in the MIT section below.

## Sounds

Every bundled clip under `src/Aetherphone/Sounds/` is rebuilt by
`tools/sound-generator/generate-sounds.py`, which downloads the sources below
from pinned URLs (the CC0 shutter recording is re-mastered from the committed
copy instead), trims leading silence, fades the tail, resamples to 48 kHz
and normalizes loudness. Those edits are the only changes made to third-party
recordings.

### Original sounds

Most interface clips (taps, keyboard clicks, toggles, app and sheet
transitions, Dynamic Island, lock, message sent and received, success,
caution, blocked, coin, call connect and end, voice note record cues,
pull-to-refresh, outgoing ringback) and the synthesized mini-game clips (hits,
wood knocks, glass breaks, pops, jumps, lasers, explosions, chimes, power-ups,
line clears, error knocks and the four Simon tones) are original to
Aetherphone. They are synthesized from code in `tools/sound-generator` and
carry no third-party rights.

### Material Design sound resources

`Ui/win.wav`, the Ringtone "Signal" and the Notification sounds "Chime",
"Bloom", "Note", "Glint", "Beacon" and "Ping" are from the Material Design
sound resources by Google, trimmed and level-matched as described above.

- Copyright Google LLC
- Source: originally published on material.io; archived at
  https://archive.org/details/material-design-sound-resources
- License: Creative Commons Attribution 4.0 International (CC-BY 4.0),
  https://creativecommons.org/licenses/by/4.0/

### Android Open Source Project

The Ringtones "Cascade", "Horizon", "Lumen", "Orbit", "Prism", "Tide" and
"Summit" (originally Atria, Dione, Ganymede, Luna, Phobos, Sedna and Umbriel)
and the Notification sounds "Ripple", "Spark", "Drift", "Pulse", "Halo" and
"Echo" (originally Carme, Rhea, Io, Europa, Tethys and Iapetus), and the
Clock tones `Ui/alarm.wav` and `Ui/timer.wav` (originally the alarms Carbon
and Timer) are from the Android Open Source Project, renamed, trimmed and
level-matched as described above.

- Copyright The Android Open Source Project
- Source: https://android.googlesource.com/platform/frameworks/base/+/1cdfff555f4a21f71ccc978290e2e212e2f8b168/data/sounds/
- License: Apache License, Version 2.0,
  https://www.apache.org/licenses/LICENSE-2.0

### Public domain recordings

These clips are Creative Commons Zero (CC0):

- `Ui/shutter.wav`: "Trigger of camera 1" from
  [BigSoundBank](https://bigsoundbank.com/trigger-of-camera-1-s2394.html),
  by Joseph Sardin
- `Games/card_*.wav`, `Games/shuffle.wav`, `Games/deal_*.wav`,
  `Games/piece_*.wav` and `Games/chips_*.wav`: from
  [Kenney Casino Audio](https://kenney.nl/assets/casino-audio)
- `Games/tick_*.wav`: from
  [Kenney Interface Sounds](https://kenney.nl/assets/interface-sounds)

## mpv

libmpv provides video decoding and playback for the MogCast app (AetherStream
in code). No mpv binary is redistributed with this plugin:
`src/Aetherphone/Core/Video/MediaDependencies.cs` downloads an LGPL build
(`mpv-dev-lgpl-x86_64-*`, from the
[zhongfly/mpv-winbuild](https://github.com/zhongfly/mpv-winbuild) releases)
into the plugin's own Dalamud config directory on first use, and keeps it
updated from there.

- Homepage: https://mpv.io
- Source: https://github.com/mpv-player/mpv
- License: GNU Lesser General Public License v2.1 or later (LGPL build
  configuration); full text: https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html

## yt-dlp

yt-dlp resolves YouTube and other video links for MogCast, through mpv's own
`ytdl_hook`, and searches, imports and downloads songs for the Music app. As
with mpv above, no yt-dlp binary is redistributed: it is downloaded from the
project's own GitHub releases into the plugin's Dalamud config directory on
first use, and kept updated from there.

- Homepage: https://github.com/yt-dlp/yt-dlp
- License: The Unlicense (public domain)

## Deno

Deno is the JavaScript runtime the plugin hands to yt-dlp (`--js-runtimes`),
for both MogCast and the Music app. No Deno binary is redistributed:
`MediaDependencies.cs` downloads the Windows build
(`deno-x86_64-pc-windows-msvc`) from the project's own GitHub releases into
the plugin's Dalamud config directory on first use.

- Homepage: https://deno.com
- Source: https://github.com/denoland/deno
- License: MIT (Copyright 2018-2026 the Deno authors)

## AlphaChannel (Voudi)

The MogCast (AetherStream in code) video/screen engine under
`src/Aetherphone/Core/Video/` (mpv-backed playback and the world-anchored
ScreenPainter D3D11 quad renderer) is ported from
[AlphaChannel](https://github.com/Voudi/AlphaChannel) by Voudi, used with the
author's permission. Two smaller pieces ported from the same source live
outside that directory: the screen placement controls and presets in
`src/Aetherphone/Apps/AetherStream/AetherStreamApp.Screen.cs` (from
AlphaChannel's `ControlWindow.DrawScreenPositionSettings`) and the saved
screen preset shape in `src/Aetherphone/Configuration.cs` (from its
`Configuration`, with yaw added). Both are modified from the originals.

- Source: https://github.com/Voudi/AlphaChannel
- License: GNU General Public License v3.0 or later; full text reproduced in
  `src/Aetherphone/Core/Video/AlphaChannel-LICENSE`, shipped as
  `AlphaChannel-LICENSE` in every release archive.

## Concentus

`Concentus.dll` (version 2.2.2, by Logan Stromberg) is a C# implementation of
the Opus audio codec, redistributed in binary form.

- Source: https://github.com/lostromb/concentus
- License: BSD-style (Opus license)

```
Copyright (c) by various holding parties, including (but not limited to):
Skype Limited, Xiph.Org Foundation, CSIRO, Microsoft Corporation,
Jean-Marc Valin, Gregory Maxwell, Mark Borgerding, Timothy B. Terriberry,
Logan Stromberg. All rights are reserved by their respective holders.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

* Neither the name of Internet Society, IETF or IETF Trust, nor the
   names of specific contributors, may be used to endorse or promote
   products derived from this software without specific prior written
   permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

## SixLabors.ImageSharp

`SixLabors.ImageSharp.dll` (version 3.1.x, by Six Labors and contributors) is
redistributed in binary form.

- Source: https://github.com/SixLabors/ImageSharp
- License: Six Labors Split License, version 1.0
  (https://github.com/SixLabors/ImageSharp/blob/main/LICENSE). Aetherphone is
  an open-source project consuming the package unmodified, which the Split
  License covers under the terms of the Apache License, Version 2.0
  (https://www.apache.org/licenses/LICENSE-2.0).

## Bouncy Castle

`BouncyCastle.Cryptography.dll` (version 2.7.0, by The Legion of the
Bouncy Castle Inc.) is redistributed in binary form.

- Source: https://github.com/bcgit/bc-csharp
- License:

```
Copyright (c) 2000-2026 The Legion of the Bouncy Castle Inc. (https://www.bouncycastle.org).
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sub license, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions: The above copyright notice and this
permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

## MIT-licensed libraries

The following components are redistributed under the MIT License, reproduced
once at the end of this section:

| Component | Version | Copyright / project |
| --- | --- | --- |
| Phosphor Icons (rasterized) | 2.1.1 | 2023 Phosphor Icons (https://github.com/phosphor-icons/core) |
| Tabler Icons webfont (subset) | 3.46.0 | 2020-2026 Paweł Kuna (https://github.com/tabler/tabler-icons) |
| emojibase-data (catalog metadata) | 15.x | Miles Johnson (https://github.com/milesj/emojibase) |
| NAudio.Core / NAudio.WinMM / NAudio.Wasapi | 2.3.0 | Mark Heath (https://github.com/naudio/NAudio) |
| NetStone | 1.4.1 | 2024 goaaats, Koenari (https://github.com/xivapi/NetStone) |
| Vortice.Direct3D11 / Vortice.DXGI / Vortice.D3DCompiler / Vortice.DirectX | 3.8.3 | Amer Koleci (https://github.com/amerkoleci/Vortice.Windows) |
| Vortice.Mathematics | 2.1.0 | Amer Koleci (https://github.com/amerkoleci/Vortice.Mathematics) |
| SharpGen.Runtime / SharpGen.Runtime.COM | 2.4.2-beta | SharpGenTools contributors (https://github.com/SharpGenTools/SharpGenTools) |
| SharpDX / SharpDX.Direct3D11 / SharpDX.DXGI / SharpDX.D3DCompiler | 4.2.0 | Alexandre Mutel (https://github.com/sharpdx/SharpDX) |
| SharpCompress | 0.48.1 | Adam Hathcock (https://github.com/adamhathcock/sharpcompress) |
| YoutubeExplode | 6.6.1 | Oleksii Holub (https://github.com/Tyrrrz/YoutubeExplode) |
| HtmlAgilityPack | 1.11.46 | ZZZ Projects and contributors (https://github.com/zzzprojects/html-agility-pack) |
| System.Security.Cryptography.ProtectedData | 10.0.11 | Microsoft Corporation (https://github.com/dotnet/runtime) |
| NEbml | 1.1.0.5 | Oleg Zee (https://github.com/OlegZee/NEbml) |
| NLayer / NLayer.NAudioSupport | 2.0.1 | Mark Heath, Andrew Ward (https://github.com/naudio/NLayer) |
| Net.Codecrete.QrCodeGenerator | 2.0.6 | Manuel Bleichenbacher and Project Nayuki (https://github.com/manuelbl/QrCodeGenerator) |
| MeltySynth | 2.4.1 | 2021 Nobuaki Tanaka; 2017, 2018 Bernhard Schelling (TinySoundFont); 2014 Alex Veltsistas (C# Synth); 2012 Steve Folta (SFZero) (https://github.com/sinshu/meltysynth) |
| FF14 Fish Tracker App fish data (derived) | 2026-10 | 2019 icykoneko (https://github.com/icykoneko/ff14-fish-tracker-app) |

```
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Karashiiro.HtmlAgilityPack.CssSelectors.NetCoreFork

`Karashiiro.HtmlAgilityPack.CssSelectors.NetCoreFork.dll` (version 0.0.2, by
karashiiro and Thibaut Renoncourt) is a fork of HtmlAgilityPack.CssSelectors
pulled in by NetStone. The package declares no license metadata; the upstream
HtmlAgilityPack.CssSelectors project is published under the MIT License
(https://github.com/trenoncourt/HtmlAgilityPack.CssSelectors).

## Timed fish data

`src/Aetherphone/Fishing/TimedFish.txt` lists the catch conditions of timed and big
fish (Eorzea hours, weather, previous weather, bait chain, Fisher's Intuition,
hookset and tug) as game row ids. It is derived from the FF14 Fish Tracker App
data under the MIT License listed above. Names, places, weather rates and map
positions are read from the game's own data at runtime.

## Hunt data

The hunt catalogs under `src/Aetherphone/Hunts/` (`HuntMob.json`,
`HuntMobDescriptions.json`, `HuntMobRewards.json`, `HuntMobTips.json` and
`HuntPOI.json`: marks, zones and their points of interest, rewards,
descriptions and tips) were pulled from [Faloop](https://faloop.app) and ship
in every release archive. The live hunt status in the Hunts app is read from
Faloop's public service at runtime. Aetherphone is an independent client, not made, run, sponsored or
endorsed by Faloop.

## Runtime data services

These apps read live data from the services below while the plugin runs.
Their data is fetched on demand and does not ship with the plugin, and no
third-party credentials ship with it either.

- Calendar: in-game event dates served through the Aetherphone backend, which
  caches a community-maintained public events database.
- Strats: raid cheatsheets converted from the MIT-licensed
  [WTFDIG](https://github.com/mczub/wtfdig) project and served from
  Aetherphone's own media host.
- Market: prices, history and alerts from [Universalis](https://universalis.app).
- Music: synced lyrics from [LRCLIB](https://lrclib.net), radio stations from
  [radio-browser](https://www.radio-browser.info), live DJ listings from
  [Rolladeck](https://xivrolladeck.com), and songs from YouTube.
- MogCast: videos and playlists from YouTube and other sites yt-dlp supports.
- Venues: venues and events from [FFXIV Venues](https://ffxivvenues.com),
  [Partake](https://www.partake.gg) and Rolladeck.
- Hunts: live hunt status from Faloop, as described above.
- Collections: collectible data from [FFXIV Collect](https://ffxivcollect.com).
- News: Lodestone news from [lodestonenews.com](https://lodestonenews.com).
- Housing: plot listings from the Aetherphone housing service and, on the
  Chinese client, from 艾欧泽亚售楼中心 (house.ffxiv.cyou).
- Character profiles: The Lodestone, read with NetStone.

## Optional plugin integrations

Aetherphone can talk to the following Dalamud plugins through Dalamud's
inter-plugin communication (IPC). None of their code ships with Aetherphone.
Each integration only works when the user has installed that plugin
separately, and the matching feature stays unavailable otherwise.

- **Lifestream** by NightmareXIV (https://github.com/NightmareXIV/Lifestream):
  teleport, world travel and housing travel across the phone's apps and widgets.
- **Honorific** by Caraxi (https://github.com/Caraxi/Honorific): Settings >
  Nameplate Title shows your current Aetherphone status as your nameplate title.

## managed-doom (Doom engine)

The Doom mini-game runs on [managed-doom](https://github.com/sinshu/managed-doom), a C# port of the
Doom engine, compiled from the sources vendored under `src/ManagedDoom/` (upstream commit
`9365696eb44326a3aab72c4bab217f7db8a87c96`, desktop host removed, one data-directory hook added; see
`src/ManagedDoom/README.md`). The sound and music backends in `src/Aetherphone/Apps/Games/Doom/DoomSound.cs`
and `DoomMusic.cs` are derived from the upstream `SilkSound.cs` and `SilkMusic.cs`.

- Copyright (C) 1993-1996 Id Software, Inc.
- Copyright (C) 2019-2020 Nobuaki Tanaka
- License: GNU General Public License, version 2 or (at your option) any later version; full text in
  `src/ManagedDoom/LICENSE_ManagedDoom.txt`, shipped as `LICENSE_ManagedDoom.txt` in every release archive.

## MeltySynth

The Doom soundtrack is synthesized with [MeltySynth](https://github.com/sinshu/meltysynth) 2.4.1
(NuGet, redistributed as a compiled assembly).

- Copyright (C) 2021 Nobuaki Tanaka
- Copyright (C) 2017, 2018 Bernhard Schelling (TinySoundFont), based on SFZero, Copyright (C) 2012 Steve Folta
- Copyright (C) 2014 Alex Veltsistas (C# Synth)
- License: MIT; full text reproduced in the MIT section above.

## Doom game data and soundfont (downloaded on demand)

No Doom game data is bundled. When a player sets up the Doom mini-game, the plugin downloads three packages
into the player's own Aetherphone data folder, each only when the player asks for it:

- The Doom shareware episode (`doom1.wad`, version 1.9) from Debian's package archive
  (`doom-wad-shareware`). Copyright (C) 1993 id Software, Inc.; distributed under id Software's shareware
  terms, which permit free redistribution of the shareware episode.
- Freedoom 0.13.0 (`freedoom1.wad`, `freedoom2.wad`) from the Freedoom project's GitHub release, a free
  game that runs on the Doom engine. Copyright (c) 2001-2024 Contributors to the Freedoom project; License:
  BSD 3-Clause (the release archive's COPYING.txt). Source: https://freedoom.github.io/
- The TimGM6mb General MIDI soundfont (`TimGM6mb.sf2`) from Debian's `timgm6mb-soundfont` package.
  Copyright (C) 2004 Tim Brechbill; License: GNU General Public License, version 2.

Every file taken from these downloads is checked against a pinned SHA-256 checksum before use. Players may
place their own commercial IWAD (`DOOM.WAD`, `DOOM2.WAD`, `PLUTONIA.WAD`, `TNT.WAD`) or a Freedoom IWAD in the
same folder instead.

## SCOWL word lists

The English word bank of the Word Run mini-game (`src/Aetherphone/Words/en.answers.txt` and
`en.valid.txt`) is generated from SCOWL (Spell Checker Oriented Word Lists) 2020.12.07 by
`tools/build-word-banks.ps1`.

- Copyright 2000-2018 by Kevin Atkinson, with the additional copyrights listed in
  `src/Aetherphone/Words/SCOWL-Copyright.txt`, shipped next to the word lists in every release archive.
- Permission to use, copy, modify, distribute and sell these word lists, the associated scripts, the output
  created from the scripts, and its documentation for any purpose is hereby granted without fee, provided
  that the copyright notice appears in all copies.
- Source: http://wordlist.aspell.net/

## FrequencyWords

The German, Spanish, French and Portuguese word banks of the Word Run mini-game
(`src/Aetherphone/Words/de.*.txt`, `es.*.txt`, `fr.*.txt`, `pt.*.txt`) are derived from the OpenSubtitles
2018 frequency lists published in the FrequencyWords project by Hermit Dave, filtered to five-letter words
with accents normalized by `tools/build-word-banks.ps1`.

- Source: https://github.com/hermitdave/FrequencyWords
- License (content): Creative Commons Attribution-ShareAlike 4.0 International (CC BY-SA 4.0),
  https://creativecommons.org/licenses/by-sa/4.0/. Those four derived word-bank files are likewise available
  under CC BY-SA 4.0.
