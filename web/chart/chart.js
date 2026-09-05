// Extracted verbatim (Phase D CP-D1) from the inline <script> block of chart.html at integration fa6c49a.
'use strict';
// ───────────────────────────────────────────────────────────────────────────
//  0. Projection and world frame — identical to MechaTrader.Core.MapProjection
// ───────────────────────────────────────────────────────────────────────────
const W = window.WORLD;
const KM_LAT = 111.32;
const KM_LON = KM_LAT * Math.cos(47.5 * Math.PI / 180);
const ORIGIN_X = W.map.originLon * KM_LON;
const ORIGIN_Y = -W.map.originLat * KM_LAT;
const MAP_W = W.map.width * W.map.cellKm;     // 3600 km
const MAP_H = W.map.height * W.map.cellKm;    // 2100 km
const CELL = W.map.cellKm;                    // the engine's 50 km cell
const toKm = (lon, lat) => ({ x: lon * KM_LON - ORIGIN_X, y: -lat * KM_LAT - ORIGIN_Y });
const toLonLat = (x, y) => ({ lon: (x + ORIGIN_X) / KM_LON, lat: -(y + ORIGIN_Y) / KM_LAT });

const BIOME = { plain: 0, hill: 1, mountain: 2, forest: 3, swamp: 4, desert: 5, tundra: 6, water: 7, deep: 8 };
const BIOME_NAME = ['ash flats', 'broken ground', 'mountains', 'dead woods', 'toxic marsh', 'bleached waste', 'frozen ash', 'water', 'deep water'];
// Cold chart. Every ground reads blue-grey: blue is the highest channel almost everywhere,
// red the lowest, and the whole ramp sits dark. Vegetation is the one place green leads,
// and even there it is pulled toward sea-green so it belongs to the same picture.
const PAL = [
  [92, 100, 110],  // plain: cold ash flats
  [84, 90, 99],    // hill: broken ground
  [58, 64, 74],    // mountain: wet slate
  [62, 82, 76],    // forest: cold woods
  [58, 78, 78],    // swamp: brackish marsh
  [116, 122, 128], // desert: bleached waste, gone grey
  [112, 122, 134], // tundra: frozen ash
  [30, 46, 58],    // water: oil-dark
  [12, 20, 30],    // deep
];
const WATER_FAR = [18, 30, 42];
const AMBER = '#e0a030';
// Cities where selling the whole hold today would clear at least this many credits
// get an animated ring and an estimated-profit tag on the map.
const SELL_PROFIT_MIN = 20000;
const MONO = '"Cascadia Mono", Consolas, "Courier New", monospace';

// ───────────────────────────────────────────────────────────────────────────
//  1. Deterministic noise and hashing
// ───────────────────────────────────────────────────────────────────────────
const PERM = new Uint8Array(512);
{ const r = mulberry(20260901); const p = []; for (let i = 0; i < 256; i++) p.push(i); for (let i = 255; i > 0; i--) { const j = Math.floor(r() * (i + 1)); [p[i], p[j]] = [p[j], p[i]]; } for (let i = 0; i < 512; i++) PERM[i] = p[i & 255]; }

// ───────────────────────────────────────────────────────────────────────────
//  2. Geography: fine biome grid (10 km) from the game's region polygons
// ───────────────────────────────────────────────────────────────────────────
const FINE = 10;
const FW = MAP_W / FINE, FH = MAP_H / FINE;
const fine = new Uint8Array(FW * FH);
{
  const regions = W.map.regions.map(r => ({ code: BIOME[r.biome], rings: r.rings, bbox: r.rings[0].reduce((b, [x, y]) => [Math.min(b[0], x), Math.min(b[1], y), Math.max(b[2], x), Math.max(b[3], y)], [1e9, 1e9, -1e9, -1e9]) }));
  for (let r = 0; r < FH; r++) for (let c = 0; c < FW; c++) {
    const { lon, lat } = toLonLat((c + 0.5) * FINE, (r + 0.5) * FINE);
    let code = BIOME[W.map.defaultBiome];
    for (const reg of regions) { const b = reg.bbox; if (lon < b[0] || lon > b[2] || lat < b[1] || lat > b[3]) continue; if (pip(lon, lat, reg.rings[0])) code = reg.code; }
    fine[r * FW + c] = code;
  }
}
const CITIES = W.cities.map(c => ({ ...c, ...toKm(c.lon, c.lat), h: strHash(c.id) }));
const CITY_BY_ID = Object.fromEntries(CITIES.map(c => [c.id, c]));
for (const c of CITIES) { // cities stand on land
  const R = 44 / FINE, cc = Math.floor(c.x / FINE), cr = Math.floor(c.y / FINE);
  for (let r = cr - 5; r <= cr + 5; r++) for (let k = cc - 5; k <= cc + 5; k++) {
    if (r < 0 || k < 0 || r >= FH || k >= FW) continue;
    const d = Math.hypot(k + 0.5 - c.x / FINE, r + 0.5 - c.y / FINE), i = r * FW + k;
    if (d <= R && (fine[i] === BIOME.water || fine[i] === BIOME.deep || fine[i] === BIOME.mountain)) fine[i] = BIOME.plain;
  }
}
// Patch biomes are authored as rectangles; erode their edges stochastically into plain.
const PATCH = new Set([BIOME.hill, BIOME.forest, BIOME.swamp, BIOME.desert, BIOME.tundra]);
const edgeDist = new Uint8Array(FW * FH).fill(255);
{
  const q = [];
  for (let r = 0; r < FH; r++) for (let c = 0; c < FW; c++) {
    const i = r * FW + c, b = fine[i]; if (!PATCH.has(b)) continue;
    for (const [k, j] of [[c - 1, r], [c + 1, r], [c, r - 1], [c, r + 1]]) { if (k < 0 || j < 0 || k >= FW || j >= FH || fine[j * FW + k] !== b) { edgeDist[i] = 0; q.push(i); break; } }
  }
  for (let h = 0; h < q.length; h++) {
    const i = q[h], d = edgeDist[i]; if (d >= 6) continue;
    const c = i % FW, r = (i / FW) | 0;
    for (const [k, j] of [[c - 1, r], [c + 1, r], [c, r - 1], [c, r + 1]]) { if (k < 0 || j < 0 || k >= FW || j >= FH) continue; const n = j * FW + k; if (fine[n] === fine[i] && edgeDist[n] > d + 1) { edgeDist[n] = d + 1; q.push(n); } }
  }
}
const WARP_A = 34, WARP_F = 1 / 120;
const LAT = 4, LW = MAP_W / LAT + 1, LH = MAP_H / LAT + 1;
const latWX = new Float32Array(LW * LH), latWY = new Float32Array(LW * LH), latMot = new Float32Array(LW * LH), latTex = new Float32Array(LW * LH), latScar = new Float32Array(LW * LH);
for (let j = 0; j < LH; j++) for (let i = 0; i < LW; i++) {
  const x = i * LAT, y = j * LAT, k = j * LW + i;
  latWX[k] = (fbm2(x * WARP_F, y * WARP_F) - 0.5) * 2 * WARP_A + (vnoise(x / 11 + 40, y / 11) - 0.5) * 6;
  latWY[k] = (fbm2(x * WARP_F + 31.7, y * WARP_F + 17.2) - 0.5) * 2 * WARP_A + (vnoise(x / 11, y / 11 + 40) - 0.5) * 6;
  latMot[k] = vnoise(x / 90 + 5, y / 90) - 0.5;
  latTex[k] = vnoise(x / 12, y / 12);
  latScar[k] = Math.max(0, fbm2(x / 30 + 90, y / 30 + 20) - 0.64) * 2.2; // burn scars: sparse, faint
}
let _wx = 0, _wy = 0, _mot = 0, _tex = 0, _scar = 0;
const OFFROAD = W.map.offRoad;
const biomeKey = Object.keys(BIOME);

