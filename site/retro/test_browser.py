"""Browser regression checks for the isolated KeeFetch retro concept.

CI uses an HTTP server under /KeeFetch/retro/. --inline uses the packaged HTML
when local network navigation is unavailable; it does not prove HTTP routing.
Requires playwright==1.57.0. axe-core is optional locally and required by CI.
"""
from __future__ import annotations
import argparse
from functools import partial
import hashlib
from html.parser import HTMLParser
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from importlib.metadata import version
import json
from pathlib import Path
import shutil
import tempfile
from threading import Thread
import traceback
from urllib.parse import urlsplit
from playwright.sync_api import sync_playwright, expect
from package_preview import package

HERE = Path(__file__).resolve().parent
WIDTHS = (320, 360, 390, 520, 640, 768, 800, 801, 1024, 1440, 1920)
DENIED = "Object.defineProperty(window,'localStorage',{get(){throw new DOMException('Disabled by test','SecurityError')}});"
NO_MEDIA = 'window.matchMedia = undefined;'
LEGACY = """const media=window.matchMedia.bind(window);window.matchMedia=q=>{
const m=media(q);return {get matches(){return m.matches},addListener:f=>m.addEventListener('change',f)}};"""
KEY = 'keefetch-retro-theme'

class Markup(HTMLParser):
    def __init__(self):
        super().__init__(); self.tags = []; self.ids = []; self.links = []; self.refs = []
    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs); self.tags.append(tag)
        if 'id' in attrs: self.ids.append(attrs['id'])
        if tag == 'a': self.links.append(attrs.get('href', ''))
        for key in ('aria-controls', 'aria-labelledby'):
            self.refs.extend(attrs.get(key, '').split())

