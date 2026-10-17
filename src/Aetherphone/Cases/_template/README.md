# Case template

Artists work from the [case spec](https://aetherphone.net/case-spec/), check a finished case with the [case checker](https://aetherphone.net/case-checker/), and deliver it as the [Art guide](../../../../docs/art-guide.md) describes. This folder holds the engineering side. None of it ships: the build copies only the PNGs one folder up.

| File | What it is |
|---|---|
| ArtCaseTemplate.svg | The guide: silhouette, glass edge, alpha cutout, screen and the four hardware keys, traced from the engine's superellipse |
| generate-template.ps1 | Redraws the SVG and prints the band, glass and corner sizes |
| generate-case.ps1 | Bakes a reference case and its thumb in one of six styles (Tech, Ornate, Neon, Armor, Weave, Stone), for testing only |

Run both scripts with PowerShell 7 from this folder:

```powershell
pwsh ./generate-template.ps1
pwsh ./generate-case.ps1 -Id Probe -Style Tech -Top '#3A3F4B' -Bottom '#1C1F26' -Accent '#7FB2FF' -OutDir $env:TEMP
```

generate-template.ps1 always rewrites ArtCaseTemplate.svg next to itself. generate-case.ps1 writes `<Id>.png` and `<Id>.thumb.png`; without `-OutDir` it writes them into src/Aetherphone/Cases/, where the build ships them, so pass `-OutDir` for anything that is not meant to ship. Reference cases are test material, not shipped art.

generate-template.ps1 reads no code. It hard-codes its own copies of the canvas and margin (CaseArt.cs), the corner exponent (Squircle.cs), the art-case band and glass fractions (ChassisMetrics.cs), the key placements (DeviceChrome.cs) and the key proud shares (HardwareButton.cs). When any of those change, update the script, rerun it, and tell the maintainers, because the website's case pages carry the same numbers and every finished case has to be re-exported.

Landing a finished case in the plugin is the worked example in [Assets and media](../../../../docs/assets-and-media.md).
