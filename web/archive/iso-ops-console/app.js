/* Mecha Trader — Ops Console front-end.
 * Holds no game rules: it posts commands to the simulation and renders whatever view
 * comes back. When a Godot scene replaces this page, nothing behind the API changes. */

const el = (id) => document.getElementById(id);

let state = null;
let map = null;          // static road network + terrain, fetched once
let build = null;        // which build is running

let module_ = 'map';     // map | faction | city
let selected = null;     // {kind:'city'|'site'|'cell', id} on the map
let prevCash = null;
let lastToastKey = null;
let lastTrade = null;    // {goodId} for a market-row flash
let placeKey = '';       // where the convoy is; clears stale selection

const BIOME_NAME = { P: 'plain', H: 'hill', M: 'mountain', F: 'forest', A: 'desert', T: 'tundra', S: 'swamp', W: 'water', D: 'deep' };

/* ---------- transport ---------- */

async function call(path, body) {
  const response = await fetch(path, {
    method: body === undefined ? 'GET' : 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body)
  });
  if (!response.ok) throw new Error(response.status + ' ' + response.statusText);
  return response.json();
}

async function send(command) {
  try { apply(await call('/api/command', command)); }
  catch (err) { showError(err.message); }
}

function apply(snapshot) {
  state = snapshot;
  showError(snapshot.error);
  render();
}

function showError(message) {
  const box = el('error');
  box.hidden = !message;
  box.textContent = message || '';
}

const num = (v, digits = 0) =>
  (v ?? 0).toLocaleString('en-US', { minimumFractionDigits: digits, maximumFractionDigits: digits });

/* ---------- helpers ---------- */

function where(v) {
  if (v.travel) return v.travel.fromName + ' → ' + v.travel.toName + ' · ' + v.travel.daysRemaining + 'd out';
  if (v.location) return v.location.name + ', ' + v.location.region;
  if (v.site) return v.site.name;
  if (v.field) return 'Open country · ' + v.field.biome;
  return '—';
}

function currentPlaceKey(v) {
  if (v.travel) return 't:' + v.travel.toName;
  if (v.location) return 'c:' + v.location.id;
  if (v.site) return 's:' + v.site.id;
  if (v.field) return 'f:' + v.field.cellId;
  return '?';
}

const meter = (fill, tone) =>
  '<span class="meter"><span class="' + tone + '" style="width:' + Math.round(fill * 100) + '%"></span></span>';

const pips = (level, max) => {
  const pct = max > 0 ? Math.round((level / max) * 100) : 0;
  return '<span class="pips"><span style="width:' + pct + '%"></span></span>';
};

const skillLine = (skills) =>
  skills.map((s) => s.name.slice(0, 3) + ' ' + s.level).join(' · ');

const knowledgeLine = (rows) =>
  (rows || []).length
    ? rows.map((k) => k.name + ' ' + k.level).join(' · ')
    : '';

const traitLine = (rows) =>
  (rows || []).length
    ? rows.map((t) => '<span class="chip trait" title="' + (t.blurb || '') + '">' + t.name + '</span>').join('')
    : '';

/* ---------- render ---------- */

function render() {
  const v = state.view;

  if (currentPlaceKey(v) !== placeKey) { selected = null; placeKey = currentPlaceKey(v); }

  renderHeader(v);

  if (module_ === 'map') renderMap(v);
  else if (module_ === 'faction') renderFaction(v);
  else renderCity(v);

  if (lastTrade) flashLastTrade();
  renderLog();
  maybeToast();
}

function renderHeader(v) {
  el('stat-day').textContent = v.day;

  const cashEl = el('stat-cash');
  cashEl.textContent = num(v.cash) + ' cr';
  const deltaEl = el('stat-cash-delta');
  if (prevCash !== null && v.cash !== prevCash) {
    const d = v.cash - prevCash;
    deltaEl.textContent = (d > 0 ? '+' : '') + num(d);
    deltaEl.className = 'delta ' + (d > 0 ? 'up' : 'down');
    cashEl.classList.remove('flash-up', 'flash-down');
    void cashEl.offsetWidth;
    cashEl.classList.add(d > 0 ? 'flash-up' : 'flash-down');
  }
  prevCash = v.cash;

  el('stat-worth').textContent = num(v.netWorth) + ' cr';
  el('stat-hold').textContent = num(v.convoy.used, 1) + ' / ' + num(v.convoy.capacity);
  const burn = v.convoy.dailyUpkeep + (v.travel ? v.travel.fuelPerDay : 0);
  el('stat-burn').textContent = num(burn) + ' cr';
  el('stat-where').textContent = where(v);

  const badge = el('build-badge');
  if (build) {
    badge.textContent = build.version + ' · ' + (build.commit || 'no commit') + (build.stale ? ' · STALE' : '');
    badge.className = 'ghost badge ' + (build.stale ? 'stale' : build.dirty ? 'dirty' : 'fresh');
  }
}

/* ---------- module switching ---------- */

function setModule(next) {
  module_ = next;
  document.querySelectorAll('#modules button').forEach((b) =>
    b.classList.toggle('active', b.dataset.module === next));
  document.querySelectorAll('.view').forEach((sec) =>
    sec.classList.toggle('active', sec.id === 'view-' + next));
  const keys = el('keys');
  if (keys) keys.style.display = next === 'map' ? '' : 'none';
  render();
}

/* ---------- map ---------- */

function renderMap(v) {
  if (map) IsoMap.sync(v, selected);
  renderTravelPanel(v);
  renderSelection(v);
  renderRoutes(v);
}

function selectNode(kind, id) {
  selected = { kind, id };
  // Defer the re-render out of the click handler so the DOM is not rebuilt in the
  // middle of the event dispatch (which would break double-click handling).
  setTimeout(() => render(), 0);
}

