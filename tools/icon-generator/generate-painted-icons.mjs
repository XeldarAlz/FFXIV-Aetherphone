import sharp from "sharp";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const ICONS_OUT = resolve(here, "../../src/Aetherphone/Icons");
const MASTERS_OUT = resolve(here, "masters");
const PHOSPHOR_VERSION = "2.1.1";
const SYMBOL_CACHE = resolve(MASTERS_OUT, `phosphor-${PHOSPHOR_VERSION}`);
const PHOSPHOR_VIEWBOX = 256;
const MASTER_SIZE = 1024;
const SHIPPED_SIZE = 512;
const MEASURE_ALPHA_THRESHOLD = 128;
const PNG_OPTIONS = { compressionLevel: 9, palette: false };

const SYMBOL_WIDTH_FRACTION = 0.58;
const ROUND_SYMBOL_WIDTH_FRACTION = 0.62;
const TALL_SYMBOL_HEIGHT_FRACTION = 0.6;
const TALL_ASPECT_LIMIT = 0.88;

const GRADIENT_TOP_LIGHTNESS = 1.1;
const GRADIENT_BOTTOM_LIGHTNESS = 0.88;
const GAMUT_SEARCH_STEPS = 32;

const WHITE = "#FFFFFF";
const SETTINGS_INK = "#D8D8DC";
const GAMBA_GOLD = "#FFD36B";
const GRAPHITE_STOPS = ["#3A3A3C", "#1C1C1E"];
const PAPER_STOPS = ["#FFFFFF", "#F2F2F7"];
const PHOTOS_STOPS = ["#F0B445", "#F77B6B", "#4E9FF0"];

const hues = {
  Green: "#21A837",
  Orange: "#E1741D",
  Gold: "#BE871D",
  Lime: "#809C1D",
  Emerald: "#21A47D",
  Teal: "#21A29D",
  Cyan: "#219FB6",
  Azure: "#1F96F1",
  Indigo: "#728AF9",
  Violet: "#A778F9",
  Orchid: "#EC42F8",
  Rose: "#F95589",
  GambaRose: "#D4246A",
  Red: "#F95C53",
  Slate: "#8A8F9C",
  Chirper: "#2985F0",
  Aethergram: "#EB4D61",
  Velvet: "#E51A5B",
};

function icon(symbol, family, hue, options = {}) {
  return { symbol, family, hue, round: options.round === true, ink: options.ink };
}

