# Getting started

This page takes you from `git clone` to a running development build of Aetherphone inside Final Fantasy XIV. Read it first, before any other doc: it covers prerequisites, what Dalamud and Dear ImGui are, building the solution, loading the result as a dev plugin, the chat commands, the edit-build-reload loop, where logs go, and how the repo is laid out. Everything here describes the client plugin only; the Aethernet backend is a separate ASP.NET service that lives in its own repository and is not covered by these docs.

## Key files

| Path | Role |
|---|---|
| `Aetherphone.sln` | Solution: the plugin project plus the test project |
| `Directory.Build.props` | Shared MSBuild settings, including the single `<Version>` that drives releases |
| `src/Aetherphone/Aetherphone.csproj` | The plugin project, built on `Dalamud.NET.Sdk/15.0.0` |
| `src/Aetherphone/Plugin.cs` | Plugin entry point: service wiring, window setup, command registration |
| `src/Aetherphone/Core/AepConstants.cs` | Plugin name and the command strings (`/phone`, `/aetherphone`) |
| `src/Aetherphone/Core/AepLog.cs` | Thin logging wrapper over Dalamud's `IPluginLog` |
| `src/Aetherphone/Windows/PhoneWindow.cs` | The ImGui window that renders the whole phone |
| `.github/workflows/ci.yml` | CI: version-sync guard, Release build, tests |
| `CONTRIBUTING.md` | PR expectations and the short version of this page |

## Prerequisites

