<p align="center">
  <img src="src/Aetherphone/Images/Icon.png" width="180" alt="Aetherphone icon" />
</p>

<h1 align="center">Aetherphone</h1>

<p align="center">
  <a href="https://github.com/XeldarAlz/FFXIV-Aetherphone/releases/latest"><img alt="Release" src="https://img.shields.io/github/v/release/XeldarAlz/FFXIV-Aetherphone?style=flat-square&color=blue"></a>
  <a href="https://github.com/XeldarAlz/FFXIV-Aetherphone/releases"><img alt="Downloads" src="https://img.shields.io/github/downloads/XeldarAlz/FFXIV-Aetherphone/total?style=flat-square&color=blue&cacheSeconds=300"></a>
  <a href="https://github.com/XeldarAlz/FFXIV-Aetherphone/actions/workflows/release.yml"><img alt="Build" src="https://img.shields.io/github/actions/workflow/status/XeldarAlz/FFXIV-Aetherphone/release.yml?style=flat-square"></a>
  <a href="LICENSE.md"><img alt="License" src="https://img.shields.io/badge/license-AGPL--3.0--or--later-blue?style=flat-square"></a>
</p>

<p align="center">
  <em>A smartphone, built for you. Built on Dalamud.</em>
</p>

---

<p align="center">
  <img src="src/Aetherphone/Images/screenshots/Home.png" width="280" alt="Aetherphone in-game" />
</p>

## What it does

Puts a real smartphone on screen: a docked, always-on device with a home screen, a status bar, app icons, notifications, ringtones, and themeable wallpapers. At its core is a small social network for Aetherphone users: Chirper for microblogging, Aethergram for sharing photos, and Phone for group voice calls, all part of the same social network alongside a Messages app that turns the game's `/tell` system into a proper chat client.

## Features

### The device

- **Home screen & shell**: a docked device with a status bar and a multi-page app grid. Long-press to enter edit mode, then drag icons to rearrange them across pages. Smooth slide transitions between every screen.
- **Lock screen**: a real lock screen with a large clock and date, your latest notifications stacked as cards (tap one to jump straight into its app), and a swipe to get back in.
- **Control Center**: swipe down for quick toggles: Do Not Disturb, position lock, idle scrolling, plus accent-color swatches, brightness (text size) and volume sliders, and live music controls.
- **Notifications**: a notification center, optional toasts, game-sound ringtones, and a Do Not Disturb switch.

### Social

Aetherphone runs its own social network, Aethernet, so these apps work across characters and sessions, not just locally.

- **Chirper**: an X/Twitter-style microblog. Post short updates, browse For You and Following feeds, search, edit your profile, and report content.
- **Aethergram**: an Instagram-style photo feed built on your Photos library. Post your captures, follow other players, and browse what they share.
- **Messages**: reads incoming `/tell`s and lays them out as chat bubbles you can reply to, with toast notifications and an unread badge on the server-info bar.
- **Phone**: place group voice calls to other Aetherphone users, right from the device.
- **Find People**: look players up on the Lodestone and jump straight into a conversation with them.
- **Velvet**: a separate, private 18+ companion app for sharing work and connecting with other creators. Ships as an optional build.

### Apps

- **Contacts**: your friend list as an address book, with Lodestone portraits; start a conversation straight from a contact.
- **Character**: a profile card for the local character, with gear, Lodestone portrait, and a fitness-style Activity dashboard tracking job mastery rings.
- **Skywatcher**: live Eorzean weather for your current zone, with a forecast for the hours ahead.
- **Venues**: browse community venues and events, nightclubs, bars, photography spots, and more, with live and upcoming filters, tag and name search, and favorites. One tap travels you there with Lifestream (or copies the command), or opens the venue's listing and Discord.
- **News**: the Lodestone feed for your region: Topics, Notices, Maintenance, and Updates, with images, maintenance windows, and a tap to open the full story.
- **Market**: live market board prices from Universalis. Search any item (or right-click one in-game), see the cheapest listings, price stats, sale velocity, and recent-sale history with a trend graph across your World, Data Center, or Region. Set price-drop alerts that ping the phone, compare against NPC vendor prices, and star favorites.
- **Wallet**: track your gil, currencies, tomestones, hunt seals, and PvP marks at a glance, with progress toward weekly caps.
- **Music**: an in-game player. Browse genre stations or search for any track, with playback controls and a Now Playing banner on the home screen.
- **Camera**: capture in-game shots straight from the phone, with square, photo, and pano modes, an optional framing grid, and a flash.
- **Photos**: a gallery for your captures, laid out like a real photo library with a full-screen viewer.
- **Games**: a pocket arcade of 15 mini-games across logic, memory, match-3, and puzzle: Sweeper, Nonogram, Pairs, Simon, Gem Swap, 2048, Water Sort, Flow, Solitaire, Reversi, Breakout, Bubble Shooter, Snake, Flap, and Whack, each tracking your best score.
- **Clock**: an analog clock on Eorzea time.
- **Timers**: countdowns to the daily, Grand Company, and weekly resets, plus Fashion Report, Jumbo Cactpot, and Ocean Fishing windows and your retainer ventures, with optional reminders.
- **Fishing**: Ocean Fishing voyage predictions, so you know what's coming before you set sail.
- **Dailies**: track your recurring daily and weekly content, with checkable rows, badge counts, and auto-detected completion.
- **Collections**: browse your mounts, minions, and other collectibles.
- **Inventory**: a quick look at your bags without opening the game menu.
- **Maps**: in-world maps and points of interest.

### Personalization

- **Themes & wallpapers**: pick an accent palette and a wallpaper: built-in art, one of your own photos, or any image you import. The whole device restyles to match.
- **Lodestone portraits**: real character avatars and portraits, pulled from the Lodestone and shown on your profile, contacts, and chats (toggleable).
- **Text size**: an accessibility zoom that scales the on-device type without resizing the phone.
- **Idle animation**: your character idly scrolls the phone (Tomescroll) when you're standing around, optional.
- **About window**: an animated credits and links screen, reachable from Settings or `/phone about`.

## Install

In-game: `/xlsettings` → **Experimental** → paste into **Custom Plugin Repositories**:

```
https://raw.githubusercontent.com/XeldarAlz/DalamudPlugins/main/repo.json
```

Tick **Enabled**, click **+**, then **Save and Close**. Open `/xlplugins` → **All Plugins**, search for **Aetherphone**, and install.

## Commands

| Command | Action |
|---|---|
| `/phone` | Toggle the phone |
| `/aetherphone` | Alias for `/phone` |
| `/phone about` | Open credits / links |

## More from me

If you liked this plugin, take a look at my other Dalamud work. You might find something else there for you.

→ [XeldarAlz Dalamud Plugins](https://github.com/XeldarAlz/DalamudPlugins)

## License

AGPL-3.0-or-later. See [LICENSE.md](LICENSE.md).
