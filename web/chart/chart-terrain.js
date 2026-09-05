// Extracted verbatim (Phase D CP-D2) from web/chart/chart.js at integration 67b6fb5.
'use strict';
function mulberry(seed) { return () => { seed |= 0; seed = seed + 0x6D2B79F5 | 0; let t = Math.imul(seed ^ seed >>> 15, 1 | seed); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
function strHash(s) { let h = 2166136261; for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 16777619); } return h >>> 0; }
function h2(x, y) { return PERM[(PERM[x & 255] + y) & 255] * (1 / 255); }
function vnoise(x, y) {
  const xi = Math.floor(x), yi = Math.floor(y), xf = x - xi, yf = y - yi;
  const u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
  const a = h2(xi, yi), b = h2(xi + 1, yi), c = h2(xi, yi + 1), d = h2(xi + 1, yi + 1);
  const t = a + (b - a) * u; return t + ((c + (d - c) * u) - t) * v;
}
function fbm2(x, y) { return (vnoise(x, y) * 2 + vnoise(x * 2.1 + 7.3, y * 2.1 + 3.1)) / 3; }
function pip(px, py, ring) { let inside = false; for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) { const [xi, yi] = ring[i], [xj, yj] = ring[j]; if ((yi > py) !== (yj > py) && px < (xj - xi) * (py - yi) / (yj - yi) + xi) inside = !inside; } return inside; }
function lattice(x, y) {
  const fx = Math.min(LW - 1.001, Math.max(0, x / LAT)), fy = Math.min(LH - 1.001, Math.max(0, y / LAT));
  const i = fx | 0, j = fy | 0, u = fx - i, v = fy - j; const k = j * LW + i;
  const a = (1 - u) * (1 - v), b = u * (1 - v), c = (1 - u) * v, d = u * v;
  _wx = latWX[k] * a + latWX[k + 1] * b + latWX[k + LW] * c + latWX[k + LW + 1] * d;
  _wy = latWY[k] * a + latWY[k + 1] * b + latWY[k + LW] * c + latWY[k + LW + 1] * d;
  _mot = latMot[k] * a + latMot[k + 1] * b + latMot[k + LW] * c + latMot[k + LW + 1] * d;
  _tex = latTex[k] * a + latTex[k + 1] * b + latTex[k + LW] * c + latTex[k + LW + 1] * d;
  _scar = latScar[k] * a + latScar[k + 1] * b + latScar[k + LW] * c + latScar[k + LW + 1] * d;
}
function biomeAt(x, y) {
  lattice(x, y);
  const c = Math.min(FW - 1, Math.max(0, (x + _wx) / FINE | 0)), r = Math.min(FH - 1, Math.max(0, (y + _wy) / FINE | 0));
  const i = r * FW + c, b = fine[i];
  if (edgeDist[i] < 6) { const n = 0.65 * _tex + 0.35 * (_mot + 0.5); if (n * 6.5 > edgeDist[i] + 0.6) return BIOME.plain; }
  return b;
}
function offroadMult(b) { const o = OFFROAD[biomeKey[b]]; return o ? o.speedMultiplier : 0; }
function offroadCost(b) { const o = OFFROAD[biomeKey[b]]; return o ? o.costMultiplier : 1; }
function pickSprite(pool, r) { let total = 0; for (const s of pool) total += s.w; let x = r * total; for (const s of pool) { x -= s.w; if (x <= 0) return s; } return pool[pool.length - 1]; }
// Elongated sprites (rotate:false) are drawn along a heading instead of spun at random:
// the prevailing wind out in the open, the road where the rule places them on one.
const WIND = -0.38;
function heading(sp, v, along) { return sp.rotate ? v * Math.PI * 2 : (along == null ? WIND : along) + (v - 0.5) * 0.3; }
// Points of interest placed by rule once the sprites are in: dead settlements off the
// roads between cities, ruins inside every city ring, wrecks on the road shoulders.
const POIS = [];
function buildPois() {
  const r = mulberry(11);
  if (RUIN_POOL.length) for (const e of EDGES) {
    if (e.terrain === 'strait' || e.visKm < 220 || r() > 0.7) continue;
    const p = pointAlong(e, (0.35 + r() * 0.3) * e.visKm); const nx = p.nx / p.len, ny = p.ny / p.len; const side = r() < 0.5 ? 1 : -1; const off = 22 + r() * 18;
    const x = p.x + nx * off * side, y = p.y + ny * off * side; const b = biomeAt(x, y);
    if (b === BIOME.water || b === BIOME.deep || b === BIOME.mountain) continue;
    const sp = pickSprite(RUIN_POOL, r());
    POIS.push({ sp, x, y, size: 18 + r() * 8, rot: heading(sp, r(), Math.atan2(p.ny, p.nx)), label: 'RUINS' });
  }
  if (WRECK_POOL.length) for (const e of EDGES) {
    if (e.terrain === 'strait') continue;
    for (let s = 30; s < e.visKm - 30; s += 70) {
      if (r() > 0.4) continue;
      const p = pointAlong(e, s); const nx = p.nx / p.len, ny = p.ny / p.len; const side = r() < 0.5 ? 1 : -1; const off = 6 + r() * 5;
      POIS.push({ sp: pickSprite(WRECK_POOL, r()), x: p.x + nx * off * side, y: p.y + ny * off * side, size: 7 + r() * 4, rot: Math.atan2(p.ny, p.nx) + (r() - 0.5) * 0.6 + (side > 0 ? 0 : Math.PI) });
    }
  }
  if (RUIN_POOL.length) for (const c of CITIES) {
    const rr = mulberry(c.h + 5); const R = 12 + c.pop * 6; const n = 2 + (rr() * 2 | 0);
    for (let k = 0; k < n; k++) { const a = rr() * 6.28, d = R * (0.3 + rr() * 0.3); const sp = pickSprite(RUIN_POOL, rr()); POIS.push({ sp, x: c.x + Math.cos(a) * d, y: c.y + Math.sin(a) * d, size: R * (0.6 + rr() * 0.3), rot: heading(sp, rr()), city: true }); }
  }
}
// How much of a biome's lattice is used at all. Forest is full now; plain, hill and swamp
// carry more than they did so copses, hedgerows and standing structures show up out there.
const GLYPH_DENSITY = { 0: 0.13, 1: 0.62, 2: 0.85, 3: 1.0, 4: 0.65, 5: 0.4, 6: 0.35, 7: 0.03, 8: 0 };
const glyphs = []; const BUCKET = 150; const buckets = new Map();
function buildGlyphs() {
  const r = mulberry(7);
  // Two passes: the coarse 17 km lattice for procedural glyphs and sprite biomes with a
  // coarse step, then a fine lattice for sprite biomes that ask for one (forests).
  const passes = [17]; for (const k in SPRITE_RULE) if (SPRITE_RULE[k].step < 17 && !passes.includes(SPRITE_RULE[k].step)) passes.push(SPRITE_RULE[k].step);
  for (const step of passes) for (let y = step; y < MAP_H - step; y += step) for (let x = step; x < MAP_W - step; x += step) {
    const gx = x + (r() - 0.5) * step * 0.9, gy = y + (r() - 0.5) * step * 0.9;
    const b = biomeAt(gx, gy);
    const rule = SPRITE_RULE[b]; const pool = SPRITES[b];
    const wantStep = rule ? Math.min(17, rule.step) : 17;
    if (step !== wantStep) continue;                       // each biome is filled by exactly one pass
    let density = GLYPH_DENSITY[b];
    if (rule && rule.step > 17) density *= (17 * 17) / (rule.step * rule.step);
    if (r() > density) continue;
    let near = false; for (const c of CITIES) { if (Math.hypot(c.x - gx, c.y - gy) < 30) { near = true; break; } }
    if (near) continue;
    const g = { t: b, x: gx, y: gy, s: 0.75 + r() * 0.5, v: r() };
    if (pool && r() < rule.share) g.sp = pickSprite(pool, r());
    else if (pool && rule.share < 1 && r() < 0.5) continue; // sprite biomes thin out their procedural marks
    glyphs.push(g);
    const key = ((gx / BUCKET) | 0) + ',' + ((gy / BUCKET) | 0);
    if (!buckets.has(key)) buckets.set(key, []); buckets.get(key).push(g);
  }
}
