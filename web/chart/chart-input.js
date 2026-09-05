// Extracted verbatim (Phase D CP-D2) from web/chart/chart.js at integration 67b6fb5.
'use strict';
const SEC_PER_DAY = 2.4;
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
