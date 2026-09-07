# Third-Party Notices

This project bundles or depends on third-party software. Their licenses and
required notices are reproduced below. This file is shipped inside every
release archive alongside the components it covers.

---

## Inter font family

The fonts under `src/Aetherphone/Fonts/` (Inter Regular, Medium, SemiBold,
Bold) are redistributed unmodified.

- Copyright (c) 2016 The Inter Project Authors (https://github.com/rsms/inter)
- License: SIL Open Font License 1.1
- Full license text: `src/Aetherphone/Fonts/Inter-OFL.txt`, shipped next to
  the fonts in every release archive.

## Tabler Icons

The application icons under `src/Aetherphone/Icons/` are derived from
[Tabler Icons](https://tabler.io/icons) (recolored and rasterized to PNG).
`src/Aetherphone/Fonts/TablerIcons.ttf` is a 97 glyph subset of the same
project's webfont, remapped into a private codepoint range; see
`tools/icon-font/` for the generator.

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

## Interface sounds

The interface sound clips under `src/Aetherphone/Sounds/Ui/` come from two
sources, re-encoded to 48 kHz PCM WAV and level-matched:

Most clips (taps, toggles, transitions, send, caution, blocked, success,
keystrokes) are from the SND01 "sine" kit of [SND](https://snd.dev),
designed by Yasuhiro Tsuchiya.

- Copyright DENTSU INC. and STARRYWORKS inc.; audio copyright remains with
  the credited sound designer
- Source: https://github.com/snd-lib/snd-lib
- License: free for commercial and non-commercial use per the SND terms
  (https://snd.dev); credit requested, provided here

The remaining clips are public domain:

- `shutter.wav`: "Trigger of camera 1" from
  [BigSoundBank](https://bigsoundbank.com/trigger-of-camera-1-s2394.html),
  by Joseph Sardin, CC0
- `coin.wav`: "chips-stack-1" from
  [Kenney Casino Audio](https://kenney.nl/assets/casino-audio), CC0

The mini-game clips under `src/Aetherphone/Sounds/Games/` are Creative Commons
Zero (CC0) by [Kenney](https://kenney.nl), re-encoded to mono 48 kHz PCM WAV
and level-matched:

- [Impact Sounds](https://kenney.nl/assets/impact-sounds): hits, breaks,
  explosions
- [Digital Audio](https://kenney.nl/assets/digital-audio): retro blips,
  lasers, jumps, power-ups
- [Casino Audio](https://kenney.nl/assets/casino-audio): card sounds
- [Interface Sounds](https://kenney.nl/assets/interface-sounds): clicks,
  ticks, errors

The four `simon_*.wav` tones are sine waves synthesized for this plugin with
ffmpeg (E3, A3, C#4, E4) and carry no third-party rights.

## mpv

libmpv provides video decoding and playback for the AetherStream app. No mpv
binary is redistributed with this plugin:
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

yt-dlp is used by mpv's own `ytdl_hook` to resolve video URLs from sites other
than YouTube (YouTube itself is resolved separately via YoutubeExplode, already
a dependency). As with mpv above, no yt-dlp binary is redistributed: it is
downloaded from the project's own GitHub releases into the plugin's Dalamud
config directory on first use.

- Homepage: https://github.com/yt-dlp/yt-dlp
- License: The Unlicense (public domain)

## AlphaChannel (Voudi)

AetherStream's video/screen engine under `src/Aetherphone/Core/Video/`
(mpv-backed playback and the world-anchored ScreenPainter D3D11 quad
renderer) is ported from
[AlphaChannel](https://github.com/Voudi/AlphaChannel) by Voudi, used with the
author's permission. Two smaller pieces ported from the same source live
outside that directory: the screen placement controls and presets in
`src/Aetherphone/Apps/AetherStream/AetherStreamApp.Casting.cs` (from
AlphaChannel's `ControlWindow.DrawScreenPositionSettings`) and the saved
screen preset shape in `src/Aetherphone/Configuration.cs` (from its
`Configuration`, with yaw added). Both are modified from the originals.

- Source: https://github.com/Voudi/AlphaChannel
- License: GNU General Public License v3.0 or later; full text reproduced in
  `src/Aetherphone/Core/Video/AlphaChannel-LICENSE`.

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
Copyright (c) 2000-2025 The Legion of the Bouncy Castle Inc. (https://www.bouncycastle.org).
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
| Tabler Icons (rasterized) | n/a | 2020-2026 Paweł Kuna (https://github.com/tabler/tabler-icons) |
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

## Calendar event data

The Calendar app shows in-game event dates served through the Aetherphone
backend, which caches a community-maintained public events database. The data
is fetched server-side; no third-party credentials ship with the plugin.

## managed-doom (Doom engine)

The Doom mini-game runs on [managed-doom](https://github.com/sinshu/managed-doom), a C# port of the
Doom engine, compiled from the sources vendored under `src/ManagedDoom/` (upstream commit
`9365696eb44326a3aab72c4bab217f7db8a87c96`, desktop host removed, one data-directory hook added; see
`src/ManagedDoom/README.md`). The sound and music backends in `src/Aetherphone/Apps/Games/Doom/DoomSound.cs`
and `DoomMusic.cs` are derived from the upstream `SilkSound.cs` and `SilkMusic.cs`.

- Copyright (C) 1993-1996 Id Software, Inc.
- Copyright (C) 2019-2020 Nobuaki Tanaka
- License: GNU General Public License, version 2 or (at your option) any later version; full text in
  `src/ManagedDoom/LICENSE_ManagedDoom.txt`, shipped in every release archive.

## MeltySynth

The Doom soundtrack is synthesized with [MeltySynth](https://github.com/sinshu/meltysynth) 2.4.1
(NuGet, redistributed as a compiled assembly).

- Copyright (c) 2021 Nobuaki Tanaka
- License: MIT; full text reproduced in the MIT section below.

## Doom game data and soundfont (downloaded on demand)

No Doom game data is bundled. When a player sets up the Doom mini-game, the plugin downloads two files
into the player's own Aetherphone data folder, each only when the player asks for it:

- The Doom shareware episode (`doom1.wad`, version 1.9) from Debian's package archive
  (`doom-wad-shareware`). Copyright (C) 1993 id Software, Inc.; distributed under id Software's shareware
  terms, which permit free redistribution of the shareware episode.
- Freedoom 0.13.0 (`freedoom1.wad`, `freedoom2.wad`) from the Freedoom project's GitHub release, a free
  game that runs on the Doom engine. Copyright (c) 2001-2024 Contributors to the Freedoom project; License:
  BSD 3-Clause (the release archive's COPYING.txt). Source: https://freedoom.github.io/
- The TimGM6mb General MIDI soundfont (`TimGM6mb.sf2`) from Debian's `timgm6mb-soundfont` package.
  Copyright (C) 2004 Tim Brechbill; License: GNU General Public License, version 2.

Both downloads are verified against a known checksum before use. Players may place their own commercial
IWAD (`DOOM.WAD`, `DOOM2.WAD`, `PLUTONIA.WAD`, `TNT.WAD`) or a Freedoom IWAD in the same folder instead.

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