class Quiet(SimpleHTTPRequestHandler):
    def log_message(self, *_): pass

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--browser', choices=('chromium','firefox','webkit'), default='chromium')
    parser.add_argument('--executable')
    parser.add_argument('--inline', action='store_true')
    parser.add_argument('--axe', type=Path)
    parser.add_argument('--output', type=Path, default=HERE / 'qa-output')
    args = parser.parse_args()
    if args.axe and not args.axe.is_file(): parser.error('--axe must point to axe.min.js')
    args.output.mkdir(parents=True, exist_ok=True)
    cases = []; audits = []
    report = {'browser': args.browser, 'playwright': version('playwright'), 'mode': 'inline' if args.inline else 'http-subpath', 'cases': cases, 'axe': audits,
              'source_sha256': {name: hashlib.sha256((HERE/name).read_bytes()).hexdigest() for name in ('index.html','style.css','app.js')}}
    html = package(HERE)
    def record(name, action, page=None):
        row = {'name':name,'passed':False}
        try: action(); row['passed'] = True
        except Exception as exc:
            row['error'] = str(exc); row['traceback'] = traceback.format_exc()
            if page:
                try: page.screenshot(path=str(args.output / ('FAIL-' + name + '.png')),full_page=True)
                except Exception: pass
        cases.append(row)
        print(('PASS ' if row['passed'] else 'FAIL ') + name, flush=True)
        if not row['passed']: print(row['error'],flush=True)
    def static():
        m=Markup(); m.feed((HERE/'index.html').read_text())
        assert m.tags.count('main') == m.tags.count('h1') == 1
        assert len(m.ids) == len(set(m.ids)), 'Duplicate ids'
        assert all(ref in m.ids for ref in m.refs), 'Broken ARIA references'
        for link in m.links:
            assert link and link != '#', 'Empty/placeholder link'
            if link.startswith('#'): assert link[1:] in m.ids, link
            else: assert link.startswith('https://github.com/tzii/KeeFetch'), link
        assert m.links.count('https://github.com/tzii/KeeFetch/releases/download/v1.2.0/KeeFetch.plgx') == 2
        assert (HERE.parent/'assets/icons/keefetch-app.png').is_file()
        assert '@import' not in (HERE/'style.css').read_text()
        assert 'fetch(' not in (HERE/'app.js').read_text()
        assert 'style.css' not in html and 'src="app.js"' not in html
    record('source-links-semantics-and-packaging', static)
    with tempfile.TemporaryDirectory(prefix='keefetch-retro-') as temp:
        shutil.copytree(HERE.parent, Path(temp)/'KeeFetch', ignore=shutil.ignore_patterns('qa-output','__pycache__'))
        server=ThreadingHTTPServer(('127.0.0.1',0),partial(Quiet,directory=temp))
        thread=Thread(target=server.serve_forever,daemon=True); thread.start()
        base=f'http://127.0.0.1:{server.server_port}/KeeFetch/retro/'
        try:
            with sync_playwright() as pw:
                opts = {'executable_path':args.executable} if args.executable else {}
                browser=getattr(pw,args.browser).launch(**opts); report['browser_version']=browser.version
                def visit(page):
                    if args.inline: page.set_content(html,wait_until='load')
                    else:
                        response=page.goto(base,wait_until='networkidle')
                        assert response and response.status == 200
                def layout(page):
                    result=page.evaluate('''() => ({width:innerWidth,scroll:document.documentElement.scrollWidth,
                    bad:[...document.querySelectorAll('body *')].filter(e=>{const r=e.getBoundingClientRect();return r.width&&r.right>innerWidth+1}).slice(0,6).map(e=>e.tagName+'.'+e.className)})''')
                    assert result['scroll'] <= result['width']+1, result
                    expect(page.locator('h1')).to_be_visible()
                    assert page.locator('main').count()==1
                    assert page.evaluate('Array.from(document.images).every(i=>i.complete&&i.naturalWidth>0)')
                    for control in page.locator('button,.primary,#main-nav a').all():
                        if control.is_visible():
                            assert control.bounding_box()['height'] >= 43.9, control.get_attribute('id')
                def audit(page,name):
                    if not args.axe: return
                    page.add_script_tag(path=str(args.axe))
                    result=page.evaluate("""async()=>{const r=await axe.run(document,{runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa','wcag22aa']}});return {violations:r.violations.map(v=>({id:v.id,impact:v.impact,nodes:v.nodes.map(n=>({target:n.target,summary:n.failureSummary}))})),incomplete:r.incomplete.map(v=>({id:v.id,targets:v.nodes.map(n=>n.target)})),passes:r.passes.length}}""")
                    audits.append({'case':name,**result}); assert not result['violations'], result['violations']
                for js in (True,False):
                    for theme in ('light','dark'):
                        context=browser.new_context(viewport={'width':1440,'height':900},java_script_enabled=js,color_scheme=theme)
                        context.set_default_timeout(6000); page=context.new_page(); errors=[]; requests=[]
                        page.on('pageerror',lambda error:errors.append(str(error)))
                        page.on('request',lambda req:requests.append(req.url))
                        visit(page)
                        for width in WIDTHS:
                            page.set_viewport_size({'width':width,'height':900})
                            for zoom in (100,200):
                                page.evaluate('(z)=>document.documentElement.style.fontSize=z+"%"',zoom)
                                record(f'layout-{width}-{zoom}pct-{theme}-js{js}',lambda:layout(page),page)
                            page.evaluate('document.documentElement.style.fontSize="100%"')
                            if width in (390,1440):
                                name=f'{width}-{theme}-js{js}'
                                page.screenshot(path=str(args.output/(name+'.png')),full_page=True)
                                if js and args.axe: record('axe-'+name,lambda:audit(page,name),page)
                        def assets():
                            assert not errors,errors
                            if not args.inline:
                                assert all(url.startswith(f'http://127.0.0.1:{server.server_port}/KeeFetch/') for url in requests if url.startswith('http')),requests
                        record(f'assets-errors-{theme}-js{js}',assets,page)
                        context.close()
                def case(name,action,*,width=390,theme='light',init=None,js=True,reduced='no-preference'):
                    context=browser.new_context(viewport={'width':width,'height':900},color_scheme=theme,java_script_enabled=js,reduced_motion=reduced)
                    context.set_default_timeout(6000)
                    if init: context.add_init_script(init)
                    page=context.new_page(); errors=[]; page.on('pageerror',lambda e:errors.append(str(e)))
                    visit(page)
                    def run():
                        action(page,context);layout(page);assert not errors,errors
                    record(name,run,page);context.close()
                def demo(page,context):
                    page.locator('#demo-before').click()
                    expect(page.locator('#demo')).not_to_have_class('workshop resolved')
                    expect(page.locator('#demo-before')).to_have_attribute('aria-pressed','true')
                    page.locator('#demo-after').click();expect(page.locator('#demo-state')).to_have_text('4 familiar faces')
                    page.evaluate("for(let i=0;i<20;i++){document.querySelector('#demo-before').click();document.querySelector('#demo-after').click()}document.querySelector('#demo-before').click()")
                    expect(page.locator('#demo-state')).to_have_text('4 generic icons')
                    expect(page.locator('#demo')).not_to_have_class('workshop resolved')
                case('demo-rapid-switching',demo)
                case('demo-reduced-motion',demo,reduced='reduce')
                def prefs(page,context):
                    page.locator('#theme-toggle').click();expect(page.locator('html')).to_have_attribute('data-theme','dark')
                    page.locator('#theme-reset').click();expect(page.locator('html')).to_have_attribute('data-theme','light')
                    expect(page.locator('#theme-toggle')).to_be_focused()
                    page.emulate_media(color_scheme='dark');expect(page.locator('html')).to_have_attribute('data-theme','dark')
                    page.locator('#theme-toggle').click();page.emulate_media(color_scheme='light');page.emulate_media(color_scheme='dark')
                    expect(page.locator('html')).to_have_attribute('data-theme','light')
                case('system-appearance-and-user-override',prefs)
                case('legacy-media-listener',prefs,init=LEGACY)
                def toggles(page,context):
                    page.locator('#theme-toggle').click();expect(page.locator('html')).to_have_attribute('data-theme','dark');demo(page,context)
                case('denied-storage',toggles,init=DENIED)
                case('missing-match-media',toggles,init=NO_MEDIA)
                def menu(page,context):
                    page.keyboard.press('Tab');expect(page.locator('.skip-link')).to_be_focused()
                    page.locator('#menu-toggle').focus();page.keyboard.press('Enter');expect(page.locator('#main-nav')).to_be_visible()
                    page.keyboard.press('Escape');expect(page.locator('#menu-toggle')).to_be_focused();expect(page.locator('#main-nav')).to_be_hidden()
                    page.locator('#menu-toggle').click();page.locator('#main-nav a[href="#features"]').click()
                    expect(page.locator('#features')).to_be_focused();expect(page.locator('#main-nav')).to_be_hidden()
                case('keyboard-menu-escape-and-anchor-focus',menu)
                def presets(page,context):
                    for name in ('fast','thorough','custom','balanced'):
                        page.locator(f'[data-preset="{name}"]').focus();page.keyboard.press('Space')
                        expect(page.locator('#preset-'+name)).to_be_visible()
                        assert page.locator('.preset-panel:visible').count()==1
                        expect(page.locator(f'[data-preset="{name}"]')).to_have_attribute('aria-pressed','true')
                case('keyboard-preset-guide',presets)
                def no_js(page,context):
                    expect(page.locator('#main-nav')).to_be_visible()
                    expect(page.locator('#theme-toggle')).to_be_hidden()
                    expect(page.locator('#demo-controls')).to_be_hidden()
                    assert page.locator('.preset-panel:visible').count()==4
                    page.locator('.verification>summary').click();expect(page.locator('#checksum')).to_be_visible()
                case('no-js-navigation-guides-and-disclosures',no_js,js=False)
                def copy_fallback(page,context):
                    page.evaluate("Object.defineProperty(navigator,'clipboard',{configurable:true,value:{writeText:()=>Promise.reject(new Error('denied'))}})")
                    page.locator('.verification>summary').click();page.locator('#copy-checksum').click()
                    expect(page.locator('#copy-status')).to_contain_text('checksum is selected')
                    assert page.evaluate('window.getSelection().toString()')==page.locator('#checksum').inner_text()
                case('clipboard-denial-selection-fallback',copy_fallback)
                def copy_success(page,context):
                    page.evaluate("Object.defineProperty(navigator,'clipboard',{configurable:true,value:{writeText:t=>{window.__copied=t;return Promise.resolve()}}})")
                    page.locator('.verification>summary').click();page.locator('#copy-checksum').click()
                    expect(page.locator('#copy-status')).to_have_text('SHA-256 copied.')
                    assert page.evaluate('window.__copied')==page.locator('#checksum').inner_text()
                case('clipboard-success-handler-test-double',copy_success)
                def long(page,context):
                    page.locator('.entry-name strong').first.evaluate("e=>e.textContent='LongAccountName'.repeat(12)+' 日本語 العربية'")
                    page.locator('.entry-name span').first.evaluate("e=>e.textContent='subdomain.'.repeat(30)+'example.test'")
                    page.locator('.verification>summary').click()
                case('long-unbroken-and-multilingual-labels',long,width=320)
                def forced(page,context):
                    page.emulate_media(forced_colors='active');demo(page,context)
                    report['forced_colors_active']=page.evaluate("matchMedia('(forced-colors: active)').matches")
                    page.screenshot(path=str(args.output/'forced-colors.png'),full_page=True)
                case('forced-colors-rendering',forced)
                def print_case(page,context):
                    page.emulate_media(media='print');expect(page.locator('.site-header')).to_be_hidden()
                    expect(page.locator('#install')).to_be_visible();page.screenshot(path=str(args.output/'print.png'),full_page=True)
                case('print-installation-readable',print_case,width=1440)
                def stored(page,context):
                    if args.inline:
                        page.evaluate("dispatchEvent(new StorageEvent('storage',{key:'keefetch-retro-theme',newValue:'dark'}))")
                        expect(page.locator('html')).to_have_attribute('data-theme','dark')
                        report['storage_validation']='Storage-event handler only; opaque inline document has no persistent origin.'
                    else:
                        page.locator('#theme-toggle').click();page.reload();expect(page.locator('html')).to_have_attribute('data-theme','dark')
                        other=context.new_page();visit(other);other.locator('#theme-toggle').click()
                        expect(page.locator('html')).to_have_attribute('data-theme','light')
                        other.evaluate('localStorage.clear()');expect(page.locator('html')).to_have_attribute('data-theme','light');other.close()
                        report['storage_validation']='Real reload persistence and cross-tab storage on HTTP.'
                case('appearance-storage-events',stored)
                browser.close()
        finally:
            server.shutdown();server.server_close();thread.join(timeout=5)
    report['passed']=sum(row['passed'] for row in cases);report['failed']=sum(not row['passed'] for row in cases)
    report['axe_executed']=len(audits)
    (args.output/'results.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(f"{report['passed']} passed, {report['failed']} failed, {len(audits)} axe scans",flush=True)
    return int(report['failed']>0)

if __name__=='__main__': raise SystemExit(main())