function renderTravelPanel(v) {
  const holder = el('travel-panel');

  if (v.travel) {
    const t = v.travel;
    const done = t.totalDays - t.daysRemaining;
    const pct = t.totalDays > 0 ? done / t.totalDays : 0;
    holder.innerHTML = '<h2>In transit</h2>' +
      '<div class="sel-card">' +
        '<div class="sel-name">' + t.fromName + ' → ' + t.toName + '</div>' +
        '<div class="sel-sub">' + t.daysRemaining + ' of ' + t.totalDays + ' day(s) remaining · ' + num(t.fuelPerDay) + ' cr/day fuel</div>' +
        '<div class="progress"><span class="bar"><span style="width:' + Math.round(pct * 100) + '%"></span></span>' +
          '<div class="meta"><span>arrival ~ d' + (v.day + t.daysRemaining) + '</span><span>' + Math.round(pct * 100) + '%</span></div></div>' +
        '<div class="row-actions"><button class="go" data-wait="' + t.daysRemaining + '">Continue to arrival</button><button data-wait="1">Advance 1 day</button></div>' +
      '</div>';
    wireWaitButtons(holder);
    return;
  }

  if (v.location) {
    const c = v.location;
    holder.innerHTML = '<h2>Position</h2>' +
      '<div class="sel-card">' +
        '<div class="sel-name">' + c.name + '</div>' +
        '<div class="sel-sub">' + c.region + ' · ' + c.industries.join(' · ') + '</div>' +
        '<div class="sel-acts"><button class="go" id="btn-open-city">Open city desk →</button></div>' +
      '</div>';
    const b = holder.querySelector('#btn-open-city');
    if (b) b.addEventListener('click', () => setModule('city'));
    return;
  }

  if (v.site) {
    holder.innerHTML = '<h2>Position</h2>' +
      '<div class="sel-card">' +
        '<div class="sel-name">' + v.site.name + '</div>' +
        '<div class="sel-sub">' + num(v.site.remaining) + ' units remaining · ' + num(v.site.expectedYield) + ' expected / day</div>' +
        '<div class="sel-acts"><button class="go" id="btn-open-site">Open excavation panel →</button></div>' +
      '</div>';
    const b = holder.querySelector('#btn-open-site');
    if (b) b.addEventListener('click', () => setModule('city'));
    return;
  }

  holder.innerHTML = '<h2>Position</h2>' +
    '<div class="sel-card"><div class="sel-name">Open country</div>' +
    '<div class="sel-sub">' + (v.field ? v.field.biome + ' biome' : 'no fixed position') + '</div></div>';
}

function renderSelection(v) {
  const holder = el('select-panel');

  if (!selected) {
    holder.innerHTML = '<h2>Selection</h2><div class="empty">Select a city, a mining claim or a map cell to plan a move.</div>';
    return;
  }

  const kind = selected.kind, id = selected.id;
  const inTransit = !!v.travel;

  if (kind === 'city') {
    const c = map && map.cities.find((x) => x.id === id);
    const route = (v.routes || []).find((r) => r.toId === id);
    const isHere = v.location && v.location.id === id;

    let est;
    let actions;
    if (isHere) {
      est = '<span class="sub">you are already here.</span>';
      actions = '<button class="go" id="sel-city-desk">Open city desk →</button>';
    } else if (route) {
      est = '<b>' + num(route.distanceKm) + ' km</b> · ' + route.days + ' day(s) · ~' + num(route.estimatedFuel) + ' cr fuel · ' + route.terrainName + '<br>' +
        (route.bestProfit > 0
          ? '<b>' + route.bestGoodName + '</b> ×' + num(route.bestUnits) + ' · <span class="profit">+' + num(route.bestProfit) + ' cr</span>'
          : '<span class="sub">— no profitable cargo on this leg.</span>');
      actions = inTransit
        ? ''
        : route.bestProfit > 0
          ? '<button class="go" data-run="' + id + '" data-good="' + route.bestGoodId + '" data-units="' + route.bestUnits + '">Load &amp; go · +' + num(route.bestProfit) + '</button>' +
            '<button data-depart="' + id + '">Depart empty</button>'
          : '<button class="go" data-depart="' + id + '">Depart →</button>';
    } else {
      est = '<span class="sub">no direct road estimate — the grid may still reach it.</span>';
      actions = inTransit ? '' : '<button class="go" data-depart="' + id + '">Depart →</button>';
    }

    holder.innerHTML = '<h2>Selection</h2>' +
      '<div class="sel-card">' +
        '<div class="sel-name">' + (c ? c.name : id) + '</div>' +
        '<div class="sel-sub">' + (c ? c.region : 'city') + '</div>' +
        '<div class="sel-est">' + est + '</div>' +
        '<div class="sel-acts">' + actions + '</div>' +
      '</div>';
    wireSel(holder);
    return;
  }

  if (kind === 'site') {
    const s = (v.miningSites || []).find((x) => x.id === id);
    holder.innerHTML = '<h2>Selection</h2>' +
      '<div class="sel-card">' +
        '<div class="sel-name">' + (s ? s.name : id) + '</div>' +
        '<div class="sel-sub">' + (s ? num(s.remaining) + ' units remaining' + (s.depleted ? ' · depleted' : '') : 'mining claim') + '</div>' +
        '<div class="sel-acts">' +
          (v.site && v.site.id === id
            ? '<button class="go" id="sel-site-panel">Open excavation panel →</button>'
            : '<button class="go" data-depart="' + id + '">Depart to claim →</button>') +
        '</div>' +
      '</div>';
    wireSel(holder);
    return;
  }

  const parts = id.split(',');
  let biome = 'unknown';
  if (map && map.biomes && parts.length === 2) {
    const i = (+parts[1]) * map.width + (+parts[0]);
    if (i >= 0 && i < map.biomes.length) biome = BIOME_NAME[map.biomes[i]] || map.biomes[i] || 'plain';
  }
  const here = v.field && v.field.cellId === id;
  holder.innerHTML = '<h2>Selection</h2>' +
    '<div class="sel-card">' +
      '<div class="sel-name">Open country</div>' +
      '<div class="sel-sub">' + biome + ' biome · grid ' + id + '</div>' +
      '<div class="sel-acts">' +
        (here ? '<span class="sub">you are parked here.</span>' : '<button class="go" data-depart="' + id + '">Depart to field →</button>') +
      '</div>' +
    '</div>';
  wireSel(holder);
}

