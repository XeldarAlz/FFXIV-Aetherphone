# Translating Aetherphone

This doc is for translators, not engineers. You do not need C#, .NET, git, or a build to help. Aetherphone ships nine language files, one flat JSON file per language, and improving a translation means editing one of those files and opening a pull request. Everything below can be done in a web browser with a free GitHub account. If you are a developer adding a new string, read [Localization](localization.md) instead: new keys are a code change and must land in `L.cs` plus all nine files at once.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Localization/zh.json | Chinese (zh-CN). The file a Chinese translator edits |
| src/Aetherphone/Localization/en.json | English reference. Read it to see the source text, never edit it |
| src/Aetherphone/Localization/ | The other seven: de, fr, ja, es, pt, ru, tr |
| src/Aetherphone/Core/Localization/L.cs | Where the English text actually lives. Developers only |

The nine files are `de`, `en`, `es`, `fr`, `ja`, `pt`, `ru`, `tr`, `zh`. Pick the one for your language and stay in it. A translation pull request should touch exactly one file.

## What a line looks like

```json
  "common.cancel": "取消",
  "chirper.posts.one": "{0} 篇帖子",
  "common.photoLimit": "最多可以添加 {0} 张照片",
```

The part before the colon is the key. It is machine-readable, the code looks strings up by it, and it must never be translated, renamed, or reordered. The part after the colon, inside the quotes, is the text a player sees. That is the only thing you change.

## The rules

1. **Translate values, never keys.** Left of the colon stays byte for byte identical to en.json.
2. **Do not add, delete, or reorder lines.** All nine files carry the same keys in the same order, so the same key sits on the same line number in every file. CI compares every file against the list of keys the code declares and fails the pull request when one is missing or extra, naming the file and the key. Adding a brand new key is a code change and belongs in a developer pull request.
3. **Keep every placeholder.** `{0}`, `{1}` and so on are filled in at runtime with a name, a number, or a count. They may move to wherever your language needs them, but every placeholder in the English source must appear in the translation, and you must not invent new ones. Copy each placeholder exactly, including anything after a colon: `{0:N0}` formats a number with digit grouping and `{1:00}` pads to two digits. A translation that references `{1}` when the code only supplies one value crashes that screen on every frame.
4. **Never type a lone `{` or `}`.** CI reads every value as a format string, so a single brace outside a placeholder fails the pull request, even in a string that has no placeholders.
5. **Plurals come in pairs.** Keys ending in `.one` and `.other` are two forms of the same string. Only two forms exist. Do not add `.few` or `.many`; nothing reads them. For Chinese, both forms are usually the same text.
6. **No em dashes, anywhere.** The em dash character (Unicode U+2014, the long horizontal dash) is banned repo wide, and CI fails the pull request if it appears in any file. This doc cannot even print it. The rule matters for Chinese in particular: the Chinese dash 破折号 is that same character written twice. Use a comma, a colon, parentheses, or `、` instead.
7. **Use the ellipsis character `…`, never three periods.** For example "Loading…".
8. **Keep it short.** This is a phone screen drawn at a fixed width. Text that is much longer than the English source wraps onto extra lines or gets clipped inside buttons and chips. Aim for the English length or shorter.
9. **Some names never change.** Aetherphone, Linkpearl, Velvet, and Muster stay in Latin script in every language, including Chinese. Other app names already have settled Chinese names in zh.json (Chirper is 叽叽, Aethergram is 以太图集); reuse what the file already uses instead of inventing a second name for the same app.
10. **Be consistent.** The same in-app term should get the same translation in every string. If you change how a recurring term is translated, change it everywhere in the same pull request.
11. **Speak to the player directly**, in plain second person, the way the English source does.
12. **Keep the file valid JSON.** UTF-8, two space indent, one key per line, a comma after every entry except the last one, and the surrounding double quotes intact. If a value contains `\"` or `\\`, keep the backslash. One malformed file mutes the entire language: the catalog fails to parse and every string in your language falls back to English.
13. **Write real characters, not escapes.** Some editors and translation tools rewrite CJK text into ASCII escape sequences, so the two characters of "取消" get saved as "\u53d6\u6d88". That still parses, but it makes the file unreadable and the diff impossible to review. Save plain UTF-8 with the characters intact.

## Path A: edit in the browser

Best for fixing a handful of strings. Nothing to install.

