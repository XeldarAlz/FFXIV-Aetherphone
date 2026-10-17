<!--
Title: conventional commit style with a scope and a lowercase summary, e.g.
  feat(timers): add a retainer venture alarm
  fix(jobs): count a level 50 beastmaster as capped
One concern per PR. If the title needs an "and", split the PR.
Target dev, the default branch; master only moves when a release is cut.
Labels are set for you from the title and the files you touched; leave them alone.

Translation-only PR (editing one JSON under src/Aetherphone/Localization/)?
Fill in "What" and open it; CI runs the lockstep check for you and the rest
of this template does not apply. See docs/translating.md.
-->

## What

<!-- One or two sentences: what changes, and where (which app, Core service, or doc). -->

## Why

<!-- The motivating problem or the user-visible behavior this fixes. -->

Closes #

## How I tested it

<!--
Exact steps a reviewer can repeat.
- Release build: open the phone with /phone and exercise the screen you touched.
- Debug build: loads side by side as AetherphoneDev, opens with /phonedev, and
  talks to the development Aethernet instance. Use it for anything backend-facing.
- Beta build: loads side by side as AetherphoneBeta and opens with /phonebeta.
- Messaging: send yourself a /tell and watch it land. Notifications: /phone test.
- State: if you touched persistence, say what you did to verify old configs survive.
-->

## Screenshots

<!-- UI changes only, otherwise delete this section. Before/after images, or a short clip for motion. -->

## Backend coordination

<!--
Delete unless this changes anything shared with Aethernet: a request path, a DTO
shape, or a realtime signal name. The backend lives in a separate repository and
deploys independently, so say what has to be live on the server before this can
merge, and how the client behaves against a server without the change in the meantime.
-->

## AI involvement

<!--
One of: Assist, Pair, Copilot, Auto. Delete this section if you used no AI or
only autocomplete and inline suggestions. Nobody is judged for the level, only
for an undeclared one. Level definitions and why we ask: docs/ai-usage.md.
New asset in this PR? Say where it came from on the same line.
-->

## Checklist

Gates (CI enforces all of these):

- [ ] `dotnet build Aetherphone.sln -c Release` finishes with zero warnings (CI treats every warning, code-style rules included, as an error)
- [ ] `dotnet test` passes (this is where localization lockstep and accent contrast are enforced)
- [ ] No em dashes anywhere: code, strings, JSON catalogs, docs
- [ ] No `async void`, no new LINQ outside the allowlisted files, no raw `ImGuiHelpers.GlobalScale` (use `UiScale.Current`), no hand-formatted clock text (use `TimeText.Clock`)
- [ ] Namespaces match their folders (Windows/Components is one flat namespace), no `_`-prefixed fields, no UTF-8 byte order marks, and Core or Windows code never imports an app

Strings:

- [ ] Every new user-visible string is a LocString in `Core/Localization/L.cs` plus the same key in all nine JSONs under `src/Aetherphone/Localization/`, in this same PR
- [ ] Any changed English text is updated in both `L.cs` and `en.json` so they do not drift

Quality:

- [ ] Verified in-game on the screens this touches, on the build variant that matters (`/phone`, `/phonedev`, or `/phonebeta`)
- [ ] Draw-path code allocates nothing per frame and uses the shared `Windows/Components/` widgets, `TextStyles`, and `Metrics` tokens
- [ ] README updated if this changes what a user sees or types; the relevant page under `docs/` still tells the truth
- [ ] Commit messages and this PR body carry no AI attribution trailers
- [ ] AI involvement declared above if it went past autocomplete, and any new asset has a provenance line (docs/ai-usage.md)