function wireSel(root) {
  const openCity = root.querySelector('#sel-city-desk');
  if (openCity) openCity.addEventListener('click', () => setModule('city'));
  const openSite = root.querySelector('#sel-site-panel');
  if (openSite) openSite.addEventListener('click', () => setModule('city'));
  root.querySelectorAll('[data-depart]').forEach((b) =>
    b.addEventListener('click', () => send({ type: 'depart', toId: b.dataset.depart })));
  root.querySelectorAll('[data-run]').forEach((b) =>
    b.addEventListener('click', () => loadAndGo(b.dataset.good, +b.dataset.units, b.dataset.run)));
}

function renderRoutes(v) {
  const body = el('routes-body');

  if (v.travel) {
    body.innerHTML = '<div class="empty">In transit — the route board re-opens when the convoy parks.</div>';
    return;
  }

  if (!v.routes || !v.routes.length) {
    body.innerHTML = v.location
      ? '<div class="empty">No authed roads from here — all land is still walkable.</div>'
      : '<div class="empty">Route board only works from a city.</div>';
    return;
  }

  const rows = v.routes.map((r) => {
    const worth = r.bestProfit > 0;
    return '<tr>' +
      '<td class="name">' + r.toName +
        '<div class="sub">' + r.toRegion + ' · ' + r.terrainName + ' · ' + num(r.distanceKm) + ' km · ' + r.days + 'd · ' + num(r.estimatedFuel) + ' cr fuel</div></td>' +
      '<td>' + (worth ? '<span class="profit">' + r.bestGoodName + '</span> <span class="sub">×' + num(r.bestUnits) + '</span>' : '<span class="sub">nothing pays</span>') + '</td>' +
      '<td class="num ' + (worth ? 'profit' : 'sub') + '">' + (worth ? '+' + num(r.bestProfit) : '—') + '</td>' +
      '<td class="acts">' +
        (worth ? '<button class="go" data-run="' + r.toId + '" data-good="' + r.bestGoodId + '" data-units="' + r.bestUnits + '">Load &amp; go</button>' : '') +
        '<button data-depart="' + r.toId + '">Empty</button>' +
      '</td>' +
    '</tr>';
  }).join('');

  body.innerHTML = '<table>' +
      '<thead><tr><th>Destination</th><th>Best cargo</th><th>Est. profit</th><th></th></tr></thead>' +
      '<tbody>' + rows + '</tbody></table>' +
    '<div class="sub" style="margin-top:8px">Estimates price both legs against your order size, then ' +
      'deducts fuel and upkeep. Markets move while you travel.</div>';

  body.querySelectorAll('[data-depart]').forEach((b) =>
    b.addEventListener('click', () => send({ type: 'depart', toId: b.dataset.depart })));
  body.querySelectorAll('[data-run]').forEach((b) =>
    b.addEventListener('click', () => loadAndGo(b.dataset.good, +b.dataset.units, b.dataset.run)));
}

/* ---------- faction ---------- */

function renderFaction(v) {
  renderAccounts(v);
  renderConvoy(v);
  renderCargo(v);
  renderCrewFaction(v);
  renderDepot(v);
}

function renderAccounts(v) {
  const burn = v.convoy.dailyUpkeep + (v.travel ? v.travel.fuelPerDay : 0);
  el('fin-body').innerHTML = '<div class="stat-cards fin">' +
    '<article class="stat-card"><label>Credits</label><strong class="' + (v.cash < 0 ? 'loss' : '') + '">' + num(v.cash) + '<span class="unit">cr</span></strong></article>' +
    '<article class="stat-card"><label>Net worth</label><strong>' + num(v.netWorth) + '<span class="unit">cr</span></strong></article>' +
    '<article class="stat-card"><label>Account</label><strong class="' + (v.bankrupt ? 'loss' : 'profit') + '">' + (v.bankrupt ? 'In the red' : 'Solvent') + '</strong></article>' +
    '<article class="stat-card"><label>Burn / day</label><strong>' + num(burn) + '<span class="unit">cr</span></strong></article>' +
    '<article class="stat-card"><label>Payroll</label><strong>' + num(v.crew.dailyWages) + '<span class="unit">cr</span></strong></article>' +
    '<article class="stat-card"><label>Crew seats</label><strong>' + v.crew.size + '<span class="unit">/ ' + v.crew.capacity + '</span></strong></article>' +
    '<article class="stat-card"><label>Vehicles</label><strong>' + v.convoy.trucks.length + '</strong></article>' +
    '<article class="stat-card"><label>Convoy speed</label><strong>' + num(v.convoy.speedKmPerDay) + '<span class="unit">km/d</span></strong></article>' +
    '</div>';
}

function renderConvoy(v) {
  const c = v.convoy;
  const fill = c.capacity > 0 ? c.used / c.capacity : 0;
  el('convoy-body').innerHTML =
    '<div class="stat-list">' +
      '<div class="stat-line"><span>Vehicles</span><span>' + (c.trucks.join(', ') || 'none') + '</span></div>' +
      '<div class="stat-line"><span>Tools aboard</span><span>' + (c.gear.length ? c.gear.join(', ') : 'none') + '</span></div>' +
      '<div class="stat-line"><span>Daily upkeep</span><span>' + num(c.dailyUpkeep) + ' cr</span></div>' +
      '<div class="stat-line"><span>Convoy speed</span><span>' + num(c.speedKmPerDay) + ' km/day</span></div>' +
      (c.canMine ? '<div class="stat-line"><span>Mining yield</span><span>' + num(c.mineYield) + ' units/day</span></div>' : '') +
    '</div>' +
    '<div class="convoy-line-row"><span class="cap">Hold ' + num(c.used, 1) + ' / ' + num(c.capacity) + '</span>' +
      '<span class="meter" style="flex:1"><span class="meter-brand" style="width:' + Math.round(fill * 100) + '%"></span></span></div>';
}

