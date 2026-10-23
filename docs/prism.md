# Prism

Prism is the phone's glamour app: a library of glams people share, a weekly contest that runs on the same glams, and a private wardrobe for your own outfits. Modded glams come first and vanilla glams sit alongside them, marked on each glam's own page. Prism never hosts a mod file: a glam names its mods and links to where they live.

## The glam

A glam is one outfit with up to four portrait (3:4) photos. Everything about the outfit is captured from the game when the photo is taken, not typed by the poster.

| Part | Where it comes from |
| --- | --- |
| Gear per slot (item id, two dyes) | Glamourer's current state when Glamourer is on (`Core/Glamour/GlamourerDesign.cs`), otherwise the equipped glamour (`Core/Glamour/EquippedOutfit.cs`) |
| Race and body type | Glamourer's state, otherwise the character |
| Mods per slot (name, author, version, link, picked options) | Penumbra, through `Core/Glamour/GlamourService.Capture` |
| Character mods (body, face, hair) | The same capture, on slot `-1` |
| Vanilla stamp | No mods on any slot |
| Owned pieces ("you own 7 of 9") | Bags, armoury, glamour dresser and armoire on the viewer's side (`OwnedItems`) |

### How mods are attributed

`Penumbra.GetPlayerResourcePaths.V5` lists every file the player's character draws, as the path on disk and the game paths it replaces. A file under Penumbra's mod directory belongs to the mod folder it sits in. Its game paths name the slot: `chara/equipment/...` and `chara/accessory/...` files carry a slot token (`_met`, `_top`, `_glv`, `_dwn`, `_sho`, `_ear`, `_nek`, `_wrs`, `_rir`, `_ril`), and `chara/weapon/...` is the main hand. Anything else (body, face, hair, skin) goes on slot `-1`. The mod's name, author, version and website come from Penumbra's mod list and the mod's own `meta.json`; the picked options come from `Penumbra.GetCurrentModSettings.V5` for the player's collection.

### Trying a glam on

With Glamourer, Try on sets each slot with `Glamourer.SetItem.V3` (once, equipment only) and switches on the glam's mods that the viewer has installed with `Penumbra.SetTemporaryModSettingsPlayer.V5`, matched by mod name and with the poster's options. Nothing in the viewer's collections changes. The try on stays on while the viewer browses or leaves the phone (a Wearing bar above Prism's tabs takes it off), and comes off when the plugin unloads. Taking it off puts back the Glamourer state saved just before the first try on (falling back to `Glamourer.RevertState`), so a look the viewer had on survives, and drops the temporary settings with `Penumbra.RemoveAllTemporaryModSettingsPlayer.V5` under Prism's own key. Without Glamourer, Try on falls back to the game's own Try On window for each item, with its dyes.

### Mod links

A link is clickable only when it is `https` on an allowed host: `xivmodarchive.com`, `heliosphere.app`, `patreon.com`, `ko-fi.com`, `gumroad.com` and `*.carrd.co` (`Core/Glamour/ModLinks.cs`). Any other link shows as plain text, and the server applies the same list by dropping it when the glam is posted. A mod that is 18+ can't be linked at all, and every glam has to be safe for work as shown.

### What the client adds on its own

- Owned pieces count the bags, armoury, glamour dresser and armoire (`Core/Glamour/OwnedItems.cs`). The dresser and armoire only count once the player has opened them this session, and the glam page says so.
- An item's page shows whether you own it, its gil vendor price and a market board link, from the phone's market index. The server only sends the glam count and what the item is worn with.
- Search on Discover matches tradable gear by name through the market index and opens that item's glams, so untradable pieces are only reachable from a glam that uses them. A single word or `#text` also offers that tag. The glam of the day card ignores the feed filters.
- Without Penumbra the glam page says mods need it and a new post is stamped vanilla; without Glamourer it says Try on uses the game's window.

## Server contract

