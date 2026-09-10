import { test, expect } from '@playwright/test';
import { createRequire } from 'node:module';
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve, sep } from 'node:path';
const require = createRequire(import.meta.url);
const axePath = require.resolve('axe-core/axe.min.js');
test.beforeAll(async ({ browser, browserName }) => {
  mkdirSync('../../site-qa', { recursive: true });
  writeFileSync(`../../site-qa/browser-${browserName}.json`, JSON.stringify({ engine: browserName, version: browser.version() }, null, 2));
});
const pages = ['index.html', 'getting-started.html', 'profiles.html', 'privacy.html', 'troubleshooting.html', 'benchmarks.html', 'contributing.html'];
const modes = [
  { name: 'desktop-light', width: 1440, height: 1000, color: 'light', audit: true },
  { name: 'desktop-dark', width: 1440, height: 1000, color: 'dark', audit: true },
  { name: 'small-phone', width: 320, height: 568, color: 'light', audit: true },
  { name: 'phone-dark', width: 375, height: 812, color: 'dark' },
  { name: 'tablet', width: 768, height: 1024, color: 'light' },
  { name: 'small-desktop', width: 1024, height: 768, color: 'dark' },
  { name: 'wide-desktop', width: 1920, height: 1080, color: 'light' },
  { name: 'landscape', width: 844, height: 390, color: 'light', touch: true },
  { name: 'no-javascript', width: 375, height: 812, color: 'light', noJS: true },
  { name: 'reduced-motion', width: 375, height: 812, color: 'light', reduced: true },
  { name: 'large-text-phone', width: 375, height: 812, color: 'light', largeText: true },
  { name: 'large-text-desktop', width: 1440, height: 1000, color: 'light', largeText: true }
];
async function audit(page) {
  await page.addScriptTag({ path: axePath });
  const result = await page.evaluate(async () => {
    const { violations } = await window.axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] } });
    return violations.map(v => ({ id: v.id, impact: v.impact, help: v.help, nodes: v.nodes.map(n => ({ target: n.target, summary: n.failureSummary })) }));
  });
  expect(result, JSON.stringify(result, null, 2)).toEqual([]);
}
async function layout(page) {
  const failures = await page.evaluate(() => {
    const found = [];
    if (document.documentElement.scrollWidth > innerWidth + 1) {
      found.push('Document overflows: ' + document.documentElement.scrollWidth + ' / ' + innerWidth);
      for (const element of document.querySelectorAll('main *, footer *')) {
        if (element.closest('.table-wrap')) continue;
        const box = element.getBoundingClientRect();
        if (box.width && box.right > innerWidth + 1) found.push(element.tagName + '.' + element.className + ': right ' + Math.round(box.right));
      }
    }
    for (const element of document.querySelectorAll('a,button,summary')) {
      if (!element.getClientRects().length || element.closest('.table-wrap') || element.classList.contains('skip-link')) continue;
      const r = element.getBoundingClientRect();
      if (r.width && (r.left < -1 || r.right > innerWidth + 1)) found.push('Control outside viewport: ' + element.textContent.slice(0, 70));
    }
    for (const icon of document.querySelectorAll('.icon-button svg')) {
      if (!icon.getClientRects().length) continue;
      const box = icon.getBoundingClientRect();
      const button = icon.closest('button').getBoundingClientRect();
      if (box.left < button.left || box.right > button.right || box.top < button.top || box.bottom > button.bottom) found.push('Toolbar symbol escapes its button: ' + icon.closest('button').id);
    }
    for (const image of document.images) if (!image.complete || !image.naturalWidth) found.push('Broken image: ' + image.getAttribute('src'));
    return found;
  });
  expect(failures).toEqual([]);
  await expect(page.locator('main#main')).toHaveCount(1);
  await expect(page.locator('h1')).toHaveCount(1);
}
for (const mode of modes) {
  for (const file of pages) {
    test(`${mode.name}: ${file}`, async ({ browser }, testInfo) => {
      const context = await browser.newContext({ viewport: { width: mode.width, height: mode.height }, colorScheme: mode.color, reducedMotion: mode.reduced ? 'reduce' : 'no-preference', javaScriptEnabled: !mode.noJS, hasTouch: !!mode.touch });
      const page = await context.newPage();
      const problems = [];
      page.on('pageerror', error => problems.push(error.message));
      page.on('response', response => { if (response.status() >= 400) problems.push('HTTP ' + response.status() + ': ' + response.url()); });
      page.on('requestfailed', request => problems.push('Request failed: ' + request.url()));
      page.on('request', request => { if (!request.url().startsWith('http://127.0.0.1:8765/')) problems.push('External runtime request: ' + request.url()); });
      await page.goto('http://127.0.0.1:8765/' + file);
      // Firefox cannot settle page-owned promises with page JavaScript disabled.
      if (!mode.noJS) await page.evaluate(() => document.fonts.ready);
      if (mode.largeText) await page.addStyleTag({ content: 'html { font-size: 200% !important; }' });
      await layout(page);
      if (!mode.noJS) await expect(page.locator('html')).toHaveAttribute('data-theme', mode.color);
      else {
        await expect(page.locator('#primary-nav')).toBeVisible();
        await expect(page.locator('#theme-toggle')).toBeHidden();
      }
      if (mode.reduced) await expect(page.locator('html')).toHaveAttribute('data-motion', 'off');
      if (file === 'index.html' || (['getting-started.html', 'privacy.html', 'profiles.html'].includes(file) && ['desktop-light', 'phone-dark', 'large-text-phone'].includes(mode.name))) {
        const directory = `../../site-qa/screenshots/${testInfo.project.name}`;
        mkdirSync(directory, { recursive: true });
        await page.screenshot({ path: `${directory}/${file.replace('.html', '')}-${mode.name}.png`, fullPage: true, animations: 'disabled' });
      }
      if (mode.audit) await audit(page);
      expect(problems).toEqual([]);
      await context.close();
    });
  }
}