// ───────────────────────────────────────────────────────────────────────────
//  3. Optional image assets (art/*.png). Missing files fall back to procedural.
// ───────────────────────────────────────────────────────────────────────────
const ART = {};
function loadArt(name) { const img = new Image(); img.onload = () => { ART[name] = img; if (name.startsWith('tex-')) sendTextureToWorker(name, img); }; img.onerror = () => {}; img.src = 'art/' + name + '.png'; }
for (const k of Object.keys(BIOME)) loadArt('tex-' + k);
loadArt('truck');
// Generated sprites (art/manifest.js, written by generator/server.py for APPROVED assets).
// Grouped by biome; a biome with sprites uses them instead of the procedural glyphs.
const SPRITES = {};      // biome code -> [{img, fp, rotate, w}]
const SPRITE_RULE = {};  // biome code -> {share, step}  (how much of the biome uses sprites, lattice spacing)
const RUIN_POOL = [], WRECK_POOL = [];   // placed by rule (settlements, city rings, roadsides), not by biome
const spritesReady = (() => {
  const m = window.MANIFEST; if (!m || !m.sprites || !m.sprites.length) return Promise.resolve(0);
  const loads = m.sprites.map(s => new Promise(res => {
    const img = new Image();
    img.onload = () => {
      const rule = (m.categories || {})[s.category] || {};
      const entry = { img, fp: s.footprintKm || 12, rotate: s.rotate !== false, id: s.id, w: s.weight == null ? 1 : s.weight };
      if (s.category === 'ruin') RUIN_POOL.push(entry);
      if (s.category === 'wreck') WRECK_POOL.push(entry);
      if (s.category === 'unit') {   // one sprite per truck class; the fleet's own class wins, else the heaviest
        (ART.units ||= {})[s.type] = img;
        if (!ART.truck || entry.w > (ART.truckW || 0)) { ART.truck = img; ART.truckW = entry.w; }
        if (WORLD.truck && s.type === WORLD.truck.id) { ART.truck = img; ART.truckW = 1e9; }
      }
      for (const b of s.biomes || []) {
        if (BIOME[b] === undefined) continue;
        (SPRITES[BIOME[b]] ||= []).push(entry);
        const prev = SPRITE_RULE[BIOME[b]] || { share: 0, step: 17 }; // two categories on one biome: densest wins
        SPRITE_RULE[BIOME[b]] = { share: Math.max(prev.share, rule.share == null ? 1 : rule.share), step: Math.min(prev.step, rule.stepKm || 17) };
      }
      res(1);
    };
    img.onerror = () => res(0); img.src = s.file;
  }));
  // A hung image must not hold boot forever, but the budget has to scale with the library:
  // glyphs are built from whatever has arrived, so timing out early yields a thin map, and
  // at 111 sprites a fixed 8 s was already losing the race on a throttled tab.
  const budget = Math.max(8000, 500 * m.sprites.length);
  return Promise.race([Promise.all(loads).then(r => r.reduce((a, b) => a + b, 0)), new Promise(res => setTimeout(() => res(-1), budget))]);
})();

// ───────────────────────────────────────────────────────────────────────────
//  4. Base raster: 1 px per km, painted once, chunked
// ───────────────────────────────────────────────────────────────────────────
const base = document.createElement('canvas'); base.width = MAP_W; base.height = MAP_H;
const inkBaked = document.createElement('canvas'); inkBaked.width = MAP_W; inkBaked.height = MAP_H;
const costCanvas = document.createElement('canvas'); costCanvas.width = MAP_W; costCanvas.height = MAP_H;
const biomePx = new Uint8Array(MAP_W * MAP_H);
const landField = new Float32Array(MAP_W * MAP_H);

