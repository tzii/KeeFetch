"""Exercise the built site under its real GitHub Pages subpath.

Test-only dependencies: playwright==1.62.0; axe-core==4.10.3 (optional locally,
required in CI). Run `python site/build.py`, then this script. Nothing deploys.
"""
from __future__ import annotations

import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from importlib.metadata import version
import json
import re
from pathlib import Path
import shutil
import tempfile
from threading import Thread
import time
import traceback
from urllib.parse import urlparse

from playwright.sync_api import sync_playwright, expect

ROOT = Path(__file__).resolve().parents[1]
PAGES = ('index', 'getting-started', 'profiles', 'privacy', 'troubleshooting', 'benchmarks', 'contributing')
WIDTHS = (320, 390, 768, 1024, 1440, 1920)
OVERFLOW = """() => {
 const root = document.documentElement;
 return {width: innerWidth, scroll: Math.max(root.scrollWidth, document.body.scrollWidth),
  offenders: [...document.querySelectorAll('main *,header *,footer *')].filter(e => {
   const r=e.getBoundingClientRect();
   return r.width && r.right > innerWidth+1 && !e.closest('.table-wrap,pre');
  }).slice(0,8).map(e=>e.tagName+'.'+e.className)};
}"""
STORAGE_DENIED = """Object.defineProperty(window, 'localStorage', {get() {
 throw new DOMException('Storage disabled for this test', 'SecurityError');
}});"""
LEGACY_MEDIA = """const original = window.matchMedia.bind(window);
window.matchMedia = q => {
 const m=original(q);
 return {get matches(){return m.matches;}, media:m.media,
 addListener:fn=>m.addEventListener('change',fn),
 removeListener:fn=>m.removeEventListener('change',fn)};
};"""


