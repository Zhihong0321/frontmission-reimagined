// Extracted verbatim (Phase D CP-D2) from web/chart/chart.js at integration 67b6fb5.
'use strict';
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
function fitAll() { cam.tz = Math.min(VW / MAP_W, VH / MAP_H) * 0.96; cam.tx = MAP_W / 2; cam.ty = MAP_H / 2; cam.follow = false; syncButtons(); }
