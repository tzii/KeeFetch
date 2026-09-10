import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: '.',
  testMatch: 'website.spec.js',
  outputDir: '../../site-qa/test-results',
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: 0,
  workers: process.env.CI ? 2 : undefined,
  timeout: 30000,
  expect: { timeout: 7000 },
  reporter: [['list'], ['json', { outputFile: '../../site-qa/results.json' }], ['html', { outputFolder: '../../site-qa/report', open: 'never' }]],
  use: { baseURL: 'http://127.0.0.1:8765', trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: ['chromium', 'firefox', 'webkit'].map(browserName => ({ name: browserName, use: { browserName } })),
  webServer: { command: 'python3 -m http.server 8765 --bind 127.0.0.1 --directory ../../site/dist', url: 'http://127.0.0.1:8765', reuseExistingServer: !process.env.CI, timeout: 10000 }
});
