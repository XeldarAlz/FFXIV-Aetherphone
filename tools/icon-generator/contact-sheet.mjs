import sharp from "sharp";
import { existsSync, mkdirSync, readdirSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const ICONS_DIR = resolve(here, "../../src/Aetherphone/Icons");
const DEFAULT_OUT = resolve(here, "masters");
const FOREGROUND_SUFFIX = ".fg.png";
const SHEET_BACKGROUND = "#E5E5EA";
const LABEL_INK = "#1C1C1E";
const MASK_CORNER_FRACTION = 0.26;
const MASK_EXPONENT = 4.2;
const MASK_SAMPLES_PER_CORNER = 32;

const sheets = [
  { name: "painted-icons-sheet.png", tile: 96, columns: 8, gutter: 28, labelHeight: 20, fontSize: 10 },
  { name: "painted-icons-sheet-32.png", tile: 32, columns: 12, gutter: 44, labelHeight: 14, fontSize: 7 },
];

function paintedIds() {
  const files = readdirSync(ICONS_DIR);
  const ids = [];
  for (const file of files) {
    if (!file.endsWith(FOREGROUND_SUFFIX)) {
      continue;
    }
    const id = file.slice(0, -FOREGROUND_SUFFIX.length);
    if (files.includes(`${id}.png`)) {
      ids.push(id);
    }
  }
  return ids.sort();
}

function squirclePath(side) {
  const corner = side * MASK_CORNER_FRACTION;
  const power = 2 / MASK_EXPONENT;
  const centres = [
    [side - corner, corner, -Math.PI / 2],
    [side - corner, side - corner, 0],
    [corner, side - corner, Math.PI / 2],
    [corner, corner, Math.PI],
  ];
  const points = [];
  for (const [centreX, centreY, startAngle] of centres) {
    for (let sample = 0; sample <= MASK_SAMPLES_PER_CORNER; sample++) {
      const angle = startAngle + (sample / MASK_SAMPLES_PER_CORNER) * (Math.PI / 2);
      const cosine = Math.cos(angle);
      const sine = Math.sin(angle);
      const pointX = centreX + corner * Math.sign(cosine) * Math.abs(cosine) ** power;
      const pointY = centreY + corner * Math.sign(sine) * Math.abs(sine) ** power;
      points.push(`${pointX.toFixed(3)} ${pointY.toFixed(3)}`);
    }
  }
  return `M${points.join("L")}Z`;
}

async function maskedTile(id, side, mask) {
  return sharp(resolve(ICONS_DIR, `${id}.png`))
    .resize(side, side, { kernel: "lanczos3" })
    .ensureAlpha()
    .composite([{ input: mask, blend: "dest-in" }])
    .png()
    .toBuffer();
}

async function label(text, width, fontSize) {
  const markup = `<span foreground="${LABEL_INK}">${text}</span>`;
  return sharp({ text: { text: markup, rgba: true, width, align: "centre", font: `sans-serif ${fontSize}` } })
    .png()
    .toBuffer({ resolveWithObject: true });
}

async function buildSheet(ids, layout, outDir) {
  const cellWidth = layout.tile + layout.gutter;
  const cellHeight = layout.tile + layout.labelHeight + layout.gutter;
  const rows = Math.ceil(ids.length / layout.columns);
  const width = layout.columns * cellWidth + layout.gutter;
  const height = rows * cellHeight + layout.gutter;
  const maskSvg = `<svg xmlns="http://www.w3.org/2000/svg" width="${layout.tile}" height="${layout.tile}"><path d="${squirclePath(layout.tile)}" fill="#FFFFFF"/></svg>`;
  const mask = await sharp(Buffer.from(maskSvg)).png().toBuffer();
  const layers = [];
  for (let index = 0; index < ids.length; index++) {
    const column = index % layout.columns;
    const row = Math.floor(index / layout.columns);
    const cellLeft = layout.gutter + column * cellWidth;
    const cellTop = layout.gutter + row * cellHeight;
    layers.push({ input: await maskedTile(ids[index], layout.tile, mask), left: cellLeft, top: cellTop });
    const caption = await label(ids[index], cellWidth, layout.fontSize);
    const captionLeft = cellLeft + Math.round((layout.tile - caption.info.width) / 2);
    layers.push({ input: caption.data, left: Math.max(0, captionLeft), top: cellTop + layout.tile + 2 });
  }
  const sheet = await sharp({ create: { width, height, channels: 4, background: SHEET_BACKGROUND } })
    .composite(layers)
    .removeAlpha()
    .png()
    .toBuffer();
  const outPath = resolve(outDir, layout.name);
  writeFileSync(outPath, sheet);
  console.log(`  ${layout.name} (${ids.length} icons at ${layout.tile} px) -> ${outPath}`);
}

const outDir = process.argv[2] ? resolve(process.argv[2]) : DEFAULT_OUT;
if (!existsSync(outDir)) {
  mkdirSync(outDir, { recursive: true });
}
const ids = paintedIds();
for (const layout of sheets) {
  await buildSheet(ids, layout, outDir);
}
