---
name: phone-preview
description: Render and drive the Aetherphone UI headless (no game, no Dalamud) to review a change visually. Use when asked to check how a screen looks, verify a UI change, take a screenshot of the phone, or click through a flow.
---

# Phone preview harness

The harness runs the real plugin against fake Dalamud services and renders every frame on the CPU. It works on macOS, Windows, and Linux without the game installed. Full reference: docs/harness.md.

## One-time setup

```bash
dotnet run --project tools/harness/Bootstrap -c Release
dotnet build src/Aetherphone.Harness -c Release
```

The bootstrap fills `~/.aetherphone-harness` (Dalamud dev build, native ImGui, fonts). Rebuild the harness after every plugin change; it rebuilds the plugin too.

## Start the server

Run it in the background and wait for `~/.aetherphone-harness/driver.json` to appear:

```bash
tools/harness/aep serve
```

The server steps frames only when a command asks it to. Nothing moves between commands, so screenshots are deterministic.

A person can watch and use the same phone in a browser: `tools/harness/aep url` prints the address (http://127.0.0.1:47821/ by default). While that page is open, time runs in real time and mouse and keyboard go to the phone, so take driver screenshots when nobody is interacting.

## Commands

Every command is `tools/harness/aep <command>` (`tools\harness\aep.cmd` on Windows). Coordinates are phone-relative pixels matching the cropped screenshot; add `--space screen` for display coordinates.

| Command | What it does |
| --- | --- |
| `state` | Frame counter, current app id, phone rect, minimize phase |
| `step N` | Advance N frames (60 per simulated second) |
| `shot [path] [--full]` | Save a PNG of the phone (or the whole display); then Read the file |
| `anchors` | Named UI elements on screen with centers, for taps by name |
| `tap NAME` or `tap X Y` | Tap an anchor or a point; `--settle N` frames afterwards (default 12) |
| `drag X1 Y1 X2 Y2 [--frames N]` | Swipe or drag |
| `scroll X Y DY` | Mouse wheel at a point |
| `type TEXT` | Type into the focused field |
| `key NAME` | Press an ImGui key: Enter, Escape, Backspace, Tab, ... |
| `open APPID` | Open the phone on an app (ids in Core/Apps/AppRegistry.cs); goes home first, like a launch |
| `home` | Return to the home screen |
| `url` | Print the browser viewer address |
| `settings` | Open the Settings app the way Dalamud's config button would |
| `log [--since N]` | Plugin warnings, errors, and harness notes since a sequence number |
| `login` / `logout` | Simulate the character logging in or out |
| `command "/aep ..."` | Run a chat command through the plugin's handler |
| `quit` | Stop the server |

## Typical review loop

1. `tools/harness/aep open settings` then `tools/harness/aep shot /tmp/settings.png`, then Read the PNG.
2. Find targets with `anchors`; tap by name when one exists, by pixel otherwise.
3. After each action, `shot` again and check `log` for warnings.

## What the harness cannot show

- No game memory: inventory, retainers, chat send, friend list, and hooks return empty results.
- No game data unless an exd pack is present in `~/.aetherphone-harness/sqpack/ffxiv/`. Without it the sheet-bound apps (Skywatcher, Dailies, Wallet, Jobs, Inventory, Market, Hunts, Maps, Housing, Collections) are hidden and world or item names are blank.
- No audio, no video screen, no camera capture.
- A fresh config runs the first-run boot and onboarding tour. `step 1000` gets past the boot; the tour has buttons to tap. Config persists in `~/.aetherphone-harness/config`, so this happens once.