test('keyboard skip link and mobile navigation', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await page.goto('/index.html');
  await page.keyboard.press('Tab');
  await expect(page.locator('.skip-link')).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.locator('#main')).toBeFocused();
  await page.locator('#menu-toggle').focus();
  await page.keyboard.press('Enter');
  await expect(page.locator('#primary-nav')).toBeVisible();
  await expect(page.locator('#menu-toggle')).toHaveAttribute('aria-expanded', 'true');
  await page.keyboard.press('Escape');
  await expect(page.locator('#menu-toggle')).toBeFocused();
  await expect(page.locator('#primary-nav')).toBeHidden();
  await page.locator('#menu-toggle').click();
  await page.locator('#primary-nav a[href="privacy.html"]').click();
  await expect(page).toHaveURL(/privacy\.html$/);
  await expect(page.locator('#primary-nav a[aria-current="page"]')).toHaveText('Privacy');
});

test('local comparison is reversible, keyboard usable and immediate', async ({ page }) => {
  await page.goto('/index.html');
  const before = page.getByRole('button', { name: 'Before', exact: true });
  const after = page.getByRole('button', { name: 'After', exact: true });
  await before.focus();
  await page.keyboard.press('Space');
  await expect(before).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('.vault-entry.is-resolved')).toHaveCount(0);
  await after.click();
  await expect(page.locator('.vault-entry.is-resolved')).toHaveCount(6);
  await expect(page.locator('#demo-status')).toContainText('Easier to recognize');
  for (let i = 0; i < 6; i++) { await before.click(); await after.click(); }
  await expect(after).toHaveAttribute('aria-pressed', 'true');
  await expect(after).toBeEnabled();
});

