# Headless preview harness

This doc covers `src/Aetherphone.Harness`, a console host that runs the real plugin without the game or Dalamud, renders every frame on the CPU, and exposes a small driver so a person or an AI coding session can tap, type, and screenshot the phone. Read it when you want to see a UI change on a machine that cannot run Final Fantasy XIV, or when you want a repeatable way to walk a flow.

## Key files

| Path | Role |
| --- | --- |
| tools/harness/Bootstrap | Fills the local cache: Dalamud dev build, native ImGui, fonts |
| tools/harness/aep, tools/harness/aep.cmd | CLI wrappers that forward to the harness binary |
| src/Aetherphone.Harness/Host/PhoneHost.cs | Injects the fakes, constructs `Plugin`, steps frames, renders |
| src/Aetherphone.Harness/Driver/DriverServer.cs | The HTTP driver and its routes |
| src/Aetherphone.Harness/Fakes | One fake per Dalamud service the plugin consumes |
| src/Aetherphone.Harness/Fonts | The font atlas fake over ImGui's own atlas |
| src/Aetherphone.Harness/Rendering | The CPU rasterizer and PNG output, used for driver screenshots |
| src/Aetherphone.Harness/Viewer | The native window: GLFW input and the OpenGL renderer |
| src/Aetherphone.Harness/Scenarios | Loads a scenario file into the simulated character |
| tools/harness/scenarios | The bundled scenarios, copied next to the harness binary |
| src/Aetherphone/Core/Game/GameMemory.cs | The game-memory gate the harness switches off |
| src/Aetherphone/Core/Game/SimulatedGame.cs | The raw character state the gated readers return while detached |
| src/Aetherphone/Core/Game/GameSheets.cs | The game-data gate that hides sheet-bound apps |
| .claude/skills/phone-preview/SKILL.md | The short version for Claude Code sessions |

## Setup

```bash
dotnet run --project tools/harness/Bootstrap -c Release
dotnet build src/Aetherphone.Harness -c Release
```

The bootstrap needs only the .NET 10 SDK plus git and a C++ compiler on macOS or Linux (Xcode command line tools, or clang or g++). It creates `~/.aetherphone-harness` (or `$AETHERPHONE_HARNESS_CACHE`) with:

- `dalamud/`: the same Dalamud dev zip CI downloads. On an arm64 host the bootstrap retargets the x64-stamped managed assemblies so .NET will load them. When `DALAMUD_HOME` is unset and no XIVLauncher install exists, `Directory.Build.props` points every project at this folder, so the whole solution builds on a machine without Dalamud.
- `native/`: `libcimgui` built from goatcorp's gc-cimgui at the commit matching that Dalamud. Windows uses the `cimgui.dll` from the zip instead.
- `assets/`: the Dalamud font assets (FontAwesome, Noto Sans CJK, Inconsolata).

Game data is optional but unlocks most of the phone. Lumina reads the game's own sqpack files without the game running, and the harness looks for them in this order: `--sqpack`, `AETHERPHONE_SQPACK`, the install XIVLauncher records in `%APPDATA%\XIVLauncher\launcherConfigV3.json`, then `~/.aetherphone-harness/sqpack`. On a Windows machine with the game installed nothing needs copying. Elsewhere, copy `sqpack/ffxiv/0a0000.win32.*` (sheets: names, worlds, jobs) and `sqpack/ffxiv/060000.win32.*` (icons) from a game install into `~/.aetherphone-harness/sqpack/ffxiv/`. Never commit game data.

## Running

`tools/harness/aep window` opens a native window (Silk.NET over GLFW and OpenGL 3.3, so it runs on macOS, Windows, and Linux) sized to the phone, renders on the GPU at vsync, and takes mouse and keyboard input directly. `tools/harness/aep serve` runs the same thing headless. Both listen on `http://127.0.0.1:47821/` (`--port` changes it; the port is written to `~/.aetherphone-harness/driver.json`). `tools/harness/aep render --out phone.png` renders a single screenshot and exits. All accept `--width`, `--height`, `--frames`, `--config`, `--assets`, `--sqpack`, `--scenario`, `--backend`, and `--cache`. With game data present the harness loads the `endgame` scenario and logs its character in before the first frame; `--scenario none` starts without a character, the way the harness behaved before scenarios existed.

`--backend` picks the Aethernet server the social apps talk to: `dev` (the default, the aethernet-dev deployment), `live` (production at `api.aetherphone.net`, where anything you post is public), or any http(s) URL. Loopback URLs are refused, because a Release build of the plugin swaps them for production; use the machine's LAN address for a local backend. Each backend keeps its own config folder (`config`, `config-live`, `config-<host>-<port>`), so a token never reaches a server it was not issued by. A default `config` folder left over from a run against another backend moves to that backend's folder on the next start, and `state` reports the backend in use.

