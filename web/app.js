/* Alpha 1 front-end.
 *
 * Holds no game rules whatsoever: it posts commands to the simulation and renders
 * whatever view comes back. That is the point of the exercise - when this is replaced
 * by a Godot scene, nothing behind the API changes.
 */

const el = (id) => document.getElementById(id);

let state = null;
let map = null;   // static road network, fetched once

/* ---------- transport ---------- */

async function call(path, body) {
  const response = await fetch(path, {
    method: body === undefined ? 'GET' : 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body)
  });

  if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
  return response.json();
}

async function send(command) {
  try {
    apply(await call('/api/command', command));
  } catch (err) {
    showError(err.message);
  }
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

/* ---------- formatting ---------- */

const num = (v, digits = 0) =>
  (v ?? 0).toLocaleString('en-US', { minimumFractionDigits: digits, maximumFractionDigits: digits });

/* ---------- rendering ---------- */

function render() {
  const v = state.view;

  el('stat-day').textContent = v.day;
  el('stat-cash').textContent = num(v.cash) + ' cr';
  el('stat-cash').className = v.cash < 0 ? 'loss' : '';
  el('stat-worth').textContent = num(v.netWorth) + ' cr';
  el('stat-hold').textContent = `${num(v.convoy.used, 1)} / ${num(v.convoy.capacity)}`;
  el('stat-upkeep').textContent = num(v.convoy.dailyUpkeep) + ' cr/day';
  el('stat-payroll').textContent = `${num(v.crew.dailyWages)} cr/day · ${v.crew.size}/${v.crew.capacity}`;

  el('stat-where').textContent = v.travel
    ? `${v.travel.fromName} → ${v.travel.toName} · ${v.travel.daysRemaining} day(s) out`
    : v.location
      ? `${v.location.name}, ${v.location.region}`
      : '—';

  renderMap(v);
  renderMarket(v);
  renderRoutes(v);
  renderCargo(v);
  renderCrew(v);
  renderShipyard(v);
  renderLog();
}

/* ---------- map ---------- */

/* Projects the loader's kilometre coordinates into the viewbox. The simulation stays
 * in kilometres; fitting them to a picture is purely a front-end concern. */
function renderMap(v) {
  const body = el('map-body');
  if (!map) { body.innerHTML = '<div class="empty">Loading map…</div>'; return; }

  const W = 900, H = 520, PAD = 34;
  const xs = map.cities.map((c) => c.x);
  const ys = map.cities.map((c) => c.y);
  const minX = Math.min(...xs), maxX = Math.max(...xs);
  const minY = Math.min(...ys), maxY = Math.max(...ys);

  const sx = (x) => PAD + ((x - minX) / (maxX - minX)) * (W - PAD * 2);
  const sy = (y) => PAD + ((y - minY) / (maxY - minY)) * (H - PAD * 2);

  const hereId = v.location ? v.location.id : null;
  const reachable = new Set(v.routes.map((r) => r.toId));
  const pos = Object.fromEntries(map.cities.map((c) => [c.id, [sx(c.x), sy(c.y)]]));

  const roads = map.roads.map((r) => {
    const [x1, y1] = pos[r.fromId];
    const [x2, y2] = pos[r.toId];
    const touching = hereId && (r.fromId === hereId || r.toId === hereId);
    return `<line class="road ${r.terrainId}${touching ? ' here' : ''}"
              x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}"></line>`;
  }).join('');

  const cities = map.cities.map((c) => {
    const [x, y] = pos[c.id];
    const state_ = c.id === hereId ? 'here' : reachable.has(c.id) ? 'reachable' : '';
    const r = c.id === hereId ? 5.5 : 3.5;
    return `
      <g>
        <circle class="city-hit ${state_}" cx="${x}" cy="${y}" r="13"
                ${state_ === 'reachable' ? `data-go="${c.id}"` : ''}>
          <title>${c.name} — ${c.region}</title>
        </circle>
        <circle class="city-dot ${state_}" cx="${x}" cy="${y}" r="${r}"></circle>
        <text class="city-label ${state_}" x="${x}" y="${y - 8}" text-anchor="middle">${c.name}</text>
      </g>`;
  }).join('');

  // On the road, draw the convoy partway along the leg it is actually travelling.
  let convoy = '';
  if (v.travel) {
    const from = map.cities.find((c) => c.name === v.travel.fromName);
    const to = map.cities.find((c) => c.name === v.travel.toName);
    if (from && to) {
      const [x1, y1] = pos[from.id];
      const [x2, y2] = pos[to.id];
      const done = (v.travel.totalDays - v.travel.daysRemaining) / v.travel.totalDays;
      const cx = x1 + (x2 - x1) * done;
      const cy = y1 + (y2 - y1) * done;
      convoy = `<line class="convoy-line" x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}"></line>
                <circle class="convoy" cx="${cx}" cy="${cy}" r="5"></circle>`;
    }
  }

  body.innerHTML = `<svg viewBox="0 0 ${W} ${H}" role="img" aria-label="Trade map of Europe">
    ${roads}${convoy}${cities}</svg>`;

  body.querySelectorAll('[data-go]').forEach((n) =>
    n.addEventListener('click', () => send({ type: 'depart', toCityId: n.dataset.go })));
}

function renderMarket(v) {
  const body = el('market-body');

  if (v.travel) {
    body.innerHTML = `
      <div class="travel-note">
        <strong>${v.travel.fromName} → ${v.travel.toName}</strong>
        ${v.travel.daysRemaining} of ${v.travel.totalDays} day(s) remaining ·
        ${num(v.travel.fuelPerDay)} cr/day fuel
        <div class="row-actions" style="justify-content:center;margin-top:14px">
          <button class="go" data-wait="${v.travel.daysRemaining}">Continue to arrival</button>
          <button data-wait="1">Advance 1 day</button>
        </div>
      </div>`;
    wireWaitButtons(body);
    return;
  }

  const rows = v.market.map((g) => {
    const spread = g.buy > 0 ? ((g.sell - g.buy) / g.buy) * 100 : 0;
    return `
      <tr>
        <td class="name">${g.name}<span class="tag ${g.flow}">${g.flow}</span>
            <div class="sub">${g.tier} · ${g.unitVolume} vol/unit</div></td>
        <td class="num">${num(g.buy, 1)}</td>
        <td class="num">${num(g.sell, 1)}</td>
        <td class="num sub">${num(g.basePrice)}</td>
        <td class="num sub">${num(g.shelf)}${g.intake > 0
              ? `<div class="intake" title="Unloaded here by caravans; not for sale until the city shelves it">+${num(g.intake)} intake</div>`
              : ''}</td>
        <td class="num ${g.held > 0 ? 'held' : 'sub'}">${g.held > 0 ? num(g.held) : '—'}</td>
        <td class="num sub">${g.held > 0 ? num(g.averageCost, 1) : '—'}</td>
        <td>
          <div class="trade-cell">
            <input type="number" min="1" step="1" value="10" data-qty="${g.goodId}">
            <button data-buy="${g.goodId}">Buy</button>
            <button data-sell="${g.goodId}" ${g.held > 0 ? '' : 'disabled'}>Sell</button>
          </div>
        </td>
      </tr>`;
  }).join('');

  body.innerHTML = `
    <table>
      <thead><tr>
        <th>Commodity</th><th>Buy</th><th>Sell</th><th>Base</th>
        <th>Shelf</th><th>Held</th><th>Avg cost</th><th></th>
      </tr></thead>
      <tbody>${rows}</tbody>
    </table>
    <div class="sub" style="margin-top:8px">A city keeps two stores. <strong>Shelf</strong> is
      what it will sell you and all you can buy. What you unload goes into its intake instead,
      so it is not back on sale the same day — but the city counts it when deciding what to
      pay you.</div>`;

  body.querySelectorAll('[data-buy]').forEach((b) =>
    b.addEventListener('click', () => trade('buy', b.dataset.buy)));

  body.querySelectorAll('[data-sell]').forEach((b) =>
    b.addEventListener('click', () => trade('sell', b.dataset.sell)));
}

/* Buy the scouted cargo and set off in one action. Kept as two separate commands so
 * the simulation still sees an ordinary buy followed by an ordinary departure. */
async function loadAndGo(goodId, units, toCityId) {
  try {
    const bought = await call('/api/command', { type: 'buy', goodId, units });
    if (bought.error) { apply(bought); return; }
    apply(await call('/api/command', { type: 'depart', toCityId }));
  } catch (err) {
    showError(err.message);
  }
}

function trade(type, goodId) {
  const input = document.querySelector(`[data-qty="${goodId}"]`);
  const units = Math.max(1, parseInt(input.value, 10) || 1);
  send({ type, goodId, units });
}

function renderRoutes(v) {
  const body = el('routes-body');

  if (v.travel) {
    body.innerHTML = '<div class="empty">On the road.</div>';
    return;
  }

  const rows = v.routes.map((r) => {
    const worth = r.bestProfit > 0;
    const cargo = worth
      ? `<span class="profit">${r.bestGoodName}</span> <span class="sub">×${num(r.bestUnits)}</span>`
      : '<span class="sub">nothing pays</span>';

    return `
    <tr>
      <td class="name">${r.toName}<div class="sub">${r.toRegion} · ${r.terrainName} ·
        ${num(r.distanceKm)} km · ${r.days}d · ${num(r.estimatedFuel)} cr fuel</div></td>
      <td>${cargo}</td>
      <td class="num ${worth ? 'profit' : 'sub'}">${worth ? '+' + num(r.bestProfit) : '—'}</td>
      <td class="acts">
        ${worth ? `<button class="go" data-run="${r.toId}" data-good="${r.bestGoodId}" data-units="${r.bestUnits}">Load &amp; go</button>` : ''}
        <button data-depart="${r.toId}">Empty</button>
      </td>
    </tr>`;
  }).join('');

  body.innerHTML = `
    <table>
      <thead><tr><th>Destination</th><th>Best cargo</th><th>Est. profit</th><th></th></tr></thead>
      <tbody>${rows}</tbody>
    </table>
    <div class="sub" style="margin-top:8px">Estimates price both legs against the depth your
      order consumes, then deduct fuel and upkeep. Markets move while you travel.</div>`;

  body.querySelectorAll('[data-depart]').forEach((b) =>
    b.addEventListener('click', () => send({ type: 'depart', toCityId: b.dataset.depart })));

  body.querySelectorAll('[data-run]').forEach((b) =>
    b.addEventListener('click', () => loadAndGo(b.dataset.good, +b.dataset.units, b.dataset.run)));
}

function renderCargo(v) {
  const body = el('cargo-body');

  if (!v.cargo.length) {
    body.innerHTML = '<div class="empty">Hold is empty.</div>';
    return;
  }

  body.innerHTML = `
    <table>
      <thead><tr><th>Commodity</th><th>Units</th><th>Avg cost</th><th>Vol</th></tr></thead>
      <tbody>${v.cargo.map((c) => `
        <tr>
          <td class="name">${c.name}</td>
          <td class="num held">${num(c.units)}</td>
          <td class="num sub">${num(c.averageCost, 1)}</td>
          <td class="num sub">${num(c.volume, 1)}</td>
        </tr>`).join('')}
      </tbody>
    </table>`;
}

/* ---------- crew ---------- */

/* Every number here arrives pre-resolved from ViewBuilder: the level bars are drawn
 * from level/maxLevel and the effect lines are strings the simulation wrote. This
 * panel knows that crew exist, not what any of them do. */
function renderCrew(v) {
  const body = el('crew-body');
  const c = v.crew;

  const bar = (level, max) => {
    const pct = max > 0 ? Math.round((level / max) * 100) : 0;
    return `<span class="pips"><span style="width:${pct}%"></span></span>`;
  };

  const skills = c.skills.map((s) => `
    <tr>
      <td class="name">${s.name}<div class="sub">${s.effectText}</div></td>
      <td class="num ${s.level > 0 ? 'held' : 'sub'}">${s.level}/${s.maxLevel}</td>
      <td class="pipcell">${bar(s.level, s.maxLevel)}</td>
      <td class="sub">${s.leaderName || '—'}</td>
    </tr>`).join('');

  const roster = c.roster.length
    ? `<table>
        <thead><tr><th>Aboard</th><th>Wage</th><th>Skills</th><th></th></tr></thead>
        <tbody>${c.roster.map((m) => `
          <tr>
            <td class="name">${m.name}<div class="sub">${m.roleName} · signed d${m.hiredDay}${m.hiredAt ? ' at ' + m.hiredAt : ''}</div></td>
            <td class="num">${num(m.dailyWage)}<div class="sub">cr/day</div></td>
            <td class="sub">${skillLine(m.skills)}</td>
            <td class="acts"><button data-dismiss="${m.id}" title="Severance ${num(m.severance)} cr">Pay off</button></td>
          </tr>`).join('')}
        </tbody>
      </table>`
    : '<div class="empty">Nobody but you. Every seat is worth its wage or it is not.</div>';

  let hiring = '<div class="empty">The recruitment centre is back in a city.</div>';

  if (c.recruitment) {
    const r = c.recruitment;
    hiring = r.candidates.length
      ? `<table>
          <thead><tr><th>Available</th><th>Wage</th><th>Signing</th><th>Skills</th><th></th></tr></thead>
          <tbody>${r.candidates.map((k) => `
            <tr>
              <td class="name">${k.name}<div class="sub">${k.roleName}</div></td>
              <td class="num sub">${num(k.dailyWage)}</td>
              <td class="num ${k.affordable ? '' : 'loss'}">${num(k.signingFee)}</td>
              <td class="sub">${skillLine(k.skills)}</td>
              <td class="acts">
                <button data-hire="${k.id}" ${k.affordable && k.roomAboard ? '' : 'disabled'}>Sign on</button>
              </td>
            </tr>`).join('')}
          </tbody>
        </table>
        <div class="sub" style="margin-top:8px">${r.cityName} recruitment centre ·
          new faces in ${r.refreshInDays} day(s) · ${c.size}/${c.capacity} seats taken ·
          wages are charged every day, hired or idle.</div>`
      : `<div class="empty">Nobody is looking for work in ${r.cityName} this round.</div>`;
  }

  body.innerHTML = `
    <table class="skills">
      <thead><tr><th>Skill</th><th>Level</th><th></th><th>Best hand</th></tr></thead>
      <tbody>${skills}</tbody>
    </table>
    <h3>Payroll · ${num(c.dailyWages)} cr/day</h3>
    ${roster}
    <h3>Recruitment</h3>
    ${hiring}`;

  body.querySelectorAll('[data-hire]').forEach((b) =>
    b.addEventListener('click', () => send({ type: 'hireCrew', candidateId: b.dataset.hire })));

  body.querySelectorAll('[data-dismiss]').forEach((b) =>
    b.addEventListener('click', () => send({ type: 'dismissCrew', crewId: b.dataset.dismiss })));
}

const skillLine = (skills) =>
  skills.map((s) => `${s.name.slice(0, 3)} ${s.level}`).join(' · ');

function renderShipyard(v) {
  const body = el('shipyard-body');

  if (v.travel) {
    body.innerHTML = '<div class="empty">Reachable in a city.</div>';
    return;
  }

  body.innerHTML = `
    <table>
      <thead><tr><th>Truck</th><th>Cap</th><th>Speed</th><th>Upkeep</th><th>Price</th><th></th></tr></thead>
      <tbody>${v.shipyard.map((t) => `
        <tr>
          <td class="name">${t.name}</td>
          <td class="num sub">${num(t.capacity)}</td>
          <td class="num sub">${num(t.speedKmPerDay)}</td>
          <td class="num sub">${num(t.upkeepPerDay)}</td>
          <td class="num">${num(t.price)}</td>
          <td><button data-truck="${t.id}" ${v.cash >= t.price ? '' : 'disabled'}>Buy</button></td>
        </tr>`).join('')}
      </tbody>
    </table>
    <div class="sub" style="margin-top:8px">Convoy: ${v.convoy.trucks.join(', ')} ·
      ${num(v.convoy.speedKmPerDay)} km/day</div>`;

  body.querySelectorAll('[data-truck]').forEach((b) =>
    b.addEventListener('click', () => send({ type: 'buyTruck', truckTypeId: b.dataset.truck })));
}

function renderLog() {
  el('log-body').innerHTML = state.log
    .map((e) => `<li class="${e.kind}"><span class="day">d${e.day}</span>${e.message}</li>`)
    .join('');
}

function wireWaitButtons(scope) {
  scope.querySelectorAll('[data-wait]').forEach((b) =>
    b.addEventListener('click', () => send({ type: 'wait', days: parseInt(b.dataset.wait, 10) })));
}

/* ---------- boot ---------- */

wireWaitButtons(document);

el('btn-new').addEventListener('click', async () => {
  apply(await call('/api/new', { seed: Math.floor(Math.random() * 1e9) }));
});

call('/api/map')
  .then((m) => { map = m; return call('/api/state'); })
  .then(apply)
  .catch((err) => showError(err.message));
