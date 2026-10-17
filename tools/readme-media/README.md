# README media

Renders the images and clips in docs/media/readme/ that the repository README shows:

- **numbers.png**: the Summer 2026 figures card.
- **Clips** (chirper.webp, aethergram.webp, chocochat.webp, velvet.webp, music.webp): looping animated WebP of each app.
- **icons/**: rounded copies of the app icons in src/Aetherphone/Icons.

This tool is for maintainers. The phones come from the website's own mockups, and the website source lives in the private Aethernet repository, so the tool cannot run from a public checkout alone. Contributors never need to run it: the rendered files are committed.

The script serves the website locally, lifts one phone onto a clean stage, and records the website's own animations through Chrome's screencast. Music is a scene of its own in scenes/music.js, drawn after the in-game Now Playing sheet with an invented track and lyrics.

home.png and the .gif files next to them (mogcast.gif, mogcast-theater.gif, venues.gif) are in-game captures and are not generated here.

## Run

```bash
npm install
node render.mjs
```

Pass target names to render only some of them, for example `node render.mjs numbers chirper`. By default the script looks for the website in an Aethernet checkout next to this repository; set `AETHERNET_WEBSITE` to the website folder when it lives elsewhere, and `CHROME_PATH` to Google Chrome when it is not in its default Windows location.
