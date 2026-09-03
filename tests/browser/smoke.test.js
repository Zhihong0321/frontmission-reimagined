const { test, expect } = require('@playwright/test');

const requiredAssets = [
  '/chart/world.js',
  '/chart/game-bridge.js',
  '/chart/ops.js',
  '/chart/ops.css',
  '/chart/chart-tiles-worker.js',
  '/chart/art/manifest.js'
];

function evidence(logs) {
  const section = (name, entries) => entries.length ? `${name}:\n${entries.slice(-20).join('\n')}` : `${name}: none`;
  return [
    section('page errors', logs.pageErrors),
    section('console errors', logs.consoleErrors),
    section('failed requests', logs.failedRequests),
    section('404 responses', logs.notFound),
    section('API failures', logs.apiFailures),
    section('worker errors', logs.workerErrors),
    section('worker messages', logs.workerMessages)
  ].join('\n');
}

function isOptionalArt(url) {
  return /\/chart\/art\/(?:tex-deep|truck)\.png(?:\?|$)/i.test(url);
}

test('Keeper chart boots, paints, opens ops, and crosses the browser bridge', async ({ page, request }) => {
  const logs = {
    pageErrors: [],
    consoleErrors: [],
    failedRequests: [],
    notFound: [],
    apiFailures: [],
    workerErrors: [],
    workerMessages: []
  };
  const workers = [];

  // Keep the production Worker object and event flow intact, but retain a small
  // test-only record of messages/errors. chart.html intentionally swallows a tile
  // response carrying `err`, so URL creation alone cannot prove tile painting.
  await page.addInitScript(() => {
    const NativeWorker = window.Worker;
    const records = [];
    window.__smokeWorkers = records;
    window.Worker = function SmokeWorker(...args) {
      const worker = new NativeWorker(...args);
      const record = { url: String(args[0]), messages: [], errors: [] };
      records.push(record);
      worker.addEventListener('message', (event) => {
        const data = event.data;
        record.messages.push(data && typeof data === 'object'
          ? { type: data.type || null, err: data.err || null }
          : { type: null, err: String(data) });
      });
      worker.addEventListener('error', (event) => {
        record.errors.push(event.message || 'worker error');
      });
      return worker;
    };
    window.Worker.prototype = NativeWorker.prototype;
  });

  // Register every listener before navigation. Optional sprite PNGs are the only
  // tolerated request failures: the current manifest loader deliberately treats
  // missing art as optional, while all scripts, stylesheets, APIs, and the manifest
  // itself remain hard failures.
  page.on('pageerror', (error) => logs.pageErrors.push(error.message));
  page.on('console', (message) => {
    if (message.type() === 'error') logs.consoleErrors.push(message.text());
  });
  page.on('requestfailed', (request_) => {
    const error = request_.failure();
    const url = request_.url();
    if (error && error.errorText === 'net::ERR_ABORTED') return;
    if (isOptionalArt(url)) return;
    logs.failedRequests.push(`${request_.method()} ${url} (${error ? error.errorText : 'unknown'})`);
  });
  page.on('response', (response) => {
    const status = response.status();
    const url = response.url();
    if (status === 404 && !isOptionalArt(url)) logs.notFound.push(`${status} ${url}`);
    if (url.includes('/api/') && status >= 400) logs.apiFailures.push(`${status} ${url}`);
  });
  page.on('worker', (worker) => {
    workers.push(worker);
    worker.on('console', (message) => {
      if (message.type() === 'error') logs.workerErrors.push(`${worker.url()}: ${message.text()}`);
    });
  });

  const collectWorkerEvidence = async () => {
    try {
      const records = await page.evaluate(() => (window.__smokeWorkers || []).map((record) => ({
        url: record.url,
        errors: record.errors.slice(),
        messages: record.messages.slice()
      })));
      for (const record of records) {
        for (const error of record.errors) logs.workerErrors.push(`${record.url}: ${error}`);
        for (const message of record.messages) logs.workerMessages.push(`${record.url}: ${JSON.stringify(message)}`);
      }
      return records;
    } catch (_) {
      return [];
    }
  };

  try {
    const response = await page.goto('/chart/', { waitUntil: 'domcontentloaded' });
    expect(response && response.status(), evidence(logs)).toBe(200);
    expect(response.headers()['content-type'] || '', evidence(logs)).toContain('text/html');

    await page.waitForFunction(() => !document.querySelector('#loading'), null, { timeout: 180000 });
    await page.waitForFunction(() => window.WORLD && window.MANIFEST && window.MECHA && window.OPS, null, { timeout: 15000 });

    const world = await page.evaluate(() => ({
      cities: Array.isArray(window.WORLD.cities) ? window.WORLD.cities.length : 0,
      routes: Array.isArray(window.WORLD.routes) ? window.WORLD.routes.length : 0,
      map: !!(window.WORLD.map && typeof window.WORLD.map === 'object'),
      truck: !!(window.WORLD.truck && typeof window.WORLD.truck === 'object'),
      manifestSprites: Array.isArray(window.MANIFEST.sprites) ? window.MANIFEST.sprites.length : 0,
      mecha: typeof window.MECHA === 'object',
      ops: typeof window.OPS === 'object'
    }));
    expect(world, evidence(logs)).toMatchObject({ map: true, truck: true, mecha: true, ops: true });
    expect(world.cities, evidence(logs)).toBeGreaterThan(0);
    expect(world.routes, evidence(logs)).toBeGreaterThan(0);
    expect(world.manifestSprites, evidence(logs)).toBeGreaterThan(0);

    const manifestFiles = await page.evaluate(() => (window.MANIFEST.sprites || [])
      .map((sprite) => sprite.file)
      .filter((file) => typeof file === 'string' && file.length > 0));
    expect(manifestFiles.length, evidence(logs)).toBe(world.manifestSprites);

    const state = await page.evaluate(async () => {
      const result = await fetch('/api/state');
      return { status: result.status, body: await result.json() };
    });
    expect(state.status, evidence(logs)).toBe(200);
    expect(state.body.view, evidence(logs)).toBeTruthy();
    expect(typeof state.body.view.day, evidence(logs)).toBe('number');
    expect(typeof state.body.view.cash, evidence(logs)).toBe('number');

    const canvas = await page.evaluate(() => {
      const map = document.querySelector('#map');
      if (!map || map.tagName !== 'CANVAS') return { ok: false, reason: 'missing canvas' };
      const ctx = map.getContext('2d');
      const points = [
        [0.12, 0.18], [0.5, 0.18], [0.88, 0.18],
        [0.12, 0.5], [0.5, 0.5], [0.88, 0.5],
        [0.12, 0.82], [0.5, 0.82], [0.88, 0.82]
      ];
      const samples = points.map(([x, y]) => {
        const pixel = ctx.getImageData(Math.floor(map.width * x), Math.floor(map.height * y), 1, 1).data;
        return Array.from(pixel);
      });
      return {
        ok: map.width > 0 && map.height > 0,
        width: map.width,
        height: map.height,
        samples,
        painted: samples.filter(([r, g, b]) => r > 10 || g > 10 || b > 10).length
      };
    });
    expect(canvas.ok, evidence(logs)).toBe(true);
    expect(canvas.width, evidence(logs)).toBeGreaterThan(0);
    expect(canvas.height, evidence(logs)).toBeGreaterThan(0);
    expect(canvas.painted, evidence(logs)).toBeGreaterThanOrEqual(4);

    await page.keyboard.press('Tab');
    await page.waitForFunction(() => window.OPS && window.OPS.isOpen(), null, { timeout: 5000 });
    await expect(page.locator('#ops')).toBeVisible({ timeout: 5000 });
    expect(await page.locator('#ops-rail button, #ops .ops-tab').count(), evidence(logs)).toBeGreaterThanOrEqual(3);
    await page.keyboard.press('Tab');
    await page.waitForFunction(() => window.OPS && !window.OPS.isOpen(), null, { timeout: 5000 });

    // A large wheel gesture is the real user path to cam.z > 3, which starts the
    // lazy worker without depending on inaccessible classic-script lexical bindings.
    // Use bounded increments so the gesture crosses the threshold without jumping
    // straight to the chart's maximum zoom (which currently exposes a canvas arc
    // edge case before the worker can run).
    const workerPromise = page.waitForEvent('worker', { timeout: 15000 });
    await page.mouse.move(640, 360);
    for (let i = 0; i < 20 && !workers.some((worker) => worker.url().includes('chart-tiles-worker.js')); i++) {
      await page.mouse.wheel(0, -150);
      await page.waitForTimeout(30);
    }
    const tileWorker = await workerPromise;
    expect(tileWorker.url(), evidence(logs)).toContain('chart-tiles-worker.js');
    await expect.poll(() => workers.some((worker) => worker.url().includes('chart-tiles-worker.js')), { timeout: 5000 }).toBe(true);
    await expect.poll(async () => page.evaluate(() => {
      const record = (window.__smokeWorkers || []).find((item) => item.url.includes('chart-tiles-worker.js'));
      const messages = record ? record.messages : [];
      return {
        ready: messages.some((message) => message.type === 'ready'),
        tile: messages.some((message) => message.type === 'tile' && !message.err),
        tileErrors: messages.filter((message) => message.type === 'tile' && message.err).map((message) => message.err),
        errors: record ? record.errors : []
      };
    }), { timeout: 15000 }).toMatchObject({ ready: true, tile: true, tileErrors: [], errors: [] });
    await collectWorkerEvidence();
    expect(logs.workerErrors, evidence(logs)).toEqual([]);

    for (const asset of requiredAssets) {
      const assetResponse = await request.get(asset);
      expect(assetResponse.status(), `${asset} ${evidence(logs)}`).toBe(200);
    }
    for (const relativeFile of manifestFiles) {
      const asset = `/chart/${relativeFile.replace(/^\/+/, '')}`;
      const assetResponse = await request.head(asset);
      expect(assetResponse.status(), `${asset} ${evidence(logs)}`).toBe(200);
    }

    const command = await page.evaluate(async () => {
      const started = await window.MECHA.newGame(24681357);
      const waited = await window.MECHA.command({ type: 'wait', days: 1 });
      return {
        started: { day: started.view && started.view.day, error: started.error || null },
        waited: { day: waited.view && waited.view.day, error: waited.error || null }
      };
    });
    expect(command.started.error, evidence(logs)).toBeNull();
    expect(command.waited.error, evidence(logs)).toBeNull();
    expect(command.waited.day, evidence(logs)).toBeGreaterThan(command.started.day);
    expect(logs.pageErrors, evidence(logs)).toEqual([]);
    // Chromium reports each intentionally absent optional-art PNG as the same generic
    // console line. The response listener above still records every non-art 404, so
    // remove only this exact line when all observed 404s were optional art.
    const consoleErrors = logs.consoleErrors.filter((message) =>
      !/^Failed to load resource: the server responded with a status of 404 \(Not Found\)$/i.test(message));
    expect(consoleErrors, evidence(logs)).toEqual([]);
    expect(logs.failedRequests, evidence(logs)).toEqual([]);
    expect(logs.notFound, evidence(logs)).toEqual([]);
    expect(logs.apiFailures, evidence(logs)).toEqual([]);
  } catch (error) {
    await collectWorkerEvidence();
    if (error && typeof error.message === 'string' && !error.message.includes('page errors:')) {
      error.message += `\n\n${evidence(logs)}`;
    }
    throw error;
  }
});
