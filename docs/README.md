# Aetherphone documentation

Welcome. Aetherphone is an open-source plugin for Dalamud, the community plugin framework that runs inside Final Fantasy XIV. It renders a complete smartphone in game: a home screen, dozens of apps, notifications, messaging, mini-games, and more, all drawn with Dear ImGui, an immediate mode UI library that redraws every visible pixel every frame. The plugin is written in C# on .NET 10, and this folder is its documentation: how the client is built, how it is structured, and how to extend it. Everything here covers the client plugin only. The online backend ("Aethernet") is a separate ASP.NET service in its own repository; these docs describe only what the client sends and expects.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Plugin.cs | Plugin entry point; constructs and owns everything |
| src/Aetherphone/Windows/PhoneWindow.cs | The single ImGui window the whole phone renders in |
| src/Aetherphone/Core/Apps/IPhoneApp.cs | The contract every phone app implements |
| src/Aetherphone/Core/Apps/AppRegistry.cs | Builds the list of every app at boot |
| src/Aetherphone/Core/Localization/L.cs | Source of truth for every user-visible string |
| CONTRIBUTING.md | Build command and pull request checklist, at the repo root |

## Reading order for new contributors

If you have never written a Dalamud plugin or used Dear ImGui, read these four in order:

1. [Getting started](getting-started.md): build the plugin and load it in game first, so the code every later doc references is something you can open, run, and poke at.
2. [Architecture overview](architecture.md): the map of layers, services, threads, and the frame loop that every other doc assumes you have seen.
3. [Conventions and code style](conventions.md): the rules reviewers will hold your first pull request to, cheaper to learn before you write code than after.
4. [Creating your own app](creating-an-app.md): a hands-on tutorial that exercises everything above end to end by building a small app.

After those four, read the rest on demand: each doc opens by saying when you need it.

## Vocabulary

Terms that recur across every doc, defined once here:

- **Dalamud**: the community plugin framework injected into Final Fantasy XIV. It loads plugin assemblies, provides game services through dependency injection, and hosts the UI layer.
- **Dear ImGui**: the immediate mode UI library Dalamud exposes. There is no retained widget tree; your draw code runs every frame and redraws everything. Stop calling a draw function and the thing disappears.
- **Draw list**: an ImGui command buffer (from `ImGui.GetWindowDrawList()`) that components paint shapes and text onto directly. Most Aetherphone UI is drawn this way rather than with stock ImGui widgets.
- **Shell**: the phone's operating layer in src/Aetherphone/Core/Shell. It draws the device body, the home screen or active app, chrome, and overlays, in that order, every frame.
- **App**: a plain C# class implementing `IPhoneApp` (src/Aetherphone/Core/Apps/IPhoneApp.cs) that the shell asks to draw itself while open. One folder per app under src/Aetherphone/Apps.
- **Aethernet**: the online backend, a separate ASP.NET service in its own repository. These docs describe only its client-visible contract.
- **Lumina**: Dalamud's reader for the game's static data sheets (items, jobs, weather). Covered in [game integration](game-integration.md).
- **FFXIVClientStructs**: community-maintained struct layouts for reading live game memory in `unsafe` code. Also covered in [game integration](game-integration.md).

## How each doc is structured

Every doc opens with one paragraph saying what it covers and when to read it, then a "Key files" table mapping the paths you will open most. Docs end with "Gotchas" (real traps verified in code, worth skimming even if you skip the middle) and "Related docs" links. When any doc and the code disagree, the code wins.

The one exception is [the art asset specification](ART-ASSET-SPEC.md), which is written for artists rather than engineers and keeps its own numbered, table-first format.

## All docs

### Orientation

| Doc | What it covers |
| --- | --- |
| [Getting started](getting-started.md) | From `git clone` to a running dev build in game: prerequisites, build, dev plugin loading, chat commands, logs, repo tour |
| [Architecture overview](architecture.md) | The big picture: plugin boot, service composition, threads, the shell layer, and how a frame reaches an app's `Draw` |
| [Conventions and code style](conventions.md) | Naming, formatting, performance rules, copy rules, localization lockstep, and git conventions |

### Building apps

| Doc | What it covers |
| --- | --- |
| [Creating your own app](creating-an-app.md) | Step-by-step tutorial: folder, class, registration, icon, accent color, localized name, plus a recipe for adding a Settings page |
| [App framework](app-framework.md) | The `IPhoneApp` contract in full: lifecycle, navigation, theming, badges, sharing, home placement, polling, home widgets, Control Center tiles |
| [UI toolkit](ui-toolkit.md) | The widget library in src/Aetherphone/Windows/Components: typography, spacing tokens, input, popups, scrolling, common widgets |
| [Mini-games framework](games-framework.md) | The Games app: the `IMiniGame` contract, juice helpers (shake, hit-stop, particles), scoring, and the daily challenge |

### Platform services

