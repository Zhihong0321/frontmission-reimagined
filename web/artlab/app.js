'use strict';

const $ = (id) => document.getElementById(id);
const LS_STYLE = 'artlab.style.v2';

let category = 'building';
let gallery = [];
let busy = false;

function defaultStyle() {
  return ArtPrompt.styleBlock();
}

function loadStyle() {
  const saved = localStorage.getItem(LS_STYLE);
  $('style-edit').value = saved || defaultStyle();
}

function fillCats() {
  const hold = $('cats');
  hold.innerHTML = '';
  Object.keys(ArtPrompt.CATALOG).forEach((id) => {
    const b = document.createElement('button');
    b.type = 'button';
    b.textContent = ArtPrompt.CATALOG[id].label;
    b.dataset.id = id;
    if (id === category) b.classList.add('on');
    b.addEventListener('click', () => { category = id; syncCat(); rebuild(); });
    hold.appendChild(b);
  });
}

function syncCat() {
  [...$('cats').children].forEach((b) => b.classList.toggle('on', b.dataset.id === category));
  const cat = ArtPrompt.CATALOG[category];
  $('cat-hint').textContent = cat.hint + ' · ' + cat.types.length + ' types';
  const sel = $('type');
  sel.innerHTML = cat.types.map((t) => '<option value="' + t.id + '">' + t.name + '</option>').join('');
}

function rebuild() {
  const prompt = ArtPrompt.assemble({
    category,
    typeId: $('type').value,
    extra: $('extra').value,
    seed: +$('seed').value || 1,
    flavorCount: 2
  });
  const lock = $('style-edit').value.trim();
  const subjectStart = prompt.indexOf('SUBJECT:');
  const rest = subjectStart >= 0 ? prompt.slice(subjectStart) : prompt;
  $('prompt').value = lock + '\n\n' + rest;
}

function setStatus(msg, cls) {
  const el = $('status');
  el.textContent = msg || '';
  el.className = 'hint ' + (cls || '');
}

function rollSeed() {
  $('seed').value = ((Math.random() * 1e9) | 0);
}

function pickRandomType() {
  const t = ArtPrompt.randomType(category);
  $('type').value = t.id;
  rollSeed();
  rebuild();
}

function pickRandomBuilding() {
  category = 'building';
  syncCat();
  pickRandomType();
}

function pickSurprise() {
  category = ArtPrompt.randomCategory();
  syncCat();
  pickRandomType();
}

function loadToCanvas(src) {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => {
      const c = document.createElement('canvas');
      c.width = img.width;
      c.height = img.height;
      const ctx = c.getContext('2d', { willReadFrequently: true });
      ctx.drawImage(img, 0, 0);
      resolve({ canvas: c, ctx, img });
    };
    img.onerror = () => reject(new Error('Could not decode PNG.'));
    img.src = src;
  });
}

function measureAlpha(data) {
  let trans = 0, opaque = 0, white = 0;
  for (let i = 0; i < data.length; i += 4) {
    const a = data[i + 3];
    if (a < 12) trans++;
    else {
      opaque++;
      if (data[i] > 245 && data[i + 1] > 245 && data[i + 2] > 245) white++;
    }
  }
  const total = trans + opaque || 1;
  return {
    transparentPct: 100 * trans / total,
    whitePct: 100 * white / total,
    hasAlpha: trans / total > 0.04
  };
}