// ───────────────────────────────────────────────────────────────────────────
//  4b. Detail tiles (§7.1): 256 km tiles at 4 px/km painted in a worker.
//  Above ZOOM_TILE_AT the 1 px/km base is too soft; on-demand tiles replace it.
// ───────────────────────────────────────────────────────────────────────────
const ZOOM_TILE_AT = 3, TILE_KM = 256, TILE_DETAIL = 4;
const detailTiles = new Map();          // "cx,cy" -> ImageBitmap (LRU, 24 tiles)
const tilePending = new Map();          // "cx,cy" -> request id  (in flight)
const tilePendingQueued = new Set();     // "cx,cy" waiting for worker ready
let tileWorker = null, tileSeq = 0, tileWorkerReady = false;
const TILE_TEX = {};                    // biome code -> texture pixel data (sent as they load)
function startTileWorker() {
  if (tileWorker) return;
  try { tileWorker = new Worker('chart-tiles-worker.js'); } catch (_) { return; }
  tileWorker.onmessage = (e) => {
    const m = e.data;
    if (m.type === 'ready') { tileWorkerReady = true; for (const k of tilePendingQueued) { const [cx, cy] = k.split(',').map(Number); tileWorker.postMessage({ type: 'tile', id: tilePending.get(k), cx, cy }); } tilePendingQueued.clear(); return; }
    if (m.type === 'tile') {
      if (m.err) { tilePending.delete(m.cx + ',' + m.cy); tilePendingQueued.delete(m.cx + ',' + m.cy); return; }
      const key = m.cx + ',' + m.cy;
      tilePendingQueued.delete(key);
      detailTiles.delete(key); detailTiles.set(key, m.bmp);
      tilePending.delete(key);

      while (detailTiles.size > 24) detailTiles.delete(detailTiles.keys().next().value);
    }
  };
  tileWorker.postMessage({
    type: 'init',
    cfg: { FW, FH, LW, LH, MAP_W, MAP_H, FINE, LAT, BIOME, PAL, WATER_FAR, FOAM: [150, 172, 186], DARK: [8, 12, 18] },
    perm: PERM.slice(),
    fine: fine.slice(),
    edge: edgeDist.slice(),
    lat: { wx: latWX.slice(), wy: latWY.slice(), mot: latMot.slice(), tex: latTex.slice(), scar: latScar.slice() },
    textures: Object.entries(TILE_TEX).filter(([c, d]) => d && d.byteLength > 0).map(([code, data]) => ({ code: +code, data: data.slice() }))
  });
}
// Send a texture image's pixels to the worker as soon as it loads
function sendTextureToWorker(name, img) {
  const code = BIOME[name.replace('tex-', '')];
  if (code === undefined) return;
  const c = document.createElement('canvas'); c.width = 256; c.height = 256;
  const g = c.getContext('2d'); g.drawImage(img, 0, 0, 256, 256);
  const td = g.getImageData(0, 0, 256, 256).data;
  TILE_TEX[code] = td;
  if (tileWorker) tileWorker.postMessage({ type: 'tex', code, data: td }, [td.buffer]);
}
function wantTile(cx, cy) {
  if (cx < 0 || cy < 0 || cx * TILE_KM >= MAP_W || cy * TILE_KM >= MAP_H) return;
  const key = cx + ',' + cy;
  if (detailTiles.has(key) || tilePending.has(key)) return;
  startTileWorker();
  const id = ++tileSeq; tilePending.set(key, id);
  if (tileWorkerReady) tileWorker.postMessage({ type: 'tile', id, cx, cy });
  else tilePendingQueued.add(key);
}
function drawDetailTiles() {
  const tl = toWorld(0, 0), br = toWorld(VW, VH);
  const c0 = Math.max(0, Math.floor(tl.x / TILE_KM)), c1 = Math.min(Math.ceil(MAP_W / TILE_KM), Math.ceil(br.x / TILE_KM));
  const r0 = Math.max(0, Math.floor(tl.y / TILE_KM)), r1 = Math.min(Math.ceil(MAP_H / TILE_KM), Math.ceil(br.y / TILE_KM));
  for (let cy = r0; cy <= r1; cy++) for (let cx = c0; cx <= c1; cx++) {
    const key = cx + ',' + cy; const bmp = detailTiles.get(key);
    if (bmp) { detailTiles.delete(key); detailTiles.set(key, bmp); ctx.drawImage(bmp, cx * TILE_KM, cy * TILE_KM, TILE_KM, TILE_KM); }
    else wantTile(cx, cy);
  }
}

// ───────────────────────────────────────────────────────────────────────────
//  5. Surface glyphs: dead trees, ridges, cracks, wrecks, craters, pylons
// ───────────────────────────────────────────────────────────────────────────