In window mode time runs in real time, one simulation step per displayed frame, and driver commands run between frames. In headless mode time advances only when a command asks for frames.

Every driver command is available as a CLI verb and as an HTTP route with the same name and query parameters:

| Verb | Route | Notes |
| --- | --- | --- |
| `state` | `/state` | Frame, current app, phone rect, minimize phase, login and data flags, scenario |
| `step N` | `/step?frames=N` | 60 frames per simulated second |
| `shot [path] [--full]` | `/shot?full=1&frames=N` | PNG cropped to the phone window by default |
| `tap NAME` or `tap X Y` | `/tap?anchor=NAME` or `/tap?x=&y=` | `--settle N`, `--button N`, `--space screen` |
| `drag X1 Y1 X2 Y2` | `/drag` | `--frames N` for the move duration |
| `scroll X Y DY` | `/scroll` | Wheel delta at a point |
| `type TEXT` | `/type?text=` | Sends the characters to ImGui |
| `key NAME` | `/key?name=` | Any `ImGuiKey` member name |
| `open [APPID]` | `/open?app=` | Opens the phone, then the app |
| `settings` | `/settings` | Fires the Dalamud config-UI hook |
| `anchors` | `/anchors` | Every `UiAnchors.Report` key on screen, phone-relative |
| `log [--since N]` | `/log?since=N` | Plugin warnings and errors plus harness notes |
| `login`, `logout` | `/login`, `/logout` | Log the scenario character in or out |
| `scenario [NAME]` | `/scenario?name=` | Lists scenarios, or switches character; `none` clears it |
| `install [APPID]` | `/install?app=` | Places an app on the home screen; with no id, every available app |
| `command TEXT` | `/command?text=` | Runs a registered chat command |
| `quit` | `/quit` | Stops the server |
| `home` | `/home` | Back to the home screen |
| `url` | | Prints the viewer address |

Coordinates are phone-relative pixels, the same space as the cropped screenshot. Anchors come from `UiAnchors.Report` calls in draw code; the harness forces recording every frame, so anything the onboarding tours can point at can be tapped by name.

## The browser viewer

Both modes also serve a page at `http://127.0.0.1:47821/` (`tools/harness/aep url` prints it). It streams the phone as PNG frames and forwards mouse, wheel, and keyboard events to ImGui through `/event`, so a person can use the phone the way they would in game. Each frame request steps the simulation by the real time elapsed since the previous one, which means time only runs while a viewer is connected; close the tab and the driver is deterministic again. Frames travel as raw pixels, and the rasterizer splits the screen into horizontal bands across every core, so expect roughly 30 frames per second on a modern machine.

## What is faked and what is real

