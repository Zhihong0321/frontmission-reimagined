// Extracted verbatim (Phase D CP-D2) from web/chart/chart.js at integration 67b6fb5.
'use strict';
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
