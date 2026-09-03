'use strict';
/* Tagged 2D map kit. Godot reads the same kit.json + PNGs; this file only blits. */

const MapKit = (() => {
  let kit = null;
  const imgs = new Map();
  const byId = new Map();

  async function load(url) {
    kit = await fetch(url).then((r) => r.json());
    byId.clear();
    imgs.clear();
    const waits = [];
    for (const a of kit.assets) {
      byId.set(a.id, a);
      if (a.draw === false || !a.file) continue;
      waits.push(loadImg(a));
    }
    await Promise.all(waits);
    return kit;
  }

  function loadImg(a) {
    return new Promise((resolve) => {
      const im = new Image();
      im.onload = () => { imgs.set(a.id, im); resolve(); };
      im.onerror = () => resolve();
      im.src = 'art/map/' + a.file;
    });
  }

  function ready() { return !!kit && imgs.size > 0; }

  function tagged(q) {
    if (!kit) return [];
    return kit.assets.filter((a) => {
      if (a.draw === false) return false;
      if (!imgs.has(a.id)) return false;
      if (q.kind && a.kind !== q.kind) return false;
      if (q.place && a.place !== q.place) return false;
      if (q.role && a.role !== q.role) return false;
      if (q.cluster && a.cluster !== q.cluster) return false;
      if (q.tags && !q.tags.every((t) => a.tags.indexOf(t) >= 0)) return false;
      if (q.biome && a.biomes && a.biomes.indexOf('*') < 0 && a.biomes.indexOf(q.biome) < 0) return false;
      return true;
    });
  }

  function pick(q, R) {
    const pool = tagged(q);
    if (!pool.length) return null;
    let sum = 0;
    for (let i = 0; i < pool.length; i++) sum += pool[i].weight || 1;
    let x = (R ? R() : Math.random()) * sum;
    for (let i = 0; i < pool.length; i++) {
      x -= pool[i].weight || 1;
      if (x <= 0) return pool[i];
    }
    return pool[pool.length - 1];
  }

  function asset(id) { return byId.get(id) || null; }

  function draw(ctx, a, x, y, t, sc) {
    const im = imgs.get(a.id);
    if (!im) return;
    const fw = (a.footprint || 64) * (sc || 1);
    const fh = fw * (im.height / Math.max(1, im.width));
    const ax = a.anchor && a.anchor.x != null ? a.anchor.x : 0.5;
    const ay = a.anchor && a.anchor.y != null ? a.anchor.y : 0.84;
    ctx.drawImage(im, x - fw * ax, y * t - fh * ay, fw, fh);
  }

  function recipe(name) { return kit && kit.recipes ? kit.recipes[name] : null; }
  function biomeFill(b) { return kit && kit.biomeFill ? (kit.biomeFill[b] || []) : []; }

  return { load, ready, tagged, pick, asset, draw, recipe, biomeFill };
})();
