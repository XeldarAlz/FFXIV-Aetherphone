# Emoji generator

Downloads the full color-emoji image set and builds the catalog the plugin renders from.

- **Images**: [Twemoji](https://github.com/jdecked/twemoji) 72x72 PNGs (CC-BY 4.0), one file per
  emoji sequence, written to `src/Aetherphone/Emoji/*.png`. Filenames use Twemoji's codepoint
  convention (FE0F stripped unless the sequence is a ZWJ join), e.g. `1f600.png`,
  `1f469-200d-1f680.png`.
- **Catalog**: `src/Aetherphone/Emoji/catalog.json`, built from
  [emojibase-data](https://github.com/milesj/emojibase) (CC0). Each entry carries the image `file`,
  the canonical `char` to insert, `group`/`order` for the picker, `label`/`tags` for search, the
  `match` strings the renderer maps back to an image, and skin-tone variants under `tones`.

Both outputs are committed to the repo (same convention as `Icons/`) so a normal build needs no
network access.

## Regenerate

```
npm install
npm run build
```

Existing PNGs are skipped, so reruns only refresh `catalog.json` unless the Twemoji or emojibase
versions in `generate-emoji.mjs` change. Emoji whose image is missing upstream are dropped from the
catalog and reported at the end.