// ───────────────────────────────────────────────────────────────────────────
//  6. Road graph — bent polylines, game distances, Dijkstra by days
// ───────────────────────────────────────────────────────────────────────────
const TRUCK = W.truck;
const EDGES = W.routes.map((r, i) => {
  const a = CITY_BY_ID[r.from], b = CITY_BY_ID[r.to]; const t = W.terrain[r.terrain];
  const straight = Math.hypot(b.x - a.x, b.y - a.y);
  const gameKm = Math.round(straight * W.roadDetourFactor);
  const rnd = mulberry(strHash(r.from + r.to)); const bend = (rnd() - 0.5) * 0.28 * straight;
  const nx = -(b.y - a.y) / straight, ny = (b.x - a.x) / straight;
  const cx = (a.x + b.x) / 2 + nx * bend, cy = (a.y + b.y) / 2 + ny * bend;
  const pts = []; const N = 28;
  for (let k = 0; k <= N; k++) { const u = k / N, w0 = (1 - u) * (1 - u), w1 = 2 * u * (1 - u), w2 = u * u; pts.push({ x: w0 * a.x + w1 * cx + w2 * b.x, y: w0 * a.y + w1 * cy + w2 * b.y }); }
  const cum = [0]; for (let k = 1; k < pts.length; k++) cum.push(cum[k - 1] + Math.hypot(pts[k].x - pts[k - 1].x, pts[k].y - pts[k - 1].y));
  const visKm = cum[cum.length - 1];
  const damage = []; for (let k = 0; k < 2 + (rnd() * 3 | 0); k++) damage.push(0.1 + rnd() * 0.8);
  const bbox = pts.reduce((bb, p) => [Math.min(bb[0], p.x), Math.min(bb[1], p.y), Math.max(bb[2], p.x), Math.max(bb[3], p.y)], [1e9, 1e9, -1e9, -1e9]);
  return { i, a: r.from, b: r.to, terrain: r.terrain, def: t, gameKm, pts, cum, visKm, bbox, damage, days: gameKm / (TRUCK.speedKmPerDay * t.speedMultiplier), fuel: gameKm * TRUCK.fuelPerKm * t.costMultiplier };
});
const ADJ = {}; for (const c of CITIES) ADJ[c.id] = []; for (const e of EDGES) { ADJ[e.a].push(e); ADJ[e.b].push(e); }
function edgePoint(e, gameDist, dir) {
  const frac = Math.min(1, Math.max(0, gameDist / e.gameKm)); const target = (dir > 0 ? frac : 1 - frac) * e.visKm;
  let k = 1; while (k < e.cum.length - 1 && e.cum[k] < target) k++;
  const seg = (target - e.cum[k - 1]) / Math.max(1e-6, e.cum[k] - e.cum[k - 1]);
  const p0 = e.pts[k - 1], p1 = e.pts[k];
  return { x: p0.x + (p1.x - p0.x) * seg, y: p0.y + (p1.y - p0.y) * seg, ang: Math.atan2((p1.y - p0.y) * dir, (p1.x - p0.x) * dir) };
}
function pointAlong(e, visTarget) { let k = 1; while (k < e.cum.length - 1 && e.cum[k] < visTarget) k++; const seg = (visTarget - e.cum[k - 1]) / Math.max(1e-6, e.cum[k] - e.cum[k - 1]); const p0 = e.pts[k - 1], p1 = e.pts[k]; return { x: p0.x + (p1.x - p0.x) * seg, y: p0.y + (p1.y - p0.y) * seg, nx: -(p1.y - p0.y), ny: p1.x - p0.x, len: Math.hypot(p1.x - p0.x, p1.y - p0.y) }; }
function nearestRoad(x, y) { // nearest road point in km; used for driving
  let best = null;
  for (const e of EDGES) {
    const bb = e.bbox; if (x < bb[0] - 12 || x > bb[2] + 12 || y < bb[1] - 12 || y > bb[3] + 12) continue;
    for (let k = 1; k < e.pts.length; k++) {
      const p = e.pts[k - 1], q = e.pts[k]; const dx = q.x - p.x, dy = q.y - p.y; const l2 = dx * dx + dy * dy;
      let t = ((x - p.x) * dx + (y - p.y) * dy) / l2; t = Math.max(0, Math.min(1, t));
      const px = p.x + dx * t, py = p.y + dy * t; const d = Math.hypot(x - px, y - py);
      if (!best || d < best.d) best = { e, d, px, py };
    }
  }
  return best;
}
function route(seeds, target) {
  const dist = {}, prev = {}; const open = new Set();
  for (const c of CITIES) { dist[c.id] = Infinity; open.add(c.id); }
  for (const s of seeds) if (s.days < dist[s.node]) { dist[s.node] = s.days; prev[s.node] = { seed: s }; }
  while (open.size) {
    let u = null; for (const n of open) if (u === null || dist[n] < dist[u]) u = n;
    if (u === null || dist[u] === Infinity) break; open.delete(u); if (u === target) break;
    for (const e of ADJ[u]) { const v = e.a === u ? e.b : e.a; const nd = dist[u] + e.days; if (nd < dist[v]) { dist[v] = nd; prev[v] = { from: u, edge: e, dir: e.a === u ? 1 : -1 }; } }
  }
  if (dist[target] === Infinity) return null;
  const legs = []; let n = target; let seed = null;
  while (prev[n]) { const p = prev[n]; if (p.seed) { seed = p.seed; break; } legs.unshift({ edge: p.edge, dir: p.dir, from: p.from, to: n }); n = p.from; }
  return { legs, seed, days: dist[target] };
}