function renderCargo(v) {
  const body = el('cargo-body');
  if (!v.cargo.length) {
    body.innerHTML = '<div class="empty">Hold is empty — load cargo in a city, or dig at a claim.</div>';
    return;
  }
  body.innerHTML = '<table>' +
      '<thead><tr><th>Commodity</th><th>Units</th><th>Grade</th><th>Avg cost</th><th>Volume</th></tr></thead>' +
      '<tbody>' + v.cargo.map((c) =>
        '<tr><td class="name">' + c.name +
          (c.category ? '<div class="sub">' + c.category + '</div>' : '') + '</td>' +
        '<td class="num held">' + num(c.units) + '</td>' +
        '<td class="num ' + (c.sTier ? 'held' : 'sub') + '">' + num(c.quality, 1) + '%' +
          (c.sTier ? '<div class="tag surplus">S-tier</div>' : '') + '</td>' +
        '<td class="num sub">' + num(c.averageCost, 1) + '</td>' +
        '<td class="num sub">' + num(c.volume, 1) + '</td></tr>').join('') +
      '</tbody></table>';
}

function renderCrewFaction(v) {
  const c = v.crew;

  const skills = c.skills.map((s) =>
    '<tr><td class="name">' + s.name + '<div class="sub">' + s.effectText + '</div></td>' +
    '<td class="num ' + (s.level > 0 ? 'held' : 'sub') + '">' + s.level + '/' + s.maxLevel + '</td>' +
    '<td class="pipcell">' + pips(s.level, s.maxLevel) + '</td>' +
    '<td class="sub">' + (s.leaderName || '—') + '</td></tr>').join('');

  const roster = c.roster.length
    ? '<table><thead><tr><th>Aboard</th><th>Wage</th><th>Skills</th><th></th></tr></thead><tbody>' +
      c.roster.map((m) =>
        '<tr><td class="name">' + m.name +
          '<div class="sub">' + m.roleName + ' · signed d' + m.hiredDay + (m.hiredAt ? ' at ' + m.hiredAt : '') + '</div>' +
          (traitLine(m.traits) ? '<div class="chips">' + traitLine(m.traits) + '</div>' : '') +
          (knowledgeLine(m.knowledge) ? '<div class="sub">' + knowledgeLine(m.knowledge) + '</div>' : '') + '</td>' +
        '<td class="num">' + num(m.dailyWage) + '<div class="sub">cr/day</div></td>' +
        '<td class="sub">' + skillLine(m.skills) + '</td>' +
        '<td class="acts"><button class="danger" data-dismiss="' + m.id + '" title="Severance ' + num(m.severance) + ' cr">Pay off</button></td></tr>').join('') +
      '</tbody></table>'
    : '<div class="empty">Nobody but you. A seat is only worth its wage while it pulls a lever you use.</div>';

  let hiring = '<div class="empty">Recruitment centres are in cities — park to see the local board.</div>';
  if (c.recruitment) {
    const r = c.recruitment;
    hiring = r.candidates.length
      ? '<table><thead><tr><th>Available</th><th>Wage</th><th>Signing</th><th>Skills</th><th></th></tr></thead><tbody>' +
        r.candidates.map((k) =>
          '<tr><td class="name">' + k.name +
            '<div class="sub">' + k.roleName + '</div>' +
            (traitLine(k.traits) ? '<div class="chips">' + traitLine(k.traits) + '</div>' : '') +
            (knowledgeLine(k.knowledge) ? '<div class="sub">' + knowledgeLine(k.knowledge) + '</div>' : '') + '</td>' +
          '<td class="num sub">' + num(k.dailyWage) + '</td>' +
          '<td class="num ' + (k.affordable ? '' : 'loss') + '">' + num(k.signingFee) + '</td>' +
          '<td class="sub">' + skillLine(k.skills) + '</td>' +
          '<td class="acts"><button data-hire="' + k.id + '" ' + (k.affordable && k.roomAboard ? '' : 'disabled') + '>Sign on</button></td></tr>').join('') +
        '</tbody></table>' +
        '<div class="sub" style="margin-top:8px">' + r.cityName + ' centre · new faces in ' + r.refreshInDays + ' day(s) · ' +
          c.size + '/' + c.capacity + ' seats taken · wages run every day, hired or idle.</div>'
      : '<div class="empty">Nobody is looking for work in ' + r.cityName + ' this round.</div>';
  }

  const body = el('crew-body');
  body.innerHTML = '<table class="skills"><thead><tr><th>Skill</th><th>Level</th><th></th><th>Best hand</th></tr></thead><tbody>' + skills + '</tbody></table>' +
    '<h3>Payroll · ' + num(c.dailyWages) + ' cr/day</h3>' + roster +
    '<h3>Recruitment</h3>' + hiring;

  body.querySelectorAll('[data-hire]').forEach((b) =>
    b.addEventListener('click', () => send({ type: 'hireCrew', candidateId: b.dataset.hire })));
  body.querySelectorAll('[data-dismiss]').forEach((b) =>
    b.addEventListener('click', () => send({ type: 'dismissCrew', crewId: b.dataset.dismiss })));
}

function renderDepot(v) {
  const body = el('shipyard-body');
  if (v.travel) { body.innerHTML = '<div class="empty">Reachable in a city.</div>'; return; }
  if (!v.location) { body.innerHTML = '<div class="empty">Depot is in a city.</div>'; return; }

  const trucks = (v.shipyard || []).filter((t) => t.kind !== 'machine');
  const machines = (v.shipyard || []).filter((t) => t.kind === 'machine');

  const row = (t) =>
    '<tr><td class="name">' + t.name + '<div class="sub">' + t.kind + (t.mineYield ? ' · ' + num(t.mineYield) + ' ore/day' : '') + '</div></td>' +
    '<td class="num sub">' + num(t.capacity) + '</td>' +
    '<td class="num sub">' + num(t.speedKmPerDay) + '</td>' +
    '<td class="num sub">' + num(t.upkeepPerDay) + '</td>' +
    '<td class="num">' + num(t.price) + '</td>' +
    '<td><button data-truck="' + t.id + '" ' + (v.cash >= t.price ? '' : 'disabled') + '>Buy</button></td></tr>';

  const table = (title, list) => list.length
    ? '<h3>' + title + '</h3><table><thead><tr><th></th><th>Cap</th><th>Speed</th><th>Upkeep</th><th>Price</th><th></th></tr></thead><tbody>' +
      list.map(row).join('') + '</tbody></table>'
    : '';

  const gear = (v.outfitters || []).map((g) =>
    '<tr><td class="name">' + g.name + '<div class="sub">' + num(g.volume) + ' vol' + (g.mineYield ? ' · ' + num(g.mineYield) + ' ore/day' : '') + '</div></td>' +
    '<td class="num">' + num(g.price) + '</td>' +
    '<td><button data-gear="' + g.id + '" ' + (g.affordable && g.fits ? '' : 'disabled') + '>Buy</button></td></tr>').join('');
  const gearTable = gear ? '<h3>Tools</h3><table><thead><tr><th>Gear</th><th>Price</th><th></th></tr></thead><tbody>' + gear + '</tbody></table>' : '';

  body.innerHTML = table('Trucks', trucks) + table('Machines', machines) + gearTable +
    '<div class="row-actions" style="margin-top:12px"><button id="btn-sys-report" class="ghost">System report…</button></div>';

  body.querySelectorAll('[data-truck]').forEach((b) =>
    b.addEventListener('click', () => send({ type: 'buyTruck', truckTypeId: b.dataset.truck })));
  body.querySelectorAll('[data-gear]').forEach((b) =>
    b.addEventListener('click', () => send({ type: 'buyGear', gearId: b.dataset.gear })));
  const sys = body.querySelector('#btn-sys-report');
  if (sys) sys.addEventListener('click', openBuildModal);
}