1. Create a free account on [github.com](https://github.com) and verify your email.
2. Open the file you want to edit: [src/Aetherphone/Localization/zh.json](https://github.com/XeldarAlz/FFXIV-Aetherphone/blob/dev/src/Aetherphone/Localization/zh.json).
3. Click the pencil icon ("Edit this file"). GitHub tells you that you need your own copy first and offers a "Fork this repository" button. Click it. A fork is your personal copy of the project; you can change anything in it without affecting the original.
4. The editor opens. Use Ctrl+F to jump to the key or the text you want to fix.
5. Change the values. Leave everything else alone.
6. Click the green "Commit changes..." button. In the dialog:
   - Commit message: a short lowercase line in the project style, for example `fix(i18n): tighten the chinese settings and notification strings`.
   - Extended description: optional, use it to explain a term choice.
   - Keep "Create a new branch for this commit and start a pull request" selected, and give the branch a name like `zh-settings-strings`.
7. Click "Propose changes". The pull request form opens, pointed at `XeldarAlz/FFXIV-Aetherphone`, branch `dev`. If the base branch says `master`, change it to `dev`.
8. Fill in the template. For a translation only pull request, write one line under "What" ("polish the Chinese strings in Settings and notifications"). The rest of the template does not apply, and you can delete it. If a translation issue exists, add `Closes #` and its number.
9. Click "Create pull request". You are done.
10. Automated checks run for a few minutes. On a pull request from a fork they may not start until a maintainer approves the run, so seeing no checks at first is normal. A red X is not a rejection; click "Details" to read what failed. The usual causes for a translation pull request are a broken JSON file, a deleted or added key, a lone `{` or `}`, or an em dash, and the failure message names the file and the key. Fix it by editing the file again on your branch: every new commit updates the same pull request automatically.
11. When review comments arrive, reply in the pull request and push more edits to the same branch. Do not open a second pull request for the same work.

## Path B: edit the whole file

Best for a full pass over thousands of lines.

- **In the browser:** open the repository and press the `.` key. That launches a full VS Code editor in the browser on your fork. Edit, then use the Source Control panel on the left to commit to a new branch and create the pull request from there.
- **On your computer:** download the raw file (the "Raw" button, then save the page), edit it in a real text editor such as VS Code or Notepad++ with UTF-8 encoding, then on your fork open the `src/Aetherphone/Localization/` folder, click "Add file" and "Upload files", and drop your `zh.json` in. Uploading a file with the same name replaces it. Commit to a new branch and open the pull request as in Path A.

Whatever you use, check the diff before you submit. If the diff shows thousands of changed lines when you only touched fifty, your editor reformatted the file: it probably changed the indentation, rewrote the line endings, or escaped the non-ASCII characters. Undo it and try again with those settings off. Reviewers cannot check a diff that touches the whole file.

Do not paste the whole file through a machine translator. It breaks keys, placeholders, and JSON structure, and it produces exactly the stiff phrasing this effort exists to fix. Machine output as a first draft that you then read and rewrite line by line is fine.

## Finding the string you saw in game

If a screen shows English while the phone is set to Chinese, the value in zh.json may still be English. Every key exists in every file (CI enforces that), so look for the value:

1. Open en.json on GitHub and use Ctrl+F to search for the English text you saw.
2. Note the key on that line, for example `settings.notificationSound`.
3. Search zh.json for the same key. If its value is still English, translate it.

If you cannot find the text in en.json at all, it may not come from the language files:

- **Names from the game itself** (items, places, duties, jobs) come from your game client in its language. They cannot be changed here.
- **Server copy**: announcements, polls, badges, frames, and shop items carry their own translations on the Aethernet server. Open a [translation issue](https://github.com/XeldarAlz/FFXIV-Aetherphone/issues/new?template=translation_report.yml) for those instead of a pull request.
- **Other players' posts and messages** show as their authors wrote them.

The English players actually see comes from the code file `src/Aetherphone/Core/Localization/L.cs`, and en.json is a copy of it. If en.json and what you saw disagree, search L.cs for the text: the key is the first quoted string after `new(`, on the same line as the English text or the line above it. Do not edit en.json to match what you saw: bringing it back in sync is a code-side task for a developer, so mention the mismatch in an issue instead.

## Before you submit

- The file still parses as JSON. Paste it into any online JSON validator if you are unsure.
- The line count matches en.json exactly. If yours differs, you added or removed a line.
- Search the file for the em dash (U+2014). There should be zero results, including the doubled Chinese 破折号.
- Every `{0}` and `{1}` that the English source has is still present in your translation, written exactly as in the source (including any `:N0` or `:00` after the number), and no lone `{` or `}` remains.
- The diff contains only lines you meant to change.

## Working as a group

If several people translate the same language, split the work by key prefix. The keys are grouped by area (`chirper.*`, `settings.*`, `velvet.*`, `changelog.*`), so one person taking `settings.*` while another takes `chirper.*` produces two pull requests that never touch the same lines. Two people editing the same lines produces a merge conflict that someone has to resolve by hand.

Small, frequent pull requests get reviewed and merged faster than one enormous one. If a pull request sits open long enough for `dev` to move ahead, GitHub shows an "Update branch" button; click it before asking for another review.

## What happens after you submit

The maintainer reviews the diff, asks questions if a term looks off, and merges. We aim to credit translators by name in the in-app changelog for the release that carries their work, so tell us in the pull request which name you want to appear. The change ships with the next plugin release, not immediately.

By opening a pull request you agree your contribution is licensed under AGPL-3.0-or-later, the same as the rest of the project.

## If GitHub is hard for you to reach

GitHub is slow or unreachable from some networks. If the pull request flow is not workable for you, bring the edited file, or a plain list of `key: new value` lines, to the [project Discord](https://discord.gg/3HbJCscMyS) and someone will open the pull request on your behalf with credit to you. It is slower for everyone and it does not scale past a few strings, so use it only if the browser flow genuinely does not work for you.

## Gotchas

- **Untranslated text does not error, it shows English.** CI checks that every key exists, not that its value is translated, and nothing warns players in a release build. Untranslated text is easy to miss and worth reporting when you spot it.
- **One malformed file mutes the whole language.** A stray comma or unbalanced quote makes every string in your language fall back to English until it is fixed.
- **Editing en.json changes nothing in game.** English text is resolved from `L.cs`, and en.json is only a reference copy. Report English typos as an issue.
- **The Chinese 破折号 is two em dashes** and fails CI. So does a single one pasted in from a document.
- **Placeholder mistakes crash a screen.** `{0}` is safe to move, never safe to delete or renumber past what the English source uses. A lone brace fails CI before it can ship.
- **The file is flat and ordered.** Keep every key on the line it is on today. The same key sits on the same line in every language file, so you can line up your file against en.json side by side.

## Related docs

- [Localization](localization.md): the developer side, how keys are declared and resolved at runtime
- [CONTRIBUTING.md](../CONTRIBUTING.md): the general pull request process
- [Conventions](conventions.md): the repo wide copy and commit rules