// ───────────────────────────────────────────────────────────────────────────
//  7. Convoy — parked at a city, auto-driving an edge, or free under WASD
// ───────────────────────────────────────────────────────────────────────────
const SEC_PER_DAY = 2.4;
const conv = {
  node: W.startCityId, edge: null, dir: 1, dist: 0, freeLeg: null,
  legs: [], target: null, leftCity: null,
  x: 0, y: 0, ang: -Math.PI / 2, paused: false, pace: 1, moving: false, surface: 'parked',
  day: 1, dayFrac: 0, cash: W.startCash, km: 0,
  trail: [], trailKm: 0, tripKm: 0, tripStartKm: 0, bumpAt: 0,
};
{ const c = CITY_BY_ID[conv.node]; conv.x = c.x; conv.y = c.y; }
const dust = []; let pulse = 0;
function curSeeds() {
  if (conv.node) return [{ node: conv.node, days: 0 }];
  if (conv.edge) {
    const e = conv.edge; const fromA = conv.dist, toB = e.gameKm - conv.dist; const sp = TRUCK.speedKmPerDay * e.def.speedMultiplier;
    return [{ node: e.b, days: toB / sp, edge: e, dir: 1, km: toB }, { node: e.a, days: fromA / sp, edge: e, dir: -1, km: fromA }];
  }
  // free position: straight off-road hop to any city within reach, then the roads
  const seeds = [];
  for (const c of CITIES) { const d = Math.hypot(c.x - conv.x, c.y - conv.y); if (d < 500) seeds.push({ node: c.id, days: d / (TRUCK.speedKmPerDay * 0.75), free: true, km: Math.round(d) }); }
  return seeds;
}
function planTo(cityId) {
  const r = route(curSeeds(), cityId); if (!r) return null;
  let km = 0, fuel = 0;
  if (r.seed && r.seed.edge) { km += r.seed.km; fuel += r.seed.km * TRUCK.fuelPerKm * r.seed.edge.def.costMultiplier; }
  if (r.seed && r.seed.free) { km += r.seed.km; fuel += r.seed.km * TRUCK.fuelPerKm * 1.25; }
  for (const l of r.legs) { km += l.edge.gameKm; fuel += l.edge.fuel; }
  const days = Math.max(1, Math.ceil(r.days - 1e-9));
  return { cityId, legs: r.legs, seed: r.seed, km: Math.round(km), fuel: Math.round(fuel), days, exactDays: r.days };
}
function depart(plan) {
  conv.target = plan.cityId; conv.legs = plan.legs.slice(); conv.tripKm = plan.km; conv.tripStartKm = conv.km; conv.freeLeg = null;
  if (conv.edge) { if (plan.seed.dir !== conv.dir) { conv.dir = plan.seed.dir; toast('Convoy turns around.', 'alert'); } }
  else if (conv.node) { conv.leftCity = conv.node; conv.node = null; }
  else if (plan.seed && plan.seed.free) { const c = CITY_BY_ID[plan.seed.node]; conv.freeLeg = { x: c.x, y: c.y, id: c.id }; }
  stepEdge();
  const t = CITY_BY_ID[plan.cityId];
  toast(`Departed for ${t.name}. ${plan.days} day${plan.days > 1 ? 's' : ''}, ~${plan.fuel.toLocaleString()} cr fuel.`);
  cam.follow = true; syncButtons();
  if (cam.z < 1.1) { cam.tz = 1.4; cam.tx = conv.x; cam.ty = conv.y; }
}
function stepEdge() {
  if (conv.freeLeg) return;
  if (!conv.edge && conv.legs.length) { const l = conv.legs.shift(); conv.edge = l.edge; conv.dir = l.dir; conv.dist = l.dir > 0 ? 0 : l.edge.gameKm; }
  if (conv.edge) { const d = conv.edge.def; if (d.speedMultiplier < 1) toast(`${d.name}: ${Math.round(d.speedMultiplier * 100)}% speed, fuel ×${d.costMultiplier}`, d.speedMultiplier < 0.6 ? 'alert' : ''); }
}
function spendKm(km, costMult, dDays) {
  if (window.MECHA) return;
  conv.km += km; conv.cash -= km * TRUCK.fuelPerKm * costMult; conv.dayFrac += dDays;
  while (conv.dayFrac >= 1) { conv.dayFrac -= 1; conv.day++; conv.cash -= TRUCK.upkeepPerDay; flip('s-day'); }
  if (conv.km - conv.trailKm >= 6) { conv.trailKm = conv.km; conv.trail.push({ x: conv.x, y: conv.y }); if (conv.trail.length > 600) conv.trail.shift(); }
  if (km > 0 && Math.random() < 0.6) dust.push({ x: conv.x - Math.cos(conv.ang) * 6 + (Math.random() - 0.5) * 4, y: conv.y - Math.sin(conv.ang) * 6 + (Math.random() - 0.5) * 4, r: 2 + Math.random() * 3, a: 0.35, vx: (Math.random() - 0.5) * 4, vy: -2 - Math.random() * 3 });
}
function arriveAt(cityId, final) {
  const c = CITY_BY_ID[cityId]; conv.node = cityId; conv.edge = null; conv.freeLeg = null; conv.x = c.x; conv.y = c.y; conv.leftCity = cityId;
  if (final) { conv.target = null; conv.legs = []; toast(`Arrived at ${c.name}, ${c.region} · day ${conv.day}`, 'arrive'); pulse = 1; }
  else { toast(`Passing ${c.name}`); stepEdge(); }
}
function advanceAuto(dtSec) {
  if (conv.paused) return;
  let dDays = dtSec / SEC_PER_DAY * conv.pace;
  if (conv.freeLeg) { // straight off-road hop toward the seed city
    const b = biomeAt(conv.x, conv.y); const mult = Math.max(0.35, offroadMult(b)); const cost = offroadCost(b);
    const kmPerDay = TRUCK.speedKmPerDay * mult; let km = kmPerDay * dDays;
    const dx = conv.freeLeg.x - conv.x, dy = conv.freeLeg.y - conv.y; const d = Math.hypot(dx, dy);
    if (km >= d) { km = d; dDays = km / kmPerDay; }
    conv.ang = Math.atan2(dy, dx); conv.x += dx / d * km; conv.y += dy / d * km; conv.surface = 'off-road · ' + BIOME_NAME[b];
    spendKm(km, cost, dDays);
    if (km >= d - 1e-6) { const id = conv.freeLeg.id; conv.freeLeg = null; if (id === conv.target || !conv.legs.length) arriveAt(id, true); else { conv.node = null; toast(`Reached the road at ${CITY_BY_ID[id].name}`); stepEdge(); } }
    return;
  }
  if (!conv.edge) return;
  const e = conv.edge; const kmPerDay = TRUCK.speedKmPerDay * e.def.speedMultiplier;
  let km = kmPerDay * dDays;
  const remaining = conv.dir > 0 ? e.gameKm - conv.dist : conv.dist;
  if (km >= remaining) { km = remaining; dDays = km / kmPerDay; }
  conv.dist += km * conv.dir;
  const p = edgePoint(e, conv.dist, conv.dir); conv.x = p.x; conv.y = p.y; conv.ang = p.ang; conv.surface = e.def.name + ' · ' + Math.round(e.def.speedMultiplier * 100) + '%';
  spendKm(km, e.def.costMultiplier, dDays);
  if (km >= remaining - 1e-9) { const id = conv.dir > 0 ? e.b : e.a; conv.edge = null; arriveAt(id, id === conv.target || !conv.legs.length); }
}
// WASD: free driving over the terrain grid. Roads within 5 km count as the road surface.
function driveFree(dtSec, ix, iy) {
  if (window.MECHA) { if (!conv.paused) MECHA.steer(ix, iy); return; }
  if (conv.paused) return;
  if (conv.edge || conv.legs.length || conv.freeLeg) { conv.edge = null; conv.legs = []; conv.target = null; conv.freeLeg = null; toast('Manual control.', 'alert'); }
  if (conv.node) { conv.leftCity = conv.node; conv.node = null; }
  const want = Math.atan2(iy, ix); let da = want - conv.ang; while (da > Math.PI) da -= 2 * Math.PI; while (da < -Math.PI) da += 2 * Math.PI;
  conv.ang += da * Math.min(1, dtSec * 10);
  const b = biomeAt(conv.x, conv.y); const road = nearestRoad(conv.x, conv.y); const onRoad = road && road.d < 5;
  const mult = onRoad ? road.e.def.speedMultiplier : offroadMult(b); const cost = onRoad ? road.e.def.costMultiplier : offroadCost(b);
  conv.surface = onRoad ? road.e.def.name + ' · ' + Math.round(mult * 100) + '%' : 'off-road · ' + BIOME_NAME[b] + ' · ' + Math.round(mult * 100) + '%';
  const dDays = dtSec / SEC_PER_DAY * conv.pace; const km = TRUCK.speedKmPerDay * Math.max(mult, onRoad ? mult : 0) * dDays;
  const nx = conv.x + Math.cos(conv.ang) * km, ny = conv.y + Math.sin(conv.ang) * km;
  const nb = biomeAt(nx, ny); const nroad = nearestRoad(nx, ny); const nOn = nroad && nroad.d < 5;
  const blocked = !nOn && (offroadMult(nb) === 0) || nx < 0 || ny < 0 || nx > MAP_W || ny > MAP_H;
  if (blocked || km <= 0) { if (performance.now() - conv.bumpAt > 1500) { conv.bumpAt = performance.now(); toast(`Impassable: ${BIOME_NAME[nb]}. Find a road.`, 'alert'); } return; }
  conv.x = nx; conv.y = ny;
  if (onRoad && road.d > 1.5) { conv.x += (road.px - conv.x) * Math.min(1, dtSec * 2); conv.y += (road.py - conv.y) * Math.min(1, dtSec * 2); } // gentle magnet onto the tarmac
  spendKm(km, cost, dDays);
  for (const c of CITIES) { const d = Math.hypot(c.x - conv.x, c.y - conv.y); if (d > 16 && conv.leftCity === c.id) conv.leftCity = null; if (d < 10 && conv.leftCity !== c.id) { arriveAt(c.id, true); break; } }
}