/* ---------- city ---------- */

function renderCity(v) {
  const body = el('city-body');

  if (v.travel) {
    const t = v.travel;
    const done = t.totalDays - t.daysRemaining;
    const pct = t.totalDays > 0 ? done / t.totalDays : 0;
    body.innerHTML = '<div class="panel"><h2>In transit</h2>' +
      '<div class="sel-card">' +
        '<div class="sel-name">' + t.fromName + ' → ' + t.toName + '</div>' +
        '<div class="sel-sub">' + t.daysRemaining + ' of ' + t.totalDays + ' day(s) remaining · ' + num(t.fuelPerDay) + ' cr/day fuel</div>' +
        '<div class="progress"><span class="bar"><span style="width:' + Math.round(pct * 100) + '%"></span></span>' +
          '<div class="meta"><span>arrival ~ d' + (v.day + t.daysRemaining) + '</span><span>' + Math.round(pct * 100) + '%</span></div></div>' +
        '<div class="row-actions"><button class="go" data-wait="' + t.daysRemaining + '">Continue to arrival</button><button data-wait="1">Advance 1 day</button></div>' +
      '</div></div>';
    wireWaitButtons(body);
    return;
  }

  if (v.site) {
    const s = v.site;
    body.innerHTML = '<div class="panel"><h2>Excavation — ' + s.name + '</h2>' +
      '<div class="sel-card">' +
        '<div class="sel-name">' + s.goodName + ' deposit</div>' +
        '<div class="sel-sub">' + num(s.remaining) + ' units remaining · expect ' + num(s.expectedYield) + ' / day</div>' +
        '<div class="sel-est">' + s.hint + '</div>' +
        (s.canMine
          ? '<div class="row-actions"><button class="go" data-wait="1">Dig 1 day</button><button data-wait="7">Dig 7 days</button></div>'
          : '<div class="empty">The convoy has no mining gear — buy a tool or machine in a city.</div>') +
      '</div></div>';
    wireWaitButtons(body);
    return;
  }

  if (v.field) {
    body.innerHTML = '<div class="panel"><h2>Open country</h2>' +
      '<div class="empty">' + v.field.biome + ' biome (grid ' + v.field.cellId + '). No market here. Open the map and pick a destination.</div></div>';
    return;
  }

  if (!v.location) {
    body.innerHTML = '<div class="panel"><div class="empty">No fixed position.</div></div>';
    return;
  }

  renderCityDossier(v, v.location, body);
}

