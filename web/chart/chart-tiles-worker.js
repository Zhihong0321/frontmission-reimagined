'use strict';
// ── Keeper's Chart detail-tile worker (§7.1) ───────────────────────────
// Paints 256 km tiles at 4 px/km on demand. Receives the fine biome grid,
// edge distances, lattice arrays and the land field once, then answers
// tile requests with ImageBitmaps. Same painter as the base bake.

let CFG = null;
const P = {};            // transferred arrays land here
const TEX = {};          // code -> Uint8ClampedArray(256*256*4)

function mulberry(seed) { return () => { seed |= 0; seed = seed + 0x6D2B79F5 | 0; let t = Math.imul(seed ^ seed >>> 15, 1 | seed); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
let PERM = new Uint8Array(512);
function h2(x, y) { return PERM[(PERM[x & 255] + y) & 255] * (1 / 255); }
function vnoise(x, y) {
  const xi = Math.floor(x), yi = Math.floor(y), xf = x - xi, yf = y - yi;
  const u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
  const a = h2(xi, yi), b = h2(xi + 1, yi), c = h2(xi, yi + 1), d = h2(xi + 1, yi + 1);
  const t = a + (b - a) * u; return t + ((c + (d - c) * u) - t) * v;
}
function fbm2(x, y) { return (vnoise(x, y) * 2 + vnoise(x * 2.1 + 7.3, y * 2.1 + 3.1)) / 3; }

let _wx = 0, _wy = 0, _mot = 0, _tex = 0, _scar = 0;
function lattice(x, y) {
  const fx = Math.min(CFG.LW - 1.001, Math.max(0, x / CFG.LAT)), fy = Math.min(CFG.LH - 1.001, Math.max(0, y / CFG.LAT));
  const i = fx | 0, j = fy | 0, u = fx - i, v = fy - j; const k = j * CFG.LW + i;
  const a = (1 - u) * (1 - v), b = u * (1 - v), c = (1 - u) * v, d = u * v;
  const L = P.lat;
  _wx = L.wx[k] * a + L.wx[k + 1] * b + L.wx[k + CFG.LW] * c + L.wx[k + CFG.LW + 1] * d;
  _wy = L.wy[k] * a + L.wy[k + 1] * b + L.wy[k + CFG.LW] * c + L.wy[k + CFG.LW + 1] * d;
  _mot = L.mot[k] * a + L.mot[k + 1] * b + L.mot[k + CFG.LW] * c + L.mot[k + CFG.LW + 1] * d;
  _tex = L.tex[k] * a + L.tex[k + 1] * b + L.tex[k + CFG.LW] * c + L.tex[k + CFG.LW + 1] * d;
  _scar = L.scar[k] * a + L.scar[k + 1] * b + L.scar[k + CFG.LW] * c + L.scar[k + CFG.LW + 1] * d;
}
function biomeAt(x, y) {
  lattice(x, y);
  const c = Math.min(CFG.FW - 1, Math.max(0, (x + _wx) / CFG.FINE | 0)), r = Math.min(CFG.FH - 1, Math.max(0, (y + _wy) / CFG.FINE | 0));
  const i = r * CFG.FW + c, b = P.fine[i];
  if (P.edge[i] < 6) { const n = 0.65 * _tex + 0.35 * (_mot + 0.5); if (n * 6.5 > P.edge[i] + 0.6) return CFG.BIOME.plain; }
  return b;
}
function blurField(src, dst, w, h, r) {
  const tmp = new Float32Array(w * h); const n = 2 * r + 1;
  for (let y = 0; y < h; y++) { let s = 0; const row = y * w; for (let x = -r; x <= r; x++) s += src[row + Math.min(w - 1, Math.max(0, x))]; for (let x = 0; x < w; x++) { tmp[row + x] = s / n; s += src[row + Math.min(w - 1, x + r + 1)] - src[row + Math.max(0, x - r)]; } }
  for (let x = 0; x < w; x++) { let s = 0; for (let y = -r; y <= r; y++) s += tmp[Math.min(h - 1, Math.max(0, y)) * w + x]; for (let y = 0; y < h; y++) { dst[y * w + x] = s / n; s += tmp[Math.min(h - 1, y + r + 1) * w + x] - tmp[Math.max(0, y - r) * w + x]; } }
}

const TX = 256, TL = 4;
const TPX = TX * TL;
const M = 10, mpx = M * TL, W = TPX + mpx * 2;
const land = new Float32Array(W * W);
const f1 = new Float32Array(W * W);
const f2 = new Float32Array(W * W);
const data = new Uint8ClampedArray(TPX * TPX * 4);
const biomePx = new Uint8Array(TPX * TPX);
// biome histogram per tile so texture pass touches only biomes actually present
const texNeed = new Uint8Array(9);
function paintTile(cx, cy) {
  const o = CFG, x0 = cx * TX, y0 = cy * TX;
  data.fill(0); biomePx.fill(0); texNeed.fill(0);
  const PAL = o.PAL, B = o.BIOME;
  // pass 1: biome, color and land field in one sweep (margins filled from the global field)
  for (let py = 0; py < W; py++) {
    const wy = y0 + (py - mpx) * 0.25 + 0.125;
    const wxs = x0 - mpx * 0.25 + 0.125;
    for (let px = 0; px < W; px++) {
      const wx = wxs + px * 0.25;
      const inner = (px >= mpx && px < mpx + TPX && py >= mpx && py < mpx + TPX);
      const b = inner ? biomeAt(wx, wy) : (() => { const ix = Math.min(CFG.MAP_W - 1, Math.max(0, wx | 0)), iy = Math.min(CFG.MAP_H - 1, Math.max(0, wy | 0)); return P.fine[(iy / CFG.FINE | 0) * CFG.FW + (ix / CFG.FINE | 0)]; })();
      const water = b === B.water || b === B.deep;
      const li = py * W + px;
      land[li] = water ? 0 : 1;
      if (!inner) continue;
      const pi = (py - mpx) * TPX + (px - mpx);
      biomePx[pi] = b; texNeed[b] = 1;
      const pal = PAL[b];
      let m = 1;
      if (b === B.mountain) { const rn = 1 - Math.abs(2 * vnoise(wx / 9, wy / 9) - 1); m = 0.7 + 0.55 * rn; if (rn > 0.88) m *= 1.35; }
      else if (water) m = 0.9 + 0.22 * vnoise(wx / 34, wy / 5);
      else if (b === B.hill) m = 0.88 + 0.26 * _tex;
      else if (b === B.forest) m = 0.86 + 0.28 * _tex;
      else if (b === B.desert) m = 0.92 + 0.16 * _tex;
      else m = 0.94 + 0.12 * _tex;
      if (!water) m *= 1 - Math.min(0.16, _scar);
      const kx = Math.floor(wx), ky = Math.floor(wy);
      let h = (kx * 374761393 + ky * 668265263) | 0; h = Math.imul(h ^ (h >>> 13), 1274126177); h ^= h >>> 16;
      const grain = ((h & 255) / 255 - 0.5) * 16 + _mot * 20;
      const j = pi * 4;
      data[j] = pal[0] * m + grain; data[j + 1] = pal[1] * m + grain; data[j + 2] = pal[2] * m + grain; data[j + 3] = 255;
    }
  }
  // pass 2: foam / dark shore via the blurred land field
  {
    const f1 = new Float32Array(W * W), f2 = new Float32Array(W * W);
    blurField(land, f1, W, W, 28); blurField(f1, f2, W, W, 28);
    const foam = o.FOAM, dark = o.DARK, FAR = o.WATER_FAR;
    for (let py = 0; py < TPX; py++) for (let px = 0; px < TPX; px++) {
      const f = f2[(py + mpx) * W + (px + mpx)];
      const j = (py * TPX + px) * 4;
      const b = biomePx[py * TPX + px];
      let a = 0, col = foam;
      if (b === B.water || b === B.deep) {
        const far = Math.min(1, (0.5 - f) * 2.2);
        if (b === B.water) { for (let k = 0; k < 3; k++) data[j + k] += (FAR[k] - PAL[7][k]) * far; }
        if (f > 0.44) a = 0.7; else if (f > 0.285 && f < 0.31) a = 0.22; else if (f > 0.165 && f < 0.18) a = 0.14;
      } else if (f < 0.58) { a = 0.35 * (0.58 - f) / 0.08; col = dark; }
      if (a > 0) for (let k = 0; k < 3; k++) data[j + k] += (col[k] - data[j + k]) * a;
    }
  }
  // pass 3: biome texture multiply, repeat every 256 km (same as the base bake)
  for (const code in TEX) {
    const td = TEX[code], cnum = +code; if (!texNeed[cnum]) continue;
    for (let py = 0; py < TPX; py++) {
      const ty = Math.floor(y0 + py * 0.25 + 0.125); const iy = ((ty % 256) + 256) % 256;
      const row = py * TPX;
      for (let px = 0; px < TPX; px++) {
        if (biomePx[row + px] !== cnum) continue;
        const tx = Math.floor(x0 + px * 0.25 + 0.125); const ix = ((tx % 256) + 256) % 256;
        const ti = (iy * 256 + ix) * 4, j = (row + px) * 4;
        data[j] *= 0.58 + 0.42 * td[ti] / 255;
        data[j + 1] *= 0.58 + 0.42 * td[ti + 1] / 255;
        data[j + 2] *= 0.58 + 0.42 * td[ti + 2] / 255;
      }
    }
  }
  const img = new ImageData(data, TPX, TPX);
  const oc = new OffscreenCanvas(TPX, TPX); oc.getContext('2d').putImageData(img, 0, 0);
  return oc.transferToImageBitmap();
}
self.onmessage = (e) => {
  const m = e.data;
  if (m.type === 'init') { CFG = m.cfg; P.fine = m.fine; P.edge = m.edge; P.land = m.land; P.lat = m.lat; if (m.perm) PERM = m.perm; if (m.textures) for (const t of m.textures) TEX[t.code] = t.data; self.postMessage({ type: 'ready' }); return; }
  if (m.type === 'tex') { TEX[m.code] = m.data; return; }
  if (m.type === 'tile') { try { const bmp = paintTile(m.cx, m.cy); self.postMessage({ type: 'tile', id: m.id, cx: m.cx, cy: m.cy, bmp }, [bmp]); } catch (err) { self.postMessage({ type: 'tile', id: m.id, cx: m.cx, cy: m.cy, err: String(err) }); } }
};