test('reduced motion keeps the comparison functional', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto('/index.html');
  await expect(page.locator('#motion-toggle')).toBeDisabled();
  await page.getByRole('button', { name: 'Before', exact: true }).click();
  await expect(page.locator('.vault-entry.is-resolved')).toHaveCount(0);
  await page.getByRole('button', { name: 'After', exact: true }).click();
  await expect(page.locator('.vault-entry.is-resolved')).toHaveCount(6);
  expect(await page.locator('.entry-icon').first().evaluate(el => getComputedStyle(el).animationName)).toBe('none');
});

test('system theme, explicit override and persistence', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'light' });
  await page.goto('/index.html');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await page.emulateMedia({ colorScheme: 'dark' });
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await page.locator('#theme-toggle').click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await page.goto('/getting-started.html');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
});

test('denied storage and absent observer do not break controls', async ({ page }) => {
  await page.addInitScript(() => {
    Object.defineProperty(window, 'localStorage', { get() { throw new DOMException('Denied', 'SecurityError'); } });
    delete window.IntersectionObserver;
  });
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  await page.goto('/index.html');
  const initial = await page.locator('html').getAttribute('data-theme');
  await page.locator('#theme-toggle').click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', initial === 'dark' ? 'light' : 'dark');
  await page.locator('#motion-toggle').click();
  await expect(page.locator('html')).toHaveAttribute('data-motion', 'off');
  await page.locator('#motion-toggle').click();
  await expect(page.locator('html')).toHaveAttribute('data-motion', 'on');
  await expect(page.locator('#get-started')).toBeVisible();
  expect(errors).toEqual([]);
});

test('failed optional data keeps the complete comparison', async ({ page }) => {
  await page.route('**/data/profiles.json', route => route.fulfill({ status: 503, body: 'Unavailable' }));
  await page.goto('/profiles.html');
  await expect(page.locator('tr[data-profile-id]')).toHaveCount(4);
  await expect(page.locator('tr[data-profile-id="everyday"]')).toContainText('Twenty Icons');
  await audit(page);
});

test('malformed optional data cannot inject markup', async ({ page }) => {
  await page.route('**/data/profiles.json', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ profiles: [{ isVisible: true, displayName: '<img src=x onerror=alert(1)>', description: 'Sample', intendedUse: 'Sample', cumulativeTimeoutMs: 1000 }] }) }));
  await page.goto('/profiles.html');
  await expect(page.locator('.profile-card h3')).toHaveText('<img src=x onerror=alert(1)>');
  await expect(page.locator('.profile-card img')).toHaveCount(0);
  await expect(page.locator('tr[data-profile-id]')).toHaveCount(4);
});

test('open comparison and mobile touch targets', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await page.goto('/index.html');
  await page.locator('.profile-details summary').click();
  await expect(page.locator('tr[data-profile-id]')).toHaveCount(4);
  await layout(page);
  for (const selector of ['#theme-toggle', '#motion-toggle', '#menu-toggle', '[data-demo-state="before"]', '[data-demo-state="after"]']) {
    const bounds = await page.locator(selector).boundingBox();
    expect(bounds.width, selector).toBeGreaterThanOrEqual(44);
    expect(bounds.height, selector).toBeGreaterThanOrEqual(44);
  }
  const comparison = page.getByRole('region', { name: 'Profile comparison', exact: true });
  await comparison.focus();
  await page.keyboard.press('ArrowRight');
  await expect.poll(() => comparison.evaluate(element => element.scrollLeft)).toBeGreaterThan(0);
  await audit(page);
});

test('forced colors retains selected state and focus', async ({ page, browserName }) => {
  test.skip(browserName === 'webkit', 'WebKit does not emulate Windows forced-colors. Chromium and Firefox execute this test.');
  await page.emulateMedia({ forcedColors: 'active' });
  await page.goto('/index.html');
  expect(await page.evaluate(() => matchMedia('(forced-colors: active)').matches)).toBe(true);
  await page.getByRole('button', { name: 'Before', exact: true }).click();
  await expect(page.locator('[data-demo-state="before"]')).toHaveAttribute('aria-pressed', 'true');
  await layout(page);
});