function renderCityDossier(v, c, body) {
  const vitals = c.vitals.map((s) => {
    const drift = s.deltaDisplay
      ? '<span class="drift">' + s.deltaDisplay + ' since founding</span>'
      : '<span class="founding">founding ' + s.foundingDisplay + '</span>';
    return '<article class="stat-card" title="' + s.blurb + '">' +
      '<label>' + s.name + '</label>' +
      '<strong class="tone-' + s.tone + '">' + s.display + '</strong>' +
      '<div class="stat-meta"><span class="tag tone-' + s.tone + '">' + s.band + '</span> · ' + drift + '</div>' +
      meter(s.fill, s.tone) + '</article>';
  }).join('');

  const supplies = c.supplies.map((s) => {
    const cover = s.daysOfCover === null ? 'no local demand' : num(s.daysOfCover, 1) + 'd cover';
    const flow = s.netFlow > 0 ? '+' + num(s.netFlow, 1) + '/day' : num(s.netFlow, 1) + '/day';
    return '<article class="stat-card" title="' + s.blurb + ' (' + s.goods.join(', ') + ')">' +
      '<label>' + s.name + '</label>' +
      '<strong class="tone-' + s.tone + '">' + num(s.index) + '<span class="unit">%</span></strong>' +
      '<div class="stat-meta"><span class="tag tone-' + s.tone + '">' + s.band + '</span><span class="tag ' + s.flow + '">' + s.flow + '</span></div>' +
      meter(s.fill, s.tone) +
      '<div class="stat-foot">' + flow + ' · ' + cover + '</div></article>';
  }).join('');

  const news = c.news.length
    ? '<ol class="news">' + c.news.map((n) =>
        '<li class="tone-' + n.tone + '"><span class="day">d' + n.day + '</span><strong>' + n.headline + '</strong>' +
        (n.daysLeft ? '<span class="news-ttl">' + n.daysLeft + 'd left</span>' : '') +
        (n.detail ? '<div class="sub">' + n.detail + '</div>' : '') + '</li>').join('') + '</ol>'
    : '<div class="wire-empty">The wire from ' + c.name + ' is quiet.</div>';

  const st = c.standing;
  const permits = (st ? st.permits : []).map((p) =>
    '<span class="chip permit ' + (p.granted ? 'granted' : 'locked') + '" title="' + p.blurb + '">' + p.name +
    (p.granted ? '' : ' · ' + p.standingRequired) + '</span>').join('');
  const actions = (st ? st.actions : []).map((a) =>
    '<button data-favor="' + a.id + '" ' + (a.affordable ? '' : 'disabled') + ' title="' + a.blurb + '">' +
    a.name + ' · ' + num(a.cost) + ' cr <span class="sub">' + a.effectText + '</span></button>').join('');

  const granted = (st ? st.permits : []).filter((p) => p.granted);
  const construction = granted.length
    ? '<section class="city-section"><h3>Construction</h3>' +
      '<div class="chips">' + granted.map((p) => '<span class="chip permit granted">' + p.name + ' — paper held</span>').join('') + '</div>' +
      '<p class="city-caption">The governor has granted the permit. Building the shop or factory is a later milestone.</p></section>'
    : '';

  body.innerHTML = '<div class="city-dossier">' +
    '<header class="city-head">' +
      '<div><strong>' + c.name + '</strong><span class="sub">' + c.region + '</span></div>' +
      '<div class="chips">' + c.industries.map((i) => '<span class="chip">' + i + '</span>').join('') + '</div>' +
    '</header>' +

    (st ? '<section class="city-section"><h3>' + st.governorTitle + '</h3>' +
      '<div class="governor">' +
        '<article class="stat-card">' +
          '<label>' + st.governorTitle + ' of ' + c.name + '</label>' +
          '<strong class="tone-' + st.tone + '">' + st.governorName + '</strong>' +
          '<div class="stat-meta"><span class="tag tone-' + st.tone + '">' + st.rank + '</span><span>' + num(st.value, 1) + ' / ' + num(st.max) + '</span></div>' +
          meter(st.fill, st.tone) +
          '<div class="stat-foot">' + st.reservedDisplay + '</div>' +
        '</article>' +
        '<div class="chips permits">' + permits + '</div>' +
        '<div class="favor-actions">' + actions + '</div>' +
      '</div></section>' : '') +

    construction +

    '<div class="city-grid">' +
      '<section class="city-section"><h3>Vitals</h3><div class="stat-cards vitals">' + vitals + '</div></section>' +
      '<section class="city-section"><h3>Supply</h3><div class="stat-cards supplies">' + supplies + '</div>' +
        '<p class="city-caption">100 is this city’s own resting level. It moves every day, and it moves when you trade.</p></section>' +
    '</div>' +

    '<section class="city-section"><h3>Storeroom</h3>' + renderWarehouse(v) + '</section>' +

    '<section class="city-section"><h3>Market</h3>' + renderMarketTable(v) + '</section>' +

    '<section class="city-section"><h3>City wire</h3>' + news + '</section>' +
    '</div>';

  body.querySelectorAll('[data-favor]').forEach((b) =>
    b.addEventListener('click', () => send({ type: 'favor', actionId: b.dataset.favor })));
  wireTrade(body);
  wireWarehouse(body);
}

function renderWarehouse(v) {
  const w = v.warehouse;
  if (!w) return '<div class="empty">No storeroom here.</div>';
  if (!w.rented) {
    return '<p class="city-caption">Rent a storeroom to park cargo and set auto-sell / auto-procure prices. The room ticks even when the convoy is elsewhere.</p>' +
      '<div class="row-actions"><button data-rent-wh>Rent storeroom · ' + num(w.rentCost) + ' cr</button>' +
      '<span class="sub">' + num(w.capacity) + ' vol · ' + num(w.dailyRent) + ' cr/day</span></div>';
  }

  const lots = (w.lots || []).map((lot) =>
    '<tr>' +
      '<td class="name">' + lot.name +
        (lot.sTier ? ' <span class="tag surplus">S-tier</span>' : '') +
        (lot.units > 0 ? '<div class="sub">' + num(lot.quality, 1) + '% grade</div>' : '') + '</td>' +
      '<td class="num ' + (lot.units > 0 ? 'held' : 'sub') + '">' + (lot.units > 0 ? num(lot.units) : '—') + '</td>' +
      '<td><input type="number" min="0" step="1" value="' + (lot.autoSell || '') + '" data-wh-sell="' + lot.goodId + '" placeholder="off"></td>' +
      '<td><input type="number" min="0" step="1" value="' + (lot.autoProcure || '') + '" data-wh-buy="' + lot.goodId + '" placeholder="off"></td>' +
      '<td class="acts">' +
        '<button data-wh-in="' + lot.goodId + '" ' + ((v.cargo || []).some((c) => c.goodId === lot.goodId && c.units > 0) ? '' : 'disabled') + '>Deposit</button>' +
        '<button data-wh-out="' + lot.goodId + '" ' + (lot.units > 0 ? '' : 'disabled') + '>Withdraw</button>' +
      '</td>' +
    '</tr>').join('');

  const extras = (v.market || []).filter((g) => !(w.lots || []).some((l) => l.goodId === g.goodId)).map((g) =>
    '<tr>' +
      '<td class="name">' + g.name + '<div class="sub">empty</div></td>' +
      '<td class="num sub">—</td>' +
      '<td><input type="number" min="0" step="1" value="" data-wh-sell="' + g.goodId + '" placeholder="off"></td>' +
      '<td><input type="number" min="0" step="1" value="" data-wh-buy="' + g.goodId + '" placeholder="off"></td>' +
      '<td class="acts">' +
        '<button data-wh-in="' + g.goodId + '" ' + (g.held > 0 ? '' : 'disabled') + '>Deposit</button>' +
        '<button data-wh-out="' + g.goodId + '" disabled>Withdraw</button>' +
      '</td>' +
    '</tr>').join('');

  const fill = w.capacity > 0 ? w.used / w.capacity : 0;
  return '<div class="convoy-line-row"><span class="cap">Storeroom ' + num(w.used, 1) + ' / ' + num(w.capacity) +
    ' · ' + num(w.dailyRent) + ' cr/day</span>' +
    '<span class="meter" style="flex:1"><span class="meter-brand" style="width:' + Math.round(fill * 100) + '%"></span></span></div>' +
    '<table><thead><tr><th>Good</th><th>Stored</th><th>Auto-sell ≥</th><th>Auto-buy ≤</th><th></th></tr></thead><tbody>' +
    lots + extras + '</tbody></table>' +
    '<p class="city-caption">Auto prices of 0 / blank are off. Unattended orders use the market spread — crew knowledge does not cherry-pick for a room you are not standing in.</p>';
}

