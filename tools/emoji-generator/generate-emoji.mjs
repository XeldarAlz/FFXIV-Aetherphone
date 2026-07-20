import { createRequire } from "node:module";
import { writeFileSync, mkdirSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const require = createRequire(import.meta.url);
const here = dirname(fileURLToPath(import.meta.url));
const OUT_DIR = resolve(here, "../../src/Aetherphone/Emoji");
const CATALOG = resolve(OUT_DIR, "catalog.json");

const TWEMOJI_VERSION = "15.1.0";
const CDN = `https://cdn.jsdelivr.net/gh/jdecked/twemoji@${TWEMOJI_VERSION}/assets/72x72`;
const CONCURRENCY = 24;

const GROUP_NAMES = {
  0: "Smileys & Emotion",
  1: "People & Body",
  3: "Animals & Nature",
  4: "Food & Drink",
  5: "Travel & Places",
  6: "Activities",
  7: "Objects",
  8: "Symbols",
  9: "Flags",
};
const KEPT_GROUPS = Object.keys(GROUP_NAMES).map(Number);

const ZWJ = "‍";
const VARIATION_SELECTOR = /️/g;

function toCodePoint(sequence) {
  const points = [];
  let high = 0;
  for (let index = 0; index < sequence.length; index++) {
    const code = sequence.charCodeAt(index);
    if (high) {
      points.push((0x10000 + ((high - 0xd800) << 10) + (code - 0xdc00)).toString(16));
      high = 0;
    } else if (code >= 0xd800 && code <= 0xdbff) {
      high = code;
    } else {
      points.push(code.toString(16));
    }
  }
  return points.join("-");
}

function twemojiFile(sequence) {
  const normalized = sequence.indexOf(ZWJ) < 0 ? sequence.replace(VARIATION_SELECTOR, "") : sequence;
  return toCodePoint(normalized);
}

async function pooledForEach(items, worker) {
  let cursor = 0;
  const runners = new Array(Math.min(CONCURRENCY, items.length)).fill(0).map(async () => {
    while (cursor < items.length) {
      const index = cursor++;
      await worker(items[index], index);
    }
  });
  await Promise.all(runners);
}

async function download(file, attempt = 0) {
  try {
    const response = await fetch(`${CDN}/${file}.png`);
    if (response.status === 404) {
      return null;
    }
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
    return Buffer.from(await response.arrayBuffer());
  } catch (error) {
    if (attempt < 3) {
      await new Promise((done) => setTimeout(done, 250 * (attempt + 1)));
      return download(file, attempt + 1);
    }
    throw error;
  }
}

const SHORTCODES = require("emojibase-data/en/shortcodes/emojibase.json");

function shortcodesFor(file, hexcode) {
  const normalize = (value) => (value === undefined ? null : Array.isArray(value) ? value : [value]);
  let codes = normalize(SHORTCODES[file.toUpperCase()]);
  if (!codes && hexcode) {
    codes = normalize(SHORTCODES[hexcode]) ?? normalize(SHORTCODES[hexcode.replace(/-FE0F/g, "")]);
  }
  if (!codes) {
    codes = [file];
  }
  return codes.map((code) => code.toLowerCase());
}

function loadEmojibase() {
  const data = require("emojibase-data/en/data.json");
  return data.filter((entry) => KEPT_GROUPS.includes(entry.group));
}

function buildCatalogEntries(source) {
  const groupIndex = new Map(KEPT_GROUPS.map((group, index) => [group, index]));
  const entries = [];
  for (const item of source) {
    const tones = [];
    if (Array.isArray(item.skins)) {
      for (const skin of item.skins) {
        if (skin.tone === undefined || Array.isArray(skin.tone)) {
          continue;
        }

        tones.push({
          tone: skin.tone,
          file: twemojiFile(skin.emoji),
        });
      }
    }

    entries.push({
      file: twemojiFile(item.emoji),
      short: shortcodesFor(twemojiFile(item.emoji), item.hexcode),
      group: groupIndex.get(item.group),
      order: item.order ?? 0,
      label: item.label,
      tags: Array.isArray(item.tags) ? item.tags.join(" ") : "",
      tones,
    });
  }

  entries.sort((left, right) => left.group - right.group || left.order - right.order);
  return entries;
}

function collectFiles(entries) {
  const files = new Set();
  for (const entry of entries) {
    files.add(entry.file);
    for (const tone of entry.tones) {
      files.add(tone.file);
    }
  }
  return [...files];
}

async function main() {
  mkdirSync(OUT_DIR, { recursive: true });
  const source = loadEmojibase();
  const entries = buildCatalogEntries(source);
  const files = collectFiles(entries);
  console.log(`Catalog: ${entries.length} base emoji, ${files.length} unique image files.`);

  const missing = new Set();
  let downloaded = 0;
  let skipped = 0;
  await pooledForEach(files, async (file) => {
    const target = resolve(OUT_DIR, `${file}.png`);
    if (existsSync(target)) {
      skipped++;
      return;
    }

    const bytes = await download(file);
    if (bytes === null) {
      missing.add(file);
      return;
    }

    writeFileSync(target, bytes);
    downloaded++;
    if (downloaded % 250 === 0) {
      console.log(`  downloaded ${downloaded}...`);
    }
  });

  const usable = entries
    .map((entry) => ({
      ...entry,
      tones: entry.tones.filter((tone) => !missing.has(tone.file)),
    }))
    .filter((entry) => !missing.has(entry.file));

  const catalog = {
    source: `Twemoji ${TWEMOJI_VERSION} (CC-BY 4.0), metadata from emojibase-data (CC0)`,
    groups: KEPT_GROUPS.map((group) => GROUP_NAMES[group]),
    emoji: usable,
  };
  writeFileSync(CATALOG, JSON.stringify(catalog));

  console.log(`\nDone.`);
  console.log(`  images written : ${downloaded} (skipped existing ${skipped})`);
  console.log(`  catalog emoji  : ${usable.length}`);
  console.log(`  missing assets : ${missing.size}`);
  if (missing.size > 0) {
    console.log(`  (missing files dropped from catalog: ${[...missing].slice(0, 12).join(", ")}${missing.size > 12 ? " ..." : ""})`);
  }
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