const map = {
  message: icon("chats", "colour", "Orange"),
  messages: icon("chat-circle", "colour", "Green", { round: true }),
  settings: icon("gear", "graphite", "Slate", { round: true, ink: SETTINGS_INK }),
  chirper: icon("feather", "colour", "Chirper"),
  aethergram: icon("aperture", "colour", "Aethergram", { round: true }),
  velvet: icon("fire", "colour", "Velvet", { round: true }),
  polls: icon("chart-bar", "colour", "Indigo"),
  announcements: icon("megaphone", "colour", "Orange"),
  camera: icon("camera", "graphite", "Slate", { ink: WHITE }),
  photos: icon("flower", "photos", "Gold", { round: true }),
  feedback: icon("chat-teardrop-text", "colour", "Teal"),
  music: icon("music-note", "colour", "Green"),
  maps: icon("map-trifold", "colour", "Teal"),
  venues: icon("map-pin", "colour", "Orchid"),
  games: icon("game-controller", "colour", "Red"),
  market: icon("chart-line-up", "colour", "Gold"),
  appstore: icon("squares-four", "colour", "Azure"),
  skywatcher: icon("cloud-sun", "colour", "Cyan"),
  collections: icon("trophy", "colour", "Indigo"),
  inventory: icon("backpack", "colour", "Orange"),
  fishing: icon("fish", "colour", "Teal"),
  clock: icon("clock", "graphite", "Red", { round: true, ink: WHITE }),
  notes: icon("note", "paper", "Gold"),
  calculator: icon("calculator", "graphite", "Slate", { ink: WHITE }),
  timers: icon("hourglass", "colour", "Lime"),
  shortcuts: icon("lightning", "colour", "Indigo"),
  wallet: icon("wallet", "colour", "Green"),
  dailies: icon("list-checks", "colour", "Teal"),
  calendar: icon("calendar", "paper", "Red"),
  news: icon("newspaper", "colour", "Slate"),
  character: icon("user-circle", "colour", "Azure", { round: true }),
  notifications: icon("bell", "colour", "Red"),
  jobs: icon("sword", "colour", "Indigo"),
  strats: icon("scroll", "colour", "Rose"),
  health: icon("heart", "colour", "Lime"),
  aetherstream: icon("monitor-play", "colour", "Violet"),
  muster: icon("flag-banner", "colour", "Cyan"),
  yellowpages: icon("book-open", "colour", "Gold"),
  casino: icon("poker-chip", "colour", "GambaRose", { round: true, ink: GAMBA_GOLD }),
  housing: icon("house", "colour", "Emerald"),
  hunts: icon("crosshair", "colour", "Red", { round: true }),
  coin: icon("coin", "colour", "Gold", { round: true }),
  phone: icon("phone", "colour", "Green"),
  contacts: icon("address-book", "colour", "Slate"),
  findpeople: icon("user-focus", "colour", "Azure"),
  kupoai: icon("sparkle", "colour", "Violet"),
  memory: icon("cards", "colour", "Gold"),
  bubbles: icon("circles-three", "colour", "Teal"),
  whack: icon("hammer", "colour", "Lime"),
  breakout: icon("wall", "colour", "Orchid"),
  nonogram: icon("grid-nine", "colour", "Slate"),
  watersort: icon("flask", "colour", "Azure"),
  simon: icon("circles-four", "colour", "Green", { round: true }),
  flap: icon("bird", "colour", "Cyan"),
  snake: icon("path", "colour", "Lime"),
  flow: icon("flow-arrow", "colour", "Violet"),
  match3: icon("diamond", "colour", "Orchid"),
  2048: icon("stack-simple", "colour", "Orange"),
  solitaire: icon("spade", "colour", "Emerald"),
  tetris: icon("puzzle-piece", "colour", "Cyan"),
  reversi: icon("yin-yang", "colour", "Teal", { round: true }),
  minesweeper: icon("bomb", "colour", "Red"),
  sudoku: icon("grid-four", "colour", "Azure"),
  chess: icon("crown", "colour", "Gold"),
  stack: icon("stack", "colour", "Indigo"),
  crystaldrop: icon("diamonds-four", "colour", "Violet"),
  beat: icon("metronome", "colour", "Rose"),
  blade: icon("knife", "colour", "Red"),
  trivia: icon("question", "colour", "Indigo", { round: true }),
  skyfall: icon("meteor", "colour", "Orange"),
  invaders: icon("alien", "colour", "Violet"),
  capman: icon("ghost", "colour", "Gold"),
  hop: icon("rabbit", "colour", "Green"),
  squadron: icon("airplane", "colour", "Azure"),
  doom: icon("skull", "colour", "Red"),
  wordrun: icon("text-aa", "colour", "Emerald"),
  coil: icon("spiral", "colour", "Orchid", { round: true }),
  updraft: icon("wind", "colour", "Cyan"),
  swoop: icon("wave-sine", "colour", "Lime"),
  slice: icon("orange-slice", "colour", "Orange", { round: true }),
  spiral: icon("tornado", "colour", "Teal"),
  mahjong: icon("flower-lotus", "colour", "Emerald"),
  gloop: icon("drop", "colour", "Lime"),
  drift: icon("planet", "colour", "Cyan", { round: true }),
  crawler: icon("bug", "colour", "Emerald"),
  trails: icon("motorcycle", "colour", "Orchid"),
  trailblaze: icon("person-simple-run", "colour", "Emerald"),
  thrust: icon("rocket-launch", "colour", "Rose"),
  pinball: icon("joystick", "colour", "Gold"),
  crates: icon("package", "colour", "Gold"),
  delve: icon("shovel", "colour", "Teal"),
  moogleclicker: icon("hand-tap", "colour", "Violet"),
  claim: icon("polygon", "colour", "Violet"),
  lander: icon("moon-stars", "colour", "Azure"),
  luckydraw: icon("clover", "colour", "Red", { round: true }),
  broadside: icon("sailboat", "colour", "Cyan"),
  pegfall: icon("confetti", "colour", "Azure"),
  fling: icon("castle-turret", "colour", "Red"),
  siege: icon("plant", "colour", "Green"),
  crater: icon("mountains", "colour", "Orange"),
  fuse: icon("fire-simple", "colour", "Indigo"),
  snip: icon("scissors", "colour", "Rose"),
  minigolf: icon("golf", "colour", "Emerald", { round: true }),
  herd: icon("footprints", "colour", "Rose"),
  tempo: icon("waveform", "colour", "Violet"),
  uno: icon("cards-three", "colour", "Rose"),
  pool: icon("number-circle-eight", "colour", "Green", { round: true }),
  connectfour: icon("number-circle-four", "colour", "Cyan", { round: true }),
};