function renderMarketTable(v) {
  if (!v.market || !v.market.length) return '<div class="empty">No market here.</div>';

  const rows = v.market.map((g) => {
    const buyTrend = g.buy > g.basePrice * 1.02 ? '<span class="trend up">▲</span>'
      : g.buy < g.basePrice * 0.98 ? '<span class="trend down">▼</span>' : '';
    const sellTrend = g.sell > g.basePrice * 1.02 ? '<span class="trend up">▲</span>'
      : g.sell < g.basePrice * 0.98 ? '<span class="trend down">▼</span>' : '';
    const grade = num(g.averageQuality, 1) + '% avg' +
      (g.sTierPossible ? ' · pick ' + num(g.pickQuality, 1) + '% <span class="tag surplus">S</span>' : (g.knowledge > 0 ? ' · pick ' + num(g.pickQuality, 1) + '%' : ''));
    return '<tr class="market-row" data-good-row="' + g.goodId + '">' +
      '<td class="name">' + g.name + '<span class="tag ' + g.flow + '">' + g.flow + '</span>' +
        '<div class="sub">' + (g.category || g.tier) + ' · ' + g.tier + ' · ' + g.unitVolume + ' vol/unit' +
          (g.eventHint ? ' · ' + g.eventHint : '') + '</div></td>' +
      '<td class="num">' + num(g.buy, 1) + buyTrend + '</td>' +
      '<td class="num">' + num(g.sell, 1) + sellTrend + '</td>' +
      '<td class="num sub" title="Shop average. Buying the whole shelf always takes this grade. Knowledge only filters a smaller order.">' + grade +
        (g.knowledge > 0 ? '<div class="intake">eye ' + num(g.knowledge, 0) + '</div>' : '') + '</td>' +
      '<td class="num sub">' + num(g.shelf) +
        (g.reserved > 0 ? '<div class="intake" title="Held for you; other caravans cannot take this first">' + num(g.reserved) + ' reserved</div>' : '') +
        (g.intake > 0 ? '<div class="intake" title="Unloaded here by caravans; not for sale until the city shelves it">+' + num(g.intake) + ' intake</div>' : '') + '</td>' +
      '<td class="num ' + (g.held > 0 ? 'held' : 'sub') + '">' + (g.held > 0 ? num(g.held) : '—') +
        (g.heldSTier ? '<div class="tag surplus">S-tier</div>' : (g.held > 0 ? '<div class="intake">' + num(g.heldQuality, 1) + '%</div>' : '')) + '</td>' +
      '<td class="num sub">' + (g.held > 0 ? num(g.averageCost, 1) : '—') + '</td>' +
      '<td><div class="trade-cell"><input type="number" min="1" step="1" value="10" data-qty="' + g.goodId + '">' +
        '<button data-buy="' + g.goodId + '">Buy</button>' +
        '<button data-sell="' + g.goodId + '" ' + (g.held > 0 ? '' : 'disabled') + '>Sell</button></div></td>' +
      '</tr>';
  }).join('');

  return '<table><thead><tr>' +
      '<th>Commodity</th><th>Buy</th><th>Sell</th><th>Grade</th><th>Shelf</th><th>Held</th><th>Avg cost</th><th></th>' +
    '</tr></thead><tbody>' + rows + '</tbody></table>' +
    '<div class="sub" style="margin-top:8px">▲ premium / ▼ discount vs base. <strong>Grade</strong> is the shop average; buying the whole shelf always takes it. Knowledge only skips worse crates on a smaller order. S-tier sells at +30%.</div>';
}

function wireTrade(scope) {
  scope.querySelectorAll('[data-buy]').forEach((b) =>
    b.addEventListener('click', () => { lastTrade = { goodId: b.dataset.buy }; trade('buy', b.dataset.buy); }));
  scope.querySelectorAll('[data-sell]').forEach((b) =>
    b.addEventListener('click', () => { lastTrade = { goodId: b.dataset.sell }; trade('sell', b.dataset.sell); }));
}

function wireWarehouse(scope) {
  const rent = scope.querySelector('[data-rent-wh]');
  if (rent) rent.addEventListener('click', () => send({ type: 'rentWarehouse' }));

  scope.querySelectorAll('[data-wh-in]').forEach((b) =>
    b.addEventListener('click', () => {
      const goodId = b.dataset.whIn;
      const held = (state.view.cargo || []).find((c) => c.goodId === goodId);
      const units = held ? held.units : 1;
      send({ type: 'warehouseDeposit', goodId, units });
    }));

  scope.querySelectorAll('[data-wh-out]').forEach((b) =>
    b.addEventListener('click', () => {
      const goodId = b.dataset.whOut;
      const lot = ((state.view.warehouse || {}).lots || []).find((l) => l.goodId === goodId);
      const units = lot && lot.units > 0 ? lot.units : 1;
      send({ type: 'warehouseWithdraw', goodId, units });
    }));

  const commitPrice = (input, type) => {
    const raw = input.value.trim();
    const price = raw === '' ? 0 : Math.max(0, parseInt(raw, 10) || 0);
    send({ type, goodId: input.dataset.whSell || input.dataset.whBuy, price });
  };

  scope.querySelectorAll('[data-wh-sell]').forEach((input) =>
    input.addEventListener('change', () => commitPrice(input, 'warehouseSell')));
  scope.querySelectorAll('[data-wh-buy]').forEach((input) =>
    input.addEventListener('change', () => commitPrice(input, 'warehouseProcure')));
}

function trade(type, goodId) {
  const input = document.querySelector('[data-qty="' + goodId + '"]');
  const units = Math.max(1, parseInt(input.value, 10) || 1);
  send({ type, goodId, units });
}

function flashLastTrade() {
  const row = document.querySelector('[data-good-row="' + lastTrade.goodId + '"]');
  if (row) {
    row.classList.add('flash');
    setTimeout(() => row.classList.remove('flash'), 900);
  }
  lastTrade = null;
}

