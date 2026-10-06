# Case template

Artists work from the [case spec](https://aetherphone.net/case-spec/), which also offers the Clip Studio
guide and the exact band and outline overlays, and check a finished case with the
[case checker](https://aetherphone.net/case-checker/). This folder holds the engineering side.

| File | What it is |
|---|---|
| `ArtCaseTemplate.svg` | The guide template: silhouette, glass edge, alpha cutout, screen and the four hardware keys, traced from the engine's superellipse |
| `generate-template.ps1` | Regenerates the SVG from its own hardcoded copies of the `ChassisMetrics` fractions and the `DeviceChrome` key placements |
| `generate-case.ps1` | Generates conforming reference cases in six styles: Tech, Ornate, Neon, Armor, Weave, Stone |

Nothing ties these scripts to the code. When `Core/Theme/ChassisMetrics.cs`,
`Windows/Components/Chrome/DeviceChrome.cs` or `Windows/Components/Chrome/HardwareButton.cs` changes,
update `generate-template.ps1`, regenerate the SVG, and update the website's copy of the same numbers
(see [art assets](../../../../docs/ART-ASSET-SPEC.md#keeping-the-website-in-sync)).

Adding a finished case to the plugin is covered in [assets and media](../../../../docs/assets-and-media.md#to-add-a-case).