Everything is under the signed-in Aethernet session. Paths are built in `Core/Aethernet/Clients/GlamClient.cs` and pinned by `GlamWireContractTests`.

### Objects

```json
{
  "id": "g_1",
  "author": { "id": "u_1", "displayName": "Ada Moon", "handle": "ada.moon", "avatarUrl": "https://..." },
  "title": "Studious magic",
  "photos": [ { "url": "https://...", "width": 810, "height": 1080 } ],
  "gear": [
    { "slot": 3, "itemId": 45123, "stain0": 12, "stain1": 0,
      "mods": [ { "name": "Cute Coat", "author": "Bea", "version": "1.2", "link": "https://heliosphere.app/mod/x",
                  "options": [ { "group": "Colour", "picked": [ "Red" ] } ] } ] },
    { "slot": -1, "mods": [ { "name": "Body Plus", "author": "Cee" } ] }
  ],
  "tags": [ "cute", "purple" ],
  "race": 6,
  "gender": 1,
  "vanilla": false,
  "weekId": "",
  "placing": 0,
  "likes": 0,
  "liked": false,
  "saved": false,
  "tryOns": 34,
  "createdAtUnix": 1792540000,
  "inspiredBy": ""
}
```

- `slot` is the equipped index (0 main hand, 1 off hand, 2 head, 3 body, 4 hands, 6 legs, 7 feet, 8 ears, 9 neck, 10 wrists, 11 right ring, 12 left ring); `-1` holds character mods and has no item. `optional` (default false) marks a piece the glam works without.
- `race` is the game's Race sheet row and `gender` is 0 male, 1 female.
- `likes` is the glam's like count, the same for every viewer.
- `weekId` and `placing` are set when the glam came from a contest entry, once the week's results are out.

### Routes

| Route | Body | Returns |
| --- | --- | --- |
| `GET /prism/glams?scope=new\|popular\|today\|following[&vanilla=true\|false][&race=&gender=][&cursor=]` | | `{ items: Glam[], nextCursor }`; `race` and `gender` (Fits me) keep glams made on that body |
| `GET /prism/glams/{id}` | | `Glam` |
| `POST /prism/glams` | `{ title, photos: [{ key, width, height }], gear, tags, race, gender, vanilla, inspiredBy }` | `Glam`; the client sets `inspiredBy` to the glam the poster still has tried on, and the page links back to it |
| `DELETE /prism/glams/{id}` | | 204 |
| `PUT` / `DELETE /prism/glams/{id}/like` | | 204 |
| `POST /prism/glams/{id}/tryon` | | 204, counted once per viewer per day |
| `GET /users/{id}/glams[?cursor=]` | | `{ items: Glam[], nextCursor }` |
| `GET /users/{id}/prism` | | `{ user, following, glamCount, tryOnsReceived, likesReceived, placings: [{ weekId, title, rank, track }] }`; follow and unfollow use the existing `/follows/{id}` |
| `GET /me/prism/collections` | | `[{ id, name, count, coverUrl }]`; the `saved` collection always exists |
| `POST /me/prism/collections` | `{ name }` | the collection |
| `DELETE /prism/collections/{id}` | | 204 |
| `GET /prism/collections/{id}/glams[?cursor=]` | | `{ items: Glam[], nextCursor }` |
| `PUT` / `DELETE /prism/collections/{id}/glams/{glamId}` | | 204; `saved` drives `Glam.saved` |
| `GET /prism/items/{itemId}` | | `{ itemId, glamCount, wornWith: [{ itemId, count }] }` |
| `GET /prism/items/{itemId}/glams[?cursor=]` | | `{ items: Glam[], nextCursor }` |
| `GET /prism/mods/glams?name=&author=[&cursor=]` | | `{ items: Glam[], nextCursor }` |
| `GET /prism/tags/{tag}/glams[?cursor=]` | | `{ items: Glam[], nextCursor }`; tags are stored lowercase without a leading `#` |
| `GET /prism/seasons/current` | | `{ id, name, startsAtUnix, endsAtUnix, standings: [{ user, points, rank }], me: { points, rank } }` |
| `POST /reports` | `{ targetType: "prism_glam", targetId, reason }` | as other reports |