test('print guide keeps content and removes navigation', async ({ page }) => {
  await page.goto('/getting-started.html');
  await page.emulateMedia({ media: 'print' });
  await expect(page.locator('#primary-nav')).toBeHidden();
  await expect(page.locator('#install')).toBeVisible();
  await expect(page.locator('#roll-back')).toBeVisible();
  await layout(page);
});


test('font failure preserves reading and controls', async ({ page }) => {
  await page.route('**/assets/fonts/*', route => route.fulfill({ status: 503, body: 'Unavailable' }));
  await page.setViewportSize({ width: 375, height: 812 });
  await page.goto('/index.html');
  await page.evaluate(() => document.fonts.ready);
  await layout(page);
  await page.getByRole('button', { name: 'Before', exact: true }).click();
  await expect(page.locator('.vault-entry.is-resolved')).toHaveCount(0);
  await audit(page);
});

test('failed scripts leave usable navigation and readable content', async ({ page }) => {
  await page.route('**/assets/js/*.js', route => route.fulfill({ status: 503, body: 'Unavailable' }));
  await page.setViewportSize({ width: 375, height: 812 });
  await page.goto('/index.html');
  await expect(page.locator('#primary-nav')).toBeVisible();
  await expect(page.locator('#theme-toggle')).toBeHidden();
  await expect(page.locator('#demo-controls')).toBeHidden();
  await expect(page.locator('#get-started')).toBeVisible();
  await page.locator('#primary-nav a[href="profiles.html"]').click();
  await expect(page.locator('tr[data-profile-id]')).toHaveCount(4);
  await layout(page);
  await audit(page);
});

test('increased text spacing does not clip any guide', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  for (const file of pages) {
    await page.goto('/' + file);
    await page.addStyleTag({ content: '* { line-height: 1.5 !important; letter-spacing: .12em !important; word-spacing: .16em !important; } p { margin-bottom: 2em !important; }' });
    await layout(page);
  }
});

test('preferences synchronize between tabs and preserve motion choice', async ({ page, context }) => {
  await page.emulateMedia({ colorScheme: 'light', reducedMotion: 'no-preference' });
  await page.goto('/index.html');
  const other = await context.newPage();
  await other.goto('/privacy.html');
  await page.locator('#theme-toggle').click();
  await expect(other.locator('html')).toHaveAttribute('data-theme', 'dark');
  await page.locator('#motion-toggle').click();
  await expect(other.locator('html')).toHaveAttribute('data-motion', 'off');
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('data-motion', 'off');
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await expect(page.locator('#motion-toggle')).toBeDisabled();
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await expect(page.locator('#motion-toggle')).toBeEnabled();
  await expect(page.locator('html')).toHaveAttribute('data-motion', 'off');
  await other.close();
});

test('GitHub Pages subdirectory keeps all assets and links relative', async ({ page }) => {
  const dist = resolve('../../site/dist');
  const requests = [];
  await page.route('http://127.0.0.1:8765/KeeFetch/**', async route => {
    const name = decodeURIComponent(new URL(route.request().url()).pathname.slice('/KeeFetch/'.length)) || 'index.html';
    const asset = resolve(dist, name);
    if (!asset.startsWith(dist + sep)) return route.abort();
    await route.fulfill({ path: asset });
  });
  page.on('request', request => requests.push(new URL(request.url()).pathname));
  await page.goto('/KeeFetch/index.html');
  await page.evaluate(() => document.fonts.ready);
  await layout(page);
  await page.getByRole('button', { name: 'Before', exact: true }).click();
  await expect(page.locator('.vault-entry.is-resolved')).toHaveCount(0);
  await page.goto('/KeeFetch/profiles.html');
  await expect(page.locator('.profile-card')).toHaveCount(4);
  expect(requests.every(path => path.startsWith('/KeeFetch/'))).toBe(true);
});
