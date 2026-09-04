'use strict';
/* Mecha Trader — Ops shell.
 *
 * An ERP-style workspace docked over Keeper's Chart: a nav rail, a page with tabs, a
 * data grid, and a detail pane. It owns no rule. Every figure it prints arrives already
 * resolved in the /api/state snapshot; every change goes through MECHA.command, which
 * posts to /api/command. Adding a page or a tab is one register() call at the bottom. */

const OPS = (() => {
  const LS_KEY = 'mecha.ops.v1';

  // ── state ────────────────────────────────────────────────────────────────
  const S = {
    snap: null, open: false, page: 'overview', tab: {}, detail: null, sel: null,
    full: false, wide: false, sort: {}, q: {}, cat: {}, qty: {}, mode: 'buy',
    busy: false, dirty: false, build: null, logKind: 'all', waitDays: 1, lastLocationId: null,
    // What each city's shelf asked the last time the convoy parked there, keyed by city
    // id: { day, buy: { goodId -> price } }. The Home tab diffs the current board against
    // it, so "prices moved" means "since the last visit", not "vs base".
    memo: null,
    // The park that is still being shown: { cityId, prev, day }. prev is the previous
    // visit's memo and the baseline for the first Home render of this park.
    arrival: null
  };
  try {
    const saved = JSON.parse(localStorage.getItem(LS_KEY) || '{}');
    for (const k of ['page', 'tab', 'full', 'wide', 'open']) if (saved[k] !== undefined) S[k] = saved[k];
    if (saved.memo) S.memo = saved.memo;
  } catch (e) { /* fresh */ }
  function persist() {
    try { localStorage.setItem(LS_KEY, JSON.stringify({ page: S.page, tab: S.tab, full: S.full, wide: S.wide, open: S.open, memo: S.memo })); } catch (e) { /* ignore */ }
  }

  const pages = [];
  const tabs = {};
  function registerPage(p) { pages.push(p); pages.sort((a, b) => (a.order || 0) - (b.order || 0)); tabs[p.id] = tabs[p.id] || []; }
  function registerTab(pageId, t) { (tabs[pageId] = tabs[pageId] || []).push(t); tabs[pageId].sort((a, b) => (a.order || 0) - (b.order || 0)); }

  // ── dom + format helpers ─────────────────────────────────────────────────
  function h(tag, attrs, ...kids) {
    const el = document.createElement(tag);
    if (attrs) for (const [k, v] of Object.entries(attrs)) {
      if (v == null || v === false) continue;
      if (k === 'class') el.className = v;
      else if (k === 'html') el.innerHTML = v;
      else if (k.startsWith('on')) el.addEventListener(k.slice(2), v);
      else if (k === 'style') el.style.cssText = v;
      else if (k === 'disabled' || k === 'checked') el[k] = !!v;
      else el.setAttribute(k, v);
    }
    for (const c of kids.flat(Infinity)) {
      if (c == null || c === false) continue;
      el.append(c.nodeType ? c : document.createTextNode(String(c)));
    }
    return el;
  }
  const $ = (sel, root) => (root || document).querySelector(sel);

  const fmt = {
    n: (x, d = 0) => Number(x || 0).toLocaleString(undefined, { minimumFractionDigits: d, maximumFractionDigits: d }),
    cr: (x) => Math.round(x || 0).toLocaleString() + ' cr',
    signed: (x, d = 0) => (x > 0 ? '+' : '') + fmt.n(x, d),
    signedCr: (x) => (x > 0 ? '+' : x < 0 ? '−' : '') + Math.abs(Math.round(x || 0)).toLocaleString() + ' cr',
    pct: (x) => Math.round(x * 100) + '%',
    days: (d) => d + (d === 1 ? ' day' : ' days'),
    vol: (x) => fmt.n(x, x % 1 ? 1 : 0)
  };

  const ICON = {
    overview: '<svg viewBox="0 0 24 24"><rect x="3" y="3" width="7" height="9" rx="1"/><rect x="14" y="3" width="7" height="5" rx="1"/><rect x="14" y="12" width="7" height="9" rx="1"/><rect x="3" y="16" width="7" height="5" rx="1"/></svg>',
    city: '<svg viewBox="0 0 24 24"><path d="M3 21h18"/><path d="M5 21V7l7-4 7 4v14"/><path d="M9 21v-5h6v5"/><path d="M9 10h.01M15 10h.01M9 13h.01M15 13h.01"/></svg>',
    caravan: '<svg viewBox="0 0 24 24"><path d="M3 7h11v9H3z"/><path d="M14 10h4l3 3v3h-7z"/><circle cx="7" cy="18" r="2"/><circle cx="17" cy="18" r="2"/></svg>',
    crew: '<svg viewBox="0 0 24 24"><circle cx="9" cy="8" r="3.5"/><path d="M2.5 20a6.5 6.5 0 0 1 13 0"/><circle cx="17" cy="9" r="2.5"/><path d="M15.5 14.5a5 5 0 0 1 6 4.5"/></svg>',
    ledger: '<svg viewBox="0 0 24 24"><path d="M5 3h14v18H5z"/><path d="M9 8h6M9 12h6M9 16h4"/></svg>',
    settings: '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"/></svg>',
    chev: '<svg viewBox="0 0 24 24"><path d="M9 6l6 6-6 6"/></svg>',
    close: '<svg viewBox="0 0 24 24"><path d="M6 6l12 12M18 6L6 18"/></svg>',
    expand: '<svg viewBox="0 0 24 24"><path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5"/></svg>',
    search: '<svg viewBox="0 0 24 24"><circle cx="11" cy="11" r="7"/><path d="M20 20l-3.5-3.5"/></svg>',
    back: '<svg viewBox="0 0 24 24"><path d="M15 6l-6 6 6 6"/></svg>'
  };
  const icon = (name, cls) => h('span', { class: cls || 'ico', html: ICON[name] });

  // ── components ───────────────────────────────────────────────────────────
  function card(title, body, opts = {}) {
    const head = title === null ? null : h('div', { class: 'card-head' }, h('h4', null, title), h('span', { class: 'grow' }), opts.hint ? h('span', { class: 'hint' }, opts.hint) : null, opts.actions || null);
    return h('div', { class: 'card' + (opts.class ? ' ' + opts.class : '') }, head, h('div', { class: 'card-body' + (opts.tight ? ' tight' : '') }, body), opts.foot ? h('div', { class: 'card-foot' }, opts.foot) : null);
  }
  function kpi(label, value, sub, tone) {
    return h('div', { class: 'card kpi' }, h('label', null, label), h('span', { class: 'val' + (tone ? ' ' + tone : '') }, value), sub ? h('span', { class: 'sub' }, sub) : null);
  }
  function badge(text, tone) { return h('span', { class: 'badge' + (tone ? ' ' + tone : '') }, text); }
  function meter(fill, tone, mid) { return h('div', { class: 'meter' + (tone ? ' ' + tone : '') + (mid ? ' mid' : '') }, h('i', { style: 'width:' + Math.max(0, Math.min(100, fill * 100)).toFixed(1) + '%' })); }
  function btn(label, onclick, opts = {}) {
    return h('button', { class: 'btn' + (opts.kind ? ' ' + opts.kind : '') + (opts.size ? ' ' + opts.size : ''), disabled: opts.disabled || S.busy, title: opts.title, onclick: (e) => { e.stopPropagation(); onclick(e); } }, label);
  }
  function dl(pairs) { return h('dl', { class: 'dl' }, pairs.map(([k, v, cls]) => [h('dt', null, k), h('dd', { class: cls }, v)])); }
  function empty(title, text, action) { return h('div', { class: 'empty' }, h('h3', null, title), h('div', null, text), action || null); }
  function pips(level, max, lead) {
    const n = Math.max(1, Math.min(12, max || 10));
    return h('div', { class: 'pips' }, Array.from({ length: n }, (_, i) => h('i', { class: i < Math.round(level / (max || 10) * n) ? (lead ? 'on lead' : 'on') : '' })));
  }
  function avatar(name, lg) {
    const parts = String(name || '?').split(/\s+/);
    const ini = (parts[0][0] || '') + (parts[1] ? parts[1][0] : '');
    let hue = 0; for (const ch of name || '') hue = (hue * 31 + ch.charCodeAt(0)) % 360;
    return h('div', { class: 'avatar' + (lg ? ' lg' : ''), style: `background: linear-gradient(135deg, hsl(${hue},45%,38%), hsl(${(hue + 40) % 360},55%,55%))` }, ini.toUpperCase());
  }
  function toneOf(t) { return ({ good: 'good', ok: 'ok', warn: 'warn', bad: 'bad', muted: 'muted' })[t] || 'muted'; }
  function tierName(r, opts = {}) {
    const color = r.tierColor || 'var(--ops-text-strong)';
    return h('span', { class: 'tier', style: 'color:' + color, title: (r.tierName || '') + (r.locked ? ' · locked' : '') }, h('i', { class: 'tier-dot', style: 'background:' + color }), r.name, r.locked && !opts.noLock ? [' ', badge('locked', 'muted')] : null);
  }
  function tierLegend(v) {
    return h('div', { class: 'tier-legend' }, (v.tiers || []).map((t) => h('span', null, h('i', { class: 'tier-dot', style: 'background:' + t.color }), t.name, t.minStanding > 0 ? h('span', { class: 'lock' }, ' · standing ' + fmt.n(t.minStanding)) : null)));
  }
  function segmentBars(st) {
    return h('div', { class: 'seg-bars' }, st.segments.map((sg) => h('div', { class: 'seg-bar', title: sg.blurb }, h('div', { class: 'nm' }, sg.name, h('small', null, sg.blurb)), meter(sg.fill, sg.value > 0 ? 'ok' : 'muted'), h('div', { class: 'v' }, fmt.n(sg.value, 0), h('small', null, ' / ' + fmt.n(sg.max))))));
  }
  function flowBadge(flow) {
    return flow === 'surplus' ? badge('makes', 'good') : flow === 'deficit' ? badge('eats', 'warn') : badge('balanced', 'muted');
  }

  /** Sortable data grid. cols: {id, label, num, get(row), cell(row), sortable, w}. */
  function table({ id, cols, rows, rowKey, onRow, selected, emptyText, defaultSort, rowClass }) {
    const st = S.sort[id] || (S.sort[id] = defaultSort ? { ...defaultSort } : null);
    let list = rows.slice();
    if (st) {
      const col = cols.find((c) => c.id === st.id);
      if (col) {
        const get = col.get || ((r) => r[col.id]);
        list.sort((a, b) => {
          const x = get(a), y = get(b);
          const r = typeof x === 'number' && typeof y === 'number' ? x - y : String(x ?? '').localeCompare(String(y ?? ''));
          return st.dir === 'desc' ? -r : r;
        });
      }
    }
    const thead = h('thead', null, h('tr', null, cols.map((c) => h('th', {
      class: (c.num ? 'num' : '') + (c.sortable !== false ? ' sortable' : ''), style: c.w ? 'width:' + c.w : null,
      onclick: c.sortable === false ? null : () => { const cur = S.sort[id]; S.sort[id] = cur && cur.id === c.id ? { id: c.id, dir: cur.dir === 'asc' ? 'desc' : 'asc' } : { id: c.id, dir: c.num ? 'desc' : 'asc' }; render(); }
    }, c.label, st && st.id === c.id ? h('span', { class: 'arr' }, st.dir === 'asc' ? '▲' : '▼') : null))));
    const tbody = h('tbody', null, list.map((r) => {
      const key = rowKey ? rowKey(r) : null;
      return h('tr', { class: (onRow ? 'clickable' : '') + (selected != null && key === selected ? ' sel' : '') + (rowClass ? ' ' + (rowClass(r) || '') : ''), onclick: onRow ? () => onRow(r) : null },
        cols.map((c) => h('td', { class: c.num ? 'num' : '' }, c.cell ? c.cell(r) : (c.get ? c.get(r) : r[c.id]))));
    }));
    if (!list.length) return h('div', { class: 'tbl-wrap' }, h('table', { class: 'tbl' }, thead), h('div', { class: 'tbl-empty' }, emptyText || 'Nothing here.'));
    return h('div', { class: 'tbl-wrap' }, h('table', { class: 'tbl' }, thead, tbody));
  }

  function stepper(key, { min = 0, max = Infinity, def = 1 }) {
    if (S.qty[key] === undefined) S.qty[key] = Math.max(min, Math.min(max, def));
    const set = (v) => { S.qty[key] = Math.max(min, Math.min(max, Math.floor(Number(v) || 0))); render(); };
    const input = h('input', { type: 'number', value: S.qty[key], min, onchange: (e) => set(e.target.value), onkeydown: (e) => { if (e.key === 'Enter') { set(e.target.value); e.target.blur(); } } });
    return h('div', { class: 'stepper' }, h('button', { onclick: () => set(S.qty[key] - (S.qty[key] > 10 ? 10 : 1)) }, '−'), input, h('button', { onclick: () => set(S.qty[key] + (S.qty[key] >= 10 ? 10 : 1)) }, '+'));
  }
  function quick(key, items) {
    return h('div', { class: 'quick' }, items.map(([label, v]) => btn(label, () => { S.qty[key] = v; render(); }, { size: 'xs' })));
  }

  function confirm(title, text, ok, kind) {
    const box = h('div', { class: 'confirm', onclick: (e) => { if (e.target === box) box.remove(); } },
      h('div', { class: 'box' }, h('h3', null, title), h('p', null, text),
        h('div', { class: 'acts' }, btn('Cancel', () => box.remove(), { kind: 'ghost' }), btn(ok.label, () => { box.remove(); ok.run(); }, { kind: kind || 'primary' }))));
    document.body.appendChild(box);
  }

  // ── talking to Core ───────────────────────────────────────────────────────
  function toast(msg, kind) { if (typeof window.toast === 'function') window.toast(msg, kind); else console.log(kind || 'info', msg); }
  async function send(body, opts = {}) {
    if (S.busy) return null;
    S.busy = true; render();
    try {
      const snap = await MECHA.command(body);
      if (snap && snap.error) { toast(snap.error, 'alert'); return snap; }
      if (opts.done) toast(opts.done);
      if (opts.close) close();
      return snap;
    } catch (err) {
      toast(String(err.message || err), 'alert');
      return null;
    } finally { S.busy = false; render(); }
  }
  function depart(toId) {
    const p = MECHA.go(toId);
    close();
    if (p && p.then) p.then(() => {});
  }
  const view = () => S.snap && S.snap.view;

  // ── shell ─────────────────────────────────────────────────────────────────
  let railEl, opsEl, mainEl, sideEl;
  function mount() {
    railEl = h('nav', { id: 'ops-rail', class: S.wide ? 'wide' : '' });
    opsEl = h('section', { id: 'ops', class: (S.open ? 'show' : '') + (S.full ? ' full' : '') });
    document.body.append(railEl, opsEl);
    buildRail();
    addEventListener('keydown', (e) => {
      if (!S.open) return;
      if (e.code === 'Escape') { const t = e.target; if (t && t.tagName === 'INPUT') { t.blur(); return; } close(); }
    }, true);
    opsEl.addEventListener('focusout', () => { if (S.dirty) { S.dirty = false; setTimeout(render, 0); } });
    fetch('/api/build').then((r) => r.json()).then((b) => { S.build = b; if (S.open) render(); }).catch(() => {});
  }
  function buildRail() {
    railEl.innerHTML = '';
    const top = h('div', { class: 'rail-group' }, pages.filter((p) => !p.bottom).map((p) => railBtn(p)));
    const bottom = h('div', { class: 'rail-group' },
      pages.filter((p) => p.bottom).map((p) => railBtn(p)),
      h('button', { class: 'rail-btn', title: 'Widen the rail', onclick: () => { S.wide = !S.wide; railEl.classList.toggle('wide', S.wide); persist(); } }, icon('chev'), h('span', { class: 'lbl' }, 'Collapse')));
    railEl.append(top, h('div', { class: 'rail-spacer' }), bottom);
    syncRail();
  }
  function railBtn(p) {
    return h('button', { class: 'rail-btn', 'data-page': p.id, title: p.label, onclick: () => { if (p.action) return p.action(); toggle(p.id); } }, icon(p.icon), h('span', { class: 'lbl' }, p.label), h('i', { class: 'dot' }));
  }
  function syncRail() {
    const v = view();
    for (const b of railEl.querySelectorAll('[data-page]')) {
      const p = pages.find((x) => x.id === b.dataset.page);
      b.classList.toggle('on', S.open && S.page === p.id);
      const flag = v && p.flag ? p.flag(v) : null;
      b.classList.toggle('flag', !!flag);
      const dot = b.querySelector('.dot'); dot.className = 'dot' + (flag && flag !== true ? ' ' + flag : '');
    }
  }

  function open(pageId, tabId) {
    if (pageId) S.page = pageId;
    if (tabId) S.tab[S.page] = tabId;
    S.open = true; S.detail = null;
    opsEl.classList.add('show');
    persist(); render();
  }
  function close() { S.open = false; opsEl.classList.remove('show'); persist(); syncRail(); if (typeof window.focusChart === 'function') window.focusChart(); }
  function toggle(pageId) { if (S.open && S.page === pageId) close(); else open(pageId); }
  function goTab(tabId) { S.tab[S.page] = tabId; S.detail = null; S.sel = null; persist(); render(); }
  function isOpen() { return S.open; }

  function update(snap) {
    const prev = S.snap;
    S.snap = snap;
    const v = snap && snap.view;
    if (v) {
      const loc = v.location ? v.location.id : null;
      if (loc !== S.lastLocationId) {
        // The convoy just parked somewhere (or is still parked here and the snapshot
        // side changed): remember what the shelf asked, then land on the City page's
        // home tab so the wire, the expo and the movers are in front.
        const arrived = !!v.location && !!prev && !!prev.view && !!prev.view.travel;
        S.lastLocationId = loc;
        S.sel = null;
        if (S.detail && S.detail.kind === 'candidate') S.detail = null;
        if (arrived && loc) {
          S.memo = S.memo || {};
          const buys = {};
          for (const m of v.market) buys[m.goodId] = m.buy;
          S.arrival = { cityId: loc, prev: S.memo[loc] || null, day: v.day };
          S.memo[loc] = { day: v.day, buy: buys };
          S.page = 'city'; S.tab.city = 'home'; S.open = true; S.detail = null; S.sel = null;
          if (opsEl) opsEl.classList.add('show');
          persist();
        }
      }
    }
    syncRail();
    if (!S.open) return;
    const a = document.activeElement;
    if (a && opsEl.contains(a) && (a.tagName === 'INPUT' || a.tagName === 'SELECT')) { S.dirty = true; return; }
    render();
  }

  function render() {
    if (!S.open || !opsEl) return;
    const v = view();
    const page = pages.find((p) => p.id === S.page) || pages[0];
    const scrollMain = mainEl ? mainEl.scrollTop : 0, scrollSide = sideEl ? sideEl.scrollTop : 0;
    opsEl.innerHTML = '';
    opsEl.classList.toggle('full', S.full);

    const crumbs = h('div', { class: 'ops-crumbs' }, h('span', null, 'Mecha Trader'), h('span', { class: 'sep' }, '›'), h('b', null, page.label));
    if (v) {
      const sub = page.subtitle ? page.subtitle(v) : null;
      if (sub) crumbs.append(h('span', { class: 'sep' }, '·'), h('span', { class: 'sub' }, sub));
    }
    const actions = h('div', { class: 'head-actions' });
    if (v) {
      const travelling = !!v.travel;
      actions.append(
        h('input', { class: 'inp sm', type: 'number', min: 1, max: 30, value: S.waitDays, style: 'width:56px', title: 'Days to pass', onchange: (e) => { S.waitDays = Math.max(1, Math.min(30, Math.floor(Number(e.target.value) || 1))); } }),
        btn(travelling ? 'On the road' : 'Pass ' + fmt.days(S.waitDays), () => send({ type: 'wait', days: S.waitDays }), { disabled: travelling, size: 'sm', title: travelling ? 'Days pass on the chart while you travel' : 'Let the days go by while parked' })
      );
    }
    actions.append(
      h('button', { class: 'ops-iconbtn', title: S.full ? 'Dock beside the chart' : 'Full width', onclick: () => { S.full = !S.full; persist(); render(); } }, icon('expand', '')),
      h('button', { class: 'ops-iconbtn', title: 'Close (Esc)', onclick: close }, icon('close', ''))
    );
    opsEl.append(h('header', { class: 'ops-head' }, crumbs, h('span', { class: 'grow' }), actions));

    const pageTabs = (tabs[page.id] || []).filter((t) => !t.when || (v && t.when(v)));
    let tab = null;
    if (pageTabs.length) {
      tab = pageTabs.find((t) => t.id === S.tab[page.id]) || pageTabs[0];
      opsEl.append(h('div', { class: 'ops-tabs' }, pageTabs.map((t) => {
        const cnt = v && t.count ? t.count(v) : null;
        return h('button', { class: 'ops-tab' + (t === tab ? ' on' : '') + (v && t.flag && t.flag(v) ? ' flag' : ''), onclick: () => goTab(t.id) }, t.label, cnt != null ? h('span', { class: 'cnt' }, cnt) : null);
      })));
    }

    mainEl = h('div', { class: 'ops-main' });
    sideEl = h('aside', { class: 'ops-side' });
    const body = h('div', { class: 'ops-body' }, mainEl, sideEl);
    opsEl.append(body);

    if (!v) mainEl.append(empty('Not linked', 'The house books have not arrived from the game server yet.'));
    else if (S.detail && page.detail) mainEl.append(page.detail(v, S.detail) || empty('Gone', 'That record is no longer on the books.'));
    else if (tab) { const out = tab.render(v, sideEl); if (out) mainEl.append(out); }
    else if (page.render) { const out = page.render(v, sideEl); if (out) mainEl.append(out); }

    if (page.footer) { const f = page.footer(v); if (f) opsEl.append(f); }
    mainEl.scrollTop = scrollMain; sideEl.scrollTop = scrollSide;
    syncRail();
  }

  function sidePane(side, { title, meta, body, foot }) {
    side.innerHTML = '';
    side.classList.add('show');
    side.append(
      h('div', { class: 'side-head' }, h('div', null, h('h3', null, title), meta ? h('div', { class: 'meta' }, meta) : null), h('button', { class: 'ops-iconbtn close', onclick: () => { S.sel = null; render(); } }, icon('close', ''))),
      h('div', { class: 'side-body' }, body),
      foot ? h('div', { class: 'side-foot' }, foot) : null
    );
  }

  // ── shared pieces ─────────────────────────────────────────────────────────
  function whereText(v) {
    if (v.travel) return `On the road · ${v.travel.fromName} → ${v.travel.toName}`;
    if (v.location) return `${v.location.name}, ${v.location.region}`;
    if (v.site) return v.site.name;
    if (v.field) return `Open country · ${v.field.biome}`;
    return '—';
  }
  function positionCard(v) {
    if (v.travel) {
      const t = v.travel, done = t.totalDays - t.daysRemaining;
      return card('Position', [
        h('div', { style: 'font-size:20px;font-weight:600;color:var(--ops-text-strong);margin-bottom:8px' }, t.fromName, h('span', { class: 'arrow' }, '  →  '), t.toName),
        meter(t.totalDays ? done / t.totalDays : 0, 'amber'),
        h('div', { style: 'margin-top:10px' }, dl([['Days remaining', fmt.days(t.daysRemaining)], ['Journey', fmt.days(t.totalDays)], ['Fuel per day', fmt.cr(t.fuelPerDay)], ['Arrives', 'day ' + (v.day + t.daysRemaining)]]))
      ]);
    }
    if (v.location) {
      const L = v.location;
      return card('Position', [
        h('div', { style: 'font-size:20px;font-weight:600;color:var(--ops-text-strong)' }, L.name),
        h('div', { class: 'note', style: 'margin:2px 0 10px' }, L.region + ' · ' + L.industries.join(', ')),
        dl([['Governor', L.standing.governorName], ['Standing', h('span', null, badge(L.standing.rank, toneOf(L.standing.tone)), ' ' + fmt.n(L.standing.value, 0))], ['Roads out', v.routes.length], ['Wire', L.news.length ? L.news.length + ' live' : 'quiet']])
      ], { foot: [btn('Open city', () => open('city', 'market'), { kind: 'primary', size: 'sm' }), btn('Market', () => open('city', 'market'), { size: 'sm' }), btn('Recruit', () => open('crew', 'recruit'), { size: 'sm' })] });
    }
    if (v.site) {
      const s = v.site;
      return card('Position', [
        h('div', { style: 'font-size:20px;font-weight:600;color:var(--ops-text-strong);margin-bottom:8px' }, s.name),
        dl([['Ore', s.goodName], ['Remaining', fmt.n(s.remaining, 0) + ' u'], ['Expected per day', fmt.n(s.expectedYield, 1) + ' u'], ['Rig', s.canMine ? badge('ready', 'good') : badge('no gear', 'warn')]]),
        h('div', { class: 'note', style: 'margin-top:10px' }, s.hint)
      ]);
    }
    return card('Position', [h('div', { style: 'font-size:20px;font-weight:600;color:var(--ops-text-strong)' }, 'Open country'), h('div', { class: 'note' }, v.field ? v.field.biome + ' · cell ' + v.field.cellId : '')]);
  }
  function convoyCard(v) {
    const c = v.convoy;
    return card('Convoy', [
      h('div', { class: 'summary-line' }, h('span', null, 'Hold'), h('b', null, fmt.vol(c.used) + ' / ' + fmt.vol(c.capacity) + ' vol')),
      meter(c.capacity ? c.used / c.capacity : 0, c.used / c.capacity > 0.9 ? 'warn' : 'ok'),
      h('div', { style: 'margin-top:10px' }, dl([['Speed', fmt.n(c.speedKmPerDay, 0) + ' km/day'], ['Burn per day', fmt.cr(c.dailyUpkeep)], ['Trucks', c.trucks.length ? c.trucks.join(', ') : 'none'], ['Gear', c.gear.length ? c.gear.join(', ') : 'none'], ['Mining', c.canMine ? fmt.n(c.mineYield, 1) + ' u/day' : 'no rig']]))
    ], { foot: [btn('Caravan', () => open('caravan', 'summary'), { size: 'sm' })] });
  }
  function cargoTable(v, id) {
    const sellHere = {}; for (const m of v.market) sellHere[m.goodId] = m;
    return table({
      id: id || 'cargo', rows: v.cargo, rowKey: (r) => r.goodId, emptyText: 'The hold is empty.',
      defaultSort: { id: 'name', dir: 'asc' },
      cols: [
        { id: 'name', label: 'Good', get: (r) => r.name, cell: (r) => [tierName(r), h('span', { class: 'sub' }, r.category + ' · ' + r.tierName)] },
        { id: 'units', label: 'Units', num: true, get: (r) => r.units, cell: (r) => fmt.n(r.units) },
        { id: 'averageCost', label: 'Avg cost', num: true, get: (r) => r.averageCost, cell: (r) => fmt.n(r.averageCost, 1) },
        { id: 'quality', label: 'Quality', num: true, get: (r) => r.quality, cell: (r) => [fmt.n(r.quality, 0), r.sTier ? [' ', badge('S', 's')] : null] },
        { id: 'volume', label: 'Volume', num: true, get: (r) => r.volume, cell: (r) => fmt.vol(r.volume) },
        { id: 'here', label: 'Sells here', num: true, get: (r) => sellHere[r.goodId] ? sellHere[r.goodId].sell : -1, cell: (r) => { const m = sellHere[r.goodId]; if (!m) return h('span', { class: 'tone-muted' }, '—'); const d = (m.sell - r.averageCost) * r.units; return [fmt.n(m.sell, 1), h('span', { class: 'sub ' + (d >= 0 ? 'up' : 'down') }, fmt.signedCr(d) + ' vs cost')]; } },
        { id: 'act', label: '', sortable: false, cell: (r) => sellHere[r.goodId] ? h('div', { class: 'mini-actions' }, btn('Sell', () => { S.mode = 'sell'; S.sel = r.goodId; S.qty['trade:' + r.goodId] = r.units; open('city', 'market'); }, { size: 'xs' })) : null }
      ]
    });
  }
  function roadsTable(v, { compact } = {}) {
    return table({
      id: 'roads', rows: v.routes, rowKey: (r) => r.toId, emptyText: 'No road leaves this city.',
      defaultSort: { id: 'bestProfit', dir: 'desc' },
      cols: [
        { id: 'toName', label: 'Destination', cell: (r) => [r.toName, h('span', { class: 'sub' }, r.toRegion + ' · ' + r.terrainName)] },
        { id: 'distanceKm', label: 'Distance', num: true, get: (r) => r.distanceKm, cell: (r) => fmt.n(r.distanceKm) + ' km' },
        { id: 'days', label: 'Days', num: true, get: (r) => r.days },
        { id: 'estimatedFuel', label: 'Fuel', num: true, get: (r) => r.estimatedFuel, cell: (r) => fmt.cr(r.estimatedFuel) },
        { id: 'best', label: 'Best cargo', get: (r) => r.bestGoodName || '', cell: (r) => r.bestGoodId ? [r.bestGoodName, h('span', { class: 'sub' }, fmt.n(r.bestUnits) + ' units')] : h('span', { class: 'tone-muted' }, 'nothing clears') },
        { id: 'bestProfit', label: 'Est. margin', num: true, get: (r) => r.bestProfit, cell: (r) => h('span', { class: r.bestProfit > 0 ? 'up' : 'down' }, fmt.signedCr(r.bestProfit)) },
        { id: 'act', label: '', sortable: false, cell: (r) => h('div', { class: 'mini-actions' }, r.bestGoodId && !compact ? btn('Load best', () => { S.mode = 'buy'; S.sel = r.bestGoodId; S.qty['trade:' + r.bestGoodId] = r.bestUnits; open('city', 'market'); }, { size: 'xs' }) : null, btn('Depart', () => depart(r.toId), { size: 'xs', kind: 'primary' })) }
      ]
    });
  }
  function newsList(news, emptyText) {
    if (!news.length) return h('div', { class: 'tbl-empty' }, emptyText || 'The wire is quiet.');
    return h('div', { class: 'wire' }, news.map((n) => h('div', { class: 'item' }, h('span', { class: 'day' }, 'd' + n.day), h('div', { class: 'grow' }, h('b', null, n.headline), h('span', null, n.detail)), h('div', null, badge(n.kind, toneOf(n.tone)), h('div', { class: 'note', style: 'text-align:right;margin-top:4px' }, n.daysLeft > 0 ? fmt.days(n.daysLeft) + ' left' : 'ending')))));
  }
  function logList(log, limit) {
    const rows = limit ? log.slice(0, limit) : log;
    if (!rows.length) return h('div', { class: 'tbl-empty' }, 'Nothing on the books yet.');
    const tone = (k) => ({ Trade: 'trade', Warning: 'warn', Rejected: 'reject', Bankrupt: 'bad', Alert: 'bad' })[k] || '';
    return h('div', { class: 'wire log' }, rows.map((e) => h('div', { class: 'item ' + tone(e.kind) }, h('span', { class: 'day' }, 'd' + e.day), h('span', { class: 'kind' }, badge(e.kind, tone(e.kind) === 'trade' ? 'ok' : tone(e.kind) === 'warn' || tone(e.kind) === 'reject' ? 'warn' : tone(e.kind) === 'bad' ? 'bad' : 'muted')), h('span', { class: 'msg grow' }, e.message))));
  }

  // person = crew member or candidate. teamSkills = v.crew.skills (for max + leader).
  function personPage(v, p, kind) {
    const team = v.crew.skills;
    const maxOf = (id) => { const s = team.find((x) => x.id === id); return s ? s.maxLevel : 10; };
    const leads = (id) => kind === 'member' && (team.find((x) => x.id === id) || {}).leaderName === p.name;
    const knowledge = p.knowledge.slice().sort((a, b) => b.level - a.level);
    const best = knowledge[0];
    const isMember = kind === 'member';
    let hireBlock = null;
    if (!isMember) {
      const why = !p.roomAboard ? 'No seat aboard — dismiss someone or the convoy is full.' : !p.affordable ? 'Signing fee is more than the house holds.' : null;
      hireBlock = card('Hire', [
        dl([['Signing fee', fmt.cr(p.signingFee)], ['Daily wage', fmt.cr(p.dailyWage)], ['Seats', v.crew.size + ' / ' + v.crew.capacity], ['Cash after signing', fmt.cr(v.cash - p.signingFee), v.cash - p.signingFee < 0 ? 'tone-bad' : '']]),
        why ? h('div', { class: 'note tone-warn', style: 'margin-top:10px' }, why) : null
      ], { foot: btn('Hire ' + p.name.split(' ')[0], () => send({ type: 'hireCrew', candidateId: p.id }, { done: p.name + ' signed on.' }).then((s) => { if (s && !s.error) { S.detail = null; S.tab.crew = 'roster'; S.page = 'crew'; render(); } }), { kind: 'primary', disabled: !!why }) });
    } else {
      const post = v.crew.posts.find((x) => x.id === p.postId);
      hireBlock = [
        card('Post', [
          h('div', { class: 'field' }, h('label', null, 'Job aboard'), postSelect(v, p)),
          h('div', { class: 'note', style: 'margin-top:8px' }, post ? post.blurb : 'No post. Road and book skills still count for the convoy; counter and information work needs a post.')
        ], { hint: post ? post.skillNames : null }),
        card('Contract', [
          dl([['Daily wage', fmt.cr(p.dailyWage)], ['Severance', fmt.cr(p.severance)], ['Hired', 'day ' + p.hiredDay + ' at ' + p.hiredAt], ['Aboard for', fmt.days(v.day - p.hiredDay)]])
        ], { foot: btn('Dismiss', () => confirm('Dismiss ' + p.name + '?', 'Severance of ' + fmt.cr(p.severance) + ' is paid at once and the seat opens. Whatever they lead for the convoy stops with them.', { label: 'Dismiss and pay', run: () => send({ type: 'dismissCrew', crewId: p.id }, { done: p.name + ' paid off.' }).then((s) => { if (s && !s.error) { S.detail = null; render(); } }) }, 'danger'), { kind: 'danger' }) })
      ];
    }
    return h('div', null,
      h('div', { style: 'display:flex;align-items:center;gap:14px;margin-bottom:16px' },
        btn([icon('back', ''), ' Back'], () => { S.detail = null; render(); }, { kind: 'ghost', size: 'sm' }),
        avatar(p.name, true),
        h('div', null, h('div', { style: 'font-size:24px;font-weight:600;color:var(--ops-text-strong)' }, p.name), h('div', { class: 'note' }, badge(p.roleName, 'ok'), ' ', p.postName ? [badge(p.postName, 'outline'), ' '] : null, isMember ? 'On the payroll since day ' + p.hiredDay : 'On the board at ' + v.crew.recruitment.cityName + ' · board refreshes in ' + fmt.days(v.crew.recruitment.refreshInDays) + (p.postName ? ' · signs on to ' + p.postName.toLowerCase() : ''))),
        h('span', { class: 'grow', style: 'flex:1' }),
        p.traits.map((t) => badge(t.name, 'amber'))
      ),
      h('div', { class: 'ops-grid c4' },
        kpi('Daily wage', fmt.cr(p.dailyWage)),
        kpi(isMember ? 'Severance' : 'Signing fee', fmt.cr(isMember ? p.severance : p.signingFee)),
        kpi('Best skill', (() => { const s = p.skills.slice().sort((a, b) => b.level - a.level)[0]; return s ? s.name + ' ' + s.level : '—'; })()),
        kpi('Best knowledge', best ? best.name : '—', best ? best.level + ' / ' + best.maxLevel : '')
      ),
      h('div', { class: 'ops-grid c3' },
        card('Skills', h('div', null, p.skills.map((s) => h('div', { class: 'skill' },
          h('div', { class: 'nm' }, s.name, h('small', null, (team.find((x) => x.id === s.id) || {}).lever || '')),
          pips(s.level, maxOf(s.id), leads(s.id)),
          h('div', { class: 'lv' }, s.level, h('small', null, ' / ' + maxOf(s.id)), leads(s.id) ? [' ', badge('leads', 'amber')] : null)
        ))), { hint: isMember ? 'amber = leads the convoy' : null }),
        card('Category knowledge', h('div', null, knowledge.map((k) => h('div', { class: 'stat-row' }, h('div', { class: 'name' }, h('b', null, k.name)), meter(k.level / k.maxLevel, k.level >= 50 ? 'good' : k.level >= 20 ? 'ok' : 'muted'), h('div', { class: 'val' }, k.level, h('small', null, '/ ' + k.maxLevel))))), { hint: 'the eye for a shelf' }),
        h('div', null,
          card('Traits', p.traits.length ? h('div', null, p.traits.map((t) => h('div', { class: 'stat-row' }, h('div', { class: 'name' }, h('b', null, t.name), h('span', null, t.blurb)), badge(t.kind, 'amber')))) : h('div', { class: 'note' }, 'No special trait.')),
          hireBlock
        )
      )
    );
  }
  function personRow(p, extra) {
    return [h('div', { class: 'person' }, avatar(p.name), h('div', { class: 'who' }, h('b', null, p.name), h('span', null, p.roleName + (p.postName ? ' · ' + p.postName : ''))))];
  }
  /** The post picker for one hand. Posts come from the view; the shell never decides what a post does. */
  function postSelect(v, m) {
    const nameOf = (id) => { const p = v.crew.posts.find((x) => x.id === id); return p ? p.name.toLowerCase() : ''; };
    return h('select', {
      class: 'inp sm', title: 'Which job this hand is on',
      onclick: (e) => e.stopPropagation(),
      onchange: (e) => { const id = e.target.value; send({ type: 'assignCrew', crewId: m.id, postId: id }, { done: m.name + (id ? ' posted to ' + nameOf(id) : ' stood down') + '.' }); }
    },
      h('option', { value: '', selected: !m.postId }, 'No post'),
      v.crew.posts.map((p) => h('option', { value: p.id, selected: m.postId === p.id }, p.name)));
  }
  function postCards(v) {
    const I = v.crew.intel;
    return h('div', { class: 'ops-grid c3' },
      v.crew.posts.map((p) => card(p.name, [
        h('div', { class: 'summary-line' }, h('span', null, 'Led by'), h('b', null, p.leaderName || h('span', { class: 'tone-muted' }, 'nobody'))),
        h('div', { class: 'summary-line' }, h('span', null, 'Hands'), h('b', null, p.hands)),
        h('div', { class: 'note', style: 'margin-top:8px' }, p.blurb)
      ], { hint: p.skillNames })),
      card('Word from elsewhere', [
        h('div', { class: 'summary-line' }, h('span', null, 'Informant'), h('b', null, I.informantName || h('span', { class: 'tone-muted' }, 'nobody'))),
        h('div', { class: 'summary-line' }, h('span', null, 'Reach'), h('b', null, I.reach + ' / ' + I.maxReach + ' cities')),
        h('div', { class: 'summary-line' }, h('span', null, 'Accuracy'), h('b', { class: I.active ? (I.errorPct <= 10 ? 'tone-good' : 'tone-warn') : 'tone-muted' }, I.active ? '±' + fmt.n(I.errorPct, 0) + '%' : '—')),
        h('div', { class: 'note', style: 'margin-top:8px' }, I.summary + '. Prices from other cities show in the trade pane.')
      ], { hint: 'intelligence ' + I.level + ' / ' + I.maxLevel })
    );
  }
  function skillsMini(p, team) {
    return h('div', { style: 'display:flex;gap:10px' }, p.skills.map((s) => h('span', { title: s.name }, h('span', { class: 'tone-muted', style: 'font-size:13.5px;letter-spacing:.04em' }, s.name.slice(0, 3).toUpperCase() + ' '), h('b', { style: 'color:var(--ops-text-strong)' }, s.level))));
  }

  // ── pages ─────────────────────────────────────────────────────────────────
  registerPage({
    id: 'overview', label: 'Overview', icon: 'overview', order: 10,
    subtitle: (v) => whereText(v),
    flag: (v) => v.bankrupt ? 'bad' : (v.cash < 3000 ? 'warn' : null),
    render(v) {
      const c = v.convoy;
      const out = h('div', null,
        h('div', { class: 'ops-grid c6' },
          kpi('Credits', fmt.cr(v.cash), v.bankrupt ? 'insolvent' : 'in hand', v.bankrupt ? 'bad' : v.cash < 3000 ? 'warn' : null),
          kpi('Net worth', fmt.cr(v.netWorth), 'cash + hold at local sell'),
          kpi('Day', v.day, v.travel ? 'travelling' : 'parked'),
          kpi('Burn / day', fmt.cr(c.dailyUpkeep), 'upkeep, wages' + (v.warehouse.rented ? ', rent' : '')),
          kpi('Hold', fmt.vol(c.used) + ' / ' + fmt.vol(c.capacity), fmt.pct(c.capacity ? c.free / c.capacity : 0) + ' free'),
          kpi('Crew', v.crew.size + ' / ' + v.crew.capacity, fmt.cr(v.crew.dailyWages) + ' wages / day')
        ),
        h('div', { class: 'ops-grid c3' }, positionCard(v), convoyCard(v),
          card(v.location ? 'City wire' : 'Latest', v.location ? newsList(v.location.news) : logList(S.snap.log, 6), { tight: true, actions: btn('Ledger', () => open('ledger'), { kind: 'ghost', size: 'xs' }) })),
        card('Cargo', cargoTable(v, 'cargo-ov'), { tight: true, hint: v.location ? 'valued at local sell' : 'no market here' })
      );
      if (v.location) out.append(card('Roads out of ' + v.location.name, roadsTable(v, { compact: false }), { tight: true, hint: 'best single cargo by the scouts\' estimate' }));
      return out;
    }
  });

  // City
  registerPage({
    id: 'city', label: 'City', icon: 'city', order: 20,
    subtitle: (v) => v.location ? v.location.name + ', ' + v.location.region : 'not in a city',
    flag: (v) => !!v.location,
    render(v) { return roadState(v); }
  });
  function roadState(v) {
    return empty(v.travel ? 'On the road to ' + v.travel.toName : 'Not in a city', v.travel ? fmt.days(v.travel.daysRemaining) + ' to go. City pages open when the convoy parks.' : 'Drive into a city on the chart, or click one to pathfind there.', btn('Back to the chart', close, { kind: 'primary' }));
  }
  const inCity = (v) => !!v.location;

  // The Home tab: the page that opens when the convoy rolls into town. It shows what
  // the player came to learn — the latest dispatches, the expo's status and what moved
  // on the board since the last park. All figures come already resolved from the view.
  registerTab('city', {
    id: 'home', label: 'Home', order: 0,
    render(v) {
      if (!inCity(v)) return roadState(v);
      const L = v.location, E = v.expo;
      const arr = S.arrival && S.arrival.cityId === L.id ? S.arrival : null;
      const base = arr ? arr.prev : null;           // last park's shelf: the baseline
      const sinceDay = base ? base.day : null;
      let movers = [];
      if (base && base.buy) {
        movers = v.market
          .map((m) => { const old = base.buy[m.goodId]; return old ? { m, old, pct: (m.buy - old) / old } : null; })
          .filter((x) => x)
          .sort((a, b) => Math.abs(b.pct) - Math.abs(a.pct));
        if (!movers.length || Math.abs(movers[0].pct) < 0.01) movers = [];
        else movers = movers.slice(0, 8);
      }
      const moversNote = sinceDay != null
        ? 'Nothing moved more than a percent since day ' + sinceDay + '. The shelf asks about what it asked.'
        : 'First park here — prices are as they settle.';
      const expoFoot = btn('Expo', () => goTab('expo'), { size: 'sm' });
      return h('div', null,
        h('div', { class: 'ops-grid c4' },
          kpi('Day', v.day, 'parked in ' + L.name),
          kpi('Standing', L.standing.rank, L.standing.value > 0 ? fmt.n(L.standing.value, 0) + ' total' : 'no standing yet', toneOf(L.standing.tone)),
          kpi('Wire', L.news.length, L.news.length ? 'live dispatches' : 'quiet'),
          kpi('Expo', E.running ? 'open' : 'in ' + fmt.days(E.startsIn), E.title + ' · ' + fmt.pct(E.buff) + ' buff', E.running ? 'good' : null)
        ),
        h('div', { class: 'ops-grid c2' },
          card('Latest events · ' + L.name, newsList(L.news.slice(0, 3), 'The wire is quiet.'), { tight: true, foot: L.news.length > 3 ? btn('Wire', () => goTab('wire'), { size: 'sm' }) : null }),
          card('Trade expo', dl([
            ['Status', E.running ? h('span', { class: 'tone-good' }, 'open · ' + fmt.days(E.daysLeft) + ' left') : 'opens in ' + fmt.days(E.startsIn) + ' · runs ' + fmt.days(E.durationDays)],
            ['Theme', E.title],
            ['Buff', fmt.pct(E.buff)],
            ['Pass', E.passHeld ? badge('held', 'good') : fmt.cr(E.fee) + ' to open a stall']
          ]), { foot: expoFoot })
        ),
        card('Prices moved' + (sinceDay != null ? ' · since day ' + sinceDay : ''),
          movers.length ? h('div', null, movers.map(({ m, old, pct }) => h('div', { class: 'stat-row' },
            h('div', { class: 'name' }, h('b', { style: 'color:' + m.tierColor }, m.name), h('span', null, fmt.n(old, 1) + ' → ' + fmt.n(m.buy, 1) + ' cr' + (m.eventHint ? ' · ' + m.eventHint : ''))),
            h('div', { class: 'val' }, h('span', { class: pct > 0 ? 'tone-good' : 'tone-bad' }, fmt.signed(pct * 100, 0) + '%')))))
          : h('div', { class: 'note' }, moversNote),
          { tight: true, foot: btn('Market', () => goTab('market'), { size: 'sm' }) })
      );
    }
  });

  registerTab('city', {
    id: 'market', label: 'Market', order: 10,
    count: (v) => v.location ? v.market.length : null,
    render(v, side) {
      if (!inCity(v)) return roadState(v);
      const q = (S.q.market || '').toLowerCase();
      const cat = S.cat.market || 'all';
      const cats = [...new Set(v.market.map((m) => m.category))];
      let rows = v.market.filter((m) => (cat === 'all' || m.category === cat || (cat === 'held' && m.held > 0)) && (!q || m.name.toLowerCase().includes(q) || m.category.toLowerCase().includes(q)));
      const toolbar = h('div', { class: 'tbl-toolbar' },
        h('div', { class: 'search' }, icon('search', ''), h('input', { class: 'inp sm', placeholder: 'Find a good…', value: S.q.market || '', oninput: (e) => { S.q.market = e.target.value; S.dirty = true; renderSoon(); } })),
        h('div', { class: 'seg' }, [['all', 'All'], ['held', 'In hold'], ...cats.map((c) => [c, c])].map(([id, label]) => h('button', { class: id === cat ? 'on' : '', onclick: () => { S.cat.market = id; render(); } }, label))),
        h('span', { class: 'grow' }),
        tierLegend(v)
      );
      const grid = table({
        id: 'market', rows, rowKey: (r) => r.goodId, selected: S.sel, emptyText: 'No good matches.',
        defaultSort: { id: 'name', dir: 'asc' },
        onRow: (r) => { S.sel = r.goodId; if (S.mode === 'sell' && r.held === 0) S.mode = 'buy'; render(); },
        rowClass: (r) => r.locked ? 'locked-row' : '',
        cols: [
          { id: 'name', label: 'Good', get: (r) => r.name, cell: (r) => [tierName(r), h('span', { class: 'sub' }, r.category + ' · ' + r.tierName + (r.reliefHint ? ' · relieves ' + r.reliefHint.toLowerCase() : ''))] },
          { id: 'buy', label: 'Buy', num: true, get: (r) => r.buy, cell: (r) => [fmt.n(r.buy, 1), r.eventHint ? h('span', { class: 'sub tone-warn', title: r.eventHint }, 'event') : null] },
          { id: 'sell', label: 'Sell', num: true, get: (r) => r.sell, cell: (r) => [fmt.n(r.sell, 1), r.reliefPerUnit > 0 ? h('span', { class: 'sub tone-good', title: 'citizen standing per unit sold while the shortage runs' }, '+' + fmt.n(r.reliefPerUnit * 10, 1) + ' / 10u') : null] },
          { id: 'basePrice', label: 'vs base', num: true, get: (r) => r.buy / r.basePrice, cell: (r) => { const x = r.buy / r.basePrice; return h('span', { class: x < 0.9 ? 'up' : x > 1.15 ? 'down' : 'tone-muted' }, (x * 100).toFixed(0) + '%'); } },
          { id: 'shelf', label: 'Shelf', num: true, get: (r) => r.shelf, cell: (r) => [fmt.n(r.shelf), r.reserved > 0 ? h('span', { class: 'sub' }, fmt.n(r.reserved) + ' held for you') : null] },
          { id: 'intake', label: 'Intake', num: true, get: (r) => r.intake, cell: (r) => r.intake > 0 ? fmt.n(r.intake) : h('span', { class: 'tone-muted' }, '—') },
          { id: 'flow', label: 'Flow', get: (r) => r.flow, cell: (r) => flowBadge(r.flow) },
          { id: 'averageQuality', label: 'Quality', num: true, get: (r) => r.averageQuality, cell: (r) => [fmt.n(r.averageQuality), r.knowledge > 0 && r.pickQuality > r.averageQuality ? h('span', { class: 'sub up' }, 'pick ' + fmt.n(r.pickQuality)) : null, r.sTierPossible ? [' ', badge('S', 's')] : null] },
          { id: 'held', label: 'In hold', num: true, get: (r) => r.held, cell: (r) => r.held > 0 ? [fmt.n(r.held), h('span', { class: 'sub' }, '@ ' + fmt.n(r.averageCost, 1))] : h('span', { class: 'tone-muted' }, '—') },
          { id: 'pl', label: 'Vs cost', num: true, get: (r) => r.held > 0 ? (r.sell - r.averageCost) * r.held : -1e12, cell: (r) => { if (r.held <= 0) return ''; const d = (r.sell - r.averageCost) * r.held; return h('span', { class: d >= 0 ? 'up' : 'down' }, fmt.signedCr(d)); } },
          { id: 'act', label: '', sortable: false, cell: (r) => h('div', { class: 'mini-actions' }, btn('Buy', () => { S.mode = 'buy'; S.sel = r.goodId; render(); }, { size: 'xs', disabled: r.locked, title: r.locked ? 'standing ' + fmt.n(r.unlockStanding) + ' needed' : null }), r.held > 0 ? btn('Sell', () => { S.mode = 'sell'; S.sel = r.goodId; render(); }, { size: 'xs' }) : null) }
        ]
      });
      const row = S.sel ? v.market.find((m) => m.goodId === S.sel) : null;
      if (row) tradePane(v, row, side);
      return h('div', null, crewBriefCard(v), card(null, [toolbar, grid], { tight: true }));
    }
  });
  let renderTimer = null;
  function renderSoon() { clearTimeout(renderTimer); renderTimer = setTimeout(() => { const a = document.activeElement; const restore = a && a.classList && a.classList.contains('inp') ? a.placeholder : null; S.dirty = false; render(); if (restore) { const again = opsEl.querySelector(`input[placeholder="${restore}"]`); if (again) { again.focus(); again.setSelectionRange(again.value.length, again.value.length); } } }, 120); }

  /** The crew's parked-city read: what in the hold clears the fuel line here. */
  function crewBriefCard(v) {
    const B = v.crewBrief;
    if (!B || !B.rows || !B.rows.length) return null;
    const minPct = Math.round(B.minMargin * 100);
    const head = h('thead', null, h('tr', null,
      h('th', null, 'Good'), h('th', { class: 'num' }, 'Units'), h('th', { class: 'num' }, 'Cost'),
      h('th', { class: 'num' }, 'Sells'), h('th', { class: 'num' }, 'Margin'), h('th', { class: 'num' }, 'Clear')));
    const body = h('tbody', null, B.rows.map((r) => h('tr', null,
      h('td', null, r.name, h('span', { class: 'sub' }, r.category)),
      h('td', { class: 'num' }, fmt.n(r.units)),
      h('td', { class: 'num' }, fmt.n(r.averageCost, 1)),
      h('td', { class: 'num' }, fmt.n(r.sell, 1)),
      h('td', { class: 'num' }, r.marginPct == null ? h('span', { class: 'up' }, 'free') : h('span', { class: 'up' }, '+' + fmt.n(r.marginPct, 1) + '%')),
      h('td', { class: 'num' }, h('span', { class: r.profit >= 0 ? 'up' : 'down' }, fmt.signedCr(r.profit))))));
    return card('Crew report · ' + v.location.name,
      h('div', { class: 'tbl-wrap' }, h('table', { class: 'tbl' }, head, body)),
      { tight: true, hint: 'clears the ' + minPct + '% fuel line over cost basis' });
  }

  /** What the information post says this good fetches nearby. Sell offers when selling, asks when buying. */
  function elsewhereCard(v, r, buying) {
    const I = v.crew.intel;
    const here = buying ? r.buy : r.sell;
    if (!r.elsewhere || !r.elsewhere.length) {
      return card('Nearby markets', h('div', { class: 'note' }, I.active ? 'No other market within reach.' : 'Nobody on the information post. Put a hand on it and the nearest cities report what they pay for this.'), { foot: !I.active ? btn('Crew posts', () => open('crew', 'roster'), { size: 'sm' }) : null });
    }
    const rows = r.elsewhere.slice().sort((a, b) => buying ? a.buy - b.buy : b.sell - a.sell);
    const bestVal = buying ? rows[0].buy : rows[0].sell;
    return card('Nearby markets', h('div', { class: 'tbl-wrap' }, h('table', { class: 'tbl' },
      h('thead', null, h('tr', null, h('th', null, 'City'), h('th', { class: 'num' }, 'Days'), h('th', { class: 'num' }, buying ? 'Ask' : 'Offer'), h('th', { class: 'num' }, 'vs here'))),
      h('tbody', null, rows.map((e) => {
        const val = buying ? e.buy : e.sell;
        const d = buying ? here - val : val - here;
        return h('tr', { class: val === bestVal ? 'sel' : '' },
          h('td', null, e.cityName, h('span', { class: 'sub' }, e.region + ' · ' + fmt.n(e.distanceKm) + ' km · ' + (e.flow === 'surplus' ? 'makes' : e.flow === 'deficit' ? 'eats' : 'balanced'))),
          h('td', { class: 'num' }, e.days),
          h('td', { class: 'num' }, fmt.n(val, 1), e.errorPct > 0 ? h('span', { class: 'sub' }, '±' + fmt.n(e.errorPct, 0) + '%') : null),
          h('td', { class: 'num' }, h('span', { class: d > 0 ? 'up' : d < 0 ? 'down' : 'tone-muted' }, fmt.signed(d, 1))));
      })))), { tight: true, hint: (I.informantName || 'informant') + ' · ' + (I.errorPct > 0 ? 'within ±' + fmt.n(I.errorPct, 0) + '%' : 'exact') + ' · today' });
  }

  function tradePane(v, r, side) {
    const key = 'trade:' + r.goodId;
    const buying = S.mode === 'buy';
    const cap = buying ? Math.floor(r.shelf) : r.held;
    const unit = buying ? r.buy : r.sell;
    const qty = Math.max(0, Math.min(cap, S.qty[key] === undefined ? Math.min(cap, 10) : S.qty[key]));
    S.qty[key] = qty;
    const total = qty * unit;
    const vol = qty * r.unitVolume;
    const c = v.convoy;
    const warn = [];
    if (buying && vol > c.free + 1e-9) warn.push('Needs ' + fmt.vol(vol) + ' vol; only ' + fmt.vol(c.free) + ' free in the hold.');
    if (buying && total > v.cash) warn.push('Cost is above the house cash.');
    if (buying && qty > r.shelf) warn.push('Only ' + fmt.n(r.shelf) + ' on the shelf.');
    if (!buying && qty > r.held) warn.push('You hold ' + fmt.n(r.held) + '.');
    if (buying && r.locked) warn.push(r.tierName + ' goods need total standing ' + fmt.n(r.unlockStanding) + ' here; you have ' + fmt.n(v.location.standing.value, 0) + '.');
    const quickItems = buying
      ? [['10', 10], ['50', 50], ['100', 100], ['Fits', Math.max(0, Math.min(cap, Math.floor(c.free / r.unitVolume)))], ['Shelf', cap]]
      : [['10', 10], ['50', 50], ['Half', Math.floor(cap / 2)], ['All', cap]];
    const body = [
      h('div', { class: 'seg', style: 'align-self:flex-start' },
        h('button', { class: buying ? 'on' : '', onclick: () => { S.mode = 'buy'; render(); } }, 'Buy'),
        h('button', { class: !buying ? 'on' : '', disabled: r.held === 0, onclick: () => { S.mode = 'sell'; render(); } }, 'Sell')),
      h('div', { class: 'field' }, h('label', null, 'Units'), h('div', { class: 'row' }, stepper(key, { min: 0, max: cap, def: Math.min(cap, 10) }), h('span', { class: 'note' }, 'of ' + fmt.n(cap))), quick(key, quickItems)),
      h('div', null,
        h('div', { class: 'summary-line' }, h('span', null, buying ? 'Shelf price' : 'Offer here'), h('b', null, fmt.n(unit, 2) + ' / unit')),
        h('div', { class: 'summary-line' }, h('span', null, 'Hold volume'), h('b', null, (buying ? '+' : '−') + fmt.vol(vol) + ' → ' + fmt.vol(buying ? c.used + vol : Math.max(0, c.used - vol)) + ' / ' + fmt.vol(c.capacity))),
        h('div', { class: 'summary-line total' }, h('span', null, buying ? 'Cost' : 'Revenue'), h('b', { class: buying ? '' : 'tone-good' }, fmt.cr(total))),
        h('div', { class: 'summary-line' }, h('span', null, 'Cash after'), h('b', { class: buying && v.cash - total < 0 ? 'tone-bad' : '' }, fmt.cr(buying ? v.cash - total : v.cash + total)))
      ),
      h('div', { class: 'note' }, 'One price for the whole order: today’s unit price times the quantity. Prices move at the day tick, never inside a deal. The buy side may settle a little under this when your crew picks better crates.'),
      warn.length ? h('div', { class: 'note tone-warn' }, warn.map((w) => h('div', null, w))) : null,
      elsewhereCard(v, r, buying),
      card('Quality', dl([
        ['Shelf average', fmt.n(r.averageQuality) + (r.sTierPossible ? '  (S-tier possible)' : '')],
        ['Your pick', r.knowledge > 0 ? fmt.n(r.pickQuality) + ' with ' + fmt.n(r.knowledge * 100) + '% knowledge' : 'no eye for ' + r.category.toLowerCase()],
        r.held > 0 ? ['In hold', h('span', null, fmt.n(r.heldQuality), r.heldSTier ? [' ', badge('S', 's')] : null)] : ['In hold', '—'],
        ['Flow', flowBadge(r.flow)],
        r.eventHint ? ['Event', h('span', { class: 'tone-warn' }, r.eventHint)] : ['Intake', r.intake > 0 ? fmt.n(r.intake) + ' unshelved' : 'none'],
        r.reliefPerUnit > 0 ? ['Shortage', h('span', { class: 'tone-good' }, 'selling here relieves ' + r.reliefHint.toLowerCase() + ': +' + fmt.n(r.reliefPerUnit * 10, 1) + ' citizen standing per 10 units')] : ['Tier', h('span', { style: 'color:' + r.tierColor }, r.tierName)]
      ]))
    ];
    sidePane(side, {
      title: tierName(r, { noLock: true }), meta: r.category + ' · ' + r.tierName + ' · base ' + fmt.n(r.basePrice, 1) + ' · ' + fmt.vol(r.unitVolume) + ' vol/unit',
      body,
      foot: [btn('Cancel', () => { S.sel = null; render(); }, { kind: 'ghost' }), btn((buying ? 'Buy ' : 'Sell ') + fmt.n(qty) + ' ' + r.name, () => send({ type: buying ? 'buy' : 'sell', goodId: r.goodId, units: qty }), { kind: buying ? 'primary' : 'good', disabled: qty <= 0 || (buying && r.locked) })]
    });
  }

  registerTab('city', {
    id: 'governor', label: 'Relationship', order: 20,
    render(v) {
      if (!inCity(v)) return roadState(v);
      const st = v.location.standing;
      return h('div', null,
        h('div', { class: 'ops-grid c3' },
          card('Standing', [
            h('div', { class: 'person', style: 'margin-bottom:12px' }, avatar(st.governorName, true), h('div', { class: 'who' }, h('b', { style: 'font-size:20px' }, st.governorName), h('span', null, st.governorTitle + ' of ' + v.location.name))),
            h('div', { class: 'summary-line' }, h('span', null, 'Rank'), badge(st.rank, toneOf(st.tone))),
            h('div', { class: 'summary-line' }, h('span', null, 'Total standing'), h('b', null, fmt.n(st.value, 0) + ' / ' + fmt.n(st.max))),
            meter(st.fill, toneOf(st.tone)),
            h('div', { class: 'note', style: 'margin-top:10px' }, h('b', null, 'Reserved shelf: '), st.reservedDisplay)
          ], { hint: 'rank, permits and grades read the total' }),
          card('Who holds you in regard', segmentBars(st), { hint: 'four segments of ' + fmt.n(st.segments.length ? st.segments[0].max : 100) }),
          card('Grades this city will sell you', h('div', null, st.tierGates.map((g) => h('div', { class: 'gate-row' }, h('i', { class: 'tier-dot', style: 'background:' + g.color }), h('span', { class: 'grow', style: 'color:' + g.color + ';font-weight:600' }, g.name), g.open ? badge('open', 'good') : badge('standing ' + fmt.n(g.minStanding) + ' · ' + fmt.n(g.toGo, 0) + ' to go', 'muted')))), { hint: 'any segment counts' })
        ),
        h('div', { class: 'ops-grid c2' },
          card('Permits', h('div', null, st.permits.map((p) => h('div', { class: 'stat-row' }, h('div', { class: 'name' }, h('b', null, p.name), h('span', null, p.blurb)), h('div', { class: 'val' }, p.granted ? badge('granted', 'good') : badge('at ' + fmt.n(p.standingRequired), st.value >= p.standingRequired ? 'ok' : 'muted'), h('small', null, p.granted ? 'held' : fmt.n(Math.max(0, p.standingRequired - st.value), 0) + ' to go'))))), { hint: 'a permit sticks once granted' }),
          card('Favor', h('div', null, st.actions.map((a) => h('div', { class: 'stat-row' }, h('div', { class: 'name' }, h('b', null, a.name, ' ', badge(a.segmentName, 'outline')), h('span', { title: a.blurb }, a.effectText)), h('div', { class: 'val' }, fmt.cr(a.cost)), btn(a.name, () => send({ type: 'favor', actionId: a.id }, { done: a.name + ': ' + a.effectText }), { size: 'sm', kind: 'primary', disabled: !a.affordable, title: a.blurb })))), { hint: fmt.cr(v.cash) + ' in hand · contracts and shortages move the other segments' })
        )
      );
    }
  });

  registerTab('city', {
    id: 'stats', label: 'City stats', order: 30,
    render(v) {
      if (!inCity(v)) return roadState(v);
      const L = v.location;
      return h('div', { class: 'ops-grid c2' },
        card('Vitals', h('div', null, L.vitals.map((s) => h('div', { class: 'stat-row', title: s.blurb },
          h('div', { class: 'name' }, h('b', null, s.name), h('span', null, s.blurb)),
          meter(s.fill, toneOf(s.tone)),
          h('div', { class: 'val' }, s.display, h('small', null, badge(s.band, toneOf(s.tone)), s.deltaDisplay ? ' ' + s.deltaDisplay + ' since founding' : ' founded at ' + s.foundingDisplay))))), { hint: 'live values; founding in cities.json' }),
        card('Supply', h('div', null, L.supplies.map((s) => h('div', { class: 'stat-row', title: s.blurb, style: 'align-items:flex-start' },
          h('div', { class: 'name' }, h('b', null, s.name, ' ', badge(s.band, toneOf(s.tone))), h('span', null, s.goods.join(', ')), h('div', { class: 'note', style: 'margin-top:4px' }, 'makes ' + fmt.n(s.production, 1) + ' · eats ' + fmt.n(s.consumption, 1) + ' · net ' + fmt.signed(s.netFlow, 1) + ' / day' + (s.daysOfCover != null ? ' · ' + fmt.n(s.daysOfCover, 0) + ' days of cover' : ''))),
          h('div', { style: 'width:120px;flex:0 0 120px;padding-top:4px' }, meter(s.fill, toneOf(s.tone), true)),
          h('div', { class: 'val' }, fmt.n(s.index, 0), h('small', null, 'index · ' + fmt.n(s.stock, 0) + ' u'))))), { hint: '100 = what the city would hold undisturbed' })
      );
    }
  });

  registerTab('city', {
    id: 'roads', label: 'Roads', order: 40, count: (v) => v.location ? v.routes.length : null,
    render(v) {
      if (!inCity(v)) return roadState(v);
      return card('Roads out of ' + v.location.name, roadsTable(v), { tight: true, hint: 'margin = best cargo after fuel and upkeep, at what the convoy can afford' });
    }
  });

  function recruitBoard(v) {
    const R = v.crew.recruitment;
    if (!R) return roadState(v);
    const team = v.crew.skills;
    const grid = table({
      id: 'recruit', rows: R.candidates, rowKey: (r) => r.id, emptyText: 'Nobody is on the board.',
      onRow: (r) => { S.detail = { kind: 'candidate', id: r.id }; render(); },
      defaultSort: { id: 'signingFee', dir: 'asc' },
      cols: [
        { id: 'name', label: 'Candidate', get: (r) => r.name, cell: (r) => personRow(r) },
        { id: 'skills', label: 'Skills', sortable: false, cell: (r) => skillsMini(r, team) },
        { id: 'know', label: 'Best knowledge', get: (r) => Math.max(0, ...r.knowledge.map((k) => k.level)), cell: (r) => { const k = r.knowledge.slice().sort((a, b) => b.level - a.level)[0]; return k ? [k.name, h('span', { class: 'sub' }, k.level + ' / ' + k.maxLevel)] : '—'; } },
        { id: 'traits', label: 'Trait', sortable: false, cell: (r) => r.traits.length ? r.traits.map((t) => badge(t.name, 'amber')) : h('span', { class: 'tone-muted' }, '—') },
        { id: 'dailyWage', label: 'Wage / day', num: true, get: (r) => r.dailyWage, cell: (r) => fmt.cr(r.dailyWage) },
        { id: 'signingFee', label: 'Signing fee', num: true, get: (r) => r.signingFee, cell: (r) => fmt.cr(r.signingFee) },
        { id: 'status', label: 'Status', get: (r) => (r.roomAboard ? 0 : 2) + (r.affordable ? 0 : 1), cell: (r) => !r.roomAboard ? badge('no seat', 'warn') : !r.affordable ? badge('too dear', 'bad') : badge('can sign', 'good') },
        { id: 'act', label: '', sortable: false, cell: (r) => h('div', { class: 'mini-actions' }, btn('View', () => { S.detail = { kind: 'candidate', id: r.id }; render(); }, { size: 'xs' }), btn('Hire', () => send({ type: 'hireCrew', candidateId: r.id }, { done: r.name + ' signed on.' }), { size: 'xs', kind: 'primary', disabled: !r.affordable || !r.roomAboard })) }
      ]
    });
    return h('div', null,
      h('div', { class: 'ops-grid c4' }, kpi('Seats', v.crew.size + ' / ' + v.crew.capacity, v.crew.capacity - v.crew.size + ' open'), kpi('Payroll / day', fmt.cr(v.crew.dailyWages)), kpi('Board refreshes', 'in ' + fmt.days(R.refreshInDays), R.cityName), kpi('Cash', fmt.cr(v.cash))),
      card('Recruitment board · ' + R.cityName, grid, { tight: true, hint: 'click a name for the full sheet' })
    );
  }
  registerTab('city', { id: 'recruit', label: 'Recruitment', order: 50, count: (v) => v.crew.recruitment ? v.crew.recruitment.candidates.length : null, render: recruitBoard });

  function fleetCard(v, t, inStation) {
    const fittings = h('div', null, t.fittings.map((f) => h('div', { class: 'fitting' },
      h('div', { class: 'grow' }, h('b', null, f.name, ' ', f.installed ? badge('fitted', 'good') : !f.fits ? badge('does not fit', 'muted') : null), h('span', null, f.effectText + ' · ' + f.blurb)),
      h('div', null, fmt.cr(f.price)),
      inStation && !f.installed && f.fits ? btn('Fit', () => send({ type: 'upgradeTruck', truckId: t.id, upgradeId: f.id }, { done: f.name + ' fitted to the ' + t.name + '.' }), { size: 'xs', kind: 'primary', disabled: !f.affordable, title: f.affordable ? null : 'too dear' }) : null)));
    return card(t.name, [
      dl([['Hold', fmt.vol(t.capacity)], ['Speed', fmt.n(t.speedKmPerDay) + ' km/d'], ['Upkeep', fmt.cr(t.upkeepPerDay) + '/d'], ['Fuel', fmt.n(t.fuelPerKm, 2) + ' /km'], t.mineYield > 0 ? ['Mining', fmt.n(t.mineYield, 1) + ' u/d'] : ['Kind', t.kind], ['Fitted', t.upgrades.length ? t.upgrades.join(', ') : 'nothing']]),
      h('div', { class: 'section' }, h('h5', null, 'Fittings'), fittings),
      inStation ? h('div', { class: 'note', style: 'margin-top:10px' }, t.canSell ? 'The station pays ' + fmt.cr(t.resaleValue) + ' for it as fitted.' : t.sellBlocker) : null
    ], { class: 'fleet-card', hint: t.id, foot: inStation ? btn('Sell for ' + fmt.cr(t.resaleValue), () => confirm('Sell the ' + t.name + '?', 'The station pays ' + fmt.cr(t.resaleValue) + ' (' + fmt.pct(v.station.resaleFraction) + ' of the vehicle and its fittings). Hold shrinks at once.', { label: 'Sell', run: () => send({ type: 'sellTruck', truckId: t.id }, { done: t.name + ' sold.' }) }, 'danger'), { size: 'sm', kind: 'danger', disabled: !t.canSell, title: t.canSell ? null : t.sellBlocker }) : null });
  }

  registerTab('city', {
    id: 'depot', label: 'Station', order: 60,
    render(v) {
      if (!inCity(v)) return roadState(v);
      const c = v.convoy;
      const trucks = table({
        id: 'shipyard', rows: v.station.offers, rowKey: (r) => r.id, emptyText: 'No yard here.',
        defaultSort: { id: 'price', dir: 'asc' },
        cols: [
          { id: 'name', label: 'Class', cell: (r) => [r.name, h('span', { class: 'sub' }, r.kind)] },
          { id: 'capacity', label: 'Hold', num: true, get: (r) => r.capacity, cell: (r) => fmt.vol(r.capacity) },
          { id: 'speedKmPerDay', label: 'Speed', num: true, get: (r) => r.speedKmPerDay, cell: (r) => fmt.n(r.speedKmPerDay) + ' km/d' },
          { id: 'upkeepPerDay', label: 'Upkeep', num: true, get: (r) => r.upkeepPerDay, cell: (r) => fmt.cr(r.upkeepPerDay) + '/d' },
          { id: 'fuelPerKm', label: 'Fuel', num: true, get: (r) => r.fuelPerKm, cell: (r) => fmt.n(r.fuelPerKm, 2) + ' /km' },
          { id: 'mineYield', label: 'Mine', num: true, get: (r) => r.mineYield, cell: (r) => r.mineYield > 0 ? fmt.n(r.mineYield, 1) + ' u/d' : h('span', { class: 'tone-muted' }, '—') },
          { id: 'price', label: 'Price', num: true, get: (r) => r.price, cell: (r) => fmt.cr(r.price) },
          { id: 'act', label: '', sortable: false, cell: (r) => h('div', { class: 'mini-actions' }, btn('Buy', () => confirm('Buy a ' + r.name + '?', fmt.cr(r.price) + ' now, then ' + fmt.cr(r.upkeepPerDay) + ' a day in upkeep before crew. It joins the convoy at once.', { label: 'Buy', run: () => send({ type: 'buyTruck', truckTypeId: r.id }, { done: r.name + ' joins the convoy.' }) }), { size: 'xs', kind: 'primary', disabled: r.price > v.cash })) }
        ]
      });
      const gear = table({
        id: 'outfit', rows: v.outfitters, rowKey: (r) => r.id, emptyText: 'No outfitter here.',
        defaultSort: { id: 'price', dir: 'asc' },
        cols: [
          { id: 'name', label: 'Gear', get: (r) => r.name },
          { id: 'volume', label: 'Hold used', num: true, get: (r) => r.volume, cell: (r) => fmt.vol(r.volume) },
          { id: 'mineYield', label: 'Mine', num: true, get: (r) => r.mineYield, cell: (r) => fmt.n(r.mineYield, 1) + ' u/d' },
          { id: 'price', label: 'Price', num: true, get: (r) => r.price, cell: (r) => fmt.cr(r.price) },
          { id: 'status', label: 'Status', get: (r) => (r.fits ? 0 : 2) + (r.affordable ? 0 : 1), cell: (r) => !r.fits ? badge('no room', 'warn') : !r.affordable ? badge('too dear', 'bad') : badge('available', 'good') },
          { id: 'act', label: '', sortable: false, cell: (r) => h('div', { class: 'mini-actions' }, btn('Buy', () => send({ type: 'buyGear', gearId: r.id }, { done: r.name + ' stowed.' }), { size: 'xs', kind: 'primary', disabled: !r.fits || !r.affordable })) }
        ]
      });
      return h('div', null,
        h('div', { class: 'ops-grid c4' }, kpi('Trucks', c.trucks.length, c.trucks.join(', ') || 'none'), kpi('Hold', fmt.vol(c.used) + ' / ' + fmt.vol(c.capacity)), kpi('Gear', c.gear.length, c.gear.join(', ') || 'none'), kpi('Cash', fmt.cr(v.cash))),
        card('Your fleet · sell or fit', h('div', { class: 'ops-grid c3' }, v.station.fleet.map((t) => fleetCard(v, t, true))), { hint: 'one of each fitting per vehicle; the station pays ' + fmt.pct(v.station.resaleFraction) + ' back' }),
        card('Shipyard', trucks, { tight: true, hint: 'a second truck adds hold and upkeep; the convoy moves at the slowest' }),
        card('Outfitters', gear, { tight: true, hint: 'gear rides in the hold and lets the convoy work a claim' })
      );
    }
  });

  registerTab('city', {
    id: 'storeroom', label: 'Storeroom', order: 70, flag: (v) => v.warehouse.rented,
    render(v, side) {
      if (!inCity(v)) return roadState(v);
      const W = v.warehouse;
      if (!W.rented) {
        return h('div', { class: 'ops-grid c3' }, card('Rent a storeroom in ' + v.location.name, [
          dl([['Lease', fmt.cr(W.rentCost)], ['Rent per day', fmt.cr(W.dailyRent)], ['Capacity', fmt.vol(W.capacity) + ' vol']]),
          h('div', { class: 'note', style: 'margin-top:10px' }, 'A room here holds stock between visits and can sell or procure unattended at prices you set. Unattended orders use market terms: no crew eye, no bargain.')
        ], { foot: btn('Rent for ' + fmt.cr(W.rentCost), () => send({ type: 'rentWarehouse' }, { done: 'Storeroom leased in ' + v.location.name + '.' }), { kind: 'primary', disabled: W.rentCost > v.cash }) }));
      }
      const lots = table({
        id: 'lots', rows: W.lots, rowKey: (r) => r.goodId, selected: S.sel, emptyText: 'The room is empty. Deposit from the hold.',
        onRow: (r) => { S.sel = r.goodId; render(); },
        cols: [
          { id: 'name', label: 'Good', get: (r) => r.name },
          { id: 'units', label: 'Units', num: true, get: (r) => r.units, cell: (r) => fmt.n(r.units) },
          { id: 'quality', label: 'Quality', num: true, get: (r) => r.quality, cell: (r) => [fmt.n(r.quality), r.sTier ? [' ', badge('S', 's')] : null] },
          { id: 'autoSell', label: 'Auto-sell at', num: true, get: (r) => r.autoSell, cell: (r) => r.autoSell > 0 ? fmt.cr(r.autoSell) : h('span', { class: 'tone-muted' }, 'off') },
          { id: 'autoProcure', label: 'Auto-buy at', num: true, get: (r) => r.autoProcure, cell: (r) => r.autoProcure > 0 ? fmt.cr(r.autoProcure) : h('span', { class: 'tone-muted' }, 'off') },
          { id: 'act', label: '', sortable: false, cell: (r) => h('div', { class: 'mini-actions' }, btn('Manage', () => { S.sel = r.goodId; render(); }, { size: 'xs' })) }
        ]
      });
      if (S.sel === '__deposit') depositPane(v, side);
      else if (S.sel) { const lot = W.lots.find((l) => l.goodId === S.sel); if (lot) lotPane(v, lot, side); }
      return h('div', null,
        h('div', { class: 'ops-grid c4' }, kpi('Room', fmt.vol(W.used) + ' / ' + fmt.vol(W.capacity), fmt.pct(W.capacity ? 1 - W.used / W.capacity : 0) + ' free'), kpi('Rent / day', fmt.cr(W.dailyRent)), kpi('Lots', W.lots.length), kpi('Hold', fmt.vol(v.convoy.used) + ' / ' + fmt.vol(v.convoy.capacity))),
        card('Stock in the room', lots, { tight: true, actions: btn('Deposit from hold', () => { S.sel = '__deposit'; render(); }, { size: 'xs', kind: 'primary', disabled: !v.cargo.length }) })
      );
    }
  });
  function depositPane(v, side) {
    const key = 'deposit';
    if (!S.depositGood || !v.cargo.find((c) => c.goodId === S.depositGood)) S.depositGood = v.cargo[0] && v.cargo[0].goodId;
    const c = v.cargo.find((x) => x.goodId === S.depositGood);
    const cap = c ? c.units : 0;
    const qty = Math.min(cap, S.qty[key] === undefined ? cap : S.qty[key]); S.qty[key] = qty;
    sidePane(side, {
      title: 'Deposit', meta: 'from the hold into the room',
      body: [
        h('div', { class: 'field' }, h('label', null, 'Good'), h('select', { class: 'inp', onchange: (e) => { S.depositGood = e.target.value; S.qty[key] = undefined; render(); } }, v.cargo.map((x) => h('option', { value: x.goodId, selected: x.goodId === S.depositGood }, x.name + ' (' + x.units + ')')))),
        h('div', { class: 'field' }, h('label', null, 'Units'), h('div', { class: 'row' }, stepper(key, { min: 0, max: cap, def: cap }), h('span', { class: 'note' }, 'of ' + cap)), quick(key, [['Half', Math.floor(cap / 2)], ['All', cap]])),
        h('div', { class: 'note' }, 'Quality rides along. Room after: ' + fmt.vol(v.warehouse.used + (c ? qty * (c.volume / Math.max(1, c.units)) : 0)) + ' / ' + fmt.vol(v.warehouse.capacity) + '.')
      ],
      foot: [btn('Cancel', () => { S.sel = null; render(); }, { kind: 'ghost' }), btn('Deposit ' + qty, () => send({ type: 'warehouseDeposit', goodId: S.depositGood, units: qty }).then((s) => { if (s && !s.error) { S.sel = null; render(); } }), { kind: 'primary', disabled: !c || qty <= 0 })]
    });
  }
  function lotPane(v, lot, side) {
    const key = 'withdraw:' + lot.goodId;
    const qty = Math.min(lot.units, S.qty[key] === undefined ? lot.units : S.qty[key]); S.qty[key] = qty;
    const market = v.market.find((m) => m.goodId === lot.goodId);
    const sellIn = h('input', { class: 'inp', type: 'number', min: 0, value: lot.autoSell || '', placeholder: market ? 'sells here at ' + fmt.n(market.sell, 1) : '0 = off' });
    const buyIn = h('input', { class: 'inp', type: 'number', min: 0, value: lot.autoProcure || '', placeholder: market ? 'shelf here at ' + fmt.n(market.buy, 1) : '0 = off' });
    sidePane(side, {
      title: lot.name, meta: fmt.n(lot.units) + ' units · quality ' + fmt.n(lot.quality) + (lot.sTier ? ' · S-tier' : ''),
      body: [
        card('Withdraw to hold', [h('div', { class: 'row', style: 'display:flex;gap:6px;align-items:center' }, stepper(key, { min: 0, max: lot.units, def: lot.units }), h('span', { class: 'note' }, 'of ' + lot.units)), h('div', { style: 'margin-top:8px' }, quick(key, [['Half', Math.floor(lot.units / 2)], ['All', lot.units]]))],
          { foot: btn('Withdraw ' + qty, () => send({ type: 'warehouseWithdraw', goodId: lot.goodId, units: qty }), { size: 'sm', disabled: qty <= 0 }) }),
        card('Unattended orders', [
          h('div', { class: 'field' }, h('label', null, 'Sell from the room when the offer reaches'), sellIn),
          h('div', { class: 'field', style: 'margin-top:10px' }, h('label', null, 'Buy into the room when the shelf drops to'), buyIn),
          h('div', { class: 'note', style: 'margin-top:8px' }, 'Per unit, in credits. 0 clears. Fills happen on the day tick at market terms.')
        ], { foot: [btn('Save prices', async () => { await send({ type: 'warehouseSell', goodId: lot.goodId, price: Math.max(0, Math.floor(Number(sellIn.value) || 0)) }); await send({ type: 'warehouseProcure', goodId: lot.goodId, price: Math.max(0, Math.floor(Number(buyIn.value) || 0)) }, { done: 'Standing orders updated.' }); }, { size: 'sm', kind: 'primary' })] })
      ],
      foot: [btn('Close', () => { S.sel = null; render(); }, { kind: 'ghost' })]
    });
  }

  function contractLines(c) {
    return h('div', { class: 'lines' }, c.lines.map((l) => h('div', { class: 'ln ' + (l.satisfied ? 'ok' : l.held > 0 ? 'short' : '') }, h('span', null, h('i', { class: 'tier-dot', style: 'background:' + l.tierColor }), fmt.n(l.units) + ' × ' + l.name), h('span', null, 'hold ' + fmt.n(l.held) + (l.held > 0 && c.minGrade > 0 ? ' @ ' + fmt.n(l.heldQuality, 0) + '%' : '')))));
  }
  function offerCard(v, c) {
    return h('div', { class: 'contract' },
      h('div', { class: 'head' }, h('b', null, c.kindName), h('span', { class: 'grow' }), c.held ? badge('held', 'ok') : c.closed ? badge('settled', 'muted') : null),
      h('div', { class: 'note' }, c.blurb),
      contractLines(c),
      h('div', { class: 'terms' }, h('span', null, 'pays ', h('b', null, fmt.cr(c.reward))), h('span', null, h('b', null, '+' + fmt.n(c.standing, 0)), ' traders standing'), h('span', null, h('b', null, fmt.days(c.deadlineDays)), ' from signing'), c.minGrade > 0 ? h('span', null, 'grade ', h('b', null, fmt.n(c.minGrade, 0) + '%+')) : null),
      h('div', { class: 'acts' }, btn('Accept', () => send({ type: 'acceptContract', contractId: c.id }, { done: c.kindName + ' signed.' }), { size: 'sm', kind: 'primary', disabled: c.held || c.closed }))
    );
  }
  function heldCard(v, c) {
    return h('div', { class: 'contract' },
      h('div', { class: 'head' }, h('b', null, c.kindName), h('span', { class: 'note' }, ' · ' + c.cityName), h('span', { class: 'grow' }), c.daysLeft <= 3 ? badge(fmt.days(c.daysLeft) + ' left', 'bad') : badge('day ' + c.deadline, 'muted')),
      contractLines(c),
      h('div', { class: 'terms' }, h('span', null, 'pays ', h('b', null, fmt.cr(c.reward))), h('span', null, h('b', null, '+' + fmt.n(c.standing, 0)), ' traders standing'), c.minGrade > 0 ? h('span', null, 'grade ', h('b', null, fmt.n(c.minGrade, 0) + '%+')) : null),
      c.blocker ? h('div', { class: 'note tone-warn' }, c.blocker) : null,
      h('div', { class: 'acts' }, btn('Deliver', () => send({ type: 'deliverContract', contractId: c.id }, { done: c.kindName + ' delivered for ' + fmt.cr(c.reward) + '.' }), { size: 'sm', kind: 'good', disabled: !c.deliverable, title: c.blocker || null }))
    );
  }
  function heldContracts(v) {
    const C = v.contracts;
    if (!C.held.length) return h('div', { class: 'tbl-empty' }, 'The house holds no contract. Boards are in every city.');
    return h('div', { class: 'contracts-grid', style: 'padding:12px' }, C.held.map((c) => heldCard(v, c)));
  }
  registerTab('city', {
    id: 'contracts', label: 'Contracts', order: 62, count: (v) => v.location ? v.contracts.board.filter((c) => !c.held && !c.closed).length || null : null,
    flag: (v) => v.contracts.held.some((c) => c.here && c.deliverable),
    render(v) {
      if (!inCity(v)) return roadState(v);
      const C = v.contracts;
      return h('div', null,
        h('div', { class: 'ops-grid c4' }, kpi('On the board', C.board.length, C.boardCity), kpi('Board refreshes', 'in ' + fmt.days(C.refreshInDays)), kpi('Held', C.held.length, C.held.filter((c) => c.here).length + ' settle here'), kpi('Traders standing', fmt.n((v.location.standing.segments.find((s) => s.id === 'traders') || { value: 0 }).value, 0), 'here')),
        card('Contract board · ' + C.boardCity, C.board.length ? h('div', { class: 'contracts-grid', style: 'padding:12px' }, C.board.map((c) => offerCard(v, c))) : h('div', { class: 'tbl-empty' }, 'Nothing posted. The city makes everything it wants.'), { tight: true, hint: 'a city only asks for what it does not make' }),
        card('Held by the house', heldContracts(v), { tight: true, hint: 'deliver in the city that issued it, before the deadline' })
      );
    }
  });

  // ── expo: stall, asks, and the hall ──────────────────────────────────────
  function expoHall(v) {
    const E = v.expo, R = E.report;
    const key = R ? R.day + ':' + v.location.id + ':' + R.visits.length + ':' + R.revenue : null;
    if (!R || !R.visits.length) { S.expo = null; return h('div', { class: 'expo-hall' }, h('div', { class: 'hall-empty' }, E.running ? (E.passHeld ? 'Set an ask on a listed good and pass a day. Buyers walk the hall while the day goes by.' : 'Buy a pass to open the stall.') : 'The hall is dark. It opens in ' + fmt.days(E.startsIn) + '.')); }
    if (S.expo && S.expo.key === key && S.expo.wrap) { S.expo.detachedAt = 0; return S.expo.wrap; }
    const wrap = h('div', { class: 'expo-hall' });
    const canvas = h('canvas');
    const hud = h('div', { class: 'hall-hud' });
    const replay = btn('Replay', () => start(), { size: 'xs', kind: 'ghost' });
    replay.style.cssText = 'position:absolute;right:10px;top:8px';
    wrap.append(canvas, hud, replay);
    const scene = { key, wrap, canvas, hud, buyers: [], t0: 0, detachedAt: 0, done: false };
    S.expo = scene;
    const goods = [...new Set(R.visits.filter((x) => x.goodId).map((x) => x.goodId))];
    const names = {}; for (const x of R.visits) if (x.goodId) names[x.goodId] = x.goodName;
    const colorOf = (o) => ({ bought: '#45c281', tooDear: '#ef6b6b', close: '#e2b357', browse: '#8b99ab', noStall: '#5d6b7c' })[o] || '#8b99ab';
    const SPAWN = 0.75, DWELL = 1.6, WALK = 1.4;
    function start() {
      scene.t0 = performance.now(); scene.done = false;
      scene.buyers = R.visits.map((x, i) => ({ v: x, born: i * SPAWN, stall: goods.indexOf(x.goodId), x: -20, y: 0, phase: 0 }));
      scene.sold = 0; scene.rev = 0;
      loop();
    }
    function layout() {
      const W = canvas.width = wrap.clientWidth || 600, H = canvas.height = wrap.clientHeight || 340;
      const n = Math.max(1, goods.length);
      const stalls = goods.map((g, i) => ({ id: g, x: W * (i + 1) / (n + 1), y: 62, w: Math.min(150, W / (n + 1) - 16) }));
      return { W, H, stalls };
    }
    function draw(now) {
      const { W, H, stalls } = layout();
      const ctx = canvas.getContext('2d');
      ctx.clearRect(0, 0, W, H);
      // floor
      ctx.strokeStyle = 'rgba(90,162,255,.08)'; ctx.lineWidth = 1;
      for (let x = 0; x < W; x += 40) { ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, H); ctx.stroke(); }
      for (let y = 0; y < H; y += 40) { ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(W, y); ctx.stroke(); }
      // door
      ctx.fillStyle = 'rgba(214,222,233,.12)'; ctx.fillRect(0, H - 90, 10, 60);
      ctx.fillStyle = '#8b99ab'; ctx.font = '15px system-ui, sans-serif'; ctx.textAlign = 'left'; ctx.fillText('door', 14, H - 55);
      // stalls
      for (const st of stalls) {
        ctx.fillStyle = '#1b2532'; ctx.strokeStyle = '#34465b';
        ctx.beginPath(); ctx.roundRect(st.x - st.w / 2, st.y - 22, st.w, 44, 6); ctx.fill(); ctx.stroke();
        ctx.fillStyle = '#f2f6fb'; ctx.font = '600 16px system-ui, sans-serif'; ctx.textAlign = 'center';
        ctx.fillText(names[st.id] || st.id, st.x, st.y - 3);
        const ask = (E.listings.find((l) => l.goodId === st.id) || {}).ask;
        ctx.fillStyle = '#8b99ab'; ctx.font = '15px system-ui, sans-serif';
        ctx.fillText(ask ? fmt.cr(ask) + ' / u' : 'off the stall', st.x, st.y + 13);
      }
      const t = (now - scene.t0) / 1000;
      let alive = 0;
      for (const b of scene.buyers) {
        const age = t - b.born; if (age < 0) continue;
        const stall = b.stall >= 0 ? stalls[b.stall] : { x: W * (0.3 + 0.4 * ((b.v.sequence * 7919) % 100) / 100), y: H * 0.55 };
        const tx = stall.x + ((b.v.sequence * 37) % 30) - 15, ty = stall.y + 40;
        const ex = W + 20, ey = H - 60;
        const sx = -10, sy = H - 60;
        let x, y, bubble = null, col = '#8b99ab';
        if (age < WALK) { const k = age / WALK; x = sx + (tx - sx) * k; y = sy + (ty - sy) * k; }
        else if (age < WALK + DWELL) { x = tx; y = ty; bubble = b.v.remark; col = colorOf(b.v.outcome); if (!b.counted && b.v.outcome === 'bought') { b.counted = true; scene.sold += b.v.units; scene.rev += b.v.units * b.v.price; } }
        else if (age < WALK * 2 + DWELL) { const k = (age - WALK - DWELL) / WALK; x = tx + (ex - tx) * k; y = ty + (ey - ty) * k; col = colorOf(b.v.outcome); }
        else continue;
        alive++;
        ctx.beginPath(); ctx.fillStyle = col; ctx.arc(x, y, 9, 0, Math.PI * 2); ctx.fill();
        ctx.fillStyle = '#0b1017'; ctx.font = '600 13.5px system-ui, sans-serif'; ctx.textAlign = 'center'; ctx.fillText((b.v.buyer || '?')[0], x, y + 4);
        if (bubble) {
          ctx.font = '16px system-ui, sans-serif'; const tw = ctx.measureText(bubble).width + 16;
          const bx = Math.max(8, Math.min(W - tw - 8, x - tw / 2)), by = y - 40;
          ctx.fillStyle = '#f2f6fb'; ctx.beginPath(); ctx.roundRect(bx, by, tw, 24, 6); ctx.fill();
          ctx.fillStyle = '#0b1017'; ctx.textAlign = 'left'; ctx.fillText(bubble, bx + 8, by + 16);
          if (b.v.outcome === 'bought') { ctx.fillStyle = '#45c281'; ctx.font = '600 15px system-ui, sans-serif'; ctx.fillText('+' + fmt.cr(b.v.units * b.v.price), bx + 8, by - 4); }
        }
      }
      const last = scene.buyers.length ? scene.buyers[scene.buyers.length - 1].born + WALK * 2 + DWELL : 0;
      if (t > last) scene.done = true;
      hud.innerHTML = '';
      hud.append(h('span', null, 'day ', h('b', null, R.day)), h('span', null, 'buyers ', h('b', null, R.buyers)), h('span', null, 'sold ', h('b', null, fmt.n(scene.sold) + ' u')), h('span', null, 'takings ', h('b', null, fmt.cr(scene.rev))));
      if (scene.done) hud.append(h('span', { class: 'tone-good' }, 'day over'));
      return alive;
    }
    function loop() {
      if (S.expo !== scene) return;
      if (!wrap.isConnected) { if (!scene.detachedAt) scene.detachedAt = performance.now(); else if (performance.now() - scene.detachedAt > 3000) return; }
      else scene.detachedAt = 0;
      draw(performance.now());
      if (!scene.done) requestAnimationFrame(loop); else setTimeout(() => { if (S.expo === scene && wrap.isConnected) draw(performance.now()); }, 0);
    }
    setTimeout(start, 0);
    return wrap;
  }
  registerTab('city', {
    id: 'expo', label: 'Expo', order: 64,
    flag: (v) => !!(v.expo && v.expo.running),
    count: (v) => v.expo && v.expo.running ? fmt.days(v.expo.daysLeft) : null,
    render(v) {
      if (!inCity(v)) return roadState(v);
      const E = v.expo;
      const status = card(E.running ? E.title : 'Next expo · ' + E.title, [
        h('div', { class: 'chips', style: 'margin-bottom:10px' }, E.categories.map((c) => badge(c, 'ok'))),
        dl([
          ['Status', E.running ? h('span', { class: 'tone-good' }, 'open · ' + fmt.days(E.daysLeft) + ' left') : 'opens in ' + fmt.days(E.startsIn) + ' · runs ' + fmt.days(E.durationDays)],
          ['Theme buff', fmt.pct(E.buff) + ' · ' + E.categories.length + ' categories'],
          ['Buyers per day', 'about ' + E.buyersPerDay],
          ['Pass', E.passHeld ? badge('held', 'good') : fmt.cr(E.fee) + ' · open to any trader']
        ]),
        h('div', { class: 'note', style: 'margin-top:10px' }, 'Narrow themes buff harder. Buyers come from across the map and pay around base price plus the premium; a fair ask sells, a greedy one does not. ' + E.cityName + '\u2019s own produce is never allowed on a stall here.')
      ], { foot: E.running && !E.passHeld ? btn('Buy a pass for ' + fmt.cr(E.fee), () => send({ type: 'expoRegister' }, { done: 'Stall booked at the ' + E.title + '.' }), { kind: 'primary', disabled: E.fee > v.cash }) : null });
      const asks = card('Stall · asking price per unit', E.listings.length ? h('div', null, E.listings.map((l) => {
        const key = 'ask:' + l.goodId;
        const input = h('input', { class: 'inp sm', type: 'number', min: 0, value: l.ask || S.ask && S.ask[key] || '', placeholder: l.suggested ? 'try ' + fmt.n(l.suggested) : '—', disabled: !l.eligible, onchange: (e) => { (S.ask = S.ask || {})[key] = e.target.value; } });
        return h('div', { class: 'ask-row' },
          h('div', { class: 'nm' }, h('b', null, h('i', { class: 'tier-dot', style: 'background:' + l.tierColor }), l.name, ' ', l.ask > 0 ? badge('listed', 'good') : null), h('span', null, fmt.n(l.held) + ' held @ ' + fmt.n(l.quality, 0) + '% · ' + l.category + ' · city offers ' + fmt.n(l.localSell, 1) + (l.suggested ? ' · buyers around ' + fmt.n(l.suggested) : '') + (l.reason ? ' · ' + l.reason : ''))),
          input,
          h('div', { class: 'mini-actions' }, btn(l.ask > 0 ? 'Update' : 'List', () => send({ type: 'expoList', goodId: l.goodId, price: Math.max(0, Math.floor(Number(input.value) || 0)) }), { size: 'xs', kind: 'primary', disabled: !l.eligible }), l.ask > 0 ? btn('Take down', () => send({ type: 'expoList', goodId: l.goodId, price: 0 }), { size: 'xs' }) : null));
      })) : h('div', { class: 'note' }, 'The hold is empty. Bring goods the theme admits and this city does not make.'), { hint: E.passHeld ? 'buyers visit each day you pass here' : 'a pass opens the stall' });
      const feed = R => h('div', { class: 'expo-feed' }, R.visits.map((x) => h('div', { class: 'item ' + x.outcome }, h('span', { class: 'who' }, x.buyer), h('span', { class: 'what' }, (x.goodName ? x.goodName + ' · ' : '') + x.remark + (x.outcome === 'bought' ? ' (' + fmt.n(x.units) + ' u @ ' + fmt.cr(x.price) + ')' : '')))));
      return h('div', null,
        h('div', { class: 'ops-grid c2' }, status, asks),
        card('The hall', expoHall(v), { tight: true, hint: E.report ? 'replay of day ' + E.report.day + ': ' + fmt.n(E.report.unitsSold) + ' u sold for ' + fmt.cr(E.report.revenue) : 'pass a day with a stall to see buyers', actions: E.running && E.passHeld ? btn('Pass a day', () => send({ type: 'wait', days: 1 }), { size: 'xs', kind: 'primary' }) : null }),
        E.report ? card('Who came by', feed(E.report), { tight: true }) : null
      );
    }
  });

  registerTab('city', {
    id: 'wire', label: 'Wire', order: 80, count: (v) => v.location && v.location.news.length ? v.location.news.length : null,
    render(v) {
      if (!inCity(v)) return roadState(v);
      return h('div', { class: 'ops-grid c2' }, card('City wire · ' + v.location.name, newsList(v.location.news), { tight: true }), card('House ledger', logList(S.snap.log, 20), { tight: true }));
    }
  });

  // Caravan
  registerPage({
    id: 'caravan', label: 'Caravan', icon: 'caravan', order: 30,
    subtitle: (v) => v.convoy.trucks.length + (v.convoy.trucks.length === 1 ? ' truck · ' : ' trucks · ') + fmt.vol(v.convoy.used) + ' / ' + fmt.vol(v.convoy.capacity) + ' vol',
    flag: (v) => v.site && !v.site.canMine ? 'warn' : null
  });
  registerTab('caravan', {
    id: 'summary', label: 'Summary', order: 10,
    render(v) {
      const c = v.convoy;
      return h('div', null,
        h('div', { class: 'ops-grid c6' },
          kpi('Hold', fmt.vol(c.used) + ' / ' + fmt.vol(c.capacity), fmt.vol(c.free) + ' free'),
          kpi('Speed', fmt.n(c.speedKmPerDay) + ' km/d', 'trucks × navigation'),
          kpi('Burn / day', fmt.cr(c.dailyUpkeep), 'upkeep after crew, plus wages'),
          kpi('Wages / day', fmt.cr(v.crew.dailyWages), v.crew.size + ' aboard'),
          kpi('Mining', c.canMine ? fmt.n(c.mineYield, 1) + ' u/d' : 'no rig', c.canMine ? 'while parked on a claim' : 'buy gear or a Digger'),
          kpi('Net worth', fmt.cr(v.netWorth))
        ),
        h('div', { class: 'ops-grid c3' },
          positionCard(v),
          card('Trucks', v.station.fleet.length ? h('div', null, v.station.fleet.map((t) => h('div', { class: 'stat-row' }, h('div', { class: 'name' }, h('b', null, t.name), h('span', null, fmt.vol(t.capacity) + ' hold · ' + fmt.n(t.speedKmPerDay) + ' km/d · ' + (t.upgrades.length ? t.upgrades.join(', ') : 'no fittings'))), badge(t.id, 'muted')))) : h('div', { class: 'note' }, 'No truck.'), { hint: v.location ? 'sell or fit at the station' : null, foot: v.location ? btn('Station', () => open('city', 'depot'), { size: 'sm' }) : null }),
          card('Gear', c.gear.length ? h('div', null, c.gear.map((g) => h('div', { class: 'stat-row' }, h('div', { class: 'name' }, h('b', null, g))))) : h('div', { class: 'note' }, 'Nothing stowed. Gear rides in the hold and lets the convoy work a mining claim.'), { foot: v.location ? btn('Outfitters', () => open('city', 'depot'), { size: 'sm' }) : null })
        ),
        card('Cargo', cargoTable(v, 'cargo-cv'), { tight: true, hint: v.location ? 'valued at ' + v.location.name + '\'s offer' : 'no market on the road' })
      );
    }
  });
  registerTab('caravan', {
    id: 'cargo', label: 'Cargo', order: 20, count: (v) => v.cargo.length || null,
    render(v) {
      const totalCost = v.cargo.reduce((a, r) => a + r.averageCost * r.units, 0);
      const sellHere = {}; for (const m of v.market) sellHere[m.goodId] = m;
      const localValue = v.location ? v.cargo.reduce((a, r) => a + (sellHere[r.goodId] ? sellHere[r.goodId].sell * r.units : 0), 0) : null;
      return h('div', null,
        h('div', { class: 'ops-grid c4' }, kpi('Lots', v.cargo.length), kpi('Units', fmt.n(v.cargo.reduce((a, r) => a + r.units, 0))), kpi('Paid', fmt.cr(totalCost), 'at average cost'), kpi(v.location ? 'Offer here' : 'Offer', localValue != null ? fmt.cr(localValue) : '—', localValue != null ? fmt.signedCr(localValue - totalCost) + ' vs paid' : 'park in a city', localValue != null ? (localValue >= totalCost ? 'good' : 'bad') : null)),
        card('Hold', cargoTable(v, 'cargo-full'), { tight: true })
      );
    }
  });
  registerTab('caravan', {
    id: 'contracts', label: 'Contracts', order: 25, count: (v) => v.contracts.held.length || null,
    render(v) { return card('Contracts held by the house', heldContracts(v), { tight: true, hint: 'deliver in the issuing city before the deadline; a lapse costs traders standing' }); }
  });
  registerTab('caravan', {
    id: 'effects', label: 'Crew effects', order: 30,
    render(v) {
      return card('What the roster does for the convoy', table({
        id: 'effects', rows: v.crew.skills, rowKey: (r) => r.id, defaultSort: { id: 'name', dir: 'asc' },
        cols: [
          { id: 'name', label: 'Skill', cell: (r) => [r.name, h('span', { class: 'sub' }, r.blurb)] },
          { id: 'lever', label: 'Lever', get: (r) => r.lever, cell: (r) => badge(r.lever, 'outline') },
          { id: 'level', label: 'Best aboard', num: true, get: (r) => r.level, cell: (r) => h('div', { style: 'display:flex;gap:8px;align-items:center;justify-content:flex-end' }, pips(r.level, r.maxLevel, false), h('b', null, r.level + ' / ' + r.maxLevel)) },
          { id: 'leaderName', label: 'Led by', get: (r) => r.leaderName || '', cell: (r) => r.leaderName ? r.leaderName : h('span', { class: 'tone-muted' }, 'nobody') },
          { id: 'effectText', label: 'Effect today', sortable: false, cell: (r) => r.effectText }
        ]
      }), { tight: true, hint: 'a skill is led by the best hand aboard' });
    }
  });

  // Crew
  registerPage({
    id: 'crew', label: 'Crew', icon: 'crew', order: 40,
    subtitle: (v) => v.crew.size + ' of ' + v.crew.capacity + ' seats · ' + fmt.cr(v.crew.dailyWages) + ' / day',
    flag: (v) => v.crew.recruitment && v.crew.size < v.crew.capacity && v.crew.recruitment.candidates.some((c) => c.affordable) ? true : null,
    detail(v, d) {
      if (d.kind === 'member') { const m = v.crew.roster.find((x) => x.id === d.id); return m ? personPage(v, m, 'member') : null; }
      if (d.kind === 'candidate') { const R = v.crew.recruitment; const c = R && R.candidates.find((x) => x.id === d.id); return c ? personPage(v, c, 'candidate') : null; }
      return null;
    }
  });
  registerTab('crew', {
    id: 'roster', label: 'Roster', order: 10, count: (v) => v.crew.size,
    render(v) {
      const team = v.crew.skills;
      if (!v.crew.roster.length) {
        return empty('Nobody on the payroll', 'The convoy runs on the market\'s terms until someone aboard can read a road or argue a price.', v.crew.recruitment ? btn('Open the recruitment board', () => goTab('recruit'), { kind: 'primary' }) : null);
      }
      const grid = table({
        id: 'roster', rows: v.crew.roster, rowKey: (r) => r.id, onRow: (r) => { S.detail = { kind: 'member', id: r.id }; render(); },
        defaultSort: { id: 'name', dir: 'asc' },
        cols: [
          { id: 'name', label: 'Name', get: (r) => r.name, cell: (r) => personRow(r) },
          { id: 'post', label: 'Post', get: (r) => r.postName, cell: (r) => postSelect(v, r) },
          { id: 'skills', label: 'Skills', sortable: false, cell: (r) => skillsMini(r, team) },
          { id: 'leads', label: 'Leads', sortable: false, cell: (r) => { const led = team.filter((s) => s.leaderName === r.name); return led.length ? led.map((s) => badge(s.name, 'amber')) : h('span', { class: 'tone-muted' }, '—'); } },
          { id: 'know', label: 'Best knowledge', get: (r) => Math.max(0, ...r.knowledge.map((k) => k.level)), cell: (r) => { const k = r.knowledge.slice().sort((a, b) => b.level - a.level)[0]; return k ? [k.name, h('span', { class: 'sub' }, k.level + ' / ' + k.maxLevel)] : '—'; } },
          { id: 'traits', label: 'Trait', sortable: false, cell: (r) => r.traits.length ? r.traits.map((t) => badge(t.name, 'amber')) : h('span', { class: 'tone-muted' }, '—') },
          { id: 'dailyWage', label: 'Wage / day', num: true, get: (r) => r.dailyWage, cell: (r) => fmt.cr(r.dailyWage) },
          { id: 'hiredDay', label: 'Hired', num: true, get: (r) => r.hiredDay, cell: (r) => ['day ' + r.hiredDay, h('span', { class: 'sub' }, r.hiredAt)] },
          { id: 'act', label: '', sortable: false, cell: (r) => h('div', { class: 'mini-actions' }, btn('Sheet', () => { S.detail = { kind: 'member', id: r.id }; render(); }, { size: 'xs' })) }
        ]
      });
      return h('div', null,
        h('div', { class: 'ops-grid c4' }, kpi('Aboard', v.crew.size + ' / ' + v.crew.capacity), kpi('Payroll / day', fmt.cr(v.crew.dailyWages)), kpi('Severance owed', fmt.cr(v.crew.roster.reduce((a, r) => a + r.severance, 0)), 'if everyone were paid off'), kpi('Board', v.crew.recruitment ? v.crew.recruitment.candidates.length + ' on offer' : 'not in a city', v.crew.recruitment ? v.crew.recruitment.cityName : '')),
        postCards(v),
        card('Roster', grid, { tight: true, hint: 'click a name for the full sheet · the post column changes at once' })
      );
    }
  });
  registerTab('crew', { id: 'recruit', label: 'Recruitment', order: 20, when: (v) => !!v.crew.recruitment, count: (v) => v.crew.recruitment ? v.crew.recruitment.candidates.length : null, render: recruitBoard });
  registerTab('crew', { id: 'effects', label: 'Convoy effects', order: 30, render: (v) => tabs.caravan.find((t) => t.id === 'effects').render(v) });

  // Ledger
  registerPage({
    id: 'ledger', label: 'Ledger', icon: 'ledger', order: 50,
    subtitle: (v) => 'day ' + v.day + ' · ' + fmt.cr(v.cash),
    render(v) {
      const log = S.snap.log || [];
      const kinds = ['all', ...new Set(log.map((e) => e.kind))];
      const rows = S.logKind === 'all' ? log : log.filter((e) => e.kind === S.logKind);
      return h('div', null,
        h('div', { class: 'ops-grid c4' }, kpi('Credits', fmt.cr(v.cash)), kpi('Net worth', fmt.cr(v.netWorth)), kpi('Burn / day', fmt.cr(v.convoy.dailyUpkeep + (v.warehouse.rented ? v.warehouse.dailyRent : 0)), 'convoy' + (v.warehouse.rented ? ' + storeroom rent' : '')), kpi('Entries', log.length, 'newest first')),
        card(null, [h('div', { class: 'tbl-toolbar' }, h('div', { class: 'seg' }, kinds.map((k) => h('button', { class: k === S.logKind ? 'on' : '', onclick: () => { S.logKind = k; render(); } }, k === 'all' ? 'All' : k)))), logList(rows)], { tight: true })
      );
    },
    footer() {
      const b = S.build; if (!b) return null;
      return h('div', { class: 'footer-build' }, h('span', null, 'build ', h('b', null, b.version)), h('span', null, 'commit ', h('b', null, b.commit || '—'), b.dirty ? ' +' + b.dirtyFiles + ' uncommitted' : ''), h('span', null, 'built ', h('b', null, b.builtAgo)), b.stale ? h('span', { class: 'stale' }, 'stale: ' + b.staleReason) : h('span', { class: 'tone-good' }, 'current'));
    }
  });

  registerPage({
    id: 'newrun', label: 'New run', icon: 'settings', order: 90, bottom: true,
    action() { confirm('Start a new run?', 'The current house is closed and a fresh one opens at the start city with the starting purse. Nothing is saved.', { label: 'New run', run: () => { S.memo = {}; S.arrival = null; persist(); MECHA.newGame().then(() => { toast('New run opened.'); close(); }).catch((e) => toast(String(e.message || e), 'alert')); } }, 'danger'); }
  });

  // ── boot ──────────────────────────────────────────────────────────────────
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', mount); else mount();

  return { open, close, toggle, isOpen, update, registerPage, registerTab, render, h, fmt, state: S };
})();
window.OPS = OPS;
