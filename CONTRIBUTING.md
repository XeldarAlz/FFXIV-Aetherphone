# Contributing

Thanks for taking an interest. This is a small solo project, but PRs are welcome and I'll review them.

## Quick start

```bash
git clone https://github.com/XeldarAlz/FFXIV-Aetherphone.git
cd FFXIV-Aetherphone
dotnet build Aetherphone.sln -c Release
```

You need the .NET 10 SDK. The plugin requires Dalamud at runtime; CI pulls a Dalamud dev build automatically and that's enough to compile. See `.github/workflows/release.yml` if you want to reproduce CI locally.

Load the built plugin via `/xlsettings` -> **Experimental** -> **Dev Plugin Locations**, pointing at `src/Aetherphone/bin/Release/Aetherphone.dll`.

## Project layout

- `src/Aetherphone/Core/`: the device platform, meaning app framework and navigation, theming, messaging, notifications, character/contacts, game data readers.
- `src/Aetherphone/Apps/`: the phone's apps (Messages, Contacts, Settings, and friends), one folder each.
- `src/Aetherphone/Windows/`: the ImGui window plus a reusable `Components/` UI library.
- `src/Aetherphone/`: plugin entry point, config, command wiring.
- `docs/`: product (`DESIGN.md`) and technical (`ARCHITECTURE.md`) docs.

Keep logic small and direct, and prefer the existing `Components/` over hand-rolling one-off UI.

## Before you open a PR

1. `dotnet build -c Release` cleanly.
2. Test in-game: open the phone with `/phone`, exercise the app or screen you touched, and watch a real notification/tell flow through if you changed messaging.
3. Keep the diff focused. One concern per PR.
4. Match the existing style. Code is self-documenting; comments explain *why* (or a hard-coded constant), never *what*. No heavy abstractions "for later."
5. If your change affects what a user sees or types (commands, layout, settings), update the README.

## Good first issues

Check the tracker for anything labeled `good first issue`. Self-contained UI work is usually the lowest-friction way to help: a new `Components/` widget, a Settings page, or polishing an existing app's layout. Attach a screenshot of before/after and the change is easy to land.

## Security

Please don't file public issues for security problems; see [SECURITY.md](SECURITY.md).

## Code of conduct

See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md). Be decent.

## License

By contributing, you agree your contributions are licensed under AGPL-3.0-or-later, the same as the project.