Real: the plugin assembly, every app, the shell, fonts through `FontService`, ImGui itself (Dalamud's own binding and its cimgui fork), the Aethernet network stack, config persistence.

Faked: every Dalamud service. `IFramework` ticks once per stepped frame and runs marshaled work on that thread. `ITextureProvider` decodes images with ImageSharp into CPU textures. `IUiBuilder` and `IFontAtlas` build fonts on ImGui's atlas from the cached Dalamud assets. `IDataManager` wraps Lumina when a sqpack folder exists and throws a clear message otherwise. Services the plugin barely touches are dynamic proxies that return defaults and log the first unfaked call.

Switched off through the plugin's own gates: `GameMemory.Detach()` stops every FFXIVClientStructs read and keeps the video engine from creating a DX11 surface. While a scenario character is logged in, `GameMemory.Simulate` hands the gated readers a `SimulatedGame`, and they return its values in place of game memory. `GameSheets.MarkUnavailable()` makes the `GameData` wrapper return blank names and reports the sheet-bound apps as unavailable so the home layout skips them. In the game none of these switches is ever thrown, so nothing changes there.

The fake sits at the raw-read layer on purpose. A scenario supplies levels, item stacks, and currency counts, and the plugin's own code still turns them into the jobs roster, the inventory sections, and the wallet. That keeps the harness honest about the UI and about the logic above the readers, but it says nothing about the readers themselves: an offset that moved in a game patch, a RowId used as an array index, or a container a reader forgot to visit only shows up in game.

Game actions behind the gate (party invites, chat sends, gearset swaps, item links, friend actions, targeting) report success and land in the log as `game action: ...` lines, so a walk can check that a button asked the game for the right thing. Equipping a gearset also changes the simulated job, so the UI follows the switch.

## Scenarios

A scenario is a JSON file describing one logged-in character: who they are, where they stand, what they carry, and what they have done this week. The harness searches `~/.aetherphone-harness/scenarios/` first (keep private ones there, a copy of your own character for example), then the bundled `tools/harness/scenarios/`. `scenario` with no name lists both; a path to a file also works.

| Bundled | What it shows |
| --- | --- |
| `endgame` | A level 100 White Mage with full bags, armoury, retainers, a free company, friends, linkshells, timers, and dailies in progress |
| `fresh` | A level 15 Archer with almost nothing, for empty states and locked jobs |

Names are resolved against the game sheets, so a scenario needs game data and reads like the game does: items, worlds, weathers, and job abbreviations by name, territories by row id. Anything that does not resolve is skipped and reported as a warning in the log and in the `scenario` response. The fields, all optional except the character's `name`, `world`, and `job`:

| Field | Shape |
| --- | --- |
| `character` | `name`, `world`, `currentWorld`, `job` (abbreviation), `race` (`Hyur` to `Viera`), `gender`, `tribe`, `grandCompany`, `freeCompanyTag`, `contentId` |
| `location` | `territory`, `map`, `weather`, `eorzeaTime` (`"18:30"` pins the clock), `indoors`, `district`, `ward`, `plot`, `room` |
| `gil`, `currencies` | Currencies map an item name to an amount: tomestones, seals, scrips, MGP, ventures |
| `weeklyTomestones`, `weeklyTomestoneLimit`, `companySealCap` | Weekly and cap figures for the wallet |
| `jobs` | Abbreviation to a level, or to `[level, experience]` |
| `maxLevel`, `restedExperience` | Level cap and rested bonus |
| `gearsets`, `activeGearset` | `{ "job", "name", "itemLevel" }` entries; ids follow the list order |
| `inventory` | `bags`, `armoury`, `equipped`, `crystals`, `saddlebag`, each a list of item names or `{ "item", "quantity", "hq" }`; armoury and equipped slots come from the item's equip slot |
| `retainers`, `activeRetainer` | `{ "name", "gil", "selling", "venture": { "hours", "minutesLeft" }, "items", "crystals" }`; the active one counts as open at a summoning bell |
| `freeCompany` | `{ "name", "items", "crystals" }` |
| `vessels`, `mapAllowanceMinutesLeft` | Submersibles and airships as `{ "name", "airship", "hours", "minutesLeft" }`; a negative `minutesLeft` is already back |
| `friends` | `{ "name", "world", "currentWorld", "online", "job", "territory", "freeCompany" }` |
| `linkshells`, `crossWorldLinkshells` | Up to eight names each |
| `dailies` | `beastTribeAllowances`, `customDeliveries`, `leveAllowances`, `wondrousTailsStickers`, `wondrousTailsExpired`, `domanDonated`, `domanAllowance`, `dutyRouletteComplete`, `huntBillsUnlocked`, `huntBillsComplete` |
| `questsCompleted`, `collected` | Every quest done or none; the share of mounts, minions, emotes, and other collectibles unlocked, picked by a stable hash so it is the same on every run |

Content ids are derived from the name and world unless `contentId` is set, so a scenario keeps its per-character config (inventory captures, timers, dailies) across runs. Switching scenarios logs the old character out and the new one in, which is what an alt switch looks like to the plugin. The first run against a config that was created without game data has no sheet-bound apps on the home screen; `install` places them.

## Gotchas

- Three Dalamud statics resolve through Dalamud's service locator and wait forever without one: `UiBuilder.DefaultFontSizePx`, `UiBuilder.IconFont`, and `WindowSystem.Draw`. The plugin reads the first two through instance seams and the harness draws windows with its own loop. If the harness hangs on a new Dalamud API, `dotnet-stack report -p <pid>` shows which static is waiting.
- Dalamud stamps its assemblies x64-only. The harness retargets `Aetherphone.dll` in its own output at startup; the test project's output needs the same treatment to run on Apple Silicon.
- The rasterizer has no GPU. A phone frame costs about 12 ms at 1600x1200 on a 10-core machine, so `step 1000` takes around fifteen seconds.
- A fresh config runs the full first-run boot (emblem and greetings, roughly 15 seconds of frames) and then the onboarding tour. Config lives in `~/.aetherphone-harness/config` (one folder per backend), so it happens once per backend.
- The Aethernet apps talk to a real backend: aethernet-dev unless `--backend` says otherwise. The dev server is shared by the whole team, so posts made there are visible to every tester; reuse an existing test account instead of creating a new one per run.
- Readers that touch game memory without a `GameMemory.Attached` check throw `InvalidOperationException` from FFXIVClientStructs once a character is logged in, because no address was ever resolved. The exception lands in `log` with the reader's name; gate the reader and give it a simulated branch.
- The phone window is pinned at a fixed spot inside the display. In game the chassis can be dragged; under the harness that drag is ignored so the crop and the viewer's input mapping stay stable.

## Related docs

- [Getting started](getting-started.md) for the in-game dev loop
- [Game integration](game-integration.md) for what the two gates stand in for
- [Testing, CI, and releases](testing-and-release.md)
