# Aetherphone documentation

Aetherphone is an open-source plugin for Dalamud, the community plugin framework that runs inside Final Fantasy XIV. It draws a complete smartphone in game: a home screen, dozens of apps, notifications, messaging, mini-games, and more. The plugin is written in C# on .NET 10 and draws everything with Dear ImGui. This folder explains how the plugin is built, how it works, and how to add to it, for developers, artists, and translators.

These docs cover the client plugin only. The online service behind accounts, chat, and other social features (Aethernet) lives in a separate repository. Where a doc and the code disagree, the code wins, and a fix to the doc is welcome.

## Start here

Pick the line that fits you.

- **I want to write code.** Read [Getting started](getting-started.md) to build the plugin and load it in game, then [Architecture](architecture.md) for the big picture, [Conventions](conventions.md) for the rules reviewers check, and [Creating your own app](creating-an-app.md) to build something end to end.
- **I make art.** Read the [Art guide](art-guide.md). It needs no code and covers app icons, wallpapers, phone cases, frames, and sounds, from the spec to how your work reaches players.
- **I want to translate.** Read the [Translator guide](translating.md). You can do everything in the browser.
- **I maintain releases.** Read [Testing, CI, and releases](testing-and-release.md).
- **I play the game and want to know what the plugin does with my data.** Read the [project README](../README.md), the [privacy policy](../PRIVACY.md), and the [terms](../TERMS.md).

## Words you will see everywhere

- **Dalamud**: the plugin framework injected into the game. It loads plugins, gives them game services, and hosts their windows.
- **Dear ImGui**: the UI library Dalamud provides. It is "immediate mode": your draw code runs every frame and redraws everything, so there is no widget tree to keep in sync. Stop calling a draw function and the thing disappears.
- **Draw list**: a buffer you paint shapes and text onto directly. Most Aetherphone UI is painted this way rather than built from stock ImGui widgets.
- **Shell**: the phone's operating layer. Every frame it draws the device, the home screen or the open app, the system chrome, and overlays, in that order.
- **App**: a C# class that implements `IPhoneApp` and draws itself while it is open. Each app has a folder under src/Aetherphone/Apps.
- **Aethernet**: the online service. The docs describe only what the plugin sends to it and how the plugin reacts to its answers.
- **Lumina**: Dalamud's reader for the game's data sheets (items, jobs, weather).
- **FFXIVClientStructs**: community-maintained layouts for reading the live game's memory.

## All docs

### Getting going

| Doc | What it covers |
| --- | --- |
| [Getting started](getting-started.md) | Clone, build, and load the plugin; the Release, Debug, and Beta builds; chat commands; logs; troubleshooting |
| [Architecture](architecture.md) | How the plugin starts, how its services fit together, which thread does what, and how a frame reaches an app |
| [Conventions](conventions.md) | Code style, performance rules, and git conventions |

### Building apps

| Doc | What it covers |
| --- | --- |
| [Creating your own app](creating-an-app.md) | A step-by-step tutorial, plus how to add a Settings page |
| [App framework](app-framework.md) | The app contract: lifecycle, navigation inside an app, theming, badges, sharing, home placement |
| [Widgets and Control Center](widgets-and-control-center.md) | Home screen widgets and Control Center tiles, and how to add each |
| [UI toolkit](ui-toolkit.md) | Which component to use for what, text, spacing, input, popups, scrolling, and the UI design rules |
| [Accent colors](design-accents.md) | Why every app color passes a white-glyph contrast rule, and how colors are assigned |

### How the phone works

