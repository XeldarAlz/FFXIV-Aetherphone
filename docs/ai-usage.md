# AI Usage

**Last updated:** 6 October 2026

This document explains how AI tools are used to build Aetherphone, the level of involvement that amounts to, where the content inside the phone comes from, and how AI relates to your content on Aethernet.

Aetherphone is distributed through its own repository, so the [official Dalamud plugin repository's AI policy](https://dalamud.dev/plugin-publishing/ai-policy/) does not formally apply to it. We follow that policy anyway, because it is the community's standard for disclosing AI use in Dalamud plugins. This document declares our level in the policy's terms and applies its rules to code, translations and assets. The Aethernet online service is built the same way.

## Declared level

Dalamud's policy defines six levels of AI involvement: None, Hint, Assist, Pair, Copilot and Auto.

**Aetherphone declares Copilot.** AI tools carry out most of the implementation, while people plan the work, review it, test it and take responsibility for the result.

We chose this label deliberately rather than the lowest one that could be argued. Within a single task, implementation runs without step-by-step supervision, which on its own could read as Auto. Every task is bounded on both sides by people who decide what gets built and whether it ships, which is what Copilot describes.

## How the work is done

Development uses AI coding agents in structured workflows: agents implement a change, other agents review the result, and further checks exercise it before anyone signs off. The same tooling supports code review against the project's written conventions.

Every change passes these human gates, in order, and none of them can be skipped:

1. **The specification comes first.** What to build, and the constraints it must respect, are written down before any work starts.
2. **The change is reviewed.** Output is read and checked before it is merged, not after users find a problem.
3. **It is tested in the game.** On a real game client, on the screen that changed. A successful build is not a test.
4. **Responsibility stays with people.** Every merged change is ours to explain, defend and fix. "The AI did it" is never an answer.
5. **Feedback is taken on its merits.** AI-assisted work deserves close review from users and contributors, and the answer to a review comment is a fix or a reason.

## Standards

Using AI does not lower any standard. AI-assisted code is held to exactly the same written conventions as all other code in the project, and a reviewer should not be able to tell the difference.

What AI changes is where verification effort goes. AI tools get game and Dalamud interfaces wrong often enough that every change touching the game is treated as unproven until it has been tested in game.

## Contributing

Contributors follow four rules:

1. **Declare your level** in the pull request description if you used AI beyond autocomplete or inline suggestions, using Dalamud's level names. None and Hint need no declaration.
2. **Test your change in game** before you open the pull request.
3. **Be able to explain your code.** If you cannot say why something is written the way it is, it is not ready.
4. **Document every new asset's source**, and add attribution where its license requires it. **AI-generated assets are not accepted.** The phone ships none, and this rule has no exceptions.

Nobody is judged for the level they declare. An undeclared one is the problem.

## Content inside the phone

**No AI-generated content ships inside the phone: no icon, wallpaper, phone case, sound, ringtone or font.** Players see and hear these directly, so the rule for content is stricter than the rule for code. A contribution that adds AI-generated content is rejected on that basis alone.

| Content | Source |
| --- | --- |
| App icons | Painted from [Phosphor Icons](https://phosphoricons.com) fill glyphs, recolored, by the generator in tools/icon-generator |
| Emoji | [Twemoji](https://github.com/jdecked/twemoji) 15.1.0, unmodified |
| Fonts | Inter in four weights, [Pirata One](https://github.com/google/fonts/tree/main/ofl/pirataone) for the seasonal Chirper and Aethergram wordmarks, and a subset of [Tabler Icons](https://tabler.io/icons) for the glyphs inside apps |
| Phone cases | Drawn by human artists, each credited by name in the app's Settings |
| Interface sounds | Original to Aetherphone, synthesized from code in tools/sound-generator, plus a CC0 shutter from BigSoundBank, one Material Design sound by Google and two Android Open Source Project alarm tones |
| Game sounds | Original to Aetherphone, synthesized from code in tools/sound-generator, plus CC0 card and chip recordings from Kenney |
| Wallpapers | Original to Aetherphone |
| Ringtones and notification sounds | Material Design sounds by Google and the Android Open Source Project |

Licenses and attributions are listed in [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md), which ships with every release.

## Interface translations

English text is written by hand. The eight other interface languages are translated with AI assistance and reviewed by people. Official in-game terms, such as job, place and item names, are taken from the game's own data rather than translated freely, so they match what players see in their client. Native-speaker corrections are welcome from anyone through [the translator guide](translating.md).

## AI and your content

The **Translate** feature uses AI to translate the text you choose, in the plugin and the companion app. Only that text is sent, never your account details, and translations of private messages are not stored.

No AI moderates content on Aethernet. Moderation is done by people, who act on reports and may also review content directly. We do not use your posts or messages to train AI models and do not provide them to anyone for that purpose.

## Questions

Questions about this document can be sent to **contact@aetherphone.net**.
