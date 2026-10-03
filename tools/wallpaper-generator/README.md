# Wallpaper generator

Renders the bundled abstract wallpapers in `src/Aetherphone/Wallpapers/`: soft colour fields built
from a vertical base gradient, a few oversized blurred blobs, two soft light ribbons and a faint
diagonal gloss band. Each name ships as a Light and a Dark variant so the day and
night slots and the theme crossfade have a matching pair.

## Run

```bash
pip install pillow numpy
python generate-wallpapers.py
```

Writes every pair into the plugin's wallpaper folder. Pass an output directory as the first argument
and names as the following arguments to regenerate a subset:

```bash
python generate-wallpapers.py ../../src/Aetherphone/Wallpapers Bloom Prism
```

## Adding a pair

Add an entry to `PALETTES` with a `base` top and bottom colour, four `blobs` colours, two `ribbons`
colours and a `gloss` strength, for both `Light` and `Dark`. Placement is seeded from the name, so
re-running produces the same image. Output is 1290 x 2796 JPEG, quality 88, progressive, around 50 to
120 KB each.
