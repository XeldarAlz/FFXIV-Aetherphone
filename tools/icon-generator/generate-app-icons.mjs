// Regenerates the app icon PNGs under src/Aetherphone/Icons from Tabler Icons.
//
// Usage:
//   cd tools/icon-generator
//   npm install
//   node generate-app-icons.mjs
//
// Each icon is downloaded as an outline SVG, recolored to white, given a
// slightly heavier stroke for small-size legibility, and rasterized to a
// 256px transparent PNG named after the app's IPhoneApp.Id. They ship white
// so the client tints them to the active theme at runtime (see
// Windows/Components/AppIconTextures.cs).

import sharp from "sharp";
import { writeFileSync, mkdirSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const OUT = resolve(here, "../../src/Aetherphone/Icons");
const TABLER_VERSION = "3.44.0";
const SIZE = 256;
const STROKE = "2.25";

// app.Id -> Tabler outline icon name. Note: Tabler has no literal "bird"
// (chirper uses feather), and brand-instagram is intentionally avoided as a
// trademarked logo (aethergram uses aperture).
const map = {
  phone: "phone",
  messages: "message-circle",
  contacts: "address-book",
  character: "user-circle",
  camera: "camera",
  photos: "photo",
  skywatcher: "cloud",
  venues: "map-pin",
  maps: "map-2",
  findpeople: "user-search",
  chirper: "feather",
  aethergram: "aperture",
  velvet: "flame",
  news: "news",
  collections: "trophy",
  market: "chart-bar",
  wallet: "wallet",
  inventory: "backpack",
  music: "music",
  clock: "clock",
  timers: "hourglass",
  dailies: "checklist",
  fishing: "fish",
  notifications: "bell",
  settings: "settings",
  games: "device-gamepad-2",
  calendar: "calendar",
  feedback: "message-report",
  dev: "terminal-2",
  polls: "chart-bar-popular",
};

function recolor(svg) {
  return svg
    .replaceAll('stroke="currentColor"', 'stroke="#ffffff"')
    .replaceAll('fill="currentColor"', 'fill="#ffffff"')
    .replace('stroke-width="2"', `stroke-width="${STROKE}"`)
    .replace('width="24"', `width="${SIZE}"`)
    .replace('height="24"', `height="${SIZE}"`);
}

mkdirSync(OUT, { recursive: true });

let ok = 0;
const failed = [];
for (const [id, icon] of Object.entries(map)) {
  const url = `https://cdn.jsdelivr.net/npm/@tabler/icons@${TABLER_VERSION}/icons/outline/${icon}.svg`;
  try {
    const res = await fetch(url);
    if (!res.ok) {
      failed.push(`${id} (${icon}): HTTP ${res.status}`);
      continue;
    }
    const svg = recolor(await res.text());
    const png = await sharp(Buffer.from(svg), { density: 384 })
      .resize(SIZE, SIZE, { fit: "contain", background: { r: 0, g: 0, b: 0, alpha: 0 } })
      .png()
      .toBuffer();
    writeFileSync(resolve(OUT, `${id}.png`), png);
    ok++;
    console.log(`  ${id.padEnd(13)} <- ${icon}`);
  } catch (error) {
    failed.push(`${id} (${icon}): ${error.message}`);
  }
}

console.log(`\nDone: ${ok} icons written to ${OUT}`);
if (failed.length) {
  console.log("FAILED:\n" + failed.map((line) => "  " + line).join("\n"));
  process.exitCode = 1;
}
