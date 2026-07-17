# Security policy

## Supported versions

Only the latest release published to the custom Dalamud repo is supported. If you're on an older `vX.Y.Z`, please update before reporting.

## Reporting a vulnerability

Please report security issues privately via GitHub's private vulnerability reporting:

https://github.com/XeldarAlz/FFXIV-Aetherphone/security/advisories/new

Please don't open a public issue or Discussion for anything that could let someone else exploit users of the plugin before a fix is out.

What counts:

- Code execution or crashes triggerable by crafted game state, chat input, or a malformed message.
- The plugin reading, sending, or persisting chat/tells or character data it shouldn't (beyond the documented messaging features, which are the point).
- The Aethernet backend: anything in the account, transport, or end-to-end-encryption layer that would expose another user's identity or messages.

What doesn't:

- The fact that Aetherphone reads your tells and lets you reply to them. That's the whole feature; the data stays on your machine until you choose to send it.

I'll aim to acknowledge reports within a few days and to ship a fix or workaround as soon as I've verified the issue.
