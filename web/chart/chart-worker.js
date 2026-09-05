// Extracted verbatim (Phase D CP-D2) from web/chart/chart.js at integration 67b6fb5.
'use strict';
const ZOOM_TILE_AT = 3, TILE_KM = 256, TILE_DETAIL = 4;
const detailTiles = new Map();          // "cx,cy" -> ImageBitmap (LRU, 24 tiles)
const tilePending = new Map();          // "cx,cy" -> request id  (in flight)
const tilePendingQueued = new Set();     // "cx,cy" waiting for worker ready
let tileWorker = null, tileSeq = 0, tileWorkerReady = false;
const TILE_TEX = {};                    // biome code -> texture pixel data (sent as they load)
function startTileWorker() {
  if (tileWorker) return;
  try { tileWorker = new Worker('chart-tiles-worker.js'); } catch (_) { return; }
  tileWorker.onmessage = (e) => {
    const m = e.data;
    if (m.type === 'ready') { tileWorkerReady = true; for (const k of tilePendingQueued) { const [cx, cy] = k.split(',').map(Number); tileWorker.postMessage({ type: 'tile', id: tilePending.get(k), cx, cy }); } tilePendingQueued.clear(); return; }
    if (m.type === 'tile') {
      if (m.err) { tilePending.delete(m.cx + ',' + m.cy); tilePendingQueued.delete(m.cx + ',' + m.cy); return; }
      const key = m.cx + ',' + m.cy;
      tilePendingQueued.delete(key);
      detailTiles.delete(key); detailTiles.set(key, m.bmp);
      tilePending.delete(key);

      while (detailTiles.size > 24) detailTiles.delete(detailTiles.keys().next().value);
    }
  };
  tileWorker.postMessage({
    type: 'init',
    cfg: { FW, FH, LW, LH, MAP_W, MAP_H, FINE, LAT, BIOME, PAL, WATER_FAR, FOAM: [150, 172, 186], DARK: [8, 12, 18] },
    perm: PERM.slice(),
    fine: fine.slice(),
    edge: edgeDist.slice(),
    lat: { wx: latWX.slice(), wy: latWY.slice(), mot: latMot.slice(), tex: latTex.slice(), scar: latScar.slice() },
    textures: Object.entries(TILE_TEX).filter(([c, d]) => d && d.byteLength > 0).map(([code, data]) => ({ code: +code, data: data.slice() }))
  });
}
// Send a texture image's pixels to the worker as soon as it loads
function sendTextureToWorker(name, img) {
  const code = BIOME[name.replace('tex-', '')];
  if (code === undefined) return;
  const c = document.createElement('canvas'); c.width = 256; c.height = 256;
  const g = c.getContext('2d'); g.drawImage(img, 0, 0, 256, 256);
  const td = g.getImageData(0, 0, 256, 256).data;
  TILE_TEX[code] = td;
  if (tileWorker) tileWorker.postMessage({ type: 'tex', code, data: td }, [td.buffer]);
}
function wantTile(cx, cy) {
  if (cx < 0 || cy < 0 || cx * TILE_KM >= MAP_W || cy * TILE_KM >= MAP_H) return;
  const key = cx + ',' + cy;
  if (detailTiles.has(key) || tilePending.has(key)) return;
  startTileWorker();
  const id = ++tileSeq; tilePending.set(key, id);
  if (tileWorkerReady) tileWorker.postMessage({ type: 'tile', id, cx, cy });
  else tilePendingQueued.add(key);
}
function drawDetailTiles() {
  const tl = toWorld(0, 0), br = toWorld(VW, VH);
  const c0 = Math.max(0, Math.floor(tl.x / TILE_KM)), c1 = Math.min(Math.ceil(MAP_W / TILE_KM), Math.ceil(br.x / TILE_KM));
  const r0 = Math.max(0, Math.floor(tl.y / TILE_KM)), r1 = Math.min(Math.ceil(MAP_H / TILE_KM), Math.ceil(br.y / TILE_KM));
  for (let cy = r0; cy <= r1; cy++) for (let cx = c0; cx <= c1; cx++) {
    const key = cx + ',' + cy; const bmp = detailTiles.get(key);
    if (bmp) { detailTiles.delete(key); detailTiles.set(key, bmp); ctx.drawImage(bmp, cx * TILE_KM, cy * TILE_KM, TILE_KM, TILE_KM); }
    else wantTile(cx, cy);
  }
}
