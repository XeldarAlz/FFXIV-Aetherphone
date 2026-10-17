# Translating Aetherphone

This guide is for translators. You do not need to know how to code, and you do not need to install anything: everything below works in a web browser with a free GitHub account. Aetherphone ships one text file per language, and improving a translation means editing your language's file and sending the change to the project as a pull request. If you are a developer adding a new string, read [Localization](localization.md) instead.

## Pick your language

| Language | File |
| --- | --- |
| German | [de.json](https://github.com/XeldarAlz/FFXIV-Aetherphone/blob/dev/src/Aetherphone/Localization/de.json) |
| Spanish | [es.json](https://github.com/XeldarAlz/FFXIV-Aetherphone/blob/dev/src/Aetherphone/Localization/es.json) |
| French | [fr.json](https://github.com/XeldarAlz/FFXIV-Aetherphone/blob/dev/src/Aetherphone/Localization/fr.json) |
| Japanese | [ja.json](https://github.com/XeldarAlz/FFXIV-Aetherphone/blob/dev/src/Aetherphone/Localization/ja.json) |
| Portuguese (Brazil) | [pt.json](https://github.com/XeldarAlz/FFXIV-Aetherphone/blob/dev/src/Aetherphone/Localization/pt.json) |
| Russian | [ru.json](https://github.com/XeldarAlz/FFXIV-Aetherphone/blob/dev/src/Aetherphone/Localization/ru.json) |
| Turkish | [tr.json](https://github.com/XeldarAlz/FFXIV-Aetherphone/blob/dev/src/Aetherphone/Localization/tr.json) |
| Chinese (Simplified) | [zh.json](https://github.com/XeldarAlz/FFXIV-Aetherphone/blob/dev/src/Aetherphone/Localization/zh.json) |
| English (reference only) | [en.json](https://github.com/XeldarAlz/FFXIV-Aetherphone/blob/dev/src/Aetherphone/Localization/en.json) |

Read en.json to see the English source, but never edit it. A translation pull request changes exactly one file: yours.

## Words you will meet

- **Repository**: the project's files on GitHub.
- **Fork**: your own copy of the repository, which you can change freely.
- **Branch**: a named set of changes. GitHub creates one when you save an edit, with a name like `patch-1`.
- **Commit**: one saved change, with a short message.
- **Pull request**: your request asking the project to take the changes from your branch. Reviewers comment on it, and new commits on the branch join it.
- **Diff**: the view of what changed: removed lines in red, added lines in green.
- **Checks (CI)**: programs that test the files on every pull request. A green tick means they passed, a red X means one failed.
- **Placeholder**: a marker such as `{0}` that the phone replaces with a name, a number, or a count.

## What a line looks like

Three lines, two from the German file and one from the Chinese file:

```json
  "common.cancel": "Abbrechen",
  "common.photoLimit": "Du kannst bis zu {0} Fotos hinzufügen",
  "chirper.posts.one": "{0} 篇帖子",
```

The part before the colon is the **key**. The phone looks the text up by it, so it must never be translated, renamed, or moved. The part after the colon, inside the quotes, is the **value**: the text a player sees. The value is the only thing you change.

## Rules that keep the phone working

Breaking one of these either fails the checks or breaks a screen in the game.

1. **Translate values, never keys.** Everything left of the colon stays exactly as it is in en.json.
2. **Do not add, delete, or reorder lines.** All nine files have the same keys in the same order, so a key sits on the same line number in every file. The checks fail a pull request with a missing or extra key.
3. **Keep every placeholder.** You may move `{0}` to wherever your language needs it, but every placeholder in the English must appear in your value, and you must not invent new ones. Copy each one exactly, including anything after a colon: `{0:N0}` formats a number with digit grouping, and `{1:00}` pads it to two digits.
4. **Never type a lone `{` or `}`.** The checks read every value as a template, so a single brace outside a placeholder fails, even in a value with no placeholders.
5. **Plurals come in pairs.** Keys ending in `.one` and `.other` are two forms of one string: `.one` is for exactly 1 (0 and 1 in French), `.other` for everything else. Do not add other forms. In Russian, write a label and then the number, as in "Новых сообщений: {0}", so 2, 5, and 21 all read correctly.
6. **No em dashes.** The long dash (Unicode U+2014) is banned everywhere in the project, and the checks fail any file that contains one. The Chinese dash 破折号 is that character twice. Use a comma, a colon, parentheses, or 、 instead.
7. **Keep the file valid JSON.** Keep the double quotes around every key and value, the comma after every line except the last, the two-space indent, and any backslash already in a value. One broken line makes the phone ignore the whole file and show English everywhere.
8. **Write real characters.** Some tools save non-Latin text as escape codes (a backslash, the letter u, and four digits per character). That still works, but nobody can review it. Save plain UTF-8 text.

Style, tone, length, consistent terms, and the names that stay in Latin script in every language are in [Localization](localization.md), under its copy rules and its names that never change; follow them too.

## Finding where a string appears

The first part of a key names the area of the phone: `settings.` is the Settings app, `chirper.` the Chirper app, `common.` buttons shared everywhere (Cancel, Loading…), and `changelog.` the changelog in Settings. Neighboring keys usually belong to the same screen, which helps when a short label needs context.

To find a string you saw in the game:

1. Open en.json on GitHub and press Ctrl+F to search for the English text.
2. Note the key on that line, for example `settings.notificationSound`.
3. Search your file for the same key. If its value is still in English, translate it.

If the text is not in en.json, it does not come from these files:

- **Names from the game** (items, places, duties, jobs) come from your game client in its own language and cannot be changed here.
- **Server text** (announcements, polls, badges, frames, shop items) has its own translations. Open a [translation issue](https://github.com/XeldarAlz/FFXIV-Aetherphone/issues/new?template=translation_report.yml) for it instead of a pull request.
- **Other players' posts and messages** show as their authors wrote them.

If en.json and the English you saw in the game disagree, do not edit en.json; mention it in an issue. You cannot preview a translation in the game before release: merged changes appear with the next beta or release build.

## Path A: edit in the browser

Best for fixing a handful of strings.

1. Create a free account on [github.com](https://github.com) and verify your email.
2. Open your language file from the table above.
3. Click the pencil icon ("Edit this file"). GitHub says you need your own copy to propose changes; click **Fork this repository**.
4. The editor opens on your fork. Press Ctrl+F to find the key or the text you want to fix.
5. Change the values. Leave everything else alone.
6. Click **Commit changes...**. In the Propose changes dialog, write a commit message in the project's shape, all lowercase at the start and with no period at the end, for example `fix(i18n): polish the German settings strings`. GitHub uses it as the pull request title, and a title in any other shape fails a check. Click **Propose changes**.
7. GitHub saves your edit on a new branch of your fork and opens a **Comparing changes** page with the diff. Check that the base repository is `XeldarAlz/FFXIV-Aetherphone` and the base branch is `dev`, then click **Create pull request**.
8. The pull request form opens. Check that the title still has the shape from step 6. Under "What", write one line ("polish the German strings in Settings"), and delete the rest of the template. If a translation issue exists, add `Closes #` and its number. Click **Create pull request**.
9. The checks run for a few minutes, and on a first pull request they may wait for a maintainer's approval. A red X is not a rejection: click **Details** and look for a line naming your file and a key, such as `'de.json' key 'common.photoLimit' cannot be parsed`. Usual causes: a broken line, a deleted or added key, a lone brace, an em dash.
10. To fix something, open your pull request, go to the **Files changed** tab, and choose **Edit file** from the file's menu (the three dots). Every new commit updates the same pull request. Reply to review comments there too, and never open a second pull request for the same work.

## Path B: edit the whole file

Best for a full pass over thousands of lines. Fork first: on the [project page](https://github.com/XeldarAlz/FFXIV-Aetherphone), click **Fork**, then **Create fork**.

Either way, write your commit message in the shape from step 6 of Path A.

- **In the browser:** on your fork's page (github.com, then your name, then FFXIV-Aetherphone), press the `.` key. A full editor opens in the browser. Open your file, edit, then commit from the Source Control panel on the left. Go back to your fork's page, click **Contribute**, then **Open pull request**, and continue from step 7 of Path A.
- **On your computer:** open your language file on GitHub and click **Download raw file** (the download icon above the text). Edit it in a text editor that saves UTF-8, such as VS Code or Notepad++, and keep its name (de.json stays de.json).
- **Uploading it back:** on your fork, open the src/Aetherphone/Localization/ folder, click **Add file**, then **Upload files**, and drop your file in to replace the file there. Commit to a new branch, then continue from step 7 of Path A.

Whichever you use, read the diff before you submit. If it shows thousands of changed lines when you touched fifty, your editor reformatted the file (indentation, line endings, or escape codes). Undo it and try again with those settings off.

Do not paste the whole file through a machine translator: it breaks keys, placeholders, and the file format. A machine draft that you read and rewrite line by line is fine. The project's own translations are drafted with AI help and reviewed by people, so native-speaker corrections are exactly what improves them.

## Before you submit

- The file is still valid JSON. Paste it into an online JSON validator if you are unsure.
- The line count matches en.json exactly.
- A search for the em dash (U+2014) finds nothing, including the doubled Chinese dash.
- Every placeholder in the English is still in your value, written exactly the same, and no lone brace remains.
- The diff contains only the lines you meant to change.

## Working as a group

If several people translate the same language, split the work by key prefix: one person takes `settings.` while another takes `chirper.`, and the two pull requests never touch the same lines. Say which prefix you are taking in a translation issue or on Discord before you start. Small, frequent pull requests are reviewed faster than one huge one.

## After you submit

A maintainer reviews the diff, asks about any term that looks off, and merges it. Translators are credited by name in the in-app changelog of the release that carries their work, so say in the pull request which name you want shown. By opening a pull request you agree that your contribution is licensed under AGPL-3.0-or-later, like the rest of the project.

## If GitHub is hard to reach

GitHub is slow or blocked on some networks. If the steps above do not work for you, bring your edited file, or a list of `key: new value` lines, to the [project Discord](https://discord.gg/3HbJCscMyS), and someone will open the pull request for you, with credit to you.

## Good to know

- **Untranslated text shows English, not an error.** The checks make sure every key exists, not that it is translated. English left in a file is worth reporting when you spot it.
- **Editing en.json changes nothing in the game.** The English the phone shows comes from the code. Report English typos in an issue.
