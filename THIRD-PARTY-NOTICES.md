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

- Homepage: https://tabler.io/icons
- Source: https://github.com/tabler/tabler-icons
- License: MIT (Copyright (c) 2020-2026 Paweł Kuna); full text reproduced in
  the MIT section below.

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

## MIT-licensed libraries

The following components are redistributed under the MIT License, reproduced
once at the end of this section:

| Component | Version | Copyright / project |
| --- | --- | --- |
| Tabler Icons (rasterized) | n/a | 2020-2026 Paweł Kuna (https://github.com/tabler/tabler-icons) |
| NAudio.Core / NAudio.WinMM / NAudio.Wasapi | 2.3.0 | Mark Heath (https://github.com/naudio/NAudio) |
| NetStone | 1.4.1 | 2024 goaaats, Koenari (https://github.com/xivapi/NetStone) |
| Vortice.Direct3D11 / Vortice.DXGI / Vortice.DirectX | 3.8.3 | Amer Koleci (https://github.com/amerkoleci/Vortice.Windows) |
| Vortice.Mathematics | 2.1.0 | Amer Koleci (https://github.com/amerkoleci/Vortice.Mathematics) |
| SharpGen.Runtime / SharpGen.Runtime.COM | 2.4.2-beta | SharpGenTools contributors (https://github.com/SharpGenTools/SharpGenTools) |
| YoutubeExplode | 6.6.0 | Oleksii Holub (https://github.com/Tyrrrz/YoutubeExplode) |
| JsonExtensions | 1.2.0 | Oleksii Holub (https://github.com/Tyrrrz/JsonExtensions) |
| AngleSharp | 1.4.0 | AngleSharp contributors (https://github.com/AngleSharp/AngleSharp) |
| HtmlAgilityPack | 1.11.74 | ZZZ Projects and contributors (https://github.com/zzzprojects/html-agility-pack) |
| System.Security.Cryptography.ProtectedData | 10.0.0 | Microsoft Corporation (https://github.com/dotnet/runtime) |
| NEbml | 0.11.0 | Oleg Zee (https://github.com/Oleg-Zee/NEbml) |

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