async function loadAndGo(goodId, units, toCityId) {
  try {
    const bought = await call('/api/command', { type: 'buy', goodId, units });
    if (bought.error) { apply(bought); return; }
    apply(await call('/api/command', { type: 'depart', toId: toCityId }));
  } catch (err) { showError(err.message); }
}

/* ---------- activity feed ---------- */

function renderLog() {
  el('log-body').innerHTML = state.log
    .map((e) => '<li class="' + e.kind + '"><span class="day">d' + e.day + '</span>' + e.message + '</li>')
    .join('');
}

/* ---------- toasts ---------- */

const TOAST_KINDS = new Set(['Arrival', 'Warning', 'World', 'Standing']);

function maybeToast() {
  if (!state.log || !state.log.length) return;
  const top = state.log[0];
  const key = top.day + '|' + top.kind + '|' + top.message;
  if (key === lastToastKey) return;
  lastToastKey = key;
  if (!TOAST_KINDS.has(top.kind)) return;

  const tags = { Arrival: 'Arrival', Warning: 'Warning', World: 'Dispatch', Standing: 'Relations' };
  const wrap = el('toasts');
  const div = document.createElement('div');
  div.className = 'toast kind-' + top.kind;
  div.innerHTML = '<span class="toast-tag">' + (tags[top.kind] || top.kind) + '</span>' + top.message;
  wrap.appendChild(div);
  setTimeout(() => {
    div.classList.add('out');
    setTimeout(() => div.remove(), 320);
  }, 5200);
}

/* ---------- build badge & modal ---------- */

async function refreshBuild() {
  try { build = await call('/api/build'); } catch (err) { build = null; }
  if (state) renderHeader(state.view);
  if (el('modal-root').children.length) openBuildModal();
}

function openBuildModal() {
  if (!build) { refreshBuild(); return; }
  const root = el('modal-root');

  const notice = build.stale
    ? '<div class="build-notice stale">Not the latest. ' + build.staleReason + '.</div>'
    : '<div class="build-notice fresh">This is the latest — no code or content on disk is newer than what is running.</div>';
  const dirty = build.dirty
    ? '<div class="build-notice dirty">' + build.dirtyFiles + ' uncommitted file(s) — the commit below does not fully describe this build.</div>'
    : '';

  const head = build.gitAvailable
    ? '<table><tbody>' +
        '<tr><td class="name sub">Version</td><td class="name">' + build.version + '</td></tr>' +
        '<tr><td class="name sub">Built</td><td class="name">' + build.builtAgo + '</td></tr>' +
        '<tr><td class="name sub">Branch</td><td class="name">' + build.branch + '</td></tr>' +
        '<tr><td class="name sub">Commit</td><td class="name">' + build.commit + ' · ' + build.commitSubject + '</td></tr>' +
      '</tbody></table>'
    : '<table><tbody>' +
        '<tr><td class="name sub">Version</td><td class="name">' + build.version + '</td></tr>' +
        '<tr><td class="name sub">Built</td><td class="name">' + build.builtAgo + '</td></tr>' +
        '<tr><td class="name sub">Commits</td><td class="sub">no repository here</td></tr>' +
      '</tbody></table>';

  const log = build.log.length
    ? '<ol class="commits">' + build.log.map((c) =>
        '<li class="' + (c.isHead ? 'head' : '') + '"><span class="hash">' + c.hash + '</span>' + c.subject +
        '<div class="sub">' + c.author + ' · ' + c.when.slice(0, 10) + (c.isHead ? ' · this build' : '') + '</div></li>').join('') + '</ol>'
    : '<div class="empty">No commit log available.</div>';

  root.innerHTML = '<div class="overlay" id="modal-overlay">' +
    '<div class="modal" role="dialog" aria-label="Build information">' +
      '<h2>Build report</h2>' +
      notice + dirty + head +
      '<h3>Commits</h3>' + log +
      '<div class="row-actions"><button id="btn-build-refresh">Re-check</button><button class="go" id="btn-build-close">Close</button></div>' +
    '</div></div>';

  el('btn-build-close').addEventListener('click', closeModal);
  el('btn-build-refresh').addEventListener('click', refreshBuild);
  el('modal-overlay').addEventListener('click', (e) => { if (e.target.id === 'modal-overlay') closeModal(); });
}

function closeModal() { el('modal-root').innerHTML = ''; }

/* ---------- wiring ---------- */

function wireWaitButtons(scope) {
  scope.querySelectorAll('[data-wait]').forEach((b) =>
    b.addEventListener('click', () => send({ type: 'wait', days: parseInt(b.dataset.wait, 10) })));
}

document.addEventListener('DOMContentLoaded', () => {
  wireWaitButtons(document);

  document.querySelectorAll('#modules button').forEach((b) =>
    b.addEventListener('click', () => setModule(b.dataset.module)));

  el('btn-new').addEventListener('click', () => {
    if (window.confirm('Start a new run? This run is lost.')) {
      call('/api/new', { seed: Math.floor(Math.random() * 1e9) }).then(apply).catch((e) => showError(e.message));
    }
  });

  el('build-badge').addEventListener('click', openBuildModal);

  el('btn-log-toggle').addEventListener('click', () => {
    const dock = el('log-dock');
    dock.classList.toggle('collapsed');
    el('btn-log-toggle').textContent = dock.classList.contains('collapsed') ? 'expand' : 'collapse';
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') { closeModal(); return; }
    const k = e.key.toLowerCase();
    if (e.key === '1') setModule('map');
    else if (e.key === '2') setModule('faction');
    else if (e.key === '3') setModule('city');
    else if (k === 'm') setModule('map');
    else if (k === 'f') setModule('faction');
    else if (k === 'c') setModule('city');
    else if (k === 'n') el('btn-new').click();
    else if (k === 'b') openBuildModal();
  });

  refreshBuild();

  IsoMap.onPick = (kind, id) => selectNode(kind, id);
  IsoMap.onDbl = (id) => { if (state && state.view && !state.view.travel) send({ type: 'depart', toId: id }); };
  IsoMap.start();

  call('/api/map')
    .then((m) => { map = m; IsoMap.load(m); return call('/api/state'); })
    .then(apply)
    .catch((err) => showError(err.message));
});
