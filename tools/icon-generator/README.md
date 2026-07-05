# App icon generator

Regenerates the home-screen app icons in `src/Aetherphone/Icons/` from
[Tabler Icons](https://tabler.io/icons) (MIT).

## Run

```sh
cd tools/icon-generator
npm install
npm run build
```

This downloads each mapped Tabler outline SVG, recolors it to white, thickens
the stroke slightly for small-size legibility, and rasterizes it to a 256px
transparent PNG named after the app's `IPhoneApp.Id`.

The icons ship **white on transparent** so the client tints them to the active
theme at runtime (`Windows/Components/AppIconTextures.cs` draws them via
`AddImage(..., tint)`, falling back to the procedural `AppIconArt` for any id
without a PNG, e.g. the mini-games).

## Changing an icon

Edit the `map` (app id -> Tabler icon name) in `generate-app-icons.mjs` and
re-run. Browse icon names at https://tabler.io/icons. Avoid `brand-*` icons —
those are trademarked logos.

## License

Tabler Icons is MIT licensed; the notice ships with the plugin in
`THIRD-PARTY-NOTICES.md` at the repo root.