| Doc | What it covers |
| --- | --- |
| [State and persistence](state-and-persistence.md) | Where data lives, settings and their migrations, per-character data, and how to reset it |
| [Notifications](notifications.md) | Posting a notification, banners, sounds, channels, badges, and what happens on tap |
| [Localization](localization.md) | The nine languages, how strings are defined and looked up, plurals, and the copy rules |
| [Game integration](game-integration.md) | Reading and acting on the game safely, and what the plugin never does in game |
| [Networking](networking.md) | How the plugin talks to Aethernet: requests, errors, sign-in, the live connection, calls, encryption, uploads |
| [Messaging and chat](messaging-and-chat.md) | The shared chat screens and stores behind ChocoChat, Velvet, Aethergram messages, and Yellow Pages |
| [Linkpearl](linkpearl.md) | The in-game chat app and its pop-out windows |
| [Media and video](media-and-video.md) | Video, watch-along parties, Music and Jam, Community Radio, and the players the plugin downloads |
| [Assets and media files](assets-and-media.md) | Every asset pipeline for engineers: fonts, emoji, icons, images, sounds, wallpapers, cases |

### Games

| Doc | What it covers |
| --- | --- |
| [Games framework](games-framework.md) | How the Games app and its stage kit work, scoring, the daily challenge, the leaderboard, online rooms |
| [Building a game](building-a-game.md) | A step-by-step tutorial for a new mini-game |
| [Gamba](casino.md) | How the casino app works on the client and how to add a game to it |

### Shipping

| Doc | What it covers |
| --- | --- |
| [Testing, CI, and releases](testing-and-release.md) | Running the tests, what CI checks, the release pipeline, the changelog, and the repo's automation |

### For artists, translators, and everyone

| Doc | What it covers |
| --- | --- |
| [Art guide](art-guide.md) | For artists: what you can make, the specs and checkers, how to deliver, rights, and seasonal art |
| [Translator guide](translating.md) | For translators: edit one language file and open a pull request from the browser |
| [AI usage](ai-usage.md) | How Aetherphone uses AI tools, the rules for contributors, and where the content inside the phone comes from |

The `readme/` folder holds the eight translations of the project README, and `media/` holds the images the README shows. Neither is part of this doc set.

## Find it fast

| Question | Where to look |
| --- | --- |
| How do I build the plugin and load it in game? | [Getting started](getting-started.md) |
| My change does not show up in game. | [Getting started](getting-started.md), the Troubleshooting section |
| How do I make a new app? | [Creating your own app](creating-an-app.md) |
| How do I add a setting that survives a restart? | [State and persistence](state-and-persistence.md) |
| How do I add or change text the player sees? | [Localization](localization.md). Never hardcode English. |
| Which thread may touch game data? | [Game integration](game-integration.md), before your first game access |
| How do I post a notification and open my app on tap? | [Notifications](notifications.md) |
| How do I move between screens inside an app? | [App framework](app-framework.md) |
| Is there already a component for this button or row? | [UI toolkit](ui-toolkit.md) |
| How do I add a widget or a Control Center tile? | [Widgets and Control Center](widgets-and-control-center.md) |
| How do I call a new server endpoint? | [Networking](networking.md) |
| How do I run the tests? | [Testing, CI, and releases](testing-and-release.md) |
| I want to make an icon or a wallpaper. | [Art guide](art-guide.md) |
| I want to fix a translation but I do not write code. | [Translator guide](translating.md) |

## How these docs are written

Each engineering doc opens with what it covers and when to read it, lists the few files you will open first, explains how the thing works, and ends with real traps under "Gotchas" and links to related docs. The docs explain ideas; the code is the reference for names and numbers. They never describe how the server enforces its rules, only what the plugin sends and how it reacts.

## Contributing and questions

[CONTRIBUTING.md](../CONTRIBUTING.md) at the repo root has the build command CI uses and the checklist a pull request is reviewed against. For questions, bug reports, and ideas, open a [GitHub issue](https://github.com/XeldarAlz/FFXIV-Aetherphone/issues) (search existing issues first) or ask on the Discord server linked from the [project README](../README.md). Report security problems privately, as the [security policy](../.github/SECURITY.md) describes, and see the [code of conduct](../.github/CODE_OF_CONDUCT.md) for how everyone is expected to behave.
