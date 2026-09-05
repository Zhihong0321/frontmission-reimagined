// Extracted verbatim (Phase D CP-D2) from web/chart/chart.js at integration 67b6fb5.
'use strict';
function drawPois() {
  const tl = toWorld(0, 0), br = toWorld(VW, VH);
  for (const p of POIS) {
    if (p.x < tl.x - 40 || p.x > br.x + 40 || p.y < tl.y - 40 || p.y > br.y + 40) continue;
    ctx.save(); ctx.translate(p.x, p.y); ctx.rotate(p.rot); ctx.drawImage(p.sp.img, -p.size / 2, -p.size / 2, p.size, p.size); ctx.restore();
  }
}
function paintRows(img, r0, r1) {
  const d = img.data;
  for (let y = r0; y < r1; y++) for (let x = 0; x < MAP_W; x++) {
    const i = y * MAP_W + x;
    const b = biomeAt(x + 0.5, y + 0.5);
    biomePx[i] = b;
    const water = b === BIOME.water || b === BIOME.deep;
    landField[i] = water ? 0 : 1;
    const pal = PAL[b];
    let m = 1;
    if (b === BIOME.mountain) { const rn = 1 - Math.abs(2 * vnoise(x / 9, y / 9) - 1); m = 0.7 + 0.55 * rn; if (rn > 0.88) m *= 1.35; }
    else if (water) m = 0.9 + 0.22 * vnoise(x / 34, y / 5);        // oil-slick streaks
    else if (b === BIOME.hill) m = 0.88 + 0.26 * _tex;
    else if (b === BIOME.forest) m = 0.86 + 0.28 * _tex;
    else if (b === BIOME.desert) m = 0.92 + 0.16 * _tex;
    else m = 0.94 + 0.12 * _tex;
    if (!water) m *= 1 - Math.min(0.16, _scar);                      // burn scars
    let h = (x * 374761393 + y * 668265263) | 0; h = Math.imul(h ^ (h >>> 13), 1274126177); h ^= h >>> 16;
    const grain = ((h & 255) / 255 - 0.5) * 16 + _mot * 20;
    d[i * 4] = pal[0] * m + grain; d[i * 4 + 1] = pal[1] * m + grain; d[i * 4 + 2] = pal[2] * m + grain; d[i * 4 + 3] = 255;
  }
}
function boxBlur(src, dst, w, h, r) {
  const tmp = new Float32Array(w * h); const n = 2 * r + 1;
  for (let y = 0; y < h; y++) { let s = 0; const row = y * w; for (let x = -r; x <= r; x++) s += src[row + Math.min(w - 1, Math.max(0, x))]; for (let x = 0; x < w; x++) { tmp[row + x] = s / n; s += src[row + Math.min(w - 1, x + r + 1)] - src[row + Math.max(0, x - r)]; } }
  for (let x = 0; x < w; x++) { let s = 0; for (let y = -r; y <= r; y++) s += tmp[Math.min(h - 1, Math.max(0, y)) * w + x]; for (let y = 0; y < h; y++) { dst[y * w + x] = s / n; s += tmp[Math.min(h - 1, y + r + 1) * w + x] - tmp[Math.max(0, y - r) * w + x]; } }
}
function finishBase(img) {
  const f1 = new Float32Array(MAP_W * MAP_H), f2 = new Float32Array(MAP_W * MAP_H);
  boxBlur(landField, f1, MAP_W, MAP_H, 7); boxBlur(f1, f2, MAP_W, MAP_H, 7);
  const d = img.data; const foam = [150, 172, 186], dark = [8, 12, 18];
  for (let i = 0; i < MAP_W * MAP_H; i++) {
    const f = f2[i], b = biomePx[i];
    let a = 0, col = foam;
    if (b === BIOME.water || b === BIOME.deep) {
      const far = Math.min(1, (0.5 - f) * 2.2);
      if (b === BIOME.water) { for (let k = 0; k < 3; k++) d[i * 4 + k] += (WATER_FAR[k] - PAL[7][k]) * far; }
      if (f > 0.44) a = 0.7; else if (f > 0.285 && f < 0.31) a = 0.22; else if (f > 0.165 && f < 0.18) a = 0.14;
    } else if (f < 0.58) { a = 0.35 * (0.58 - f) / 0.08; col = dark; }
    if (a > 0) for (let k = 0; k < 3; k++) d[i * 4 + k] += (col[k] - d[i * 4 + k]) * a;
  }
  base.getContext('2d').putImageData(img, 0, 0);
  // optional ground textures multiply over each biome
  const bctx = base.getContext('2d');
  for (const [name, code] of Object.entries(BIOME)) {
    const img2 = ART['tex-' + name]; if (!img2) continue;
    const mask = document.createElement('canvas'); mask.width = MAP_W; mask.height = MAP_H; const mctx = mask.getContext('2d');
    const md = mctx.createImageData(MAP_W, MAP_H); for (let i = 0; i < MAP_W * MAP_H; i++) if (biomePx[i] === code) md.data[i * 4 + 3] = 255;
    // repeat the tile every 256 km so its grain reads as surface, not as a large symmetric pattern
    const small = document.createElement('canvas'); small.width = 256; small.height = 256; small.getContext('2d').drawImage(img2, 0, 0, 256, 256);
    mctx.putImageData(md, 0, 0); mctx.globalCompositeOperation = 'source-in'; mctx.fillStyle = mctx.createPattern(small, 'repeat'); mctx.fillRect(0, 0, MAP_W, MAP_H);
    bctx.globalCompositeOperation = 'multiply'; bctx.globalAlpha = 0.42; bctx.drawImage(mask, 0, 0); bctx.globalAlpha = 1; bctx.globalCompositeOperation = 'source-over';
  }
  const cimg = costCanvas.getContext('2d').createImageData(MAP_W, MAP_H);
  for (let i = 0; i < MAP_W * MAP_H; i++) {
    const sm = offroadMult(biomePx[i]); const t = 1 - sm;
    cimg.data[i * 4] = 210 + 30 * t; cimg.data[i * 4 + 1] = 190 - 150 * t; cimg.data[i * 4 + 2] = 50; cimg.data[i * 4 + 3] = sm === 0 ? 150 : 40 + 110 * t;
  }
  costCanvas.getContext('2d').putImageData(cimg, 0, 0);
}
const INK_D = 'rgba(14,16,18,'; const INK_L = 'rgba(190,192,186,';
function drawGlyph(ctx, g, lw) {
  const { x, y, s, v } = g; ctx.lineWidth = lw;
  if (g.sp) { // generated sprite: footprint in km, random heading unless it must follow the wind
    const size = g.sp.fp * (0.8 + s * 0.35);
    ctx.save(); ctx.translate(x, y); ctx.rotate(heading(g.sp, v)); ctx.drawImage(g.sp.img, -size / 2, -size / 2, size, size); ctx.restore();
    return;
  }
  switch (g.t) {
    case BIOME.hill: // hachure contour
      ctx.strokeStyle = INK_L + '0.35)'; ctx.beginPath(); ctx.moveTo(x - 6 * s, y + 1 * s); ctx.quadraticCurveTo(x, y - 5 * s, x + 6 * s, y + 1 * s); ctx.stroke();
      ctx.strokeStyle = INK_D + '0.35)'; ctx.beginPath(); ctx.moveTo(x - 4 * s, y + 3 * s); ctx.quadraticCurveTo(x, y - 1 * s, x + 4 * s, y + 3 * s); ctx.stroke(); break;
    case BIOME.mountain: // angular ridge with snow
      ctx.strokeStyle = INK_D + '0.85)'; ctx.beginPath(); ctx.moveTo(x - 8 * s, y); ctx.lineTo(x - 3 * s, y - 6 * s); ctx.lineTo(x - 1 * s, y - 11 * s); ctx.lineTo(x + 2 * s, y - 6 * s); ctx.lineTo(x + 8 * s, y); ctx.stroke();
      ctx.fillStyle = INK_D + '0.35)'; ctx.beginPath(); ctx.moveTo(x - 1 * s, y - 11 * s); ctx.lineTo(x + 2 * s, y - 6 * s); ctx.lineTo(x + 8 * s, y); ctx.lineTo(x - 1 * s, y); ctx.closePath(); ctx.fill();
      ctx.strokeStyle = 'rgba(225,228,230,0.8)'; ctx.beginPath(); ctx.moveTo(x - 2.5 * s, y - 8 * s); ctx.lineTo(x - 1 * s, y - 11 * s); ctx.lineTo(x + 0.6 * s, y - 8.4 * s); ctx.stroke(); break;
    case BIOME.forest: // dead tree or a black conifer
      ctx.strokeStyle = INK_D + '0.75)';
      if (v < 0.65) { ctx.beginPath(); ctx.moveTo(x, y + 2 * s); ctx.lineTo(x, y - 4 * s); ctx.moveTo(x, y - 2 * s); ctx.lineTo(x - 2.5 * s, y - 5 * s); ctx.moveTo(x, y - 3 * s); ctx.lineTo(x + 2.2 * s, y - 6 * s); ctx.moveTo(x, y - 4 * s); ctx.lineTo(x - 1 * s, y - 7 * s); ctx.stroke(); }
      else { ctx.fillStyle = INK_D + '0.65)'; ctx.beginPath(); ctx.moveTo(x - 3 * s, y); ctx.lineTo(x, y - 8 * s); ctx.lineTo(x + 3 * s, y); ctx.closePath(); ctx.fill(); ctx.beginPath(); ctx.moveTo(x, y); ctx.lineTo(x, y + 2 * s); ctx.stroke(); }
      break;
    case BIOME.swamp: // reeds and bubbles
      ctx.strokeStyle = INK_D + '0.45)'; ctx.beginPath(); ctx.moveTo(x - 5 * s, y); ctx.lineTo(x + 5 * s, y); ctx.moveTo(x - 3 * s, y - 2.5 * s); ctx.lineTo(x + 2 * s, y - 2.5 * s); ctx.moveTo(x, y - 2.5 * s); ctx.lineTo(x, y - 6 * s); ctx.stroke();
      ctx.strokeStyle = 'rgba(160,200,120,0.5)'; ctx.beginPath(); ctx.arc(x + 4 * s, y - 4 * s, 1.2 * s, 0, 7); ctx.moveTo(x + 6.5 * s, y - 2 * s); ctx.arc(x + 6 * s, y - 2 * s, 0.7 * s, 0, 7); ctx.stroke(); break;
    case BIOME.desert: case BIOME.tundra: // ground cracks
      ctx.strokeStyle = INK_D + (g.t === BIOME.desert ? '0.4)' : '0.3)'); ctx.beginPath(); ctx.moveTo(x - 6 * s, y - 1 * s); ctx.lineTo(x - 2 * s, y + 1 * s); ctx.lineTo(x + 1 * s, y - 2 * s); ctx.lineTo(x + 6 * s, y); ctx.moveTo(x - 2 * s, y + 1 * s); ctx.lineTo(x - 1 * s, y + 5 * s); ctx.stroke(); break;
    case BIOME.water: // wreck at sea
      ctx.strokeStyle = INK_L + '0.35)'; ctx.beginPath(); ctx.moveTo(x - 4 * s, y); ctx.lineTo(x + 4 * s, y); ctx.moveTo(x, y); ctx.lineTo(x, y - 4 * s); ctx.stroke(); break;
    default: // plain: wreck, crater or a pylon
      if (v < 0.4) { ctx.fillStyle = INK_D + '0.7)'; ctx.fillRect(x - 3 * s, y - 1.5 * s, 6 * s, 3 * s); ctx.strokeStyle = INK_L + '0.35)'; ctx.strokeRect(x - 3 * s, y - 1.5 * s, 6 * s, 3 * s); }
      else if (v < 0.75) { ctx.strokeStyle = INK_D + '0.45)'; ctx.beginPath(); ctx.arc(x, y, 4 * s, 0, 7); ctx.stroke(); ctx.strokeStyle = INK_L + '0.3)'; ctx.beginPath(); ctx.arc(x, y, 2.2 * s, 0, 7); ctx.stroke(); }
      else { ctx.strokeStyle = INK_D + '0.8)'; ctx.beginPath(); ctx.moveTo(x - 3 * s, y + 2 * s); ctx.lineTo(x, y - 8 * s); ctx.lineTo(x + 3 * s, y + 2 * s); ctx.moveTo(x - 4 * s, y - 4 * s); ctx.lineTo(x + 4 * s, y - 4 * s); ctx.stroke(); }
  }
}
function bakeInk() { const ctx = inkBaked.getContext('2d'); ctx.lineCap = 'round'; ctx.lineJoin = 'round'; for (const g of glyphs) drawGlyph(ctx, g, 1.1); }
const smoke = [];
function emitSmoke(dt) {
  for (const c of CITIES) { if (c.pop < 1.1) continue; if (Math.random() < dt * 2.5) { const r = mulberry(c.h + (Math.random() * 1e6 | 0))(); smoke.push({ x: c.x + (r - 0.5) * 14, y: c.y + (Math.random() - 0.5) * 14, r: 2, a: 0.28, life: 0 }); } }
  for (let i = smoke.length - 1; i >= 0; i--) { const s = smoke[i]; s.life += dt; s.x += 9 * dt; s.y -= 5 * dt; s.r += 5 * dt; s.a -= dt * 0.045; if (s.a <= 0) smoke.splice(i, 1); }
  if (smoke.length > 400) smoke.splice(0, smoke.length - 400);
}
function resize() { DPR = Math.min(2, window.devicePixelRatio || 1); VW = innerWidth; VH = innerHeight; cv.width = VW * DPR; cv.height = VH * DPR; }
const toScreen = (x, y) => ({ sx: (x - cam.x) * cam.z + VW / 2, sy: (y - cam.y) * cam.z + VH / 2 });
const toWorld = (sx, sy) => ({ x: (sx - VW / 2) / cam.z + cam.x, y: (sy - VH / 2) / cam.z + cam.y });
function tracePath(e) { ctx.beginPath(); ctx.moveTo(e.pts[0].x, e.pts[0].y); for (let k = 1; k < e.pts.length; k++) ctx.lineTo(e.pts[k].x, e.pts[k].y); }
function drawRoads() {
  const z = cam.z; ctx.lineCap = 'round'; ctx.lineJoin = 'round';
  const w = Math.max(5, 3.6 / z);                                  // tarmac width in km, never under ~3.6 px
  // ferries first: a marked lane over the water, piers at both ends
  for (const e of EDGES) if (e.terrain === 'strait') {
    tracePath(e); ctx.lineWidth = w * 1.6; ctx.strokeStyle = 'rgba(120,150,150,0.18)'; ctx.stroke();
    tracePath(e); ctx.lineWidth = Math.max(1.2, 1.4 / z); ctx.strokeStyle = 'rgba(200,215,215,0.75)'; ctx.setLineDash([Math.max(3, 3 / z), Math.max(5, 5 / z)]); ctx.stroke(); ctx.setLineDash([]);
    for (const p of [e.pts[0], e.pts[e.pts.length - 1]]) { ctx.fillStyle = 'rgba(40,42,44,0.95)'; ctx.fillRect(p.x - 4, p.y - 2, 8, 4); }
  }
  // shoulders, then tarmac with light edges, then the centre line
  for (const e of EDGES) if (e.terrain !== 'strait') { tracePath(e); ctx.lineWidth = w + Math.max(3, 3 / z); ctx.strokeStyle = e.terrain === 'coastal' ? 'rgba(150,165,160,0.45)' : 'rgba(138,148,160,0.42)'; ctx.stroke(); }
  for (const e of EDGES) if (e.terrain !== 'strait') {
    const narrow = e.terrain === 'alpine' ? 0.72 : 1;
    tracePath(e); ctx.lineWidth = w * narrow; ctx.strokeStyle = 'rgba(190,200,210,0.85)'; ctx.stroke();               // edge lines
    tracePath(e); ctx.lineWidth = w * narrow - Math.max(1.2, 1.3 / z); ctx.strokeStyle = e.terrain === 'hills' || e.terrain === 'alpine' ? '#3c4249' : '#31373e'; ctx.stroke();
  }
  if (z > 0.7) for (const e of EDGES) if (e.terrain !== 'strait') {
    tracePath(e); ctx.lineWidth = Math.max(0.5, 0.7 / z); ctx.strokeStyle = 'rgba(215,180,80,0.6)'; ctx.setLineDash([Math.max(5, 6 / z), Math.max(4, 6 / z)]); ctx.stroke(); ctx.setLineDash([]);
    if (e.terrain === 'alpine') { ctx.strokeStyle = 'rgba(14,16,18,0.7)'; ctx.lineWidth = 1 / z; for (let k = 3; k < e.pts.length - 3; k += 4) { const p = e.pts[k], q = e.pts[k + 1]; const dx = q.x - p.x, dy = q.y - p.y, l = Math.hypot(dx, dy); const nx = -dy / l * (w + 3) / 2, ny = dx / l * (w + 3) / 2; ctx.beginPath(); ctx.moveTo(p.x + nx, p.y + ny); ctx.lineTo(p.x + nx * 1.6, p.y + ny * 1.6); ctx.moveTo(p.x - nx, p.y - ny); ctx.lineTo(p.x - nx * 1.6, p.y - ny * 1.6); ctx.stroke(); } }
    // breaches: rubble on the tarmac
    for (const f of e.damage) { const p = pointAlong(e, f * e.visKm); const r = mulberry(e.i * 31 + (f * 1000 | 0)); for (let k = 0; k < 9; k++) { const a = r() * 6.28, d = r() * w * 0.6; ctx.fillStyle = k % 3 ? 'rgba(146,156,166,0.9)' : 'rgba(20,22,24,0.9)'; ctx.fillRect(p.x + Math.cos(a) * d, p.y + Math.sin(a) * d, 1.2 + r() * 1.4, 1.2 + r() * 1.4); } }
  }
  // pylons walk beside the road, wires between them
  if (z > 1.0) for (const e of EDGES) if (e.terrain === 'plain' || e.terrain === 'hills' || e.terrain === 'coastal') {
    let prev = null; ctx.lineWidth = 0.7 / z;
    for (let s = 20; s < e.visKm - 10; s += 42) {
      const p = pointAlong(e, s); const nx = p.nx / p.len, ny = p.ny / p.len; const px = p.x + nx * (w / 2 + 7), py = p.y + ny * (w / 2 + 7);
      if (prev) { ctx.strokeStyle = 'rgba(14,16,18,0.55)'; ctx.beginPath(); ctx.moveTo(prev.x, prev.y); ctx.lineTo(px, py); ctx.stroke(); }
      ctx.strokeStyle = 'rgba(14,16,18,0.9)'; ctx.lineWidth = 1 / z; ctx.beginPath(); ctx.moveTo(px - 2.5, py + 2); ctx.lineTo(px, py - 6); ctx.lineTo(px + 2.5, py + 2); ctx.moveTo(px - 3.5, py - 3); ctx.lineTo(px + 3.5, py - 3); ctx.stroke(); ctx.lineWidth = 0.7 / z;
      prev = { x: px, y: py };
    }
  }
}
function drawPath(legs, seed, color, dash, width) {
  const z = cam.z; ctx.lineCap = 'round'; ctx.lineJoin = 'round'; ctx.strokeStyle = color; ctx.lineWidth = width / z; ctx.setLineDash(dash.map(d => d / z));
  ctx.beginPath(); ctx.moveTo(conv.x, conv.y);
  if (seed && seed.edge) { const e = seed.edge; for (let k = 1; k <= 20; k++) { const d = conv.dist + (seed.dir > 0 ? (e.gameKm - conv.dist) : -conv.dist) * k / 20; const p = edgePoint(e, d, 1); ctx.lineTo(p.x, p.y); } }
  else if (seed && seed.free) { const c = CITY_BY_ID[seed.node]; ctx.lineTo(c.x, c.y); }
  for (const l of legs) { const pts = l.dir > 0 ? l.edge.pts : l.edge.pts.slice().reverse(); for (const p of pts) ctx.lineTo(p.x, p.y); }
  ctx.stroke(); ctx.setLineDash([]);
}
function drawClaims(t) {
  const z = cam.z;
  for (const c of CLAIMS) {
    ctx.save(); ctx.beginPath(); for (let k = 0; k < c.pts.length; k++) { const p = c.pts[k]; k ? ctx.lineTo(p.x, p.y) : ctx.moveTo(p.x, p.y); } ctx.closePath();
    ctx.fillStyle = 'rgba(20,18,20,0.38)'; ctx.fill(); ctx.clip();
    ctx.strokeStyle = 'rgba(200,70,50,0.22)'; ctx.lineWidth = 1.2 / z; ctx.beginPath();
    for (let d = -400; d < 400; d += 10) { ctx.moveTo(c.cx - 200 + d, c.cy - 200); ctx.lineTo(c.cx - 200 + d + 400, c.cy + 200); } ctx.stroke();
    const r = mulberry(c.h);
    for (let k = 0; k < 5; k++) { const ox = (r() - 0.5) * 120, oy = (r() - 0.5) * 80, sp = 0.15 + r() * 0.2, ph = r() * 6.28; const mx = c.cx + ox + Math.sin(t * sp + ph) * 30, my = c.cy + oy + Math.cos(t * sp * 0.7 + ph) * 20; const g = ctx.createRadialGradient(mx, my, 0, mx, my, 70); g.addColorStop(0, 'rgba(150,150,154,0.35)'); g.addColorStop(1, 'rgba(150,150,154,0)'); ctx.fillStyle = g; ctx.fillRect(mx - 70, my - 70, 140, 140); }
    ctx.restore();
    ctx.beginPath(); for (let k = 0; k < c.pts.length; k++) { const p = c.pts[k]; k ? ctx.lineTo(p.x, p.y) : ctx.moveTo(p.x, p.y); } ctx.closePath();
    ctx.lineWidth = Math.max(2, 2.5 / z); ctx.strokeStyle = 'rgba(20,20,22,0.9)'; ctx.stroke();
    ctx.setLineDash([Math.max(6, 8 / z), Math.max(6, 8 / z)]); ctx.strokeStyle = 'rgba(224,160,48,0.9)'; ctx.stroke(); ctx.setLineDash([]);
  }
}
function drawMist(t) {
  for (const m of MIST) { const mx = ((m.x + m.vx * t) % (MAP_W + 600)) - 300, my = m.y + Math.sin(t * 0.05 + m.x) * 40; const g = ctx.createRadialGradient(mx, my, 0, mx, my, m.r); g.addColorStop(0, `rgba(158,172,186,${m.a})`); g.addColorStop(1, 'rgba(158,172,186,0)'); ctx.fillStyle = g; ctx.fillRect(mx - m.r, my - m.r, m.r * 2, m.r * 2); }
}
function drawCells() {
  const z = cam.z; const tl = toWorld(0, 0), br = toWorld(VW, VH);
  ctx.lineWidth = 1 / z; ctx.strokeStyle = z > 0.9 ? 'rgba(230,232,230,0.07)' : 'rgba(230,232,230,0.04)'; ctx.beginPath();
  const step = z > 0.9 ? CELL : CELL * 5;
  for (let x = Math.max(0, Math.floor(tl.x / step) * step); x <= Math.min(MAP_W, br.x); x += step) { ctx.moveTo(x, Math.max(0, tl.y)); ctx.lineTo(x, Math.min(MAP_H, br.y)); }
  for (let y = Math.max(0, Math.floor(tl.y / step) * step); y <= Math.min(MAP_H, br.y); y += step) { ctx.moveTo(Math.max(0, tl.x), y); ctx.lineTo(Math.min(MAP_W, br.x), y); }
  ctx.stroke();
}
function drawCities(t) {
  const z = cam.z;
  for (const c of CITIES) {
    const R = 12 + c.pop * 6; const r = mulberry(c.h);
    // ring road and ruined blocks
    ctx.beginPath(); ctx.arc(c.x, c.y, R, 0, 7); ctx.lineWidth = Math.max(1.6, 1.8 / z); ctx.strokeStyle = 'rgba(58,59,61,0.95)'; ctx.stroke();
    ctx.lineWidth = Math.max(0.6, 0.6 / z); ctx.strokeStyle = 'rgba(205,205,198,0.5)'; ctx.stroke();
    const n = RUIN_POOL.length ? 4 + c.pop * 4 : 10 + c.pop * 10;   // fewer blocks once ruin sprites fill the ring
    for (let k = 0; k < n; k++) { const a = r() * 6.28, d = 3 + r() * (R - 5), w = 1.5 + r() * 3.5, h = 1.5 + r() * 3.5; const x = c.x + Math.cos(a) * d - w / 2, y = c.y + Math.sin(a) * d - h / 2; ctx.fillStyle = k % 4 === 0 ? 'rgba(146,156,166,0.9)' : 'rgba(18,20,22,0.92)'; ctx.fillRect(x, y, w, h); if (z > 1.2) { ctx.strokeStyle = 'rgba(205,205,198,0.35)'; ctx.lineWidth = 0.5 / z; ctx.strokeRect(x, y, w, h); } }
    const isHere = conv.node === c.id, isHover = hover === c.id, isTarget = conv.target === c.id || (pending && pending.cityId === c.id);
    if (isHover || isTarget) { ctx.beginPath(); ctx.arc(c.x, c.y, R + 6 / z, 0, 7); ctx.strokeStyle = 'rgba(224,160,48,0.9)'; ctx.lineWidth = 2 / z; ctx.setLineDash([4 / z, 3 / z]); ctx.stroke(); ctx.setLineDash([]); }
    // beacon
    const blink = 0.55 + 0.45 * Math.sin(t * 2.2 + c.h); const br = Math.max(2.2, 3 / z);
    ctx.beginPath(); ctx.arc(c.x, c.y, br * 2.2, 0, 7); ctx.fillStyle = `rgba(224,160,48,${0.18 * blink})`; ctx.fill();
    ctx.beginPath(); ctx.arc(c.x, c.y, br, 0, 7); ctx.fillStyle = isHere ? '#ffd070' : `rgba(224,160,48,${0.5 + 0.5 * blink})`; ctx.fill();
    if (isHere && pulse > 0) { ctx.beginPath(); ctx.arc(c.x, c.y, R + (1 - pulse) * 40 / z, 0, 7); ctx.strokeStyle = `rgba(224,160,48,${pulse * 0.8})`; ctx.lineWidth = 2 / z; ctx.stroke(); }
    // sell outlook: radar pings and a breathing halo on cities that would clear the threshold
    const sell = conv.sellOutlook && conv.sellOutlook.get(c.id);
    if (sell >= SELL_PROFIT_MIN && !isHere) {
      for (let k = 0; k < 2; k++) {
        const frac = (t * 0.55 + c.h * 0.3 + k * 0.5) % 1;
        ctx.beginPath(); ctx.arc(c.x, c.y, (R + 4 / z) * (1 + frac), 0, 7);
        ctx.strokeStyle = `rgba(120,214,150,${(1 - frac) * 0.6})`;
        ctx.lineWidth = Math.max(1.2, 1.8 / z);
        ctx.stroke();
      }
      const glow = 0.5 + 0.5 * Math.sin(t * 3 + c.h);
      const g = ctx.createRadialGradient(c.x, c.y, R * 0.5, c.x, c.y, R + 18 / z);
      g.addColorStop(0, `rgba(120,214,150,${0.10 + glow * 0.12})`);
      g.addColorStop(1, 'rgba(120,214,150,0)');
      ctx.fillStyle = g;
      ctx.beginPath(); ctx.arc(c.x, c.y, R + 18 / z, 0, 7); ctx.fill();
    }
  }
  for (const s of smoke) { ctx.beginPath(); ctx.arc(s.x, s.y, s.r, 0, 7); ctx.fillStyle = `rgba(40,40,42,${s.a})`; ctx.fill(); }
}
function drawTrail() {
  if (conv.trail.length < 2) return; const z = cam.z;
  ctx.lineCap = 'round'; ctx.lineWidth = 2.2 / z; ctx.setLineDash([1 / z, 5 / z]);
  const n = conv.trail.length;
  for (let k = 1; k < n; k++) { ctx.strokeStyle = `rgba(230,200,120,${0.12 + 0.5 * (k / n)})`; ctx.beginPath(); ctx.moveTo(conv.trail[k - 1].x, conv.trail[k - 1].y); ctx.lineTo(conv.trail[k].x, conv.trail[k].y); ctx.stroke(); }
  if (!conv.node) { ctx.strokeStyle = 'rgba(230,200,120,0.65)'; ctx.beginPath(); ctx.moveTo(conv.trail[n - 1].x, conv.trail[n - 1].y); ctx.lineTo(conv.x, conv.y); ctx.stroke(); }
  ctx.setLineDash([]);
}
function drawConvoy(t) {
  const { sx, sy } = toScreen(conv.x, conv.y); const s = Math.min(2.4, Math.max(1.3, Math.sqrt(cam.z) * 1.6));
  ctx.save(); ctx.setTransform(DPR, 0, 0, DPR, 0, 0); ctx.translate(sx, sy);
  if (!conv.node) {
    const g = ctx.createRadialGradient(0, 0, 4 * s, 0, 0, 24 * s); g.addColorStop(0, 'rgba(224,160,48,0.28)'); g.addColorStop(1, 'rgba(224,160,48,0)'); ctx.fillStyle = g; ctx.fillRect(-24 * s, -24 * s, 48 * s, 48 * s);
    if (conv.target) { const frac = conv.tripKm > 0 ? Math.min(1, (conv.km - conv.tripStartKm) / conv.tripKm) : 0; ctx.beginPath(); ctx.arc(0, 0, 15 * s, 0, 7); ctx.strokeStyle = 'rgba(14,16,18,0.35)'; ctx.lineWidth = 2; ctx.stroke(); ctx.beginPath(); ctx.arc(0, 0, 15 * s, -Math.PI / 2, -Math.PI / 2 + frac * Math.PI * 2); ctx.strokeStyle = 'rgba(224,160,48,0.95)'; ctx.lineWidth = 2.5; ctx.stroke(); }
  } else { ctx.beginPath(); ctx.arc(0, 0, 14 * s + Math.sin(t * 3) * 2, 0, 7); ctx.strokeStyle = 'rgba(224,160,48,0.8)'; ctx.lineWidth = 1.5; ctx.setLineDash([3, 4]); ctx.stroke(); ctx.setLineDash([]); }
  ctx.rotate(conv.ang); ctx.scale(s, s);
  // headlights
  const hl = ctx.createLinearGradient(9, 0, 30, 0); hl.addColorStop(0, 'rgba(255,240,190,0.28)'); hl.addColorStop(1, 'rgba(255,240,190,0)'); ctx.fillStyle = hl; ctx.beginPath(); ctx.moveTo(9, -3); ctx.lineTo(30, -9); ctx.lineTo(30, 9); ctx.lineTo(9, 3); ctx.closePath(); ctx.fill();
  const bob = conv.moving && !conv.paused ? Math.sin(t * 40) * 0.4 : 0; ctx.translate(0, bob);
  if (ART.truck) { const im = ART.truck; const w = 26, h = 26 * im.height / im.width; ctx.drawImage(im, -w / 2, -h / 2, w, h); }
  else {
    ctx.fillStyle = 'rgba(0,0,0,0.4)'; ctx.fillRect(-9, -2, 18, 9);
    ctx.fillStyle = '#0e0f11'; ctx.fillRect(-8, -5.5, 4, 2); ctx.fillRect(-8, 3.5, 4, 2); ctx.fillRect(4, -5.5, 4, 2); ctx.fillRect(4, 3.5, 4, 2);
    ctx.fillStyle = '#4a5058'; ctx.fillRect(-9, -4, 12, 8); ctx.strokeStyle = '#0b0c0e'; ctx.lineWidth = 0.8; ctx.strokeRect(-9, -4, 12, 8);
    ctx.fillStyle = '#6d6a5a'; ctx.fillRect(-8, -3, 10, 6); ctx.strokeStyle = 'rgba(0,0,0,0.5)'; ctx.beginPath(); for (let k = -6; k <= 0; k += 3) { ctx.moveTo(k, -3); ctx.lineTo(k, 3); } ctx.stroke();
    ctx.fillStyle = '#b89a3c'; ctx.fillRect(3, -3.5, 6, 7); ctx.strokeRect(3, -3.5, 6, 7);
    ctx.fillStyle = '#1c2a36'; ctx.fillRect(6.5, -2.8, 2, 5.6);
    ctx.fillStyle = (Math.sin(t * 8) > 0) ? '#ffb040' : '#8a5a10'; ctx.fillRect(4, -1, 1.5, 2); // roof beacon
    ctx.fillStyle = '#fff2b0'; ctx.fillRect(9, -3, 1, 1.5); ctx.fillRect(9, 1.5, 1, 1.5);
  }
  ctx.restore();
}
function drawLabels(t) {
  ctx.save(); ctx.setTransform(DPR, 0, 0, DPR, 0, 0);
  const z = cam.z;
  if (z < 1.8) {
    ctx.font = '600 16px ' + MONO; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
    const a = Math.max(0, Math.min(1, (1.8 - z) / 0.8)) * 0.5;
    ctx.fillStyle = `rgba(230,232,230,${a})`; try { ctx.letterSpacing = '6px'; } catch (_) {}
    for (const r of REGION_LABELS) { const { sx, sy } = toScreen(r.x, r.y); ctx.fillText(r.name.toUpperCase(), sx, sy); }
    try { ctx.letterSpacing = '0px'; } catch (_) {}
  }
  ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
  for (const c of CITIES) {
    if (z < 0.42 && c.pop < 1.0 && hover !== c.id) continue;
    const size = Math.round((14 + c.pop * 3) * Math.min(1.25, Math.max(0.85, z * 0.75 + 0.5)));
    ctx.font = `${c.pop >= 1.2 ? '700 ' : ''}${size}px ` + MONO; try { ctx.letterSpacing = '2px'; } catch (_) {}
    const { sx, sy } = toScreen(c.x, c.y); const R = (12 + c.pop * 6) * z; const name = c.name.toUpperCase();
    const tw = ctx.measureText(name).width; const x0 = sx + R + 6, y0 = sy - size * 0.75;
    ctx.fillStyle = 'rgba(10,11,13,0.78)'; ctx.fillRect(x0, y0, tw + 12, size * 1.5);
    ctx.fillStyle = hover === c.id || conv.node === c.id ? AMBER : 'rgba(224,160,48,0.7)'; ctx.fillRect(x0, y0, 2, size * 1.5);
    ctx.fillStyle = '#eef0f2'; ctx.fillText(name, x0 + 7, sy);
    try { ctx.letterSpacing = '0px'; } catch (_) {}
    if (z > 1.0) { ctx.font = `12px ` + MONO; ctx.fillStyle = 'rgba(200,202,200,0.8)'; ctx.fillText(`pop ${c.pop}k · ${c.industries.join(' · ')}`, x0 + 7, sy + size * 1.3); }
    const sellProfit = conv.sellOutlook && conv.sellOutlook.get(c.id);
    if (sellProfit >= SELL_PROFIT_MIN && conv.node !== c.id) {
      const label = '+' + (sellProfit / 1000).toFixed(1) + 'k';
      ctx.font = `700 ${Math.round(size * 0.85)}px ` + MONO;
      const w = ctx.measureText(label).width;
      const b = 0.72 + 0.28 * Math.sin(t * 3 + c.h);
      const bx = sx - w / 2 - 6, by = sy - R - size - 14;
      ctx.fillStyle = `rgba(10,11,13,${0.75 + b * 0.2})`; ctx.fillRect(bx, by, w + 12, size * 1.25);
      ctx.fillStyle = `rgba(120,214,150,${0.55 + b * 0.45})`; ctx.fillText(label, bx + 6, by + size * 0.625);
    }
  }
  if (z > 1.4) { ctx.font = '12px ' + MONO; ctx.textAlign = 'center'; try { ctx.letterSpacing = '2px'; } catch (_) {} ctx.fillStyle = 'rgba(200,202,200,0.7)'; for (const p of POIS) { if (!p.label) continue; const { sx, sy } = toScreen(p.x, p.y); if (sx < -50 || sx > VW + 50 || sy < 0 || sy > VH) continue; ctx.fillText(p.label, sx, sy + p.size * z * 0.5 + 10); } try { ctx.letterSpacing = '0px'; } catch (_) {} }
  if (layers.claims) { ctx.font = '600 13.5px ' + MONO; ctx.textAlign = 'center'; try { ctx.letterSpacing = '3px'; } catch (_) {} ctx.fillStyle = 'rgba(224,160,48,0.9)'; for (const c of CLAIMS) { const { sx, sy } = toScreen(c.cx, c.cy); ctx.fillText('HOST CLAIM · NO RETURN', sx, sy); } try { ctx.letterSpacing = '0px'; } catch (_) {} }
  ctx.restore();
}
function drawGraticule() {
  const z = cam.z; ctx.strokeStyle = 'rgba(230,232,230,0.18)'; ctx.lineWidth = 0.8 / z; ctx.setLineDash([4 / z, 4 / z]);
  ctx.beginPath();
  for (let lon = -10; lon <= 30; lon += 5) { const a = toKm(lon, 55.5), b = toKm(lon, 36); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); }
  for (let lat = 40; lat <= 55; lat += 5) { const a = toKm(-12, lat), b = toKm(36, lat); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); }
  ctx.stroke(); ctx.setLineDash([]);
  ctx.save(); ctx.setTransform(DPR, 0, 0, DPR, 0, 0); ctx.font = '12px ' + MONO; ctx.fillStyle = 'rgba(230,232,230,0.6)'; ctx.textAlign = 'left';
  for (let lon = -10; lon <= 30; lon += 5) { const p = toKm(lon, 55.3); const { sx, sy } = toScreen(p.x, p.y); ctx.fillText(`${Math.abs(lon)}°${lon < 0 ? 'W' : 'E'}`, sx + 3, Math.max(60, sy)); }
  for (let lat = 40; lat <= 55; lat += 5) { const p = toKm(-11.8, lat); const { sx, sy } = toScreen(p.x, p.y); ctx.fillText(`${lat}°N`, Math.max(4, sx), sy - 4); }
  ctx.restore();
}
function drawChrome() {
  ctx.save(); ctx.setTransform(DPR, 0, 0, DPR, 0, 0);
  const g = ctx.createRadialGradient(VW / 2, VH / 2, Math.min(VW, VH) * 0.45, VW / 2, VH / 2, Math.max(VW, VH) * 0.75); g.addColorStop(0, 'rgba(5,6,8,0)'); g.addColorStop(1, 'rgba(5,6,8,0.5)'); ctx.fillStyle = g; ctx.fillRect(0, 0, VW, VH);
  let km = 500; while (km * cam.z > 220) km /= 2; while (km * cam.z < 70) km *= 2;
  const px = km * cam.z, x0 = 250, y0 = VH - 26;
  ctx.fillStyle = 'rgba(10,11,13,0.75)'; ctx.fillRect(x0 - 8, y0 - 16, px + 16, 26);
  ctx.strokeStyle = '#cfd2d6'; ctx.lineWidth = 1; ctx.beginPath(); ctx.moveTo(x0, y0); ctx.lineTo(x0 + px, y0); ctx.moveTo(x0, y0 - 4); ctx.lineTo(x0, y0 + 4); ctx.moveTo(x0 + px, y0 - 4); ctx.lineTo(x0 + px, y0 + 4); ctx.moveTo(x0 + px / 2, y0 - 2); ctx.lineTo(x0 + px / 2, y0 + 2); ctx.stroke();
  ctx.font = '13.5px ' + MONO; ctx.fillStyle = '#cfd2d6'; ctx.textAlign = 'center'; ctx.fillText(`${km} km`, x0 + px / 2, y0 - 7);
  const cx = VW - 60, cy = 330; ctx.strokeStyle = 'rgba(230,232,230,0.6)'; ctx.fillStyle = 'rgba(230,232,230,0.6)'; ctx.lineWidth = 1;
  ctx.beginPath(); ctx.arc(cx, cy, 16, 0, 7); ctx.stroke(); ctx.beginPath(); ctx.moveTo(cx, cy - 22); ctx.lineTo(cx - 4, cy); ctx.lineTo(cx + 4, cy); ctx.closePath(); ctx.fill();
  ctx.font = '600 13.5px ' + MONO; ctx.fillText('N', cx, cy - 27);
  ctx.textAlign = 'left'; ctx.font = '13.5px ' + MONO; ctx.fillStyle = fpsAvg < 50 ? '#d0553a' : 'rgba(207,210,214,0.7)'; ctx.fillText(`${Math.round(fpsAvg)} fps · zoom ${cam.z.toFixed(2)}`, x0 + px + 24, y0 - 2);
  ctx.restore();
}
function frame(now) {
  const dt = Math.min(0.1, (now - lastT) / 1000); lastT = now; const t = now / 1000;
  if (dt > 0) fpsAvg += (1 / dt - fpsAvg) * 0.05;
  if (window.MECHA) MECHA.tick(dt);
  // input: WASD drives, arrows pan
  let ix = 0, iy = 0; if (keys.KeyW) iy -= 1; if (keys.KeyS) iy += 1; if (keys.KeyA) ix -= 1; if (keys.KeyD) ix += 1;
  const wasMoving = conv.moving; conv.moving = false;
  if (ix || iy) { driveHeld = true; driveFree(dt, ix, iy); conv.moving = true; cam.follow = true; hideCard(); }
  else {
    driveHeld = false;
    if (!window.MECHA) advanceAuto(dt);
    conv.moving = !!(window.MECHA ? MECHA.onRoad() : (!conv.node && !conv.paused));
  }
  if (conv.node && !conv.moving) conv.surface = 'parked';
  if (wasMoving !== conv.moving) syncButtons();
  const pan = 420 / cam.z * dt; if (keys.ArrowLeft) { cam.x -= pan; cam.follow = false; } if (keys.ArrowRight) { cam.x += pan; cam.follow = false; } if (keys.ArrowUp) { cam.y -= pan; cam.follow = false; } if (keys.ArrowDown) { cam.y += pan; cam.follow = false; }
  if (cam.tz !== null) { cam.z += (cam.tz - cam.z) * Math.min(1, dt * 3); cam.x += (cam.tx - cam.x) * Math.min(1, dt * 3); cam.y += (cam.ty - cam.y) * Math.min(1, dt * 3); if (Math.abs(cam.tz - cam.z) < 0.003) { cam.z = cam.tz; cam.tz = null; } }
  else if (cam.follow && conv.moving) { const look = 45 / Math.max(1, cam.z); const tx = conv.x + Math.cos(conv.ang) * look, ty = conv.y + Math.sin(conv.ang) * look; cam.x += (tx - cam.x) * Math.min(1, dt * 3); cam.y += (ty - cam.y) * Math.min(1, dt * 3); }
  pulse = Math.max(0, pulse - dt * 0.8);
  for (let i = dust.length - 1; i >= 0; i--) { const d = dust[i]; d.x += d.vx * dt; d.y += d.vy * dt; d.r += 6 * dt; d.a -= dt * 0.5; if (d.a <= 0) dust.splice(i, 1); }
  emitSmoke(dt);

  ctx.setTransform(DPR, 0, 0, DPR, 0, 0); ctx.fillStyle = '#0b0c0e'; ctx.fillRect(0, 0, VW, VH);
  const z = cam.z; ctx.setTransform(z * DPR, 0, 0, z * DPR, (VW / 2 - cam.x * z) * DPR, (VH / 2 - cam.y * z) * DPR);
  ctx.imageSmoothingEnabled = true; ctx.imageSmoothingQuality = 'high';
  ctx.drawImage(base, 0, 0);
  if (cam.z > ZOOM_TILE_AT) drawDetailTiles();
  if (layers.ink) {
    if (z < 1.15) ctx.drawImage(inkBaked, 0, 0);
    else { const tl = toWorld(0, 0), br = toWorld(VW, VH); ctx.lineCap = 'round'; ctx.lineJoin = 'round'; for (let by = (tl.y / BUCKET | 0) - 1; by <= (br.y / BUCKET | 0); by++) for (let bx = (tl.x / BUCKET | 0) - 1; bx <= (br.x / BUCKET | 0); bx++) { const list = buckets.get(bx + ',' + by); if (list) for (const g of list) drawGlyph(ctx, g, 1.1 / Math.sqrt(z)); } }
  }
  if (layers.cost) ctx.drawImage(costCanvas, 0, 0);
  if (layers.cells) drawCells();
  if (layers.claims) drawClaims(t);
  if (layers.grid) drawGraticule();
  if (layers.roads) drawRoads();
  if (layers.ink) drawPois();
  if (window.MECHA && MECHA.drawRoute) MECHA.drawRoute();
  else if (conv.target) { const p = planTo(conv.target); if (p) drawPath(p.legs, p.seed, 'rgba(224,160,48,0.95)', [], 3.2); }
  if (pending) drawPath(pending.legs, pending.seed, 'rgba(224,160,48,0.95)', [], 3.2);
  else if (hover && hover !== conv.node && hover !== conv.target) { const p = planTo(hover); if (p) drawPath(p.legs, p.seed, 'rgba(240,220,160,0.85)', [8, 6], 2.4); }
  drawTrail();
  for (const d of dust) { ctx.beginPath(); ctx.arc(d.x, d.y, d.r / Math.sqrt(z), 0, 7); ctx.fillStyle = `rgba(150,140,120,${d.a})`; ctx.fill(); }
  drawCities(t);
  if (layers.mist) drawMist(t);
  drawConvoy(t);
  if (layers.labels) drawLabels(t);
  drawChrome();
  updateHud();
  requestAnimationFrame(frame);
}