// ───────────────────────────────────────────────────────────────────────────
//  8. Camera, claims, mist, smoke
// ───────────────────────────────────────────────────────────────────────────
const cam = { x: conv.x, y: conv.y, z: 0.5, follow: true, tx: null, ty: null, tz: null };
const CLAIMS = [
  { name: 'Ruhr claim', ring: [[6.3, 51.0], [8.6, 50.9], [8.9, 51.7], [7.4, 52.0], [6.1, 51.6]] },
  { name: 'Silesia claim', ring: [[16.6, 50.3], [18.9, 50.2], [19.3, 51.2], [17.8, 51.5], [16.5, 51.0]] },
  { name: 'Loire claim', ring: [[-0.6, 46.4], [1.7, 46.3], [2.0, 47.4], [0.6, 47.7], [-0.9, 47.1]] },
].map(c => { const pts = c.ring.map(([lon, lat]) => toKm(lon, lat)); const cx = pts.reduce((s, p) => s + p.x, 0) / pts.length, cy = pts.reduce((s, p) => s + p.y, 0) / pts.length; return { ...c, pts, cx, cy, h: strHash(c.name) }; });
const REGIONS = {}; for (const c of CITIES) { (REGIONS[c.region] ||= []).push(c); }
const REGION_LABELS = Object.entries(REGIONS).map(([name, cs]) => ({ name, x: cs.reduce((s, c) => s + c.x, 0) / cs.length, y: cs.reduce((s, c) => s + c.y, 0) / cs.length - 40 }));
const MIST = []; { const r = mulberry(99); for (let k = 0; k < 14; k++) MIST.push({ x: r() * MAP_W, y: r() * MAP_H, r: 260 + r() * 300, vx: 6 + r() * 8, vy: (r() - 0.5) * 4, a: 0.035 + r() * 0.035 }); }

// ───────────────────────────────────────────────────────────────────────────
//  9. Rendering
// ───────────────────────────────────────────────────────────────────────────
const cv = document.getElementById('map'); const ctx = cv.getContext('2d');
let VW = 0, VH = 0, DPR = 1;
addEventListener('resize', resize); resize();
const layers = { ink: true, roads: true, labels: true, claims: true, mist: true, cells: true, cost: false, grid: false };
let hover = null, pending = null, mouse = { x: -1, y: -1 };

const ROAD_NAME = { plain: 'Open road', coastal: 'Coast road', hills: 'Highland', alpine: 'Alpine pass', strait: 'Strait ferry' };
let fpsAvg = 60;

const keys = {};
let lastT = performance.now(); let ready = false; let driveHeld = false;

// ───────────────────────────────────────────────────────────────────────────
//  10. HUD, input, toasts
// ───────────────────────────────────────────────────────────────────────────
const $ = (id) => document.getElementById(id);
function flip(id) { const e = $(id); e.classList.remove('flip'); void e.offsetWidth; e.classList.add('flip'); }
let lastHud = {};
function setText(id, v, cls) { if (lastHud[id] !== v) { $(id).textContent = v; lastHud[id] = v; } if (cls !== undefined) $(id).className = cls; }
function updateHud() {
  if (window.MECHA && MECHA.hud()) return;
  setText('s-day', conv.day);
  setText('s-cash', Math.round(conv.cash).toLocaleString() + ' cr', conv.cash < 5000 ? 'alert' : '');
  if (conv.node) { const c = CITY_BY_ID[conv.node]; setText('s-pos', `${c.name}, ${c.region}`, ''); setText('s-burn', TRUCK.upkeepPerDay + ' cr'); }
  else if (conv.target) { const c = CITY_BY_ID[conv.target]; setText('s-pos', `On the road → ${c.name}`, 'amber'); setText('s-burn', Math.round(TRUCK.upkeepPerDay + TRUCK.speedKmPerDay * 0.8 * TRUCK.fuelPerKm * 1.15) + ' cr'); }
  else { const { lon, lat } = toLonLat(conv.x, conv.y); setText('s-pos', `Open country · ${lat.toFixed(1)}°N ${lon.toFixed(1)}°E`, 'amber'); setText('s-burn', Math.round(TRUCK.upkeepPerDay + TRUCK.speedKmPerDay * 0.7 * TRUCK.fuelPerKm * 1.3) + ' cr'); }
  setText('s-road', conv.surface);
  setText('s-pace', conv.paused ? 'paused' : conv.pace + '×', conv.paused ? 'alert' : '');
}
function toast(msg, kind = '') { const t = document.createElement('div'); t.className = 'toast ' + kind; t.textContent = msg; $('toasts').appendChild(t); setTimeout(() => t.classList.add('fade'), kind === 'arrive' ? 4200 : 2600); setTimeout(() => t.remove(), kind === 'arrive' ? 5000 : 3300); }
function syncButtons() { $('btn-follow').classList.toggle('on', cam.follow); $('btn-pause').textContent = conv.paused ? '▶' : '❚❚'; for (const b of document.querySelectorAll('[data-pace]')) b.classList.toggle('on', +b.dataset.pace === conv.pace); }
function pickCity(sx, sy) { let best = null, bd = 1e9; for (const c of CITIES) { const { sx: x, sy: y } = toScreen(c.x, c.y); const d = Math.hypot(x - sx, y - sy); const R = (12 + c.pop * 6) * cam.z + 8; if (d < R && d < bd) { best = c.id; bd = d; } } return best; }
function showCard(plan) {
  pending = plan; const c = CITY_BY_ID[plan.cityId];
  $('c-name').textContent = c.name.toUpperCase(); $('c-region').textContent = c.region + ' · ' + c.industries.join(', ');
  $('c-days').textContent = plan.days; $('c-km').textContent = plan.km + ' km'; $('c-fuel').textContent = '~' + plan.fuel.toLocaleString() + ' cr'; $('c-arrive').textContent = 'day ' + (conv.day + plan.days);
  const legs = [];
  if (plan.seed && plan.seed.edge) legs.push(`${plan.seed.dir === conv.dir ? 'continue' : 'turn back'} to <b>${CITY_BY_ID[plan.seed.node].name}</b>`);
  if (plan.seed && plan.seed.free) legs.push(`off-road to <b>${CITY_BY_ID[plan.seed.node].name}</b>`);
  for (const l of plan.legs) legs.push(`<b>${CITY_BY_ID[l.to].name}</b> <span style="opacity:.7">(${l.edge.def.name})</span>`);
  $('c-legs').innerHTML = 'Route: ' + legs.join(' → ');
  $('btn-go').firstChild.textContent = conv.node ? 'Depart ' : 'Reroute ';
  $('card').classList.add('show');
}
function hideCard() { if (!pending) return; pending = null; $('card').classList.remove('show'); }
function focusChart() { cv.focus({ preventScroll: true }); }
window.focusChart = focusChart;
function fitAll() { cam.tz = Math.min(VW / MAP_W, VH / MAP_H) * 0.96; cam.tx = MAP_W / 2; cam.ty = MAP_H / 2; cam.follow = false; syncButtons(); }