Uploads use `/media/upload-url` with scope `prism`, the same as contest entries.

### The contest on glams

A contest entry keeps today's routes (`PUT /prism/weeks/{id}/entry`, tracks, ballots, results). Any photo can be entered in either track: a GPose or ReShade shot imported from the PC works the same as a phone camera shot. A Vanilla entry lists its gear by hand, one item and its dyes per slot, and needs at least one piece; that list is the entry's `outfit`, and the server refuses mods on it. A Modded entry has no gear list: its `outfit` is an optional list of mods on slot `-1`, read from Penumbra, with any mod the entrant leaves out removed. The entry also carries the entrant's `race` and `gender` (as on a glam) when the client knows them, so a published winner shows up under Fits me. While the week runs the entry stays nameless and out of every feed. When results are out the server publishes it as a glam on the author's profile with `weekId` and `placing` set, and a glam that is already public can't be entered. Each podium placing in the results carries that glam's `glamId`, so tapping a winner opens the full glam page (likes, saves, try on); without it the client shows the placing's photo and outfit read only.

### Prizes

Each podium place pays coins through its coin rule, plus whatever the moderator console adds: gil, a badge, and a frame for good or for a number of days. Badges, frames and coins go out when the final is counted. Gil is traded by hand from the console's payout list, so the rewards page says it can take a few days. The team cannot trade on the Chinese servers, so a Chinese account (a Rising Stones id) that wins a gil place is paid that place's `chineseCoins` instead under the `prism.gil.cn` coin rule, and no gil is owed; each prize's `chineseCoins` carries the amount, and the rewards page tells Chinese players about the swap.

A moderator can end a week early with the console's step buttons and, once its results are out, start the next week's round at once (`POST /mod/api/prism/rounds/start-next`). That round uses the next week's id and scheduled theme, runs its usual lengths from the moment it starts, and is the current round until the calendar reaches its week.

### Activity

Prism's activity screen, its home screen badge and its banners read the phone's shared notifications, the same rows Aethergram and Chirper use (`NotificationDto`; type numbers in `Core/Social/SocialActivity.cs`): `GET /notifications?app=prism[&cursor=]`, `unreadByApp.prism` on the global poll, and `POST /notifications/read { upToUnix, app: "prism" }` when the player opens the screen. Rows carry `app: "prism"` and ping the realtime social channel like any other notification. Prism needs three types:

| Type | Sent when | Fields |
| --- | --- | --- |
| `0` like | someone likes one of your glams, once per liker per glam | actor fields, `postId` (the glam id), `preview` (the glam title), `imageUrl` (the glam's cover photo) |
| `2` follow | someone follows you through Prism, that is a `/follows/{id}` call sent with `X-Aep-App: prism` | actor fields |
| `23` placing | a week's results are out, one row per entrant | `postId` (the glam published from the entry), `preview` (the week's title), `rank` (the entry's final placing, from 1), `imageUrl` (the glam's cover); actor fields empty |

Follow requests and accepts (types 14 and 15) belong to Aethergram, which owns the follow graph, so they never carry `app: "prism"`. `rank` and `imageUrl` are new optional fields on the notification row: other apps ignore them, and `imageUrl` draws a thumbnail on any Prism row that has one. Tapping a like or placing opens the glam, a follow opens the follower's profile, and a banner tap lands in the same place.

### Rules the server enforces

- Up to 5 new glams per account per day, 4 photos each, 3:4.
- Mod links outside the allowlist are dropped.
- Glam of the day (`scope=today`) changes at the daily reset, 15:00 UTC. The client shows it as the card on top of Discover, not as a tab.
- Popular (`scope=popular`) ranks the last seven days by likes and try ons.
- Popular orderings count likes and try-ons from distinct, established accounts only.
