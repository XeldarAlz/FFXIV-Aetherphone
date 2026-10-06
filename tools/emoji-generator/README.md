# Emoji generator

Downloads the full color-emoji image set and builds the catalog the plugin renders from.

- **Images**: [Twemoji](https://github.com/jdecked/twemoji) 72x72 PNGs (CC-BY 4.0), one file per
  emoji sequence, written to `src/Aetherphone/Emoji/*.png`. Filenames use Twemoji's codepoint
  convention (FE0F stripped unless the sequence is a ZWJ join), e.g. `1f600.png`,
  `1f469-200d-1f680.png`.
- **Catalog**: `src/Aetherphone/Emoji/catalog.json`, built from
  [emojibase-data](https://github.com/milesj/emojibase) (MIT). Each entry carries the image `file`,
  its `short` shortcodes, `group`/`order` for the picker, `label`/`tags` for search, and skin-tone
  variants under `tones` (each a `tone` and its `file`). Messages store emoji as `:shortcode:` text,
  which the plugin resolves back to the image through `short`.

Both outputs are committed to the repo (same convention as `Icons/`) so a normal build needs no
network access.

## Regenerate

```
npm install
npm run build
```

Existing PNGs are always skipped, so a rerun downloads only new images and rewrites `catalog.json`.
For new emoji, bump `TWEMOJI_VERSION` in `generate-emoji.mjs` or the `emojibase-data` version in
`package.json`. A version bump never refreshes art that already exists: delete a PNG to pick up an
upstream redraw. Emoji whose image is missing upstream are dropped from the catalog and reported at
the end.
