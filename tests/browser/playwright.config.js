const path = require('node:path');
const { defineConfig } = require('@playwright/test');

const repositoryRoot = path.resolve(__dirname, '..', '..');

module.exports = defineConfig({
  testDir: __dirname,
  testMatch: 'smoke.test.js',
  timeout: 300000,
  expect: { timeout: 15000 },
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  reporter: [['list']],
  use: {
    baseURL: 'http://127.0.0.1:5080',
    browserName: 'chromium',
    headless: true,
    navigationTimeout: 90000,
    actionTimeout: 15000
  },
  webServer: {
    command: 'dotnet run --project src/MechaTrader.Host -c Release',
    cwd: repositoryRoot,
    url: 'http://127.0.0.1:5080/api/state',
    timeout: 240000,
    reuseExistingServer: false,
    stdout: 'pipe',
    stderr: 'pipe'
  }
});
