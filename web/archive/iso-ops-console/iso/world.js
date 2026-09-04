'use strict';
/* MechaTrader world on top of the K3 isometric renderer.
 * SCALE stretches kilometres into K3 world units so cities sit as compact
 * districts on a wide field — the old 1km=1u packing is what made them fuse.
 * The convoy is a volume truck that drives the sim path; the camera follows. */

const IsoMap = (() => {
  const SCALE = 50;                   /* 1 km = 50 world units (50× the first pass) */
  const cam = { x: 0, y: 0 };
  const vis = { x: 0, y: 0, face: 0, on: false };
  let originX = 0, originY = 0, cellKm = 50, mapW = 0, mapH = 0;
  let biomes = '';
  let cities = [];
  let sites = [];
  let view = null;
  let selected = null;
  let reachable = new Set();
  let eventCities = new Set();
  let dragging = false, dragX = 0, dragY = 0, didDrag = false;
  let lastCamX = 0, lastCamY = 0;
  let running = false;
  let framed = false;
  let lastT = 0;
  let camHold = 0;
  let onPick = null;
  let onDbl = null;
  const keys = {};
  let truckVols = null;
  const CITYKIT = [];

  const wx = (x) => (x - originX) * SCALE;
  const wy = (y) => (y - originY) * SCALE;

  function load(mapData) {
    originX = mapData.originX; originY = mapData.originY;
    cellKm = mapData.cellKm; mapW = mapData.width; mapH = mapData.height;
    biomes = mapData.biomes || '';
    cities = (mapData.cities || []).map((c) => ({
      id: c.id, name: c.name, region: c.region,
      x: c.x, y: c.y, wx: wx(c.x), wy: wy(c.y)
    }));
    const pos = Object.fromEntries(cities.map((c) => [c.id, c]));

    MPROP = [];
    PATH = [];
    CITYKIT.length = 0;
    chunks.clear();
    sprites.clear();

    (mapData.roads || []).forEach((r) => {
      const a = pos[r.fromId], b = pos[r.toId];
      if (!a || !b) return;
      PATH.push({ x0: a.wx, y0: a.wy, x1: b.wx, y1: b.wy, w: 28 });
    });

    const boot = () => {
      cities.forEach((c) => placeCity(c));
      dressRoads();
      buildHash();
      truckVols = buildTruck();
      fit();
      if (cities.length) { cam.x = cities[0].wx; cam.y = cities[0].wy; }
      const farBoot = typeof location !== 'undefined' && (
        location.hash === '#far' || /(?:\?|&)far=1(?:&|$)/.test(location.search)
      );
      if (farBoot) setZoom(0);
      chunkBudget = 128; bakeBudget = 96;
      if (!farBoot) {
        groundPass(cam.x, cam.y * tilt, tilt);
        gather(cam.x, cam.y, tilt);
        gatherKit(cam.x, cam.y, tilt);
        for (let i = 0; i < VIS.length; i++) {
          if (!VIS[i].kit) getSprite(VIS[i].spec, VIS[i].vi, tiltQ(), 0);
        }
      }
      if (!running) { running = true; requestAnimationFrame(frame); }
    };

    if (typeof MapKit !== 'undefined') MapKit.load('art/map/kit.json?v=30').then(boot);
    else boot();
  }

  function placeCity(c) {
    const R = rngOf(idSeed(c.id) ^ 0x9e3779b9);
    const spanX = 360, spanY = 300;
    const cols = 4, rows = 3;
    const lotW = spanX / cols, lotH = spanY / rows;
    const useKit = typeof MapKit !== 'undefined' && MapKit.ready();
    for (let i = 0; i < cols; i++) for (let j = 0; j < rows; j++) {
      if ((i === 1 || i === 2) && j === 1) continue;
      if (R() < 0.10) continue;
      const x = c.wx - spanX / 2 + (i + 0.5) * lotW + (R() - .5) * 12;
      const y = c.wy - spanY / 2 + (j + 0.5) * lotH + (R() - .5) * 10;
      if (useKit) {
        const a = MapKit.pick({ place: 'city-lot' }, R);
        if (a) { CITYKIT.push({ asset: a, x: x, y: y, sc: .88 + R() * .28 }); continue; }
      }
      const roll = R();
      const spec = roll < .72 ? SPECIES.bldg : roll < .86 ? SPECIES.ruin : SPECIES.crate;
      emit(spec, x, y, (R() * spec.vari) | 0, .95 + R() * .4, 'CITY');
    }
    if (!useKit && R() < .75) emit(SPECIES.pylon, c.wx + (R() - .5) * 110, c.wy + (R() - .5) * 80,
      (R() * SPECIES.pylon.vari) | 0, .95 + R() * .25, 'CITY');
    for (let k = 0; k < 4 + (R() * 4 | 0); k++) {
      emit(SPECIES.rubble, c.wx + (R() - .5) * 200, c.wy + (R() - .5) * 160,
        (R() * SPECIES.rubble.vari) | 0, .7 + R() * .5, 'DECAL');
    }
  }

  function biomeAtWorld(px, py) {
    if (!biomes || !mapW) return 'P';
    const col = Math.floor((px / SCALE) / cellKm);
    const row = Math.floor((py / SCALE) / cellKm);
    if (col < 0 || row < 0 || col >= mapW || row >= mapH) return 'W';
    return biomes[row * mapW + col] || 'P';
  }

  function inTown(px, py) {
    for (let i = 0; i < cities.length; i++) {
      if (Math.abs(cities[i].wx - px) < 200 && Math.abs(cities[i].wy - py) < 170) return true;
    }
    return false;
  }

  const TONE = {
    P: { gtone: 'steel' }, F: { gtone: 'moss' }, H: { gtone: 'violet' },
    M: { gtone: 'steel' }, A: { gtone: 'amber' }, T: { gtone: 'azure' },
    S: { gtone: 'jade' }, W: { gtone: 'azure' }, D: { gtone: 'ink' }
  };

  function paintWorldChunk(g, X, Y, S) {
    const b = biomeAtWorld(X + S * .5, Y + S * .5);
    const tone = TONE[b] || TONE.P;
    if (inTown(X + S * .5, Y + S * .5)) {
      GROUND.blocks(g, X, Y, S, { gtone: 'steel' });
    } else if (b === 'A') {
      GROUND.dune(g, X, Y, S, tone);
    } else if (b === 'T' || b === 'W' || b === 'D') {
      GROUND.ice(g, X, Y, S, tone);
    } else if (b === 'M') {
      GROUND.plate(g, X, Y, S, tone);
    } else {
      GROUND.soil(g, X, Y, S, tone);
    }
    scatterWild(X - 40, Y - 40, X + S + 40, Y + S + 40, true, (spec, x, y, vi, sc) => {
      const r = variantR(spec, vi) * sc;
      g.save(); g.translate(x, y);
      const vols = variantVols(spec, vi, r);
      for (let i = 0; i < vols.length; i++) drawFlat(g, vols[i], 1);
      g.restore();
    });
  }
  window.paintWorldChunk = paintWorldChunk;

  function pickWild(b, layer, R, copse) {
    if (layer === 0) {
      if (b === 'F') return R() > .32 ? SPECIES.pine : SPECIES.tree;
      if (b === 'S') return R() > .35 ? SPECIES.dead : SPECIES.tree;
      if (b === 'P') {
        if (copse > .42) return R() > .35 ? SPECIES.pine : SPECIES.tree;
        return R() > .4 ? SPECIES.tree : SPECIES.rock;
      }
      if (b === 'H') return R() > .45 ? SPECIES.tree : SPECIES.rock;
      return null;
    }
    if (layer === 1) {
      if (b === 'M' || b === 'H') return R() > .4 ? SPECIES.boulder : SPECIES.rock;
      if (b === 'A') return R() > .45 ? SPECIES.rock : SPECIES.dead;
      if (b === 'T') return SPECIES.berg;
      if (b === 'F') return R() > .5 ? SPECIES.bush : SPECIES.log;
      if (b === 'P') return R() > .62 ? SPECIES.rock : SPECIES.bush;
      if (b === 'S') return SPECIES.dead;
      return null;
    }
    if (b === 'M') return SPECIES.mesa;
    if (b === 'P' && R() > .72) return SPECIES.ruin;
    if (b === 'F' && R() > .7) return SPECIES.pine;
    if (b === 'H') return SPECIES.boulder;
    if (b === 'A') return SPECIES.rock;
    return null;
  }

  function scatterWild(x0, y0, x1, y1, flats, cb) {
    const layers = [
      { step: 64, dens: .72 },
      { step: 108, dens: .48 },
      { step: 190, dens: .28 }
    ];
    for (let L = 0; L < layers.length; L++) {
      const step = layers[L].step, dens = layers[L].dens;
      const i0 = Math.floor(x0 / step) - 1, i1 = Math.floor(x1 / step) + 1;
      const j0 = Math.floor(y0 / step) - 1, j1 = Math.floor(y1 / step) + 1;
      for (let i = i0; i <= i1; i++) for (let j = j0; j <= j1; j++) {
        const n = hsh(i * 1.71 + step * .01, j * 2.33 + L * 9.1);
        if (n > dens) continue;
        const x = (i + .5) * step + (hsh(i, j) - .5) * step * .72;
        const y = (j + .5) * step + (hsh(j, i) - .5) * step * .72;
        if (x < x0 - 30 || x > x1 + 30 || y < y0 - 30 || y > y1 + 30) continue;
        if (inTown(x, y)) continue;
        const b = biomeAtWorld(x, y);
        if (!b || b === 'W' || b === 'D') continue;
        if (flats && L === 0 && n < .28) {
          const Rf = rngOf((i * 11) ^ (j * 29) ^ 91);
          cb(SPECIES.rubble, x, y, (Rf() * SPECIES.rubble.vari) | 0, .6 + Rf() * .5);
          continue;
        }
        const R = rngOf((i * 73856093) ^ (j * 19349663) ^ (L * 17 + 3));
        const copse = vnoise(x * .0034, y * .0034, 2.1);
        const spec = pickWild(b, L, R, copse);
        if (!spec) continue;
        if (flats) {
          if (!spec.flat) continue;
        } else if (spec.flat) continue;
        const vi = (R() * spec.vari) | 0;
        const sc = .82 + R() * .5;
        cb(spec, x, y, vi, sc);
      }
    }
  }

  function gatherWild(cx, cy, t) {
    const halfW = W / 2 + 180, halfH = H / 2 / t + 340;
    scatterWild(cx - halfW, cy - halfH, cx + halfW, cy + halfH, false, (spec, x, y, vi, sc) => {
      const r = variantR(spec, vi) * sc;
      VIS.push({ x: x, y: y, sy: y + r * .5, r: r, vi: vi, sc: sc, spec: spec, key: spec.id + '|' + ((x * 13) | 0) + ',' + ((y * 17) | 0) });
    });
    VIS.sort((a, b) => a.sy - b.sy);
  }

  function pushKit(asset, x, y, sc) {
    VIS.push({
      kit: asset, x: x, y: y, sc: sc,
      sy: y + (asset.footprint || 40) * .3,
      r: asset.footprint || 40
    });
  }

  function runRecipe(name, x, y, biome, R) {
    if (name === 'copse') {
      const a = MapKit.pick({ kind: 'tree', biome: biome }, R);
      if (a) pushKit(a, x, y, .85 + R() * .4);
      return;
    }
    if (name === 'scree') {
      const a = MapKit.pick({ tags: ['scree'], biome: biome }, R);
      if (a) pushKit(a, x, y, .8 + R() * .5);
      return;
    }
    if (name !== 'mountain') return;
    const rec = MapKit.recipe('mountain');
    if (!rec) return;
    const bodyN = rec.bodyCount[0] + ((R() * (rec.bodyCount[1] - rec.bodyCount[0] + 1)) | 0);
    for (let i = 0; i < bodyN; i++) {
      const a = MapKit.pick({ tags: rec.body.tags, biome: biome }, R);
      const off = rec.offsets.body[i % rec.offsets.body.length];
      if (a) pushKit(a, x + off[0], y + off[1], .9 + R() * .25);
    }
    const cap = MapKit.pick({ tags: rec.cap.tags, biome: biome }, R);
    if (cap) pushKit(cap, x + rec.offsets.cap[0], y + rec.offsets.cap[1], .95 + R() * .2);
    const screeN = rec.screeCount[0] + ((R() * (rec.screeCount[1] - rec.screeCount[0] + 1)) | 0);
    for (let i = 0; i < screeN; i++) {
      const a = MapKit.pick({ tags: rec.scree.tags, biome: biome }, R);
      const off = rec.offsets.scree[i % rec.offsets.scree.length];
      if (a) pushKit(a, x + off[0], y + off[1], .7 + R() * .4);
    }
  }

  function gatherKit(cx, cy, t) {
    if (typeof MapKit === 'undefined' || !MapKit.ready()) return;
    const halfW = W / 2 + 200, halfH = H / 2 / t + 360;
    const x0 = cx - halfW, x1 = cx + halfW, y0 = cy - halfH, y1 = cy + halfH;
    for (let i = 0; i < CITYKIT.length; i++) {
      const p = CITYKIT[i];
      if (p.x < x0 || p.x > x1 || p.y < y0 || p.y > y1) continue;
      pushKit(p.asset, p.x, p.y, p.sc);
    }
    const seen = {};
    const fills = MapKit.biomeFill;
    const biomesHit = {};
    const samples = [
      [cx, cy], [x0, y0], [x1, y0], [x0, y1], [x1, y1]
    ];
    for (let s = 0; s < samples.length; s++) {
      const b = biomeAtWorld(samples[s][0], samples[s][1]);
      if (b) biomesHit[b] = true;
    }
    for (const b in biomesHit) {
      const rules = typeof fills === 'function' ? fills(b) : [];
      for (let r = 0; r < rules.length; r++) {
        const rule = rules[r];
        const step = rule.step, dens = rule.dens;
        const i0 = Math.floor(x0 / step) - 1, i1 = Math.floor(x1 / step) + 1;
        const j0 = Math.floor(y0 / step) - 1, j1 = Math.floor(y1 / step) + 1;
        for (let i = i0; i <= i1; i++) for (let j = j0; j <= j1; j++) {
          const key = b + ':' + rule.recipe + ':' + i + ',' + j;
          if (seen[key]) continue;
          seen[key] = 1;
          const n = hsh(i * 1.71 + step * .02, j * 2.33 + r * 8.1);
          if (n > dens) continue;
          const x = (i + .5) * step + (hsh(i, j) - .5) * step * .7;
          const y = (j + .5) * step + (hsh(j, i) - .5) * step * .7;
          if (x < x0 - 40 || x > x1 + 40 || y < y0 - 40 || y > y1 + 40) continue;
          if (inTown(x, y)) continue;
          if (biomeAtWorld(x, y) !== b) continue;
          const R = rngOf((i * 73856093) ^ (j * 19349663) ^ (r + 11));
          runRecipe(rule.recipe, x, y, b, R);
        }
      }
    }
    VIS.sort((a, b) => a.sy - b.sy);
  }

  function dressRoads() {
    PATH.forEach((p, i) => {
      const L = Math.hypot(p.x1 - p.x0, p.y1 - p.y0);
      const n = Math.max(1, (L / 220) | 0);
      const R = rngOf(9001 + i * 17);
      for (let k = 1; k < n; k++) {
        if (R() < .45) continue;
        const t = k / n;
        const x = p.x0 + (p.x1 - p.x0) * t + (R() - .5) * 80;
        const y = p.y0 + (p.y1 - p.y0) * t + (R() - .5) * 80;
        const spec = R() > .7 ? SPECIES.crate : SPECIES.rock;
        emit(spec, x, y, (R() * spec.vari) | 0, .6 + R() * .4, 'ROAD');
      }
    });
  }

  function placeSites(list) {
    MPROP = MPROP.filter((p) => p.tag !== 'MINE');
    sites = list || [];
    sites.forEach((s) => {
      const x = wx(s.x), y = wy(s.y);
      const R = rngOf(idSeed(s.id));
      emit(SPECIES.crate, x, y, (R() * SPECIES.crate.vari) | 0, 1.1, 'MINE');
      emit(SPECIES.pylon, x + 36, y - 16, (R() * SPECIES.pylon.vari) | 0, .9, 'MINE');
      emit(SPECIES.contain, x - 30, y + 20, (R() * SPECIES.contain.vari) | 0, .95, 'MINE');
    });
    buildHash();
    chunks.clear();
  }

  function idSeed(id) {
    let s = 2166136261;
    for (let i = 0; i < id.length; i++) s = Math.imul(s ^ id.charCodeAt(i), 16777619);
    return s >>> 0;
  }

  function buildTruck() {
    const R = rngOf(77);
    const v = [];
    v.push(V_(bx(52, 24), 0, 12, 'steel', { mat: 'metal', face: 'rivet' }));
    v.push(V_(bx(20, 24, 18, 0), 12, 28, 'amber', { mat: 'metal', face: 'win', lit: 'amber', litD: .45 }));
    v.push(V_(bx(26, 22, -10, 0), 12, 26, 'panel-3', { mat: 'metal', face: 'corrug' }));
    for (const s of [-1, 1]) {
      v.push(V_(shift(ngon(R, 8, 5.5, .08, .9), -16, s * 14), 0, 8, 'ink-4', { mat: 'metal' }));
      v.push(V_(shift(ngon(R, 8, 5.5, .08, .9), 16, s * 14), 0, 8, 'ink-4', { mat: 'metal' }));
    }
    v.push(V_(bx(5, 5, 26, 0), 20, 30, 'amber', { lum: .4, emit: 10, edge: 0 }));
    return v;
  }

  function sync(v, sel) {
    view = v;
    selected = sel;
    reachable = new Set((v.routes || []).map((r) => r.toId));
    eventCities = new Set(v.eventCityIds || []);
    const nextSites = v.miningSites || [];
    if (sites.length !== nextSites.length || nextSites.some((s, i) => !sites[i] || sites[i].id !== s.id))
      placeSites(nextSites);
    const p = simPos();
    if (!vis.on) { vis.x = p.x; vis.y = p.y; vis.face = 0; vis.on = true; }
    if (!framed) { cam.x = p.x; cam.y = p.y; framed = true; }
  }

  function pathPts() {
    const t = view && view.travel;
    if (!t || !t.path || t.path.length < 1) return [];
    return t.path.map((p) => ({ x: wx(p.x), y: wy(p.y) }));
  }

  function along(pts, u) {
    if (!pts.length) return { x: vis.x, y: vis.y };
    if (pts.length === 1) return pts[0];
    u = clamp(u, 0, 1);
    const lens = [];
    let total = 0;
    for (let i = 0; i < pts.length - 1; i++) {
      const d = Math.hypot(pts[i + 1].x - pts[i].x, pts[i + 1].y - pts[i].y);
      lens.push(d); total += d;
    }
    if (total <= 0) return pts[0];
    let walk = u * total;
    for (let i = 0; i < lens.length; i++) {
      if (walk > lens[i] && i < lens.length - 1) { walk -= lens[i]; continue; }
      const f = lens[i] > 0 ? walk / lens[i] : 0;
      return {
        x: pts[i].x + (pts[i + 1].x - pts[i].x) * f,
        y: pts[i].y + (pts[i + 1].y - pts[i].y) * f
      };
    }
    return pts[pts.length - 1];
  }

  function simPos() {
    if (!view) return cities[0] ? { x: cities[0].wx, y: cities[0].wy } : { x: cam.x, y: cam.y };
    if (view.travel) {
      const t = view.travel;
      const pts = pathPts();
      if (pts.length >= 2) {
        const done = t.totalDays <= 0 ? 1 : (t.totalDays - t.daysRemaining) / t.totalDays;
        return along(pts, done);
      }
      return { x: wx(t.convoyX ?? 0), y: wy(t.convoyY ?? 0) };
    }
    if (view.location) {
      const c = cities.find((x) => x.id === view.location.id);
      if (c) return { x: c.wx, y: c.wy };
    }
    if (view.site) return { x: wx(view.site.x), y: wy(view.site.y) };
    if (view.field) return { x: wx(view.field.x), y: wy(view.field.y) };
    return { x: vis.x, y: vis.y };
  }

  function drive(dt) {
    const tgt = simPos();
    const dx = tgt.x - vis.x, dy = tgt.y - vis.y;
    const d = Math.hypot(dx, dy);
    if (d < 0.8) { vis.x = tgt.x; vis.y = tgt.y; return d; }
    const catchT = clamp(d / (900 * SCALE / 50), 0.45, 3.2);
    const step = d * (1 - Math.exp(-dt / catchT));
    vis.x += dx / d * step;
    vis.y += dy / d * step;
    vis.face += wrapAng(Math.atan2(dy, dx) - vis.face) * clamp(dt * 8, 0, 1);
    return d;
  }

  function wrapAng(a) {
    a = (a + PI) % TAU; if (a < 0) a += TAU; return a - PI;
  }

  function screenToWorld(sx, sy) {
    const x = sx / ZOOM - lastCamX;
    const y = (sy / ZOOM - lastCamY) / tilt;
    return { x, y };
  }

  function pickAt(sx, sy) {
    const p = screenToWorld(sx, sy);
    let best = null, bestD = Math.max(220, 28 / ZOOM);
    cities.forEach((c) => {
      const d = Math.hypot(c.wx - p.x, c.wy - p.y);
      if (d < bestD) { bestD = d; best = { kind: 'city', id: c.id }; }
    });
    sites.forEach((s) => {
      const d = Math.hypot(wx(s.x) - p.x, wy(s.y) - p.y);
      if (d < bestD) { bestD = d; best = { kind: 'site', id: s.id }; }
    });
    if (best) return best;
    const col = Math.floor((p.x / SCALE) / cellKm);
    const row = Math.floor((p.y / SCALE) / cellKm);
    if (col >= 0 && row >= 0 && col < mapW && row < mapH)
      return { kind: 'cell', id: col + ',' + row };
    return null;
  }

  function drawRoute(t) {
    const pts = pathPts();
    if (pts.length < 2) return;
    ctx.save();
    ctx.strokeStyle = C.amber; ctx.globalAlpha = .55; ctx.lineWidth = 3 * LW;
    ctx.setLineDash([14, 10]); ctx.lineCap = 'round';
    ctx.beginPath();
    ctx.moveTo(pts[0].x, pts[0].y * t);
    for (let i = 1; i < pts.length; i++) ctx.lineTo(pts[i].x, pts[i].y * t);
    ctx.stroke();
    ctx.setLineDash([]); ctx.restore();
  }

  function drawRoadsLive(t) {
    if (!PATH.length) return;
    const b = col(BIO.gtone);
    ctx.save();
    ctx.lineCap = 'round';
    for (const p of PATH) {
      ctx.strokeStyle = shade(b, -.55); ctx.lineWidth = p.w;
      ctx.beginPath(); ctx.moveTo(p.x0, p.y0 * t); ctx.lineTo(p.x1, p.y1 * t); ctx.stroke();
      ctx.strokeStyle = shade(b, -.68); ctx.lineWidth = p.w * .48;
      ctx.beginPath(); ctx.moveTo(p.x0, p.y0 * t); ctx.lineTo(p.x1, p.y1 * t); ctx.stroke();
    }
    ctx.restore();
  }

  function drawFarTowns(t) {
    const pad = W * .7;
    cities.forEach((c) => {
      if (Math.abs(c.wx - cam.x) > pad || Math.abs(c.wy - cam.y) > pad / Math.max(t, .2)) return;
      ctx.save();
      ctx.translate(c.wx, c.wy * t);
      drawVol(ctx, V_(bx(140, 110), 0, 42, 'steel', { mat: 'metal', face: 'rivet' }), t);
      drawVol(ctx, V_(bx(70, 55, 30, 10), 42, 78, 'panel-3', { mat: 'metal', face: 'win' }), t);
      ctx.restore();
    });
  }

  function drawConvoy(t, t2, moving) {
    if (!vis.on) return;
    const gy = vis.y * t;
    const far = ZOOM < 0.62;
    ctx.save();
    ctx.globalAlpha = .40; ctx.fillStyle = '#000';
    ctx.beginPath(); ctx.ellipse(vis.x, gy + 3, far ? 18 / ZOOM : 24, (far ? 18 / ZOOM : 24) * t, 0, 0, TAU); ctx.fill();
    ctx.restore();

    if (moving) {
      ctx.save(); ctx.globalCompositeOperation = 'lighter';
      const r = far ? 22 / ZOOM : 38;
      const g = ctx.createRadialGradient(vis.x, gy + 2, 2, vis.x, gy + 2, r);
      g.addColorStop(0, 'rgba(217,161,60,.22)');
      g.addColorStop(1, 'rgba(217,161,60,0)');
      ctx.fillStyle = g;
      ctx.beginPath(); ctx.ellipse(vis.x, gy + 2, r, r * t, 0, 0, TAU); ctx.fill();
      ctx.restore();
    }

    const truck = typeof MapKit !== 'undefined' && MapKit.ready() ? MapKit.asset('convoy-truck') : null;
    if (truck) {
      MapKit.draw(ctx, truck, vis.x, vis.y, t, 1);
    } else if (far || !truckVols) {
      const s = 14 / ZOOM;
      ctx.save();
      ctx.translate(vis.x, gy);
      ctx.rotate(vis.face);
      ctx.fillStyle = C.amber;
      ctx.beginPath();
      ctx.moveTo(s * 1.4, 0);
      ctx.lineTo(-s * .9, s * .7 * t);
      ctx.lineTo(-s * .5, 0);
      ctx.lineTo(-s * .9, -s * .7 * t);
      ctx.closePath();
      ctx.fill();
      ctx.restore();
    } else {
      ctx.save();
      ctx.translate(vis.x, gy);
      const vols = truckVols.map((V) => {
        const nv = Object.assign({}, V);
        nv.p = rot(V.p, vis.face);
        return nv;
      });
      vols.sort(volSort);
      for (let i = 0; i < vols.length; i++) drawVol(ctx, vols[i], t);
      ctx.restore();
    }

    const pulse = .5 + .5 * Math.sin(t2 * 4);
    const ring = far ? 16 / ZOOM : 34;
    ctx.save();
    ctx.globalAlpha = .25 + .2 * pulse;
    ctx.strokeStyle = C.amber; ctx.lineWidth = 1.6 * LW;
    ctx.beginPath(); ctx.ellipse(vis.x, gy, ring, ring * t, 0, 0, TAU); ctx.stroke();
    ctx.restore();
  }

  function drawOverlayLabels(t) {
    const hereId = view && view.location ? view.location.id : (view && view.site ? view.site.id : null);
    const reach = W * 1.2;
    ctx.font = '700 11px ui-monospace, Cascadia Mono, Consolas, monospace';
    ctx.textAlign = 'center'; ctx.textBaseline = 'bottom';
    cities.forEach((c) => {
      if (Math.hypot(c.wx - cam.x, (c.wy - cam.y) * t) > reach) return;
      const x = c.wx, y = c.wy * t - 58;
      const isHere = c.id === hereId;
      const isSel = selected && selected.kind === 'city' && selected.id === c.id;
      const isReach = reachable.has(c.id);
      const news = eventCities.has(c.id);
      if (isHere || isSel) {
        ctx.strokeStyle = C.amber; ctx.lineWidth = 1.6; ctx.globalAlpha = .85;
        ctx.beginPath(); ctx.ellipse(c.wx, c.wy * t, 52, 52 * t, 0, 0, TAU); ctx.stroke();
      } else if (isReach) {
        ctx.strokeStyle = C.jade; ctx.lineWidth = 1.2; ctx.globalAlpha = .5;
        ctx.beginPath(); ctx.ellipse(c.wx, c.wy * t, 42, 42 * t, 0, 0, TAU); ctx.stroke();
      }
      ctx.globalAlpha = 1;
      ctx.fillStyle = 'rgba(5,7,10,.72)';
      const w = ctx.measureText(c.name).width + 10;
      ctx.fillRect(x - w / 2, y - 14, w, 16);
      ctx.fillStyle = isHere || isSel ? C.amber : isReach ? C.ink : C['ink-2'];
      ctx.fillText(c.name, x, y);
      if (news) { ctx.fillStyle = C.amber; ctx.fillText('●', x + w / 2 + 4, y); }
    });
    sites.forEach((s) => {
      const sx = wx(s.x), sy = wy(s.y);
      if (Math.hypot(sx - cam.x, (sy - cam.y) * t) > reach) return;
      const isSel = selected && selected.kind === 'site' && selected.id === s.id;
      ctx.fillStyle = s.depleted ? C['ink-4'] : C.amber;
      ctx.font = '700 9px ui-monospace, Consolas, monospace';
      ctx.fillText(isSel ? '◆ ' + s.name : '◆', sx, sy * t - 32);
    });
  }

  function frame(t2) {
    requestAnimationFrame(frame);
    const dt = lastT ? Math.min(.05, (t2 - lastT) / 1000) : .016;
    lastT = t2;
    tilt += (tiltT - tilt) * .12;
    bakeBudget = 12; chunkBudget = 10;
    const panning = panKeys(dt);
    if (panning || dragging) camHold = .9;
    else camHold = Math.max(0, camHold - dt);

    const moving = vis.on ? drive(dt) > 2 : 0;
    if (view && view.travel && !dragging && !panning && camHold <= 0) {
      cam.x += (vis.x - cam.x) * (1 - Math.exp(-dt * 6));
      cam.y += (vis.y - cam.y) * (1 - Math.exp(-dt * 6));
    }

    const t = tilt;
    const cx = cam.x, cy = cam.y, pcy = cy * t;
    ctx = wctx;
    ctx.fillStyle = BIO.base; ctx.fillRect(0, 0, W, H);
    ctx.save();
    const camX = snap(W / 2 - cx), camY = snap(H / 2 - pcy);
    lastCamX = camX; lastCamY = camY;
    ctx.translate(camX, camY);
    groundPass(cx, pcy, t);
    gather(cx, cy, t);
    if (typeof MapKit !== 'undefined' && MapKit.ready()) gatherKit(cx, cy, t);
    else gatherWild(cx, cy, t);
    for (let i = 0; i < VIS.length; i++) {
      if (VIS[i].kit) MapKit.draw(ctx, VIS[i].kit, VIS[i].x, VIS[i].y, t, VIS[i].sc);
      else drawInstance(VIS[i], t);
    }
    drawRoute(t);
    drawConvoy(t, t2 * .001, moving);
    ctx.restore();
    if (vig) { ctx.fillStyle = vig; ctx.fillRect(0, 0, W, H); }

    ctx = scr;
    ctx.clearRect(0, 0, SCW, SCH);
    ctx.imageSmoothingEnabled = false;
    ctx.drawImage(wb, 0, 0, wb.width, wb.height, 0, 0, SCW, SCH);
    ctx.save();
    ctx.scale(ZOOM, ZOOM);
    ctx.translate(camX, camY);
    drawOverlayLabels(t);
    ctx.restore();
  }

  function clampCam() {
    const maxX = Math.max(1, mapW * cellKm * SCALE);
    const maxY = Math.max(1, mapH * cellKm * SCALE);
    cam.x = clamp(cam.x, 0, maxX);
    cam.y = clamp(cam.y, 0, maxY);
  }

  function panKeys(dt) {
    const spd = (1100 / ZOOM) * dt;
    let moved = false;
    if (keys.KeyA || keys.ArrowLeft) { cam.x -= spd; moved = true; }
    if (keys.KeyD || keys.ArrowRight) { cam.x += spd; moved = true; }
    if (keys.KeyW || keys.ArrowUp) { cam.y -= spd; moved = true; }
    if (keys.KeyS || keys.ArrowDown) { cam.y += spd; moved = true; }
    if (moved) clampCam();
    return moved;
  }

  function bind() {
    cv.style.pointerEvents = 'auto';
    cv.addEventListener('pointerdown', (e) => {
      if (e.button !== 0) return;
      dragging = true; didDrag = false;
      dragX = e.clientX; dragY = e.clientY;
      cv.setPointerCapture(e.pointerId);
    });
    cv.addEventListener('pointermove', (e) => {
      if (!dragging) return;
      const dx = e.clientX - dragX, dy = e.clientY - dragY;
      if (Math.hypot(dx, dy) > 4) didDrag = true;
      cam.x -= dx / ZOOM;
      cam.y -= dy / ZOOM / tilt;
      clampCam();
      dragX = e.clientX; dragY = e.clientY;
    });
    cv.addEventListener('pointerup', (e) => {
      dragging = false;
      if (didDrag) { camHold = 1.2; return; }
      const r = cv.getBoundingClientRect();
      const hit = pickAt(e.clientX - r.left, e.clientY - r.top);
      if (hit && onPick) onPick(hit.kind, hit.id);
    });
    cv.addEventListener('dblclick', (e) => {
      const r = cv.getBoundingClientRect();
      const hit = pickAt(e.clientX - r.left, e.clientY - r.top);
      if (hit && onDbl) onDbl(hit.id);
    });
    cv.addEventListener('wheel', (e) => {
      e.preventDefault();
      const dir = e.deltaY > 0 ? -1 : 1;
      const next = clamp(zI + dir, 0, ZLV.length - 1);
      if (next === zI) return;
      const r = cv.getBoundingClientRect();
      const before = screenToWorld(e.clientX - r.left, e.clientY - r.top);
      setZoom(next);
      const after = screenToWorld(e.clientX - r.left, e.clientY - r.top);
      cam.x += before.x - after.x;
      cam.y += before.y - after.y;
    }, { passive: false });

    addEventListener('keydown', (e) => {
      if (e.target && /^(INPUT|TEXTAREA|SELECT)$/.test(e.target.tagName)) return;
      keys[e.code] = true;
      if (/^(KeyW|KeyA|KeyS|KeyD|ArrowUp|ArrowDown|ArrowLeft|ArrowRight)$/.test(e.code)) e.preventDefault();
      if (e.code === 'KeyZ') { e.preventDefault(); setZoom(zI + 1 >= ZLV.length ? 0 : zI + 1); }
      if (e.code === 'KeyT') { tiltT = Math.abs(tiltT - TILT_3D) < .05 ? TILT_TOP : TILT_3D; }
      if (e.code === 'KeyP') { setPXS(pxI + 1); }
    });
    addEventListener('keyup', (e) => { keys[e.code] = false; });
  }

  return {
    load, sync,
    set onPick(fn) { onPick = fn; },
    set onDbl(fn) { onDbl = fn; },
    start() { bind(); }
  };
})();