function hexToLinearRgb(hex) {
  const value = parseInt(hex.slice(1), 16);
  const channels = [(value >> 16) & 255, (value >> 8) & 255, value & 255];
  return channels.map((channel) => srgbToLinear(channel / 255));
}

function linearRgbToHex(channels) {
  const toByte = (channel) =>
    Math.round(Math.min(1, Math.max(0, linearToSrgb(channel))) * 255)
      .toString(16)
      .padStart(2, "0");
  return `#${channels.map(toByte).join("")}`.toUpperCase();
}

function srgbToLinear(channel) {
  if (channel <= 0.04045) {
    return channel / 12.92;
  }
  return ((channel + 0.055) / 1.055) ** 2.4;
}

function linearToSrgb(channel) {
  if (channel <= 0.0031308) {
    return channel * 12.92;
  }
  return 1.055 * channel ** (1 / 2.4) - 0.055;
}

function linearRgbToOklch([red, green, blue]) {
  const longCone = Math.cbrt(0.4122214708 * red + 0.5363325363 * green + 0.0514459929 * blue);
  const mediumCone = Math.cbrt(0.2119034982 * red + 0.6806995451 * green + 0.1073969566 * blue);
  const shortCone = Math.cbrt(0.0883024619 * red + 0.2817188376 * green + 0.6299787005 * blue);
  const lightness = 0.2104542553 * longCone + 0.793617785 * mediumCone - 0.0040720468 * shortCone;
  const greenRedAxis = 1.9779984951 * longCone - 2.428592205 * mediumCone + 0.4505937099 * shortCone;
  const blueYellowAxis = 0.0259040371 * longCone + 0.7827717662 * mediumCone - 0.808675766 * shortCone;
  return { lightness, chroma: Math.hypot(greenRedAxis, blueYellowAxis), hue: Math.atan2(blueYellowAxis, greenRedAxis) };
}

function oklchToLinearRgb({ lightness, chroma, hue }) {
  const greenRedAxis = chroma * Math.cos(hue);
  const blueYellowAxis = chroma * Math.sin(hue);
  const longCone = (lightness + 0.3963377774 * greenRedAxis + 0.2158037573 * blueYellowAxis) ** 3;
  const mediumCone = (lightness - 0.1055613458 * greenRedAxis - 0.0638541728 * blueYellowAxis) ** 3;
  const shortCone = (lightness - 0.0894841775 * greenRedAxis - 1.291485548 * blueYellowAxis) ** 3;
  return [
    4.0767416621 * longCone - 3.3077115913 * mediumCone + 0.2309699292 * shortCone,
    -1.2684380046 * longCone + 2.6097574011 * mediumCone - 0.3413193965 * shortCone,
    -0.0041960863 * longCone - 0.7034186147 * mediumCone + 1.707614701 * shortCone,
  ];
}