class QuietHandler(SimpleHTTPRequestHandler):
    def log_message(self, *_args):
        pass


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--browser', choices=('chromium', 'firefox', 'webkit'), default='chromium')
    parser.add_argument('--site', type=Path, default=ROOT / 'site/dist')
    parser.add_argument('--output', type=Path, default=ROOT / 'site-qa')
    parser.add_argument('--axe', type=Path)
    parser.add_argument('--executable', help='Optional installed browser executable for local runs')
    args = parser.parse_args()
    if not (args.site / 'index.html').is_file():
        parser.error('Build the site first: python site/build.py')
    if args.axe and not args.axe.is_file():
        parser.error('--axe must identify axe.min.js')
    args.output.mkdir(parents=True, exist_ok=True)
    results = []
    axe_runs = []
    report = {'browser': args.browser, 'playwright': version('playwright'), 'cases': results, 'axe': axe_runs}

    with tempfile.TemporaryDirectory(prefix='keefetch-site-test-') as temporary:
        shutil.copytree(args.site, Path(temporary) / 'KeeFetch')
        server = ThreadingHTTPServer(('127.0.0.1', 0), partial(QuietHandler, directory=temporary))
        thread = Thread(target=server.serve_forever, daemon=True)
        thread.start()
        base = f'http://127.0.0.1:{server.server_port}/KeeFetch/'
        try:
            with sync_playwright() as playwright:
                options = {'executable_path': args.executable} if args.executable else {}
                browser = getattr(playwright, args.browser).launch(**options)
                report['browser_version'] = browser.version

                def check_layout(page):
                    info = page.evaluate(OVERFLOW)
                    assert info['scroll'] <= info['width'] + 1, f'Horizontal overflow: {info}'
                    expect(page.locator('h1')).to_be_visible()
                    assert page.locator('#primary-nav a').count() == 8
                    assert page.locator('main').count() == 1
                    page.wait_for_function('Array.from(document.images).every(i => i.complete && i.naturalWidth > 0)')
                    for selector in ('.icon-button', '.button', '.segmented button', '.motion-toolbar button', '.preset-controls button'):
                        for control in page.locator(selector).all():
                            if control.is_visible():
                                rect = control.bounding_box()
                                assert rect and rect['height'] >= 43.9, f'Small target: {selector} {rect}'

                def case(name, action, *, width=1440, theme='light', js=True, reduced='no-preference', init=None, screenshot=False, axe=False, allow_failed=False):
                    context = browser.new_context(viewport={'width': width, 'height': 900}, color_scheme=theme, java_script_enabled=js, reduced_motion=reduced)
                    context.set_default_timeout(10000)
                    if init:
                        context.add_init_script(init)
                    page = context.new_page()
                    errors, external, failures, bad = [], [], [], []
                    page.on('pageerror', lambda error: errors.append(str(error)))
                    page.on('request', lambda request: external.append(request.url) if urlparse(request.url).scheme in ('http', 'https') and not request.url.startswith(base) else None)
                    page.on('requestfailed', lambda request: failures.append(request.url))
                    page.on('response', lambda response: bad.append((response.url, response.status)) if response.status >= 400 else None)
                    row = {'name': name, 'passed': False}
                    try:
                        action(page, context)
                        check_layout(page)
                        assert not errors, f'JavaScript errors: {errors}'
                        assert not external, f'Off-origin requests: {external}'
                        assert not bad, f'HTTP errors: {bad}'
                        if not allow_failed:
                            assert not failures, f'Failed requests: {failures}'
                        if axe and args.axe:
                            page.add_script_tag(path=str(args.axe))
                            audit = page.evaluate("""async () => {
                              const r=await axe.run(document, {runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa','wcag22aa']}});
                              return {violations:r.violations.map(v=>({id:v.id,impact:v.impact,nodes:v.nodes.map(n=>({target:n.target,summary:n.failureSummary}))})), incomplete:r.incomplete.map(v=>({id:v.id,targets:v.nodes.map(n=>n.target)})), passes:r.passes.length};
                            }""")
                            axe_runs.append({'case': name, **audit})
                            assert not audit['violations'], f'axe violations: {audit["violations"]}'
                        if screenshot:
                            page.screenshot(path=str(args.output / f'{name}.png'), full_page=True)
                        row['passed'] = True
                    except Exception as exc:
                        row['error'] = str(exc)
                        row['traceback'] = traceback.format_exc()
                        try:
                            page.screenshot(path=str(args.output / f'FAIL-{name}.png'), full_page=True)
                        except Exception:
                            pass
                    finally:
                        results.append(row)
                        print(('PASS ' if row['passed'] else 'FAIL ') + name, flush=True)
                        context.close()

                def visit(page, name='index'):
                    response = page.goto(base + name + '.html', wait_until='networkidle')
                    assert response and response.status == 200
                    # Firefox cannot settle this page's fonts.ready promise when
                    # JavaScript is disabled. Poll its synchronous state from the
                    # host so these cases still check readiness within a deadline.
                    deadline = time.monotonic() + 10
                    while page.evaluate('document.fonts.status') != 'loaded':
                        assert time.monotonic() < deadline, 'Fonts did not finish loading within 10 seconds'
                        time.sleep(0.05)

                for name in PAGES:
                    for width in WIDTHS:
                        for theme in ('light', 'dark'):
                            def layout(page, _context, name=name, theme=theme):
                                visit(page, name)
                                assert page.locator('html').get_attribute('data-theme') == theme
                            label = f'{name}-{width}-{theme}'
                            case(label, layout, width=width, theme=theme,
                                 screenshot=width in (390, 1440), axe=width in (390, 1440))
                    for width in (390, 1440):
                        case(f'{name}-{width}-no-js', lambda p, c, n=name: visit(p, n), width=width, js=False, screenshot=True)
                        def large(page, _context, name=name):
                            visit(page, name)
                            page.add_style_tag(content='html {font-size:200% !important;}')
                        case(f'{name}-{width}-200pct-text', large, width=width, screenshot=True)

                def demo(page, _context):
                    visit(page)
                    expect(page.locator('#demo')).to_have_class(re.compile(r'\bresolved\b'))
                    page.locator('#demo-before').click()
                    expect(page.locator('#demo')).not_to_have_class(re.compile(r'\bresolved\b'))
                    expect(page.locator('#demo-before')).to_have_attribute('aria-pressed', 'true')
                    page.locator('#demo-after').click()
                    expect(page.locator('#machine-count')).to_have_text('04', timeout=8000)
                    expect(page.locator('#demo-announcement')).to_contain_text('not a live fetch')
                    # Race prevention: rapid switches must end on the last selection.
                    page.evaluate("""() => {for(let i=0;i<10;i++){document.querySelector('#demo-before').click();document.querySelector('#demo-after').click();} document.querySelector('#demo-before').click();}""")
                    page.wait_for_timeout(1000)
                    expect(page.locator('#demo')).not_to_have_class(re.compile(r'\bresolved\b'))
                    expect(page.locator('#machine-count')).to_have_text('00')
                for reduced in ('reduce', 'no-preference'):
                    case('demo-' + reduced, demo, width=390, reduced=reduced, screenshot=True)

                def preferences(page, context):
                    visit(page)
                    expect(page.locator('html')).to_have_attribute('data-theme', 'light')
                    page.emulate_media(color_scheme='dark')
                    expect(page.locator('html')).to_have_attribute('data-theme', 'dark')
                    page.locator('#theme-toggle').click()
                    expect(page.locator('html')).to_have_attribute('data-theme', 'light')
                    page.reload()
                    expect(page.locator('html')).to_have_attribute('data-theme', 'light')
                    page.locator('#motion-toggle').click()
                    expect(page.locator('html')).to_have_attribute('data-motion', 'off')
                    page.reload()
                    expect(page.locator('html')).to_have_attribute('data-motion', 'off')
                    assert page.locator('meta[name="theme-color"]').get_attribute('content') == '#f4f1e9'
                    page.emulate_media(reduced_motion='reduce')
                    expect(page.locator('#motion-toggle')).to_be_disabled()
                    page.locator('#demo-before').click()
                    page.locator('#demo-after').click()
                    expect(page.locator('#demo')).to_have_class(re.compile(r'\bresolved\b'))
                case('saved-and-system-preferences', preferences)
                case('legacy-media-listener', preferences, init=LEGACY_MEDIA)

                def denied(page, _context):
                    visit(page)
                    page.locator('#theme-toggle').click()
                    expect(page.locator('html')).to_have_attribute('data-theme', 'dark')
                    page.locator('#motion-toggle').click()
                    expect(page.locator('html')).to_have_attribute('data-motion', 'off')
                    page.locator('#demo-before').click()
                    page.locator('#demo-after').click()
                    expect(page.locator('#demo')).to_have_class(re.compile(r'\bresolved\b'))
                case('storage-denied', denied, init=STORAGE_DENIED, width=390)
                case('no-match-media', denied, init='window.matchMedia = undefined;', width=390)
                case('no-intersection-observer', demo, init='window.IntersectionObserver = undefined;', width=390)

                def keyboard(page, _context):
                    visit(page)
                    page.keyboard.press('Tab')
                    expect(page.locator('.skip-link')).to_be_focused()
                    page.keyboard.press('Enter')
                    expect(page.locator('#main')).to_be_focused()
                    page.locator('#menu-toggle').focus()
                    page.keyboard.press('Enter')
                    expect(page.locator('#primary-nav')).to_be_visible()
                    page.keyboard.press('Escape')
                    expect(page.locator('#menu-toggle')).to_be_focused()
                    expect(page.locator('#primary-nav')).to_be_hidden()
                    page.locator('#demo-before').focus()
                    page.keyboard.press('Space')
                    expect(page.locator('#demo')).not_to_have_class(re.compile(r'\bresolved\b'))
                    page.locator('#demo-after').focus()
                    page.keyboard.press('Enter')
                    expect(page.locator('#machine-count')).to_have_text('04', timeout=8000)
                    page.locator('[data-preset=privacy]').focus()
                    page.keyboard.press('Space')
                    expect(page.locator('#preset-privacy')).to_be_visible()
                    expect(page.locator('#preset-everyday')).to_be_hidden()
                case('keyboard-mobile', keyboard, width=390, screenshot=True)

                def navigation(page, _context):
                    visit(page)
                    page.locator('#menu-toggle').click()
                    page.locator('#primary-nav a[href="privacy.html"]').click()
                    expect(page).to_have_url(base + 'privacy.html')
                    expect(page.locator('#primary-nav a[aria-current]')).to_have_attribute('href', 'privacy.html')
                    page.go_back()
                    expect(page.locator('h1')).to_contain_text('KeeFetch')
                    page.locator('[data-preset=privacy]').click()
                    expect(page.locator('#preset-privacy')).to_contain_text('No favicon resolvers')
                case('subpath-navigation-and-history', navigation, width=390)

                for theme in ('light', 'dark'):
                    def forced(page, _context):
                        visit(page)
                        page.emulate_media(forced_colors='active')
                        page.locator('#demo-before').click()
                        page.locator('#demo-after').click()
                        expect(page.locator('#machine-count')).to_have_text('04', timeout=8000)
                    case('forced-colors-' + theme, forced, width=390, theme=theme, screenshot=True)

                def offline(page, context):
                    visit(page, 'profiles')
                    context.set_offline(True)
                    page.locator('#theme-toggle').click()
                    expect(page.locator('table').first).to_be_visible()
                    assert page.locator('[data-profile-id]').count() == 4
                case('offline-after-load', offline, width=390)

                def media(page, _context):
                    visit(page)
                    assert page.locator('#screenshots img').count() == 4
                    for image in page.locator('#screenshots img').all():
                        assert image.get_attribute('alt')
                        assert image.evaluate('e => e.closest("a").getAttribute("href") === e.getAttribute("src")')
                    page.locator('.verification summary').click()
                    expect(page.locator('#checksum')).to_have_text('1fd7e12590bfc2d23ede93c1ae5e61de0662187321caccdfeeb0bbcb995f5275')
                case('genuine-media-and-stable-checksum', media, width=390, screenshot=True)

                def print_page(page, _context):
                    visit(page, 'getting-started')
                    page.emulate_media(media='print')
                    expect(page.locator('#primary-nav')).to_be_hidden()
                    assert page.evaluate("getComputedStyle(document.documentElement).getPropertyValue('--paper').trim()") == '#fff'
                case('print-install-guide', print_page, screenshot=True)

                for width in (320, 1440):
                    def expanded(page, _context):
                        visit(page)
                        page.locator('.entry-name strong').first.evaluate("e => e.textContent = 'LangerKontoname' .repeat(7) + ' 日本語 العربية'")
                        page.locator('.entry-name').first.evaluate("e => e.textContent = 'subdomain.'.repeat(15) + 'example.test'")
                    case(f'long-content-{width}', expanded, width=width, screenshot=True)
                browser.close()
        finally:
            server.shutdown()
            server.server_close()
            thread.join(timeout=5)
            report['passed'] = sum(row['passed'] for row in results)
            report['failed'] = sum(not row['passed'] for row in results)
            report['axe_executed'] = len(axe_runs)
            (args.output / 'results.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(f"{args.browser}: {report['passed']} passed, {report['failed']} failed; {len(axe_runs)} axe scans")
    return 1 if report['failed'] else 0


if __name__ == '__main__':
    raise SystemExit(main())