function knockOutCard(ctx) {
  const { width: w, height: h } = ctx.canvas;
  const img = ctx.getImageData(0, 0, w, h);
  const d = img.data;
  const stats = measureAlpha(d);
  if (stats.hasAlpha && stats.whitePct < 8) return { knocked: false, stats };

  const at = (x, y) => ((y * w) + x) * 4;
  const corners = [at(2, 2), at(w - 3, 2), at(2, h - 3), at(w - 3, h - 3)];
  let cr = 0, cg = 0, cb = 0, ca = 0;
  corners.forEach((i) => { cr += d[i]; cg += d[i + 1]; cb += d[i + 2]; ca += d[i + 3]; });
  cr /= 4; cg /= 4; cb /= 4; ca /= 4;
  if (ca < 20) return { knocked: false, stats };

  const tol = 42;
  const match = (i) =>
    d[i + 3] > 8 &&
    Math.abs(d[i] - cr) + Math.abs(d[i + 1] - cg) + Math.abs(d[i + 2] - cb) <= tol;

  const seen = new Uint8Array(w * h);
  const qx = [];
  const qy = [];
  const push = (x, y) => {
    if (x < 0 || y < 0 || x >= w || y >= h) return;
    const p = y * w + x;
    if (seen[p]) return;
    if (!match(p * 4)) return;
    seen[p] = 1;
    qx.push(x);
    qy.push(y);
  };
  for (let x = 0; x < w; x++) { push(x, 0); push(x, h - 1); }
  for (let y = 0; y < h; y++) { push(0, y); push(w - 1, y); }

  let qi = 0;
  while (qi < qx.length) {
    const x = qx[qi];
    const y = qy[qi++];
    const i = at(x, y);
    d[i + 3] = 0;
    push(x + 1, y); push(x - 1, y); push(x, y + 1); push(x, y - 1);
  }

  // Fringe: any remaining near-card pixel next to a hole becomes transparent.
  for (let y = 1; y < h - 1; y++) {
    for (let x = 1; x < w - 1; x++) {
      const i = at(x, y);
      if (d[i + 3] < 8) continue;
      if (!match(i)) continue;
      const n =
        d[at(x + 1, y) + 3] < 8 || d[at(x - 1, y) + 3] < 8 ||
        d[at(x, y + 1) + 3] < 8 || d[at(x, y - 1) + 3] < 8;
      if (n) d[i + 3] = 0;
    }
  }

  ctx.putImageData(img, 0, 0);
  return { knocked: true, stats: measureAlpha(d) };
}

function canvasToPngUrl(canvas) {
  return canvas.toDataURL('image/png');
}

function setAlphaLine(stats, knocked) {
  const el = $('alpha');
  if (!stats) { el.textContent = ''; return; }
  const pct = stats.transparentPct.toFixed(1);
  if (stats.hasAlpha && stats.whitePct < 6) {
    el.textContent = pct + '% of pixels are transparent. Good sprite.';
    el.className = 'hint good';
  } else if (knocked && stats.hasAlpha) {
    el.textContent = 'Knocked out a solid card. Now ' + pct + '% transparent — still inspect the silhouette.';
    el.className = 'hint';
  } else {
    el.textContent = 'Almost no alpha (' + pct + '%). White card in the PNG. Switch to gpt-image-1 or tighten the lock.';
    el.className = 'hint bad';
  }
}

async function showPngUrl(url, slug, saved) {
  const { canvas, ctx } = await loadToCanvas(url);
  let knocked = false;
  let stats = measureAlpha(ctx.getImageData(0, 0, canvas.width, canvas.height).data);
  if ($('knockout').checked && (!stats.hasAlpha || stats.whitePct > 10)) {
    const r = knockOutCard(ctx);
    knocked = r.knocked;
    stats = r.stats;
  }
  const out = canvasToPngUrl(canvas);
  const img = new Image();
  img.src = out;
  img.alt = slug;
  $('stage').innerHTML = '';
  $('stage').appendChild(img);
  const a = $('btn-dl');
  a.hidden = false;
  a.href = out;
  a.download = (saved ? saved.split('/').pop() : slug + '.png');
  setAlphaLine(stats, knocked);
  gallery.unshift({ url: out, slug, saved });
  renderGallery();
}

function renderGallery() {
  const g = $('gallery');
  g.innerHTML = '';
  gallery.slice(0, 48).forEach((item) => {
    const b = document.createElement('button');
    b.type = 'button';
    const im = new Image();
    im.src = item.url;
    b.appendChild(im);
    b.addEventListener('click', () => {
      $('stage').innerHTML = '';
      const big = new Image();
      big.src = item.url;
      $('stage').appendChild(big);
      $('btn-dl').hidden = false;
      $('btn-dl').href = item.url;
      $('btn-dl').download = (item.saved ? item.saved.split('/').pop() : item.slug + '.png');
    });
    g.appendChild(b);
  });
}

