import http from 'node:http';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, rmSync } from 'node:fs';
import { readFile, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, extname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import ffmpegPath from 'ffmpeg-static';
import { chromium } from 'playwright-core';
import sharp from 'sharp';

const toolDirectory = dirname(fileURLToPath(import.meta.url));
const websiteRoot = process.env.AETHERNET_WEBSITE ?? resolve(toolDirectory, '../../../FFXIV-Aethernet/website');
const chromePath = process.env.CHROME_PATH ?? 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const outputDirectory = resolve(toolDirectory, '../../docs/media/readme');
const port = 8765;
const framesPerSecond = 20;
const pixelRatio = 2;
const staleFrameCount = 2;

const clipWidth = 400;
const clipHeight = 760;
const phoneWidth = 340;

const clips = [
  { name: 'chirper', slot: 'chirper', glow: '#3e91f1', warmupMilliseconds: 1200, seconds: 9 },
  { name: 'aethergram', slot: 'gram', glow: '#ed5e71', warmupMilliseconds: 1200, seconds: 9 },
  { name: 'chocochat', slot: 'choco', glow: '#d19153', warmupMilliseconds: 0, seconds: 15 },
  { name: 'velvet', slot: 'velvet', glow: '#da3e75', warmupMilliseconds: 1400, seconds: 18.8 },
  { name: 'music', scene: 'readmeMusic', sceneArgument: 'assets/rewind/p02.webp', glow: '#c0607e', warmupMilliseconds: 300, seconds: 13 },
];

const stats = {
  name: 'numbers',
  width: 900,
  height: 360,
  eyebrow: 'Summer 2026 · the first season',
  figures: [
    { value: '14.4M', label: 'recorded actions', color: '#a99bff' },
    { value: '5.8M', label: 'private messages', color: '#f0a868' },
    { value: '3.2M', label: 'likes given', color: '#f5497a' },
    { value: '348K', label: 'photos shared', color: '#ed5e71' },
    { value: '343K', label: 'connections made', color: '#3e91f1' },
    { value: '113', label: 'worlds reached', color: '#5fd3b0' },
  ],
};

const iconSourceDirectory = resolve(toolDirectory, '../../src/Aetherphone/Icons');
const iconSize = 96;
const iconNames = [
  'chirper', 'aethergram', 'message', 'velvet', 'aetherstream', 'music', 'venues',
  'messages', 'strats', 'hunts', 'market', 'housing', 'fishing', 'jobs', 'inventory', 'dailies', 'timers',
  'muster', 'yellowpages', 'announcements', 'polls', 'coin',
  'camera', 'photos', 'notes', 'calendar', 'clock', 'skywatcher', 'wallet', 'health', 'shortcuts', 'news',
  'games', 'doom', 'chess', 'tetris', 'wordrun', 'solitaire', '2048',
];

const contentTypes = {
  '.html': 'text/html',
  '.css': 'text/css',
  '.js': 'text/javascript',
  '.json': 'application/json',
  '.svg': 'image/svg+xml',
  '.webp': 'image/webp',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.woff2': 'font/woff2',
};

function startServer() {
  const server = http.createServer(async (request, response) => {
    let path = decodeURIComponent(request.url.split('?')[0]);
    if (path.endsWith('/')) {
      path += 'index.html';
    }
    try {
      const body = await readFile(join(websiteRoot, path));
      response.writeHead(200, { 'content-type': contentTypes[extname(path)] ?? 'application/octet-stream' });
      response.end(body);
    } catch {
      response.writeHead(404);
      response.end();
    }
  });
  return new Promise(resolvePromise => server.listen(port, () => resolvePromise(server)));
}

async function openStage(browser, width, height) {
  const page = await browser.newPage({ viewport: { width, height }, deviceScaleFactor: pixelRatio });
  await page.emulateMedia({ reducedMotion: 'no-preference', colorScheme: 'dark' });
  await page.setViewportSize({ width: 800, height });
  await page.goto(`http://localhost:${port}/`, { waitUntil: 'networkidle' });
  await page.setViewportSize({ width, height });
  await page.evaluate(() => document.fonts.ready);
  return page;
}

async function loadStageScripts(page) {
  const scripts = ['stage.js', 'music.js'];
  for (let scriptIndex = 0; scriptIndex < scripts.length; scriptIndex++) {
    await page.addScriptTag({ path: join(toolDirectory, 'scenes', scripts[scriptIndex]) });
  }
}

async function mountSlots(page, slotNames, glow, phoneWidth, gap) {
  await loadStageScripts(page);
  await page.evaluate(({ slotNames, glow, phoneWidth, gap }) => {
    const stage = window.readmeStage.create(glow, gap);
    for (let slotIndex = 0; slotIndex < slotNames.length; slotIndex++) {
      window.readmeStage.slot(stage, slotNames[slotIndex], phoneWidth);
    }
    window.readmeStage.finish(stage);
  }, { slotNames, glow, phoneWidth, gap });
}

async function mountShell(page, clip, phoneWidth) {
  await loadStageScripts(page);
  await page.evaluate(({ clip, phoneWidth }) => {
    const stage = window.readmeStage.create(clip.glow, 0);
    const screen = window.readmeStage.shell(stage, phoneWidth);
    if (clip.panel) {
      window.readmeStage.panel(screen, clip.panel);
    } else {
      window[clip.scene](screen, clip.sceneArgument);
    }
    window.readmeStage.finish(stage);
  }, { clip, phoneWidth });
}

async function recordFrames(page, seconds) {
  const session = await page.context().newCDPSession(page);
  const captured = [];
  session.on('Page.screencastFrame', frame => {
    captured.push({ timestamp: frame.metadata.timestamp, data: Buffer.from(frame.data, 'base64') });
    session.send('Page.screencastFrameAck', { sessionId: frame.sessionId }).catch(() => {});
  });
  await session.send('Page.startScreencast', { format: 'png', everyNthFrame: 1 });
  await page.waitForTimeout(seconds * 1000);
  await session.send('Page.stopScreencast');
  await session.detach();
  return resample(captured, seconds);
}

function resample(captured, seconds) {
  const settled = captured.slice(staleFrameCount);
  const start = settled[0].timestamp;
  const frameCount = Math.round(seconds * framesPerSecond);
  const frames = [];
  let sourceIndex = 0;
  for (let frameIndex = 0; frameIndex < frameCount; frameIndex++) {
    const time = start + frameIndex / framesPerSecond;
    while (sourceIndex + 1 < settled.length && settled[sourceIndex + 1].timestamp <= time) {
      sourceIndex++;
    }
    frames.push(settled[sourceIndex].data);
  }
  return frames;
}

async function writeAnimatedWebp(frames, width, height, path) {
  const frameDirectory = mkdtempSync(join(tmpdir(), 'readme-media-'));
  try {
    for (let frameIndex = 0; frameIndex < frames.length; frameIndex++) {
      await writeFile(join(frameDirectory, `${String(frameIndex).padStart(4, '0')}.png`), frames[frameIndex]);
    }
    execFileSync(ffmpegPath, [
      '-hide_banner', '-loglevel', 'error', '-y',
      '-framerate', String(framesPerSecond),
      '-i', join(frameDirectory, '%04d.png'),
      '-vf', `scale=${width}:${height}:flags=lanczos`,
      '-c:v', 'libwebp_anim', '-quality', '78', '-compression_level', '6', '-loop', '0',
      path,
    ]);
  } finally {
    rmSync(frameDirectory, { recursive: true, force: true });
  }
}

async function renderClip(browser, clip) {
  const page = await openStage(browser, clipWidth, clipHeight);
  if (clip.slot) {
    await mountSlots(page, [clip.slot], clip.glow, phoneWidth, 0);
  } else {
    await mountShell(page, clip, phoneWidth);
  }
  await page.waitForTimeout(clip.warmupMilliseconds);
  const frames = await recordFrames(page, clip.seconds);
  await page.close();
  const width = clipWidth * pixelRatio * 0.75;
  const height = clipHeight * pixelRatio * 0.75;
  await writeAnimatedWebp(frames, width, height, join(outputDirectory, `${clip.name}.webp`));
  console.log(`${clip.name}.webp  ${frames.length} frames`);
}

function roundedMask(width, height, radius) {
  return Buffer.from(`<svg width="${width}" height="${height}"><rect width="${width}" height="${height}" rx="${radius}" ry="${radius}" fill="#fff"/></svg>`);
}

async function renderIcons() {
  const iconDirectory = join(outputDirectory, 'icons');
  mkdirSync(iconDirectory, { recursive: true });
  const mask = roundedMask(iconSize, iconSize, Math.round(iconSize * 0.225));
  for (let iconIndex = 0; iconIndex < iconNames.length; iconIndex++) {
    await sharp(join(iconSourceDirectory, `${iconNames[iconIndex]}.png`))
      .resize(iconSize, iconSize)
      .composite([{ input: mask, blend: 'dest-in' }])
      .png({ compressionLevel: 9 })
      .toFile(join(iconDirectory, `${iconNames[iconIndex]}.png`));
  }
  console.log(`icons  ${iconNames.length} files`);
}

async function renderStats(browser) {
  const page = await openStage(browser, stats.width, stats.height);
  await page.evaluate(stats => {
    const card = document.createElement('div');
    card.style.cssText = [
      'position:fixed', 'inset:0', 'z-index:2147483647', 'box-sizing:border-box', 'padding:34px 40px',
      'border-radius:28px', 'display:flex', 'flex-direction:column', 'gap:22px', 'color:#f4f2ff',
      'background:radial-gradient(70% 90% at 15% 0%, #3a2a7a, transparent 60%), radial-gradient(60% 90% at 100% 100%, #5a1f45, transparent 60%), #0c0b18',
      'box-shadow:inset 0 0 0 1px rgba(255,255,255,.08)',
    ].join(';');
    const eyebrow = document.createElement('div');
    eyebrow.textContent = stats.eyebrow;
    eyebrow.style.cssText = 'font-size:13px;font-weight:700;letter-spacing:.16em;text-transform:uppercase;color:#b9b2e6;';
    card.appendChild(eyebrow);
    const grid = document.createElement('div');
    grid.style.cssText = 'flex:1;display:grid;grid-template-columns:repeat(3,1fr);grid-auto-rows:1fr;gap:14px 28px;';
    for (let figureIndex = 0; figureIndex < stats.figures.length; figureIndex++) {
      const figure = stats.figures[figureIndex];
      const cell = document.createElement('div');
      cell.style.cssText = 'display:flex;flex-direction:column;justify-content:center;';
      const value = document.createElement('div');
      value.textContent = figure.value;
      value.style.cssText = `font-size:54px;line-height:1;font-weight:800;letter-spacing:-.03em;color:${figure.color};font-variant-numeric:tabular-nums;`;
      const label = document.createElement('div');
      label.textContent = figure.label;
      label.style.cssText = 'margin-top:8px;font-size:16px;font-weight:550;color:#d8d4f2;';
      cell.appendChild(value);
      cell.appendChild(label);
      grid.appendChild(cell);
    }
    card.appendChild(grid);
    document.body.appendChild(card);
    document.documentElement.style.background = 'transparent';
    document.body.style.background = 'transparent';
  }, stats);
  await page.waitForTimeout(300);
  await page.screenshot({ path: join(outputDirectory, `${stats.name}.png`), omitBackground: true });
  await page.close();
  console.log(`${stats.name}.png`);
}

async function main() {
  if (!existsSync(join(websiteRoot, 'index.html'))) {
    throw new Error(`Website not found at ${websiteRoot}. Set AETHERNET_WEBSITE to the Aethernet website folder.`);
  }
  mkdirSync(outputDirectory, { recursive: true });
  const requested = process.argv.slice(2);
  const server = await startServer();
  const browser = await chromium.launch({ executablePath: chromePath });
  try {
    if (requested.length === 0 || requested.includes('icons')) {
      await renderIcons();
    }
    if (requested.length === 0 || requested.includes(stats.name)) {
      await renderStats(browser);
    }
    for (let clipIndex = 0; clipIndex < clips.length; clipIndex++) {
      if (requested.length > 0 && !requested.includes(clips[clipIndex].name)) {
        continue;
      }
      await renderClip(browser, clips[clipIndex]);
    }
  } finally {
    await browser.close();
    server.close();
  }
}

await main();