- **Windows** with **Final Fantasy XIV** installed. The plugin project targets Windows; the game is where you test.
- **[XIVLauncher](https://goatcorp.github.io/)** with **Dalamud** enabled. XIVLauncher is the community launcher for FFXIV, and it injects Dalamud into the game at startup.
- **.NET 10 SDK**. CI pins `dotnet-version: '10.0.x'` in `.github/workflows/ci.yml`, and both projects set `<LangVersion>preview</LangVersion>`, so use a current SDK release.

You do not need the backend to build or run the plugin. Apps that talk to Aethernet degrade to offline behavior when the service is unreachable.

### What is Dalamud?

Dalamud is a community plugin framework that XIVLauncher injects into the running game process. It gives plugins a C# API over the game: chat, game data, the object table, and a rendering hook. A plugin is a class implementing `IDalamudPlugin` (here, `Plugin` in `src/Aetherphone/Plugin.cs`), and Dalamud fills in game services through `[PluginService]` properties:

```csharp
[PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
[PluginService] internal static IPluginLog Log { get; private set; } = null!;
```

The project uses the `Dalamud.NET.Sdk` MSBuild SDK (see `src/Aetherphone/Aetherphone.csproj`), which supplies the Dalamud and ImGui assembly references and packages the build output into a plugin zip. On Windows it finds those assemblies where XIVLauncher keeps its current Dalamud build (`%AppData%\XIVLauncher\addon\Hooks\dev`); on Linux the csproj resolves them from a `DALAMUD_HOME` environment variable instead.

### What is Dear ImGui?

Dear ImGui is an immediate mode GUI library. There is no retained widget tree and no event system to subscribe to: every frame, the game calls back into the plugin, and the plugin re-declares the entire UI from scratch as a sequence of draw calls. `PhoneWindow` (in `src/Aetherphone/Windows/PhoneWindow.cs`) extends Dalamud's `Window` class, and its `Draw()` override runs once per rendered frame.

Practical consequences you will see everywhere in this codebase:

- UI "widgets" are methods you call each frame, not objects you construct once. The reusable ones live in `src/Aetherphone/Windows/Components/`.
- All state (which app is open, scroll positions, text being typed) lives in plain fields on services and views, because the UI itself remembers nothing between frames.
- Draw code runs 60+ times per second, so it must avoid per-frame allocations. See [Conventions](conventions.md) before writing any.
- The bindings come from the `Dalamud.Bindings.ImGui` namespace, shipped with Dalamud.

## Clone and build

```bash
git clone https://github.com/XeldarAlz/FFXIV-Aetherphone.git
cd FFXIV-Aetherphone
dotnet build Aetherphone.sln -c Release
```

The build needs the Dalamud assemblies described above. If XIVLauncher has run on your machine with Dalamud enabled, they are already in place and the build works out of the box.

The output lands in `src/Aetherphone/bin/Release/`: the loadable `Aetherphone.dll` plus a packaged `Aetherphone/latest.zip` that the release pipeline ships. Asset folders (`Fonts`, `Icons`, `Emoji`, `Cases`, `Localization`, `Wallpapers`, `Sounds`) are copied next to the dll by the `<Content>` items in `src/Aetherphone/Aetherphone.csproj`.

### Why Release, not Debug?

Build `Release`. It is the configuration that CONTRIBUTING.md, CI, and the release workflow all use, and (next section) it is the path you register with Dalamud. The dev plugin location is a fixed file path: if you later run a `Debug` build, the output goes to `bin/Debug/` instead, the file at the registered `bin/Release/` path never changes, and the game silently keeps loading your previous build. Sticking to `-c Release` for the whole dev loop avoids that trap.

## Load it in the game

1. Launch the game through XIVLauncher.
2. Type `/xlsettings` in chat to open Dalamud's settings.
3. Open the **Experimental** tab and find **Dev Plugin Locations**.
4. Add the full path to your built dll, ending in `src/Aetherphone/bin/Release/Aetherphone.dll`, and save.
5. Open the plugin installer with `/xlplugins`. Aetherphone now appears as a dev plugin; enable it.
6. Type `/phone`. The phone opens.

## Chat commands

`Plugin.cs` registers two handlers in the constructor, using the strings from `src/Aetherphone/Core/AepConstants.cs`:

```csharp
CommandManager.AddHandler(AepConstants.PrimaryCommand, primaryCommand);
CommandManager.AddHandler(AepConstants.AliasCommand, aliasCommand);
```

Both run the same `OnCommand` method, which dispatches on the argument:

| Command | Effect |
|---|---|
| `/phone` | Toggle the phone open or closed |
| `/aetherphone` | Alias for `/phone`, same handler |
| `/phone reset` | Recenter the phone window on screen |
| `/phone market [item]` | Open the Market app, optionally searching for the item |
| `/phone test` | Fire a sample notification (useful when testing notification UI) |

Any argument `OnCommand` does not recognize falls through to the plain toggle. The same list is shown in-game by the Settings app (`src/Aetherphone/Apps/Settings/Pages/CommandsPage.cs`); if you add a command, update that page too.

## The dev loop

1. Edit code.
2. `dotnet build Aetherphone.sln -c Release`. The game can stay running; the dll on disk is not locked.
3. In game, open `/xlplugins`, find Aetherphone in the dev plugins list, and reload it (or disable and re-enable it).
4. `/phone`, exercise your change, watch the log for errors.

Reloading calls `Plugin.Dispose()`, which unhooks every event handler and command before the new build constructs a fresh `Plugin`. If your change adds a hook or a window, mirror it in `Dispose` or reloads will leak the old registration.

## Logs

- `/xllog` in game opens Dalamud's log console. Filter it to Aetherphone to see only this plugin.
- Dalamud also writes the same stream to `dalamud.log` inside the `XIVLauncher` folder under `%AppData%`.
- In code, log through `Plugin.Log` (Dalamud's `IPluginLog`) or the shorthand wrapper `AepLog` in `src/Aetherphone/Core/AepLog.cs`:

```csharp
internal static class AepLog
{
    public static void Debug(string message) => Plugin.Log.Debug(message);

    public static void Info(string message) => Plugin.Log.Information(message);

    public static void Warning(string message) => Plugin.Log.Warning(message);

    public static void Error(string message) => Plugin.Log.Error(message);
}
```

If the plugin fails to load at all, the cause is in `/xllog`: the `Plugin` constructor wraps its setup in a try/catch that tears down partial construction and rethrows, so Dalamud reports the original exception.

## Repo tour

| Path | Contents |
|---|---|
| `src/Aetherphone/Core/` | The device platform: app framework and navigation (`Core/Apps/`), shell and home screen (`Core/Shell/`, `Core/Home/`), notifications, theming, localization, networking (`Core/Aethernet/`, `Core/Net/`), crypto, media, game-data readers |
| `src/Aetherphone/Apps/` | The phone apps, one folder each (Message, Chirper, Settings, Market, Games, ...) |
| `src/Aetherphone/Windows/` | `PhoneWindow.cs` plus the reusable widget library in `Components/` and `Widgets/` |
| `src/Aetherphone/Fonts/`, `Emoji/`, `Icons/`, `Images/`, `Sounds/`, `Wallpapers/`, `Cases/`, `Localization/` | Bundled assets, copied to the output folder at build time |
| `src/Aetherphone.Tests/` | xUnit tests, run with `dotnet test` and in CI |
| `tools/` | Asset generators (`emoji-generator`, `icon-generator`) |
| `docs/` | These docs |

Prefer an existing `Components/` widget over hand-rolling one-off UI; that is the single strongest style expectation in `CONTRIBUTING.md`.

## How CI relates to your local build

`.github/workflows/ci.yml` runs on every push and pull request to `master`, in two jobs:

1. **Guards**: checks that `<Version>` in `Directory.Build.props` matches `AssemblyVersion` and `TestingAssemblyVersion` in `repo.json`. This fails fast, before any compilation.
2. **Build (Windows)**: downloads the latest Dalamud dev build from `goatcorp.github.io/dalamud-distrib` into the standard `XIVLauncher\addon\Hooks\dev` path (recreating what XIVLauncher provides on your machine), then runs `dotnet restore Aetherphone.sln --locked-mode`, `dotnet build --configuration Release`, and `dotnet test`.

So CI is your local build with the Dalamud dependency fetched explicitly. If `dotnet build -c Release` and `dotnet test` pass locally, the only extra ways CI can fail are the version-sync guard and the locked restore (see Gotchas). The release pipeline (`.github/workflows/release.yml`) is separate and covered in [Testing and release](testing-and-release.md).

## Gotchas

- **A Debug build never reaches the game.** The dev plugin location points at `bin/Release/Aetherphone.dll`; `dotnet build` without `-c Release` writes to `bin/Debug/` and Dalamud keeps loading your stale Release dll with no error anywhere.
- **NuGet restore is locked.** `Directory.Build.props` sets `RestorePackagesWithLockFile`, and CI restores with `--locked-mode`. If you add or bump a `PackageReference`, run `dotnet restore` locally so `packages.lock.json` updates and gets committed, or CI fails at the restore step.
- **Version lives in three places.** Bumping `<Version>` in `Directory.Build.props` without updating `AssemblyVersion` and `TestingAssemblyVersion` in `repo.json` fails the CI guards job before the build even starts.
- **Unknown command arguments are silent.** `OnCommand` in `Plugin.cs` treats anything that is not `test`, `reset`, or `market` as a plain toggle, so a typo like `/phone rset` opens or closes the phone instead of erroring.
- **Every hook needs a matching unhook.** The dev loop reloads the plugin in-process; anything registered in the `Plugin` constructor (events, commands, windows, the DTR (server info bar) entry) must be released in `Dispose`, or the second load doubles it up.

## Related docs

- [Architecture](architecture.md): the next read, covering plugin boot, services, the frame loop, and the shell.
- [Creating an app](creating-an-app.md): step-by-step tutorial for adding a phone app.
- [UI toolkit](ui-toolkit.md): the `Windows/Components/` widget library.
- [Game integration](game-integration.md): Dalamud services and the framework thread in depth.
- [Testing and release](testing-and-release.md): the test project, CI workflows, and the release pipeline.
- [Conventions](conventions.md): code style and performance rules to follow before your first PR.