async function generateOnce() {
  if ($('auto-roll').checked) {
    rollSeed();
    rebuild();
  }
  const body = {
    prompt: $('prompt').value.trim(),
    size: $('size').value,
    quality: $('quality').value,
    model: $('model').value,
    slug: category + '-' + $('type').value
  };
  if (!body.prompt) throw new Error('Empty prompt.');
  const res = await fetch('/api/artlab/generate', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  });
  const data = await res.json();
  if (!res.ok || data.error) {
    const e = data.error;
    const text = typeof e === 'string' ? e
      : (e && e.message) ? e.message
      : data.message || ('HTTP ' + res.status);
    throw new Error(text);
  }
  const b64 = data.b64 || (data.data && data.data[0] && (data.data[0].b64_json || data.data[0].b64));
  if (!b64) throw new Error('API returned no image bytes.');
  const url = b64.startsWith('http') || b64.startsWith('data:') ? b64 : ('data:image/png;base64,' + b64);
  await showPngUrl(url, body.slug, data.saved);
  const used = data.model ? (' via ' + data.model) : '';
  setStatus((data.saved ? 'Saved ' + data.saved : 'Done.') + used, 'good');
}

async function generate() {
  if (busy) return;
  busy = true;
  $('btn-gen').disabled = true;
  $('btn-batch').disabled = true;
  setStatus('Generating… timeouts retry automatically (up to ~3 min). Keep quality on low.', '');
  try {
    await generateOnce();
  } catch (err) {
    setStatus(err.message, 'bad');
  } finally {
    busy = false;
    $('btn-gen').disabled = false;
    $('btn-batch').disabled = false;
  }
}

async function batchFour() {
  if (busy) return;
  busy = true;
  $('btn-gen').disabled = true;
  $('btn-batch').disabled = true;
  try {
    for (let i = 0; i < 4; i++) {
      pickSurprise();
      setStatus('Batch ' + (i + 1) + '/4 — ' + category + '/' + $('type').value + '…', '');
      await generateOnce();
    }
  } catch (err) {
    setStatus(err.message, 'bad');
  } finally {
    busy = false;
    $('btn-gen').disabled = false;
    $('btn-batch').disabled = false;
  }
}

async function loadLibrary() {
  try {
    const res = await fetch('/api/artlab/library');
    if (!res.ok) return;
    const files = await res.json();
    files.forEach((f) => gallery.push({ url: '/' + f.file, slug: f.slug, saved: f.file }));
    renderGallery();
  } catch (_) { /* first run */ }
}

$('btn-reset-style').addEventListener('click', () => {
  localStorage.removeItem(LS_STYLE);
  loadStyle();
  rebuild();
});
$('style-edit').addEventListener('input', () => {
  localStorage.setItem(LS_STYLE, $('style-edit').value);
});
$('type').addEventListener('change', rebuild);
$('extra').addEventListener('input', rebuild);
$('seed').addEventListener('change', rebuild);
$('btn-rand-type').addEventListener('click', pickRandomType);
$('btn-rand-bldg').addEventListener('click', pickRandomBuilding);
$('btn-surprise').addEventListener('click', pickSurprise);
$('btn-build').addEventListener('click', rebuild);
$('btn-gen').addEventListener('click', generate);
$('btn-batch').addEventListener('click', batchFour);
$('btn-copy').addEventListener('click', async () => {
  await navigator.clipboard.writeText($('prompt').value);
  setStatus('Prompt copied.', 'good');
});

fillCats();
loadStyle();
syncCat();
rebuild();
loadLibrary();

fetch('/api/artlab/status')
  .then((r) => r.json())
  .then((s) => {
    if (!s.ok) throw new Error('no');
    if (!s.hasKey) setStatus('Server is up but has no API key (.artlab-secret).', 'bad');
    else setStatus('Ready. Edit the lock, pick a type, Generate.', 'good');
  })
  .catch(() => setStatus('Restart the game server (Play.cmd) so /api/artlab exists.', 'bad'));