let drag = null;
cv.addEventListener('mousedown', (e) => { drag = { sx: e.clientX, sy: e.clientY, cx: cam.x, cy: cam.y, moved: false }; cv.classList.add('grabbing'); });
addEventListener('mousemove', (e) => {
  mouse = { x: e.clientX, y: e.clientY };
  if (drag) { const dx = e.clientX - drag.sx, dy = e.clientY - drag.sy; if (Math.hypot(dx, dy) > 3) { drag.moved = true; cam.follow = false; cam.tz = null; syncButtons(); } cam.x = drag.cx - dx / cam.z; cam.y = drag.cy - dy / cam.z; }
  const h = pickCity(e.clientX, e.clientY); hover = h; cv.classList.toggle('pick', !!h && !drag);
  const tip = $('tip');
  if (h && !drag) { const c = CITY_BY_ID[h]; let html = `<b>${c.name.toUpperCase()}</b><span>${c.region} · pop ${c.pop}k · ${c.industries.join(', ')}</span>`; if (h !== conv.node) { const p = planTo(h); if (p) html += `<br><em>${p.days} day${p.days > 1 ? 's' : ''}</em> <span>· ${p.km} km · ~${p.fuel.toLocaleString()} cr fuel</span>`; } else html += '<br><span>you are here</span>'; tip.innerHTML = html; tip.style.display = 'block'; tip.style.left = (e.clientX + 16) + 'px'; tip.style.top = (e.clientY + 16) + 'px'; }
  else tip.style.display = 'none';
});
addEventListener('mouseup', (e) => {
  if (!drag) return; const moved = drag.moved; drag = null; cv.classList.remove('grabbing');
  if (moved) return;
  if (window.MECHA) { MECHA.clickWorld(e.clientX, e.clientY); return; }
  const h = pickCity(e.clientX, e.clientY);
  if (h && h !== conv.node) { const p = planTo(h); if (p) showCard(p); }
  else if (!h) hideCard();
});
cv.addEventListener('wheel', (e) => {
  e.preventDefault(); const f = Math.exp(-e.deltaY * 0.0012); const nz = Math.min(10, Math.max(0.25, cam.z * f));
  const w = toWorld(e.clientX, e.clientY); cam.x = w.x - (e.clientX - VW / 2) / nz; cam.y = w.y - (e.clientY - VH / 2) / nz; cam.z = nz; cam.tz = null;
}, { passive: false });
cv.tabIndex = 0;
cv.addEventListener('pointerdown', () => cv.focus({ preventScroll: true }));
addEventListener('keydown', (e) => {
  const t = e.target;
  const typing = t && (t.tagName === 'TEXTAREA' || t.tagName === 'SELECT' || (t.tagName === 'INPUT' && t.type !== 'checkbox' && t.type !== 'radio'));
  if (typing) return;
  if (e.code === 'Tab' && window.OPS) { e.preventDefault(); if (OPS.isOpen()) OPS.close(); else OPS.open(); return; }
  if (window.OPS && OPS.isOpen()) return; // the books have the keyboard; Esc closes them
  keys[e.code] = true;
  if (['KeyW', 'KeyA', 'KeyS', 'KeyD', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Space'].includes(e.code)) e.preventDefault();
  if (e.repeat) return;
  if (e.code === 'Space') { conv.paused = !conv.paused; syncButtons(); }
  else if (e.code === 'KeyF') { cam.follow = !cam.follow; cam.tz = null; syncButtons(); }
  else if (e.code === 'KeyH') fitAll();
  else if (e.code === 'Digit1') { conv.pace = 1; syncButtons(); } else if (e.code === 'Digit2') { conv.pace = 2; syncButtons(); } else if (e.code === 'Digit3') { conv.pace = 4; syncButtons(); }
  else if (e.code === 'KeyL') toggleLayer('cost'); else if (e.code === 'KeyG') toggleLayer('grid'); else if (e.code === 'KeyC') toggleLayer('claims');
  else if (e.code === 'Escape') hideCard();
  else if (e.code === 'Enter' && pending) { depart(pending); hideCard(); }
});
addEventListener('keyup', (e) => { keys[e.code] = false; });
addEventListener('blur', () => { for (const k in keys) keys[k] = false; });
function toggleLayer(k) { layers[k] = !layers[k]; document.querySelector(`[data-layer="${k}"]`).checked = layers[k]; }
for (const i of document.querySelectorAll('[data-layer]')) i.addEventListener('change', () => { layers[i.dataset.layer] = i.checked; i.blur(); });
$('btn-pause').addEventListener('click', () => { conv.paused = !conv.paused; syncButtons(); });
for (const b of document.querySelectorAll('[data-pace]')) b.addEventListener('click', () => { conv.pace = +b.dataset.pace; syncButtons(); });
$('btn-follow').addEventListener('click', () => { cam.follow = !cam.follow; cam.tz = null; syncButtons(); });
$('btn-fit').addEventListener('click', fitAll);
$('btn-go').addEventListener('click', () => { if (pending) { depart(pending); hideCard(); } });
$('btn-cancel').addEventListener('click', hideCard);
$('btn-notes').addEventListener('click', () => { $('notes').classList.toggle('show'); $('btn-notes').style.display = $('notes').classList.contains('show') ? 'none' : ''; });
$('notes').addEventListener('click', () => { $('notes').classList.remove('show'); $('btn-notes').style.display = ''; });
for (const b of document.querySelectorAll('button')) b.addEventListener('mouseup', () => b.blur()); // keep WASD reaching the map
{
  const rows = $('legend-rows');
  for (const id of Object.keys(ROAD_NAME)) {
    const row = document.createElement('div'); row.className = 'row'; const c = document.createElement('canvas'); c.width = 88; c.height = 24; const g = c.getContext('2d');
    g.fillStyle = '#5c636b'; g.fillRect(0, 0, 88, 24); g.lineCap = 'round';
    if (id === 'strait') { g.fillStyle = '#2a3a40'; g.fillRect(0, 0, 88, 24); g.strokeStyle = 'rgba(200,215,215,0.8)'; g.lineWidth = 2; g.setLineDash([4, 6]); g.beginPath(); g.moveTo(8, 12); g.lineTo(80, 12); g.stroke(); }
    else { const w = id === 'alpine' ? 8 : 11; g.strokeStyle = 'rgba(138,148,160,0.6)'; g.lineWidth = w + 4; g.beginPath(); g.moveTo(8, 12); g.lineTo(80, 12); g.stroke(); g.strokeStyle = '#c0c8d0'; g.lineWidth = w; g.stroke(); g.strokeStyle = '#31373e'; g.lineWidth = w - 2; g.stroke(); g.strokeStyle = 'rgba(215,180,80,0.7)'; g.lineWidth = 1; g.setLineDash([6, 5]); g.stroke(); }
    row.appendChild(c); const s = document.createElement('span'); s.textContent = ROAD_NAME[id]; row.appendChild(s); const i = document.createElement('i'); i.textContent = Math.round(W.terrain[id].speedMultiplier * 100) + '%'; row.appendChild(i); rows.appendChild(row);
  }
}

// ───────────────────────────────────────────────────────────────────────────
//  11. Boot: paint the chart in chunks, then the establishing shot
// ───────────────────────────────────────────────────────────────────────────
(function boot() {
  const bctx = base.getContext('2d'); const img = bctx.createImageData(MAP_W, MAP_H);
  // pre-warm detail tiles for a deep-linked view while the base bakes (worker paints on real time)
  try { const qv = new URLSearchParams(location.search).get('view'); if (qv) { const [lo, la, zz] = qv.split(',').map(Number); if (zz > ZOOM_TILE_AT) { const p = toKm(lo, la), cw = (VW / 2) / zz, ch = (VH / 2) / zz; const c0 = Math.max(0, Math.floor((p.x - cw) / TILE_KM)), c1 = Math.min(Math.ceil(MAP_W / TILE_KM), Math.ceil((p.x + cw) / TILE_KM)); const r0 = Math.max(0, Math.floor((p.y - ch) / TILE_KM)), r1 = Math.min(Math.ceil(MAP_H / TILE_KM), Math.ceil((p.y + ch) / TILE_KM)); startTileWorker(); for (let cy = r0; cy <= r1; cy++) for (let cx = c0; cx <= c1; cx++) wantTile(cx, cy); } } } catch (_) {}
  const CH = 60; let row = 0; const bootT0 = performance.now();
  const chan = new MessageChannel(); let pendingStep = null; chan.port1.onmessage = () => pendingStep && pendingStep();
  const yieldTo = (fn) => { pendingStep = fn; chan.port2.postMessage(0); };
  function step() {
    const t0 = performance.now();
    while (row < MAP_H && performance.now() - t0 < 200) { paintRows(img, row, Math.min(MAP_H, row + CH)); row += CH; }
    $('loadbar').style.width = (row / MAP_H * 80) + '%';
    if (row < MAP_H) return yieldTo(step);
    $('loadmsg').textContent = 'tracing the coast…'; $('loadbar').style.width = '85%';
    yieldTo(() => {
      finishBase(img); $('loadmsg').textContent = 'surveying the surface…'; $('loadbar').style.width = '93%';
      yieldTo(() => spritesReady.then((n) => {
        window.__loadMs = Math.round(performance.now() - bootT0); window.__sprites = n;
        buildGlyphs(); bakeInk(); buildPois(); $('loadbar').style.width = '100%';
        $('loading').style.transition = 'opacity .5s'; $('loading').style.opacity = 0; setTimeout(() => $('loading').remove(), 600);
        cam.z = Math.min(VW / MAP_W, VH / MAP_H) * 0.96; cam.x = MAP_W / 2; cam.y = MAP_H / 2;
        cam.tz = 1.5; cam.tx = conv.x; cam.ty = conv.y;
        try { const qv = new URLSearchParams(location.search).get('view'); if (qv) { const [lo, la, zz] = qv.split(',').map(Number); const p = toKm(lo, la); cam.z = zz; cam.x = cam.tx = p.x; cam.y = cam.ty = p.y; cam.tz = null; cam.follow = false; } } catch (_) {}
        ready = true; syncButtons(); lastT = performance.now(); requestAnimationFrame(frame);
        if (window.MECHA) MECHA.boot();
        else {
          const c = CITY_BY_ID[conv.node]; if (c) setTimeout(() => toast(`Convoy registered at ${c.name}, ${c.region}. WASD to drive, or click a city.`), 900);
        }
      }));
    });
  }
  // give the optional textures a moment to arrive before the base is composed
  setTimeout(step, 150);
})();
