/* Alpha 1 front-end.
 *
 * Holds no game rules whatsoever: it posts commands to the simulation and renders
 * whatever view comes back. That is the point of the exercise - when this is replaced
 * by a Godot scene, nothing behind the API changes.
 */

const el = (id) => document.getElementById(id);

let state = null;

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

  el('stat-where').textContent = v.travel
    ? `${v.travel.fromName} → ${v.travel.toName} · ${v.travel.daysRemaining} day(s) out`
    : v.location
      ? `${v.location.name}, ${v.location.region}`
      : '—';

  renderMarket(v);
  renderRoutes(v);
  renderCargo(v);
  renderShipyard(v);
  renderLog();
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
        <td class="num sub">${num(g.stock)}</td>
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
        <th>Stock</th><th>Held</th><th>Avg cost</th><th></th>
      </tr></thead>
      <tbody>${rows}</tbody>
    </table>`;

  body.querySelectorAll('[data-buy]').forEach((b) =>
    b.addEventListener('click', () => trade('buy', b.dataset.buy)));

  body.querySelectorAll('[data-sell]').forEach((b) =>
    b.addEventListener('click', () => trade('sell', b.dataset.sell)));
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

  const rows = v.routes.map((r) => `
    <tr>
      <td class="name">${r.toName}<div class="sub">${r.toRegion} · ${r.terrainName}</div></td>
      <td class="num sub">${num(r.distanceKm)} km</td>
      <td class="num">${r.days}d</td>
      <td class="num sub">${num(r.estimatedFuel)} cr</td>
      <td><button class="go" data-depart="${r.toId}">Go</button></td>
    </tr>`).join('');

  body.innerHTML = `
    <table>
      <thead><tr><th>Destination</th><th>Dist</th><th>Time</th><th>Fuel</th><th></th></tr></thead>
      <tbody>${rows}</tbody>
    </table>`;

  body.querySelectorAll('[data-depart]').forEach((b) =>
    b.addEventListener('click', () => send({ type: 'depart', toCityId: b.dataset.depart })));
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

call('/api/state').then(apply).catch((err) => showError(err.message));