function isInsideGamut(channels) {
  return channels.every((channel) => channel >= -0.0005 && channel <= 1.0005);
}

function clipChromaToGamut(color) {
  if (isInsideGamut(oklchToLinearRgb(color))) {
    return oklchToLinearRgb(color);
  }
  let lowChroma = 0;
  let highChroma = color.chroma;
  for (let step = 0; step < GAMUT_SEARCH_STEPS; step++) {
    const middleChroma = (lowChroma + highChroma) / 2;
    if (isInsideGamut(oklchToLinearRgb({ ...color, chroma: middleChroma }))) {
      lowChroma = middleChroma;
    } else {
      highChroma = middleChroma;
    }
  }
  return oklchToLinearRgb({ ...color, chroma: lowChroma });
}

function shiftLightness(hex, factor) {
  const color = linearRgbToOklch(hexToLinearRgb(hex));
  return linearRgbToHex(clipChromaToGamut({ ...color, lightness: color.lightness * factor }));
}

function tileFor(entry) {
  const hue = hues[entry.hue];
  switch (entry.family) {
    case "colour":
      return { stops: [shiftLightness(hue, GRADIENT_TOP_LIGHTNESS), shiftLightness(hue, GRADIENT_BOTTOM_LIGHTNESS)], ink: entry.ink ?? WHITE };
    case "paper":
      return { stops: PAPER_STOPS, ink: hue };
    case "graphite":
      return { stops: GRAPHITE_STOPS, ink: entry.ink ?? WHITE };
    case "photos":
      return { stops: PHOTOS_STOPS, ink: WHITE };
    default:
      throw new Error(`unknown tile family ${entry.family}`);
  }
}

async function fetchSymbolMarkup(name) {
  mkdirSync(SYMBOL_CACHE, { recursive: true });
  const cachePath = resolve(SYMBOL_CACHE, `${name}-fill.svg`);
  if (existsSync(cachePath)) {
    return innerMarkup(readFileSync(cachePath, "utf8"));
  }
  const url = `https://cdn.jsdelivr.net/npm/@phosphor-icons/core@${PHOSPHOR_VERSION}/assets/fill/${name}-fill.svg`;
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`HTTP ${response.status} for ${url}`);
  }
  const svg = await response.text();
  writeFileSync(cachePath, svg);
  return innerMarkup(svg);
}

function innerMarkup(svg) {
  const openTagEnd = svg.indexOf(">", svg.indexOf("<svg")) + 1;
  const closeTagStart = svg.lastIndexOf("</svg>");
  return svg.slice(openTagEnd, closeTagStart);
}

function symbolGroup(markup, ink, transform) {
  return `<g transform="${transform}" fill="${ink}">${markup.replaceAll("currentColor", ink)}</g>`;
}

function svgDocument(body) {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${MASTER_SIZE}" height="${MASTER_SIZE}" viewBox="0 0 ${MASTER_SIZE} ${MASTER_SIZE}">${body}</svg>`;
}

async function measureSymbol(markup) {
  const measureScale = MASTER_SIZE / PHOSPHOR_VIEWBOX;
  const svg = svgDocument(symbolGroup(markup, "#000000", `scale(${measureScale})`));
  const { data, info } = await sharp(Buffer.from(svg)).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  let left = info.width;
  let top = info.height;
  let right = -1;
  let bottom = -1;
  for (let row = 0; row < info.height; row++) {
    for (let column = 0; column < info.width; column++) {
      const alpha = data[(row * info.width + column) * info.channels + 3];
      if (alpha < MEASURE_ALPHA_THRESHOLD) {
        continue;
      }
      left = Math.min(left, column);
      right = Math.max(right, column);
      top = Math.min(top, row);
      bottom = Math.max(bottom, row);
    }
  }
  if (right < left) {
    throw new Error("symbol rendered no opaque pixels");
  }
  return {
    left: left / measureScale,
    top: top / measureScale,
    width: (right - left + 1) / measureScale,
    height: (bottom - top + 1) / measureScale,
  };
}

