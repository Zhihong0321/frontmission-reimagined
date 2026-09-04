'use strict';
/* Links Keeper's Chart to MechaTrader.Core. Rendering stays in chart.html;
 * day, cash, and every move go through /api/command. */

const MECHA = (() => {
  let snap = null;
  let grid = null;
  let lastGo = '';
  let failedGo = '';
  let queued = null;
  let inflight = false;
  let waitAcc = 0;
  let waiting = false;
  let pendingWaits = 0;

  async function call(path, body) {
    const response = await fetch(path, {
      method: body === undefined ? 'GET' : 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body)
    });
    if (!response.ok) throw new Error(response.status + ' ' + response.statusText);
    return response.json();
  }

  function px(p) { return p.x ?? p.X; }
  function py(p) { return p.y ?? p.Y; }

  function alongAbs(path, t) {
    if (!path || !path.length) return null;
    t = Math.max(0, Math.min(1, t));
    if (path.length === 1) return path[0];
    let total = 0;
    const lengths = [];
    for (let i = 0; i < path.length - 1; i++) {
      const dx = px(path[i + 1]) - px(path[i]), dy = py(path[i + 1]) - py(path[i]);
      const len = Math.hypot(dx, dy);
      lengths.push(len);
      total += len;
    }
    if (total <= 0) return path[0];
    let along = t * total;
    for (let i = 0; i < lengths.length; i++) {
      if (along > lengths[i] && i < lengths.length - 1) { along -= lengths[i]; continue; }
      const u = lengths[i] <= 0 ? 1 : along / lengths[i];
      return {
        x: px(path[i]) + (px(path[i + 1]) - px(path[i])) * u,
        y: py(path[i]) + (py(path[i + 1]) - py(path[i])) * u
      };
    }
    return path[path.length - 1];
  }

  function apply(s) {
    snap = s;
    const v = s.view;
    if (!v) return;
    conv.sellOutlook = new Map((v.sellOutlook || []).map((o) => [o.cityId, o.profit]));
    conv.day = v.day;
    conv.cash = v.cash;
    if (v.travel) {
      conv.node = null;
      conv.target = null;
      conv.edge = null;
      conv.legs = [];
      conv.freeLeg = null;
      conv.surface = v.travel.fromName + ' → ' + v.travel.toName;
    } else if (v.location) {
      const c = CITY_BY_ID[v.location.id];
      conv.node = v.location.id;
      conv.target = null;
      conv.edge = null;
      conv.legs = [];
      conv.freeLeg = null;
      if (c) { conv._tx = c.x; conv._ty = c.y; }
      conv.surface = 'parked';
      lastGo = '';
      waitAcc = 0;
      pendingWaits = 0;
    } else if (v.site) {
      conv.node = null;
      const site = (v.miningSites || []).find((m) => m.id === v.site.id);
      if (site) { conv._tx = site.x - ORIGIN_X; conv._ty = site.y - ORIGIN_Y; }
      conv.surface = v.site.name;
      lastGo = '';
      waitAcc = 0;
      pendingWaits = 0;
    } else if (v.field) {
      conv.node = null;
      conv._tx = v.field.x - ORIGIN_X;
      conv._ty = v.field.y - ORIGIN_Y;
      conv.surface = 'open country · ' + (v.field.biome || '');
      lastGo = '';
      waitAcc = 0;
      pendingWaits = 0;
    }
    if (window.OPS) OPS.update(s);
  }

  /* Any command from the ops shell. Applies the snapshot even on a rejection: the
   * view is still the truth, only the error rides along. */
  function command(body) {
    return call('/api/command', body).then((s) => { apply(s); return s; });
  }

  function newGame(seed) {
    return call('/api/new', seed === undefined ? {} : { seed }).then((s) => {
      lastGo = ''; failedGo = ''; waitAcc = 0; pendingWaits = 0;
      apply(s);
      if (typeof conv._tx === 'number') { conv.x = conv._tx; conv.y = conv._ty; cam.follow = true; }
      return s;
    });
  }

  function snapshot() { return snap; }

  /* The chart drives at sub-cell resolution: Core's WorldMap.SubDiv, 12.5 km here.
   * A sub-cell destination id looks like "s<sc>,<sr>"; cities keep their plain id.
   * CELL is declared later in chart.html's inline script, so its value can only be
   * read at call time — never at bridge load time (that would throw in the TDZ). */
  const SUB = 4;
  const subStep = () => CELL / SUB;

  function subCellId(sc, sr) { return 's' + sc + ',' + sr; }

  function parseCell(id) {
    const s = id[0] === 's' ? 1 : 0;
    const i = id.indexOf(',');
    return [+id.slice(s, i), +id.slice(i + 1)];
  }

  function cellAt(x, y) {
    const w = (grid ? grid.w : W.map.width) * SUB, h = (grid ? grid.h : W.map.height) * SUB;
    const sc = Math.max(0, Math.min(w - 1, Math.floor(x / subStep()) | 0));
    const sr = Math.max(0, Math.min(h - 1, Math.floor(y / subStep()) | 0));
    return subCellId(sc, sr);
  }

  /* Walkability of a sub-cell. Terrain is per parent 50 km cell; mountain (M) is now
   * slow but passable off-road, only water (W) and deep (D) stay walls. */
  function landAt(sc, sr) {
    const w = grid ? grid.w : W.map.width, h = grid ? grid.h : W.map.height;
    const c = (sc / SUB) | 0, r = (sr / SUB) | 0;
    if (c < 0 || r < 0 || c >= w || r >= h) return false;
    if (grid) {
      const i = r * grid.w + c;
      if (grid.roads[i] === '1') return true;
      const b = grid.biomes[i];
      return b !== 'W' && b !== 'D';
    }
    const x = (c + 0.5) * CELL, y = (r + 0.5) * CELL;
    const biome = biomeAt(x, y);
    if (biome === BIOME.water || biome === BIOME.deep) {
      const road = nearestRoad(x, y);
      return !!(road && road.d < 10);
    }
    return true;
  }

  function nearestLand(sc0, sr0) {
    if (landAt(sc0, sr0)) return subCellId(sc0, sr0);
    for (let rad = 1; rad <= SUB * 2; rad++) {          // ~100 km, not 400
      for (let dc = -rad; dc <= rad; dc++) {
        for (let dr = -rad; dr <= rad; dr++) {
          if (Math.max(Math.abs(dc), Math.abs(dr)) !== rad) continue;
          if (landAt(sc0 + dc, sr0 + dr)) return subCellId(sc0 + dc, sr0 + dr);
        }
      }
    }
    return null;
  }

  function go(id, silent) {
    if (!id) return;
    if (id === lastGo) return;
    if (silent && id === failedGo) return;
    if (inflight) { queued = { id, silent: !!silent }; return; }
    inflight = true;
    queued = null;
    return call('/api/command', { type: 'depart', toId: id })
      .then((s) => {
        if (s.error) {
          lastGo = '';
          failedGo = id;
          if (!silent) toast(s.error, 'alert');
          return;
        }
        lastGo = id;
        failedGo = '';
        waitAcc = 0;
        pendingWaits = 0;
        cam.follow = true;
        apply(s);
      })
      .catch((err) => {
        lastGo = '';
        if (!silent) toast(String(err.message || err), 'alert');
      })
      .finally(() => {
        inflight = false;
        if (queued && queued.id !== lastGo) {
          const next = queued;
          queued = null;
          go(next.id, next.silent);
        } else queued = null;
      });
  }

  function steer(ix, iy) {
    if (conv.paused) return;
    hideCard();
    const len = Math.hypot(ix, iy) || 1;
    const nx = ix / len, ny = iy / len;
    const here = cellAt(conv.x, conv.y);

    // Already converging on a sub-cell target in this direction: keep pushing.
    if (lastGo && lastGo[0] === 's') {
      const [c, r] = parseCell(lastGo);
      const dx = (c + 0.5) * subStep() - conv.x, dy = (r + 0.5) * subStep() - conv.y;
      if (dx * nx + dy * ny > 0 && Math.hypot(dx, dy) > subStep() * 1.5) return;
    }

    // Target the farthest walkable sub-cell within ~2 parent cells ahead: fine,
    // responsive steps instead of one 50 km cell-hop per keypress.
    let pick = null;
    for (let step = SUB * 2; step >= 1; step--) {
      const id = cellAt(conv.x + nx * subStep() * step, conv.y + ny * subStep() * step);
      if (id === here) continue;
      const [c, r] = parseCell(id);
      if (!landAt(c, r)) continue;
      pick = id;
      break;
    }
    if (pick) go(pick, true);
  }

  function onRoad() {
    return !!(snap && snap.view && snap.view.travel);
  }

  function tick(dt) {
    const v = snap && snap.view;
    if (v && v.travel && v.travel.path && v.travel.path.length) {
      if (!conv.paused) waitAcc += dt * (conv.pace || 1);
      const total = Math.max(1, v.travel.totalDays);
      const remaining = v.travel.daysRemaining;
      const extra = waitAcc / SEC_PER_DAY + pendingWaits;
      const frac = Math.min(1, (total - remaining + extra) / total);
      const p = alongAbs(v.travel.path, frac);
      if (p) {
        conv._tx = px(p) - ORIGIN_X;
        conv._ty = py(p) - ORIGIN_Y;
      }
      if (!conv.paused && waitAcc >= SEC_PER_DAY && !waiting && !inflight) {
        waitAcc -= SEC_PER_DAY;
        pendingWaits++;
        waiting = true;
        call('/api/command', { type: 'wait', days: 1 })
          .then((s) => {
            pendingWaits = Math.max(0, pendingWaits - 1);
            if (s.error) toast(s.error, 'alert');
            else apply(s);
          })
          .catch((err) => toast(String(err.message || err), 'alert'))
          .finally(() => { waiting = false; });
      }
    }

    if (typeof conv._tx === 'number') {
      conv.x += (conv._tx - conv.x) * Math.min(1, dt * 8);
      conv.y += (conv._ty - conv.y) * Math.min(1, dt * 8);
      const dx = conv._tx - conv.x, dy = conv._ty - conv.y;
      if (Math.hypot(dx, dy) > 0.4) conv.ang = Math.atan2(dy, dx);
    }
  }

  function drawRoute() {
    const v = snap && snap.view;
    if (!v || !v.travel || !v.travel.path || v.travel.path.length < 2) return;
    const path = v.travel.path;
    let best = 0, bd = 1e9;
    for (let i = 0; i < path.length; i++) {
      const d = Math.hypot(px(path[i]) - ORIGIN_X - conv.x, py(path[i]) - ORIGIN_Y - conv.y);
      if (d < bd) { bd = d; best = i; }
    }
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    ctx.strokeStyle = 'rgba(224,160,48,0.95)';
    ctx.lineWidth = 3.2 / cam.z;
    ctx.beginPath();
    ctx.moveTo(conv.x, conv.y);
    for (let i = best; i < path.length; i++) {
      ctx.lineTo(px(path[i]) - ORIGIN_X, py(path[i]) - ORIGIN_Y);
    }
    ctx.stroke();
  }

  function hud() {
    if (!snap || !snap.view) return false;
    const v = snap.view;
    setText('s-day', v.day);
    setText('s-cash', Math.round(v.cash).toLocaleString() + ' cr', v.cash < 5000 || v.bankrupt ? 'alert' : '');
    setText('s-burn', Math.round(v.convoy.dailyUpkeep) + ' cr');
    setText('s-pace', conv.paused ? 'paused' : conv.pace + '×', conv.paused ? 'alert' : '');
    setText('s-road', conv.surface);
    if (v.location) setText('s-pos', v.location.name + ', ' + v.location.region, '');
    else if (v.travel) setText('s-pos', 'On the road → ' + v.travel.toName, 'amber');
    else setText('s-pos', conv.surface, 'amber');
    return true;
  }

  function clickWorld(sx, sy) {
    hideCard();
    const w = toWorld(sx, sy);
    const cityId = pickCity(sx, sy);
    if (cityId) {
      lastGo = '';
      go(cityId);
      return;
    }
    const sw = (grid ? grid.w : W.map.width) * SUB, sh = (grid ? grid.h : W.map.height) * SUB;
    const sc = Math.max(0, Math.min(sw - 1, Math.floor(w.x / subStep()) | 0));
    const sr = Math.max(0, Math.min(sh - 1, Math.floor(w.y / subStep()) | 0));
    const id = nearestLand(sc, sr);
    if (!id) { toast('Impassable. Find a road or open ground.', 'alert'); return; }
    lastGo = '';
    go(id);
  }

  function boot() {
    return Promise.all([call('/api/state'), call('/api/map')])
      .then(([s, m]) => {
        grid = { w: m.width, h: m.height, biomes: m.biomes, roads: m.roadsMask };
        apply(s);
        if (typeof conv._tx === 'number') {
          conv.x = conv._tx; conv.y = conv._ty;
          cam.x = conv.x; cam.y = conv.y;
        }
        toast('House books linked. WASD drives. Click anywhere to pathfind.');
      })
      .catch((err) => toast('Game server not linked: ' + err.message, 'alert'));
  }

  return { go, steer, tick, hud, boot, cellAt, clickWorld, drawRoute, onRoad, command, newGame, snapshot };
})();
window.MECHA = MECHA;
