# Getting started

This page takes you from `git clone` to a development build of Aetherphone running inside Final Fantasy XIV. Read it first. It covers what to install, the three build flavors, loading a build in the game, the chat commands, the edit and reload loop, logs, and what to do when a step fails. It describes the client plugin only; the Aethernet online service lives in its own repository.

## Key files

| Path | Role |
|---|---|
| Aetherphone.sln | The plugin, the tests, and the vendored ManagedDoom engine |
| src/Aetherphone/Aetherphone.csproj | The plugin project: flavor names and the asset folders copied next to the dll |
| Directory.Build.props | Settings every project shares: version, code style in the build, CI warnings as errors |
| src/Aetherphone/Plugin.cs | The entry point Dalamud constructs |
| src/Aetherphone/PhoneCommands.cs | What each chat command does |
| src/Aetherphone/Core/Commands/PhoneCommandCatalog.cs | The player-facing command list behind help and the Commands page |
| src/Aetherphone/Core/AepConstants.cs | The plugin name and command strings of each flavor |

## Prerequisites

- **Final Fantasy XIV** and **[XIVLauncher](https://goatcorp.github.io/)** with Dalamud enabled. XIVLauncher is the community launcher that loads Dalamud into the game.
- **The .NET 10 SDK.** The projects use the preview C# language version, so keep the SDK current.
- **Windows** is the main platform. On macOS the build finds Dalamud where XIV on Mac keeps it. On Linux you set `DALAMUD_HOME` (see [Troubleshooting](#troubleshooting)).

You do not need to run the backend. The build talks to an existing server, and the offline apps work without signing in.

**Dalamud** is the plugin framework XIVLauncher loads into the game. It gives plugins a C# API over chat, game data, the object table, textures, and rendering. Aetherphone's `Plugin` class implements `IDalamudPlugin`, and Dalamud fills its `[PluginService]` properties with game services. The project builds on the `Dalamud.NET.Sdk`, which references the Dalamud assemblies from your local install and packages the output.

**Dear ImGui** is an immediate mode UI library: nothing is kept between frames, and the plugin declares the whole phone again every frame. Widgets are methods you call each frame, not objects you keep, and draw code must not allocate per frame (see [Conventions](conventions.md)).

## Clone and build

```bash
git clone https://github.com/XeldarAlz/FFXIV-Aetherphone.git
cd FFXIV-Aetherphone
dotnet build Aetherphone.sln -c Release
```

If XIVLauncher has started the game on this machine with Dalamud enabled, the Dalamud assemblies are in place and the build works as is. The build copies the asset folders (fonts, icons, sounds, wallpapers, translations, data files) next to the dll, so a changed asset reaches the game only after a rebuild.

## Release, Debug, and Beta

The project builds three flavors. Each has its own internal name, so Dalamud treats them as three plugins with separate settings, and all three can be loaded at once.

| Flavor | Build with | Dll to register | Command | Server |
|---|---|---|---|---|
| Release | `-c Release` | src/Aetherphone/bin/Release/Aetherphone.dll | `/phone` | Live |
| Debug | `-c Debug` | src/Aetherphone/bin/Debug/AetherphoneDev.dll | `/phonedev` | Development |
| Beta | `-c Release -p:AetherphoneBeta=true` | src/Aetherphone/bin/Release/AetherphoneBeta.dll | `/phonebeta` | Development |

Each command also has a long alias: `/aetherphone`, `/aetherphonedev`, `/aetherphonebeta`.

**Release** is the plugin players get. It has the same internal name as the copy from the custom repository, so it shares that copy's settings and cannot load beside it. CI builds Release, so build it once before you open a pull request.

**Debug** is the flavor for daily work. It loads beside your normal copy, keeps its own settings, and talks to the development server, so test accounts and posts never reach live players. `dotnet build` without `-c` builds Debug.

**Beta** is what beta testers run: a Release build with the `BETA` symbol and its own manifest. Debug and Beta both show a Test server row in Settings, About, which switches between the live and development servers without a rebuild and signs you out. [Networking](networking.md) explains how a build picks its server.

## Load it in the game

1. Start the game through XIVLauncher.
2. Type `/xlsettings` and open the **Experimental** tab.
3. Under **Dev Plugin Locations**, add the full path of the dll from the table above and save. You can register all three.
4. Type `/xlplugins`, find the plugin among your installed plugins (marked as a dev plugin), and enable it. Debug is listed as Aetherphone Development and Beta as Aetherphone Beta.
5. Type the flavor's command, for example `/phonedev`.

The first open plays a boot animation while the fonts finish building, then a setup flow. Signing in is optional: choose **Set Up Later** on the Aethernet Account page and finish the remaining pages. The offline apps work without an account.

## Chat commands

`/phone` alone opens or closes the phone. Everyday forms:

| Command | Effect |
|---|---|
| `/phone mini` | Switch between the full and the mini phone |
| `/phone open <app>` | Open an app by name |
| `/phone settings` | Open Settings |
| `/phone reset` | Move the phone back to the center of the screen |
| `/phone test` | Send a sample notification |
| `/phone help` | Print every player-facing command in chat |

On Debug and Beta builds, use `/phonedev` or `/phonebeta` instead of `/phone`.

`/phone help` and the Settings, Commands page both read PhoneCommandCatalog, so they always list the same commands. In PhoneCommands.cs, the first word after the command is the verb, and it must match a known verb exactly (case does not matter). Anything else prints "Unknown command" and points to `/phone help`. So `/phone marketboard` is unknown, while `/phone market board` opens Market and searches for "board".

Two developer tools stay out of the catalog: `/phone videodebug` opens the video decode panel, and `/phone perfhud` toggles a frame-time readout above the phone.

To add a command, add a `case` to `PhoneCommands.Run`, add an entry with a localized description to PhoneCommandCatalog (see [Localization](localization.md)), and update the command list in README.md.

## The dev loop

1. Edit code.
2. Build the flavor you registered. The game can keep running.
3. Reload the plugin in `/xlplugins`, or turn on the dev plugin's automatic reloading toggle there so Dalamud reloads it whenever the dll changes.
4. Open the phone and try your change.

A reload calls `Plugin.Dispose()`, then constructs a fresh `Plugin`. Anything the constructor registers (an event handler, a command, a window) must be released in `Dispose` and in `TearDownPartialConstruction`, which runs when the constructor throws. A missed release doubles up on the next reload.

## Logs

- `/xllog` opens Dalamud's log window; filter it to Aetherphone.
- The same log goes to dalamud.log in %AppData%\XIVLauncher.
- In code, use `AepLog`: `AepLog.Warning("[market] price fetch failed")`, or `AepLog.Error(exception, "...")` to include the exception. A bracketed tag at the start makes lines easy to filter.

## Repo tour

| Path | Contents |
|---|---|
| src/Aetherphone/Core/ | The device platform: app framework, shell, services, networking, game data |
| src/Aetherphone/Apps/ | The phone's apps, one folder each |
| src/Aetherphone/Windows/ | The phone window, pop-out windows, the UI component library in Components/, and the home screen widget plumbing in Widgets/ |
| src/Aetherphone.Tests/ | The xUnit tests, run with `dotnet test` |
| src/ManagedDoom/ | The vendored engine behind the Doom game, kept in its own style |
| tools/ | Generators for icons, emoji, sounds, wallpapers, word lists, and README media |

The asset folders sit beside Core/ in src/Aetherphone/. [Architecture](architecture.md) explains how the pieces fit together.

## How CI relates to your local build

CI builds Release, runs the tests, and first runs fast guard checks over the source. It also treats every warning as an error, while a local build only prints them so work in progress compiles. If your Release build has no warnings and `dotnet test` passes, only the guards and the pull request title check can still fail; [Testing and release](testing-and-release.md) lists them.

## Troubleshooting

### "Dalamud installation not found"

The build stops with `Dalamud.NET.Sdk: Dalamud installation not found at <path>` when the expected folder does not exist. The SDK looks in %AppData%\XIVLauncher\addon\Hooks\dev on Windows and in XIV on Mac's dalamud/Hooks/dev folder on macOS. A `DALAMUD_HOME` environment variable overrides both.

To fix it, start the game once through XIVLauncher with Dalamud enabled, which downloads Dalamud there, or set `DALAMUD_HOME` to a folder that contains Dalamud.dll. On Linux, Aetherphone.csproj reads the path only from `DALAMUD_HOME`; if it is unset, you get missing Dalamud types instead of this message.

### Two copies of Aetherphone

If you play with Aetherphone from the custom repository, disable that copy in `/xlplugins` before you enable the Release dev build. Both are named Aetherphone, so Dalamud will not load the second one, and they share one settings file. Enable your normal copy again afterwards. Debug and Beta load beside it without this step.

### My change does not show

- Check that the dll you built is the one you registered. A Debug build does not replace the Release dll, so the game keeps your earlier build without any error.
- Reload the plugin, or turn on automatic reloading.
- Rebuild after changing an asset.

### The plugin does not load

Open `/xllog`. The constructor cleans up what it built and rethrows, so the log shows the original exception.

### A SixLabors license error

The image library reads its license from sixlabors.lic at the repo root. Keep that file in place and unchanged.

## Gotchas

- **Restore is locked.** The plugin and ManagedDoom restore from a committed packages.lock.json, and CI restores in locked mode. After you add or bump a package there, run `dotnet restore` and commit the updated lock file.
- **Leave the version alone.** CI checks the version in Directory.Build.props against repo.json, and only a release bumps it.

## Related docs

- [Architecture](architecture.md): the next read, covering boot, threads, the frame loop, and the shell.
- [Creating an app](creating-an-app.md): build a small phone app end to end.
- [Conventions](conventions.md): the code style and performance rules a review checks.
- [Testing and release](testing-and-release.md): tests, CI, and the release pipeline.
- [State and persistence](state-and-persistence.md): where settings live and how to reset them.