| Doc | What it covers |
| --- | --- |
| [State and persistence](state-and-persistence.md) | Where data lives: the shared config object, migrations, per-character stores, media on disk |
| [Notifications](notifications.md) | The full pipeline: posting, the notification center, banners, sounds, channels, deep links, badges |
| [Localization](localization.md) | Nine languages: `L.cs` as source of truth, the JSON catalogs, runtime lookup, plurals, copy rules |
| [Messaging and chat](messaging-and-chat.md) | The shared chat stack: transcript, composer, message model, pagination, read receipts, and the apps that consume it |
| [Networking and the Aethernet backend](networking.md) | The client side of everything online: HTTP, sessions, the websocket, voice calls, rate limits, end-to-end encryption, media |
| [Game integration](game-integration.md) | Reading and acting on the live game safely: Dalamud services, the threading rule, Excel sheets, ClientStructs |
| [Assets and media](assets-and-media.md) | Every asset pipeline: fonts, emoji, icons, images, sounds, wallpapers, cases, and how to add to each |

### Shipping

| Doc | What it covers |
| --- | --- |
| [Testing, CI, and releases](testing-and-release.md) | The test project, the pull request workflows, the tag-and-release pipeline, and the changelog system |

### Reference

| Doc | What it covers |
| --- | --- |
| [Art asset specification](ART-ASSET-SPEC.md) | The spec artists follow to produce app icons and phone cases that drop in without engineering work |

## Find it fast

Common first questions, each with the doc and section that answers it:

1. **How do I build the plugin and load it in game?** [Getting started: clone and build](getting-started.md#clone-and-build), then [load it in the game](getting-started.md#load-it-in-the-game).
2. **Why does my change not show up in game?** Usually the Release versus Debug trap: [getting started: why Release, not Debug?](getting-started.md#why-release-not-debug)
3. **How do I create a whole new app?** [Creating your own app](creating-an-app.md), start to finish.
4. **How do I add a setting that survives restarts?** [State and persistence: the Dalamud config model](state-and-persistence.md#the-dalamud-config-model).
5. **Where do notification sounds live?** Behavior in [notifications: sounds](notifications.md#sounds); the files and how to add one in [assets and media: sounds](assets-and-media.md#sounds).
6. **How do I add or change user-visible text?** [Localization: worked example, adding one string](localization.md#worked-example-adding-one-string). Never hardcode English.
7. **Which thread am I allowed to touch game data from?** [Game integration: the golden threading rule](game-integration.md#the-golden-threading-rule). Read this before your first `Plugin.ClientState` access.
8. **How do I post a notification and route the tap back into my app?** [Notifications: posting a notification](notifications.md#posting-a-notification) and [deep links](notifications.md#deep-links-what-happens-on-tap).
9. **How do I move between screens inside one app?** [App framework: ViewRouter, the in-app layer](app-framework.md#viewrouter-the-in-app-layer).
10. **Is there already a widget for this button, toggle, or chip row?** Almost certainly: [UI toolkit: which widget do I reach for](ui-toolkit.md#which-widget-do-i-reach-for).
11. **How do I run the tests?** [Testing, CI, and releases: running the tests](testing-and-release.md#running-the-tests).
12. **How do I point my build at a dev backend instead of production?** [Networking: dev vs prod endpoints](networking.md#dev-vs-prod-endpoints).
13. **How do I add a page to the Settings app?** [Creating your own app: add a Settings page](creating-an-app.md#add-a-settings-page). A common good first issue.
14. **How do I add a home screen widget or a Control Center tile?** [App framework: home widgets](app-framework.md#home-widgets) and [Control Center tiles](app-framework.md#control-center-tiles).
15. **How do voice calls work?** [Networking: calls](networking.md#calls) covers the lifecycle, audio pipeline, and UI entry points.

## Contributing and questions

Read [CONTRIBUTING.md](../CONTRIBUTING.md) at the repo root for the pull request process, the build command CI uses, and the checklist your PR is reviewed against. For questions, bug reports, and feature ideas, open a thread on [GitHub issues](https://github.com/XeldarAlz/FFXIV-Aetherphone/issues); search existing issues first.

## Gotchas

- Dev plugin loading points at a fixed dll path. If you registered `src/Aetherphone/bin/Release/Aetherphone.dll` in Dalamud and then build with `-c Debug`, the output goes to `bin/Debug/` and the game silently keeps loading your previous Release build.
- Bundled asset folders (Fonts, Emoji, Icons, Images, Sounds, Wallpapers, Cases, Localization) are copied to the build output by `<Content>` items in src/Aetherphone/Aetherphone.csproj. Editing an asset file does nothing in game until you rebuild.
- These docs cover the client only. The Aethernet backend lives in a separate repository, so server behavior can change without any commit here; where a doc and the code disagree, the code wins.

## Related docs

- [Getting started](getting-started.md), the first doc to read
- [CONTRIBUTING.md](../CONTRIBUTING.md) at the repo root
- [Project README](../README.md) for the user-facing overview
