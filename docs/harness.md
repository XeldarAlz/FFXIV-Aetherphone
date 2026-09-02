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
| src/Aetherphone.Harness/Rendering | The CPU rasterizer and PNG output |
| src/Aetherphone/Core/Game/GameMemory.cs | The game-memory gate the harness switches off |
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

Optional: copy `sqpack/ffxiv/0a0000.win32.dat0`, `.index`, and `.index2` from a game install into `~/.aetherphone-harness/sqpack/ffxiv/` (or point `AETHERPHONE_SQPACK` at a sqpack folder). Lumina reads them without the game and every sheet-backed name and icon comes back. Never commit game data.

## Running

`tools/harness/aep serve` starts the phone and listens on `http://127.0.0.1:47821/` (`--port` changes it; the port is written to `~/.aetherphone-harness/driver.json`). `tools/harness/aep render --out phone.png` renders a single screenshot and exits. Both accept `--width`, `--height`, `--frames`, `--config`, `--assets`, `--sqpack`, and `--cache`.

The server advances time only when a command asks for frames, so the state between commands is frozen and screenshots are repeatable. Every driver command is available as a CLI verb and as an HTTP route with the same name and query parameters:

| Verb | Route | Notes |
| --- | --- | --- |
| `state` | `/state` | Frame, current app, phone rect, minimize phase, login and data flags |
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
| `login`, `logout` | `/login`, `/logout` | Flip the fake client state |
| `command TEXT` | `/command?text=` | Runs a registered chat command |
| `quit` | `/quit` | Stops the server |

Coordinates are phone-relative pixels, the same space as the cropped screenshot. Anchors come from `UiAnchors.Report` calls in draw code; the harness forces recording every frame, so anything the onboarding tours can point at can be tapped by name.

## What is faked and what is real

Real: the plugin assembly, every app, the shell, fonts through `FontService`, ImGui itself (Dalamud's own binding and its cimgui fork), the Aethernet network stack, config persistence.

Faked: every Dalamud service. `IFramework` ticks once per stepped frame and runs marshaled work on that thread. `ITextureProvider` decodes images with ImageSharp into CPU textures. `IUiBuilder` and `IFontAtlas` build fonts on ImGui's atlas from the cached Dalamud assets. `IDataManager` wraps Lumina when a sqpack folder exists and throws a clear message otherwise. Services the plugin barely touches are dynamic proxies that return defaults and log the first unfaked call.

Switched off through the plugin's own gates: `GameMemory.Detach()` makes every FFXIVClientStructs reader return its empty result and keeps the video engine from creating a DX11 surface. `GameSheets.MarkUnavailable()` makes the `GameData` wrapper return blank names and reports the sheet-bound apps as unavailable so the home layout skips them. Both default to on in the game, so nothing changes there.

## Gotchas

- Three Dalamud statics resolve through Dalamud's service locator and wait forever without one: `UiBuilder.DefaultFontSizePx`, `UiBuilder.IconFont`, and `WindowSystem.Draw`. The plugin reads the first two through instance seams and the harness draws windows with its own loop. If the harness hangs on a new Dalamud API, `dotnet-stack report -p <pid>` shows which static is waiting.
- Dalamud stamps its assemblies x64-only. The harness retargets `Aetherphone.dll` in its own output at startup; the test project's output needs the same treatment to run on Apple Silicon.
- The rasterizer has no GPU. A phone frame costs about 50 ms at 1600x1200, so `step 1000` takes under a minute.
- A fresh config runs the full first-run boot (emblem and greetings, roughly 15 seconds of frames) and then the onboarding tour. Config lives in `~/.aetherphone-harness/config`, so it happens once.
- The Aethernet apps talk to the real dev backend from the harness. Anything you post is real.

## Related docs

- [Getting started](getting-started.md) for the in-game dev loop
- [Game integration](game-integration.md) for what the two gates stand in for
- [Testing, CI, and releases](testing-and-release.md)
