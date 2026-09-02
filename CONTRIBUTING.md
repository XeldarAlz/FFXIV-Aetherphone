# Contributing

Thanks for taking an interest. PRs are welcome and get reviewed.

## Quick start

```bash
git clone https://github.com/XeldarAlz/FFXIV-Aetherphone.git
cd FFXIV-Aetherphone
dotnet build Aetherphone.sln -c Release
```

You need the .NET 10 SDK, and compiling also needs the Dalamud assemblies on disk. On Windows an XIVLauncher install provides them; elsewhere, or for a custom location, point the `DALAMUD_HOME` environment variable at them. CI downloads the latest Dalamud distribution for you. See `.github/workflows/ci.yml` if you want to reproduce what a PR runs locally; [docs/getting-started.md](docs/getting-started.md) walks through it.

Load the built plugin via `/xlsettings` -> **Experimental** -> **Dev Plugin Locations**, pointing at `src/Aetherphone/bin/Release/Aetherphone.dll`.

No game on this machine? [docs/harness.md](docs/harness.md) describes the headless preview harness: one bootstrap downloads Dalamud into a local cache, and `tools/harness/aep serve` renders and drives the phone on macOS, Windows, or Linux.

A `Debug` build produces a separate side-by-side plugin instead: `src/Aetherphone/bin/Debug/AetherphoneDev.dll`, loaded as `AetherphoneDev`, opened with `/phonedev`, with its own config and pointed at the development Aethernet instance. A Beta build (`dotnet build Aetherphone.sln -c Release -p:AetherphoneBeta=true`) is a third side-by-side plugin, `AetherphoneBeta.dll`, opened with `/phonebeta` and also on the development instance by default. Register whichever path you build.

## Project layout

- `src/Aetherphone/Core/`: the device platform: app framework and navigation, theming, messaging, notifications, character/contacts, game data readers, and the shared services (networking, crypto, media, localization) the apps build on.
- `src/Aetherphone/Apps/`: the phone's apps, one folder each.
- `src/Aetherphone/Windows/`: the ImGui windows plus a reusable `Components/` UI library.
- `src/Aetherphone/`: plugin entry point, config, command wiring.
- `src/Aetherphone.Tests/`: the xUnit test suite.
- `src/ManagedDoom/`: a vendored Doom port with its own layout, exempt from the code-style enforcement and the namespace check.
- `tools/`: generators for the bundled assets (app icons, the icon font, emoji, sounds, wallpapers) and the README media.

Keep logic small and direct, and prefer the existing `Components/` over hand-rolling one-off UI.

## Documentation

The full developer documentation lives in [`docs/`](docs/README.md): the architecture map, the app framework, the UI toolkit, a step-by-step [tutorial for building your own app](docs/creating-an-app.md), and the [conventions](docs/conventions.md) your PR will be reviewed against. New here? Start with [Getting started](docs/getting-started.md).

## Before you open a PR

1. `dotnet build -c Release` with zero warnings. CI treats every warning, code-style rules included, as an error.
2. Test in-game: open the phone with `/phone`, exercise the app or screen you touched, and watch a real notification/tell flow through if you changed messaging.
3. Keep the diff focused. One concern per PR.
4. Match the existing style; the rules are written down in [docs/conventions.md](docs/conventions.md). Code is self-documenting; comments explain *why* (or a hard-coded constant), never *what*. No heavy abstractions "for later."
5. If your change affects what a user sees or types (commands, layout, settings), update the README.
6. If you used AI beyond autocomplete, say which level in the PR description; if you added an asset, say where it came from. Both are one line, and [docs/ai-usage.md](docs/ai-usage.md) explains the level names and why we ask.
7. Open the PR against `dev`, the default branch. `master` only moves when a release is cut.

## Translations

Improving one of the nine language files needs no C#, no build, and no git. You edit a single JSON file under `src/Aetherphone/Localization/` in the browser and open a pull request from there. [docs/translating.md](docs/translating.md) walks a non-developer through the whole flow, including the rules that keep the nine catalogs in lockstep. Adding a *new* string is a code change and follows [docs/localization.md](docs/localization.md) instead.

## Good first issues

Starter tasks carry the `good first issue` label when there are any. Self-contained UI work is usually the lowest-friction way to help: a new `Components/` widget, a Settings page, or polishing an existing app's layout. Attach a screenshot of before/after and the change is easy to land.

## Security

Please don't file public issues for security problems; see [SECURITY.md](.github/SECURITY.md).

## Code of conduct

See [CODE_OF_CONDUCT.md](.github/CODE_OF_CONDUCT.md).

## License

By contributing, you agree your contributions are licensed under AGPL-3.0-or-later, the same as the project.