function placementFor(bounds, round) {
  const tall = bounds.width / bounds.height < TALL_ASPECT_LIMIT;
  let scale;
  if (tall) {
    scale = (TALL_SYMBOL_HEIGHT_FRACTION * MASTER_SIZE) / bounds.height;
  } else {
    const widthFraction = round ? ROUND_SYMBOL_WIDTH_FRACTION : SYMBOL_WIDTH_FRACTION;
    scale = (widthFraction * MASTER_SIZE) / bounds.width;
  }
  const translateX = MASTER_SIZE / 2 - scale * (bounds.left + bounds.width / 2);
  const translateY = MASTER_SIZE / 2 - scale * (bounds.top + bounds.height / 2);
  return `translate(${translateX.toFixed(3)} ${translateY.toFixed(3)}) scale(${scale.toFixed(5)})`;
}

function backgroundMarkup(stops) {
  const stopMarkup = stops
    .map((stop, stopIndex) => `<stop offset="${((stopIndex / (stops.length - 1)) * 100).toFixed(1)}%" stop-color="${stop}"/>`)
    .join("");
  return `<defs><linearGradient id="tile" x1="0" y1="0" x2="0" y2="1">${stopMarkup}</linearGradient></defs><rect width="${MASTER_SIZE}" height="${MASTER_SIZE}" fill="url(#tile)"/>`;
}

async function writePair(baseName, svg, opaque) {
  const masterPipeline = sharp(Buffer.from(svg));
  if (opaque) {
    masterPipeline.removeAlpha();
  }
  const master = await masterPipeline.png(PNG_OPTIONS).toBuffer();
  writeFileSync(resolve(MASTERS_OUT, `${baseName}.png`), master);
  const shippedPipeline = sharp(master).resize(SHIPPED_SIZE, SHIPPED_SIZE, { kernel: "lanczos3" });
  if (opaque) {
    shippedPipeline.removeAlpha();
  }
  writeFileSync(resolve(ICONS_OUT, `${baseName}.png`), await shippedPipeline.png(PNG_OPTIONS).toBuffer());
}

async function paint(id, entry) {
  const markup = await fetchSymbolMarkup(entry.symbol);
  const bounds = await measureSymbol(markup);
  const transform = placementFor(bounds, entry.round);
  const tile = tileFor(entry);
  const symbol = symbolGroup(markup, tile.ink, transform);
  await writePair(id, svgDocument(backgroundMarkup(tile.stops) + symbol), true);
  await writePair(`${id}.fg`, svgDocument(symbol), false);
  return `${entry.symbol} on ${entry.family} ${entry.hue}`;
}

mkdirSync(ICONS_OUT, { recursive: true });
mkdirSync(MASTERS_OUT, { recursive: true });

const only = new Set(process.argv.slice(2));
const unknown = [...only].filter((id) => !(id in map));
if (unknown.length > 0) {
  console.error(`Unknown ids: ${unknown.join(", ")}`);
  process.exit(2);
}

let written = 0;
const failed = [];
for (const [id, entry] of Object.entries(map)) {
  if (only.size > 0 && !only.has(id)) {
    continue;
  }
  try {
    const summary = await paint(id, entry);
    written++;
    console.log(`  ${id.padEnd(14)} <- ${summary}`);
  } catch (error) {
    failed.push(`${id} (${entry.symbol}): ${error.message}`);
  }
}

console.log(`\nDone: ${written} icons written to ${ICONS_OUT} (masters in ${MASTERS_OUT})`);
if (failed.length > 0) {
  console.log("FAILED:\n" + failed.map((line) => "  " + line).join("\n"));
  process.exitCode = 1;
}
