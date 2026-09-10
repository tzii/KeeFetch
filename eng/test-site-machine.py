"""Test the real machine controller through the built GitHub Pages subpath.

python test_machine.py --executable /usr/bin/chromium --output qa-machine
The --browser switch also supports Playwright Firefox/WebKit when installed.
This suite serves site/dist under /KeeFetch/ over local HTTP. Visibility, storage
and print-event simulations are explicitly named rather than sold as device QA.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import time
import traceback
from pathlib import Path
from importlib.metadata import version
from playwright.sync_api import sync_playwright, expect
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from threading import Thread
import tempfile, shutil
from urllib.parse import urlparse

HERE = Path(__file__).resolve().parents[1] / 'site'
STATE = 'window.KeeFetchMachine.getState()'
ACTIVE = "document.getAnimations().filter(a=>a.playState==='running'||a.playState==='pending').length"
DENIED = "Object.defineProperty(window,'localStorage',{get(){throw new DOMException('Disabled by test','SecurityError')}})"
LEGACY = """const original=matchMedia;window.matchMedia=q=>{const m=original(q);return {get matches(){return m.matches;},addListener:f=>m.addEventListener('change',f)}};"""
RAF_COUNTER = """(() => {
 const request=window.requestAnimationFrame.bind(window), cancel=window.cancelAnimationFrame.bind(window);
 const ids=new Set(); window.__frames={pending:0,maxPending:0,calls:0};
 window.requestAnimationFrame=callback=>{
  const id=request(t=>{ids.delete(id);__frames.pending=ids.size;__frames.calls++;callback(t)});
  ids.add(id);__frames.pending=ids.size;__frames.maxPending=Math.max(__frames.maxPending,ids.size);return id;
 };
 window.cancelAnimationFrame=id=>{ids.delete(id);__frames.pending=ids.size;cancel(id)};
})();"""


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--browser', choices=('chromium','firefox','webkit'), default='chromium')
    parser.add_argument('--executable')
    parser.add_argument('--output', type=Path, default=HERE/'qa-machine')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    cases = []
    report = {'browser': args.browser, 'playwright': version('playwright'), 'mode': 'http-github-pages-subpath', 'cases': cases,
              'source_sha256': {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in HERE.glob('*') if p.is_file()},
              'limitations': ['Local HTTP under /KeeFetch/; not installed-phone, real screen-reader or GPU-performance testing.',
                'Visibility/storage/print signals are simulated in the explicitly named handler tests.',
                'The separate page-regression suite supplies viewport, enlarged-text and page-function checks.']}
    # Serve the actual built production files, including separate scripts and media.
    temporary = tempfile.TemporaryDirectory(prefix='keefetch-machine-')
    shutil.copytree(HERE/'dist', Path(temporary.name)/'KeeFetch')
    class QuietHandler(SimpleHTTPRequestHandler):
        def log_message(self, *_args): pass
    server = ThreadingHTTPServer(('127.0.0.1',0), partial(QuietHandler,directory=temporary.name))
    thread = Thread(target=server.serve_forever,daemon=True); thread.start()
    base = f'http://127.0.0.1:{server.server_port}/KeeFetch/'

    def checkpoint():
        report['passed'] = sum(x['passed'] for x in cases)
        report['failed'] = len(cases) - report['passed']
        (args.output/'results.json').write_text(json.dumps(report, indent=2))

    with sync_playwright() as pw:
        opts = {'executable_path': args.executable} if args.executable else {}
        browser = getattr(pw, args.browser).launch(**opts)
        report['browser_version'] = browser.version
        def state(page): return page.evaluate(STATE)
        def idle(page):
            expect(page.locator('#demo')).to_have_attribute('data-sequence','idle')
            assert state(page)['framePending'] is False
        def frozen(page):
            first = state(page)['renderedFrames']
            page.wait_for_timeout(180)
            assert state(page)['renderedFrames'] == first, 'Machine kept rendering while idle/paused'
        def start(page):
            page.locator('#replay-motion').click()
            expect(page.locator('#demo')).to_have_attribute('data-sequence','playing')
        def finished(page):
            expect(page.locator('#demo')).to_have_attribute('data-sequence','idle',timeout=8000)
            expect(page.locator('#machine-count')).to_have_text('04')
            expect(page.locator('#demo-state')).to_have_text('4 familiar faces')
            assert state(page)['framePending'] is False
        def case(name, action, *, width=1440, reduced='no-preference', init=None, js=True, touch=False, theme='light'):
            context = browser.new_context(viewport={'width':width,'height':1050}, color_scheme=theme,
                      reduced_motion=reduced, java_script_enabled=js, has_touch=touch,
                      **({'is_mobile':True} if touch and args.browser!='firefox' else {}))
            context.set_default_timeout(8000)
            if init: context.add_init_script(init)
            page = context.new_page(); errors=[]; network=[]
            page.on('pageerror',lambda e:errors.append(str(e)))
            page.on('request',lambda r:network.append(r.url) if urlparse(r.url).scheme in ('http','https') and not r.url.startswith(base) else None)
            row={'name':name,'passed':False}; started=time.monotonic()
            try:
                page.goto(base,wait_until='load')
                action(page,context)
                assert not errors, errors
                assert not network, network
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth+1'), 'Page overflow'
                row['passed']=True
            except Exception as e:
                row['error']=type(e).__name__+': '+str(e)
                row['traceback']=traceback.format_exc()
                try: page.screenshot(path=str(args.output/('FAIL-'+name+'.png')),full_page=True)
                except Exception: pass
            finally:
                row['seconds']=round(time.monotonic()-started,3); cases.append(row); checkpoint(); context.close()
                print(('PASS ' if row['passed'] else 'FAIL ')+name,flush=True)
                if not row['passed']: print(row['error'],flush=True)

        def autoplay(page,ctx):
            expect(page.locator('#demo')).to_have_attribute('data-sequence','playing')
            assert state(page)['framePending']
            finished(page); frozen(page)
            page.wait_for_timeout(700)
            assert page.evaluate(ACTIVE)==0
            assert page.evaluate('__frames.pending')==0
            assert page.evaluate('__frames.maxPending')==1
            page.locator('#install').scroll_into_view_if_needed()
            page.locator('#demo').scroll_into_view_if_needed()
            idle(page); frozen(page)
        case('one-finite-autoplay-single-frame-loop-and-zero-idle-work',autoplay,init=RAF_COUNTER)

        def frozen_pose(page,ctx):
            start(page); page.wait_for_timeout(1100)
            page.locator('#pause-machine').click()
            expect(page.locator('#demo')).to_have_attribute('data-sequence','paused')
            expect(page.locator('#pause-machine')).to_have_accessible_name('Resume machine animation')
            first=state(page)
            pose=page.locator('.machine-art').inner_html()
            frozen(page)
            assert page.locator('.machine-art').inner_html()==pose
            assert not state(page)['framePending']
            page.locator('#pause-machine').click()
            expect(page.locator('#demo')).to_have_attribute('data-sequence','playing')
            page.wait_for_timeout(200)
            assert state(page)['elapsed'] > first['elapsed']
            finished(page)
        case('pause-freezes-exact-pose-and-resume-continues-from-it',frozen_pose)

        def meaningful_motion(page,ctx):
            start(page)
            # Observe a real moving pose. A fixed 950 ms sleep can land between
            # scan passes, when the arm correctly returns to its resting angle.
            page.wait_for_function("""() => {
                const pose = s => document.querySelector(s).getAttribute('transform');
                if (pose('.scanner-upper') === 'rotate(0)' ||
                    pose('.belt-wheel') === 'rotate(0)' ||
                    pose('.feed-tile') === 'translate(0 0)' ||
                    ['rotate(-55)','rotate(55)'].includes(pose('.gauge-needle'))) return false;
                KeeFetchMachine.pause(); return true;
            }""")
            assert page.locator('.scanner-upper').get_attribute('transform')!='rotate(0)'
            assert page.locator('.belt-wheel').first.get_attribute('transform')!='rotate(0)'
            assert page.locator('.feed-tile').first.get_attribute('transform')!='translate(0 0)'
            assert page.locator('.gauge-needle').get_attribute('transform') not in ('rotate(-55)','rotate(55)')
        case('rollers-feed-arm-and-gauge-really-change-svg-poses',meaningful_motion)

        def delivery(page,ctx):
            start(page); page.wait_for_timeout(3500); page.locator('#pause-machine').click()
            assert 1<=state(page)['delivered']<=4
            assert page.locator('.output-tile').first.get_attribute('opacity')=='1'
            assert page.locator('.tile-check').first.get_attribute('opacity')=='1'
            assert float(page.locator('.found-icon').first.evaluate('e=>getComputedStyle(e).opacity'))>.9
            page.locator('#pause-machine').click();finished(page)
            assert all(page.locator(f'.output-tile[data-item="{i}"]').get_attribute('data-brand')==brand
                       for i,brand in enumerate(('github','figma','notion','spotify')))
            assert page.locator('.found-icon[style]').evaluate_all('els=>els.every(e=>!e.style.opacity&&!e.style.transform)')
        case('each-branded-output-matches-its-entry-and-counter-finishes',delivery)

        def before(page,ctx):
            start(page);page.wait_for_timeout(400);page.locator('#demo-before').click()
            idle(page);frozen(page)
            expect(page.locator('#demo-before')).to_have_attribute('aria-pressed','true')
            expect(page.locator('#machine-count')).to_have_text('00')
            expect(page.locator('#demo-state')).to_have_text('4 generic icons')
            assert page.locator('.found-icon').evaluate_all('els=>els.every(e=>getComputedStyle(e).opacity==="0")')
        case('before-interrupts-and-cleans-all-visual-overrides',before)

        def paused_before(page,ctx):
            start(page);page.locator('#pause-machine').click();page.locator('#demo-before').click()
            idle(page);frozen(page);expect(page.locator('#machine-count')).to_have_text('00')
        case('before-also-cleans-up-a-paused-batch',paused_before)

        def replay_paused(page,ctx):
            start(page);page.wait_for_timeout(650);page.locator('#pause-machine').click()
            old=state(page)['elapsed'];start(page)
            assert state(page)['elapsed']<old
            assert page.evaluate('__frames.pending')==1
            page.locator('#demo-before').click();idle(page)
        case('replay-from-paused-restarts-one-clean-clock',replay_paused,init=RAF_COUNTER)

        def stress(page,ctx):
            page.evaluate("for(let i=0;i<150;i++){document.querySelector('#replay-motion').click();KeeFetchMachine.pause();KeeFetchMachine.resume()}")
            assert page.evaluate('__frames.maxPending')==1
            assert page.evaluate('__frames.pending')==1
            page.locator('#demo-before').click();idle(page);frozen(page)
            assert page.evaluate('__frames.pending')==0
        case('150-replays-and-pause-resumes-never-accumulate-frame-loops',stress,init=RAF_COUNTER)

        def reversals(page,ctx):
            page.evaluate("for(let i=0;i<60;i++){document.querySelector('#demo-after').click();document.querySelector('#demo-before').click()}")
            idle(page);frozen(page)
            assert page.evaluate('__frames.pending')==0
            expect(page.locator('#demo-before')).to_have_attribute('aria-pressed','true')
        case('60-rapid-comparison-reversals-last-selection-wins',reversals,init=RAF_COUNTER)

        def off(page,ctx):
            start(page);page.locator('#motion-toggle').click()
            expect(page.locator('html')).to_have_attribute('data-motion','off')
            idle(page);frozen(page)
            expect(page.locator('#replay-motion')).to_be_disabled()
            expect(page.locator('#pause-machine')).to_be_disabled()
            expect(page.locator('#machine-count')).to_have_text('04')
            page.locator('#demo-before').click();expect(page.locator('#machine-count')).to_have_text('00')
            page.locator('#demo-after').click();expect(page.locator('#machine-count')).to_have_text('04')
            assert page.evaluate(ACTIVE)==0
        case('motion-off-settles-result-and-comparison-still-works',off)
        case('denied-storage-does-not-break-run-or-stop',off,init=DENIED)
        case('missing-match-media-keeps-manual-controls-working',off,init='window.matchMedia=undefined')

        def on_again(page,ctx):
            page.locator('#motion-toggle').click();page.locator('#motion-toggle').click()
            expect(page.locator('html')).to_have_attribute('data-motion','on')
            idle(page);frozen(page);start(page)
        case('enabling-motion-does-not-autoplay-again',on_again)

        def reduced(page,ctx):
            expect(page.locator('html')).to_have_attribute('data-motion','off')
            expect(page.locator('#motion-toggle')).to_be_disabled()
            expect(page.locator('#motion-toggle')).to_contain_text('Reduced motion')
            expect(page.locator('#replay-motion')).to_be_disabled()
            idle(page);frozen(page)
            page.locator('#demo-before').click();page.locator('#demo-after').click()
            expect(page.locator('#machine-count')).to_have_text('04')
            assert page.evaluate(ACTIVE)==0
        case('system-reduced-motion-static-at-load',reduced,reduced='reduce')
        def live_reduced(page,ctx):
            start(page);page.emulate_media(reduced_motion='reduce');reduced(page,ctx)
            page.emulate_media(reduced_motion='no-preference')
            expect(page.locator('html')).to_have_attribute('data-motion','on')
            idle(page);frozen(page)
        case('live-reduced-motion-cancels-the-clock-and-settles',live_reduced)
        case('legacy-media-change-listener-cancels-the-clock',live_reduced,init=LEGACY)
        def paused_reduce(page,ctx):
            start(page);page.locator('#pause-machine').click();page.emulate_media(reduced_motion='reduce');reduced(page,ctx)
        case('reduced-motion-also-cleans-up-paused-state',paused_reduce)

        def no_api(page,ctx):
            expect(page.locator('html')).to_have_attribute('data-motion','off')
            expect(page.locator('#replay-motion')).to_be_disabled()
            page.locator('#demo-before').click();page.locator('#demo-after').click()
            idle(page);expect(page.locator('#machine-count')).to_have_text('04')
        case('missing-web-animation-api-leaves-the-page-usable',no_api,init='Element.prototype.animate=undefined')
        case('missing-request-animation-frame-leaves-the-page-usable',no_api,init='window.requestAnimationFrame=undefined')
        def rejected_details(page,ctx):
            start(page);page.locator('#pause-machine').click();frozen(page)
        case('optional-waapi-effects-throwing-do-not-break-the-machine',rejected_details,
             init="Element.prototype.animate=function(){throw new Error('Deliberate optional-effect failure')}")
        def no_observer(page,ctx):
            idle(page);frozen(page);start(page)
        case('missing-intersection-observer-manual-replay-still-works',no_observer,init='window.IntersectionObserver=undefined')

        def no_js(page,ctx):
            expect(page.locator('#motion-controls')).to_be_hidden()
            expect(page.locator('.machine-art')).to_be_visible()
            expect(page.locator('#primary-nav')).to_be_visible()
            assert page.locator('.preset-panel:visible').count()==4
            assert page.locator('.output-tile').count()==4
            assert page.evaluate(ACTIVE)==0
        case('no-javascript-full-static-scene-no-dead-controls',no_js,js=False,width=390)

        def offscreen(page,ctx):
            start(page);page.locator('#install').scroll_into_view_if_needed()
            idle(page);frozen(page)
            assert page.evaluate('__frames.pending')==0
            page.locator('#demo').scroll_into_view_if_needed();idle(page);frozen(page)
        case('leaving-viewport-stops-and-return-does-not-autoplay',offscreen,init=RAF_COUNTER)
        def play_offscreen(page,ctx):
            page.locator('#install').scroll_into_view_if_needed()
            page.evaluate('KeeFetchMachine.play()');idle(page);frozen(page)
            assert page.evaluate('__frames.pending')==0
        case('api-play-offscreen-applies-result-without-starting-a-hidden-loop',play_offscreen,init=RAF_COUNTER)
        def paused_offscreen(page,ctx):
            start(page);page.locator('#pause-machine').click();page.locator('#install').scroll_into_view_if_needed()
            expect(page.locator('#demo')).to_have_attribute('data-sequence','paused');frozen(page)
            page.evaluate('KeeFetchMachine.resume()');idle(page);frozen(page)
        case('paused-offscreen-keeps-pose-but-api-resume-does-not-run-hidden',paused_offscreen)

        def keyboard(page,ctx):
            page.locator('#replay-motion').focus();page.keyboard.press('Enter')
            expect(page.locator('#demo')).to_have_attribute('data-sequence','playing')
            page.locator('#pause-machine').focus();page.keyboard.press('Space')
            expect(page.locator('#demo')).to_have_attribute('data-sequence','paused')
            page.keyboard.press('Space');expect(page.locator('#demo')).to_have_attribute('data-sequence','playing')
            page.keyboard.press('Escape');idle(page);expect(page.locator('#replay-motion')).to_be_focused()
        case('keyboard-run-pause-resume-escape-with-focus-recovery',keyboard,width=390)
        def completion_focus(page,ctx):
            start(page);page.locator('#pause-machine').focus();finished(page)
            expect(page.locator('#replay-motion')).to_be_focused()
        case('completion-recovers-focus-from-newly-disabled-pause-button',completion_focus)
        def touch(page,ctx):
            page.locator('#replay-motion').tap();expect(page.locator('#demo')).to_have_attribute('data-sequence','playing')
            page.locator('#pause-machine').tap();expect(page.locator('#demo')).to_have_attribute('data-sequence','paused')
            for control in page.locator('.motion-toolbar button').all():
                rect=control.bounding_box();assert rect['width']>=44 and rect['height']>=44,rect
        case('touch-controls-hit-targets-390',touch,width=390,touch=True)
        case('touch-controls-hit-targets-320',touch,width=320,touch=True)

        def quiet_status(page,ctx):
            page.evaluate("window.__announcements=0;new MutationObserver(()=>__announcements++).observe(document.querySelector('#demo-announcement'),{childList:true,subtree:true,characterData:true})")
            start(page);page.wait_for_timeout(1300)
            assert page.evaluate('__announcements')<=1
            assert page.locator('#machine-caption').get_attribute('aria-live') is None
        case('frame-and-counter-updates-do-not-spam-the-live-region',quiet_status)

        def visibility(page,ctx):
            start(page)
            page.evaluate("Object.defineProperty(document,'hidden',{get:()=>true,configurable:true});document.dispatchEvent(new Event('visibilitychange'))")
            idle(page);frozen(page);assert page.evaluate(ACTIVE)==0
        case('simulated-visibility-hidden-stops-all-effects',visibility)
        def pagehide(page,ctx):
            start(page);page.evaluate("dispatchEvent(new PageTransitionEvent('pagehide',{persisted:true}))")
            idle(page);frozen(page)
            page.evaluate("dispatchEvent(new PageTransitionEvent('pageshow',{persisted:true}))");idle(page);frozen(page)
        case('simulated-bfcache-pagehide-cleans-up-and-pageshow-stays-still',pagehide)
        def print_event(page,ctx):
            start(page);page.evaluate("dispatchEvent(new Event('beforeprint'))");idle(page);frozen(page)
            page.evaluate('KeeFetchMachine.play()');idle(page)
            page.evaluate("dispatchEvent(new Event('afterprint'))");start(page)
        case('simulated-beforeprint-blocks-restart-until-afterprint',print_event)
        def print_media(page,ctx):
            start(page);page.emulate_media(media='print');idle(page);frozen(page)
            expect(page.locator('#demo')).to_be_hidden()
            page.emulate_media(media='screen');idle(page)
        case('print-media-stops-machine-and-hides-workshop',print_media)
        def storage_event(page,ctx):
            start(page)
            page.evaluate("dispatchEvent(new StorageEvent('storage',{key:'keefetch-motion',newValue:'off'}))")
            idle(page);frozen(page)
            page.evaluate("dispatchEvent(new StorageEvent('storage',{key:'keefetch-motion',newValue:'on'}))")
            idle(page);start(page)
            page.evaluate("dispatchEvent(new StorageEvent('storage',{key:null,newValue:null}))")
            expect(page.locator('html')).to_have_attribute('data-motion','on')
        case('simulated-cross-tab-motion-preference-and-clear-events',storage_event)
        def unrelated_storage(page,ctx):
            start(page)
            page.evaluate("dispatchEvent(new StorageEvent('storage',{key:'unrelated',newValue:'off'}))")
            expect(page.locator('#demo')).to_have_attribute('data-sequence','playing')
        case('unrelated-storage-event-cannot-change-motion',unrelated_storage)

        def theme(page,ctx):
            start(page);page.locator('#pause-machine').click()
            pose=page.locator('.machine-art').inner_html()
            page.locator('#theme-toggle').click()
            expect(page.locator('html')).to_have_attribute('data-theme','dark')
            assert page.locator('.machine-art').inner_html()==pose
            expect(page.locator('#demo')).to_have_attribute('data-sequence','paused')
        case('theme-change-preserves-exact-paused-pose',theme)
        def forced(page,ctx):
            page.emulate_media(forced_colors='active');start(page);page.locator('#pause-machine').click();frozen(page)
            capabilities = page.evaluate("""() => ({
                mediaActive: matchMedia('(forced-colors: active)').matches,
                colorAdjustmentSupported: CSS.supports('forced-color-adjust','none'),
                sceneAdjustment: getComputedStyle(document.querySelector('.machine')).forcedColorAdjust || null
            })""")
            report['forced_colors_capabilities'] = capabilities
            if capabilities['colorAdjustmentSupported'] and capabilities['mediaActive']:
                assert capabilities['sceneAdjustment']=='none'
            else:
                report['limitations'].append('This engine does not implement the requested forced-color media/property combination; pause and control checks pass, but decorative forced-color retention is not established here.')
            expect(page.locator('#pause-machine')).to_have_accessible_name('Resume machine animation')
            expect(page.locator('.machine-art')).to_be_visible()
        case('forced-colors-keeps-decorative-scene-and-functional-controls',forced,width=390)
        def expanded(page,ctx):
            page.evaluate("document.documentElement.style.fontSize='200%'")
            start(page);page.locator('#pause-machine').click()
            page.locator('#machine-caption').evaluate("e=>e.textContent='LongCaptionWithoutSpaces'.repeat(6)+' 日本語 العربية'")
            for button in page.locator('.motion-toolbar button').all():
                assert button.bounding_box()['height']>=44
        case('200-percent-text-with-long-multilingual-caption-320',expanded,width=320)
        case('200-percent-text-with-long-multilingual-caption-801',expanded,width=801)

        def api(page,ctx):
            page.locator('#demo-before').click();page.evaluate('KeeFetchMachine.play()')
            expect(page.locator('#demo-after')).to_have_attribute('aria-pressed','true')
            expect(page.locator('#demo')).to_have_attribute('data-sequence','playing')
            page.evaluate('KeeFetchMachine.pause();KeeFetchMachine.pause();KeeFetchMachine.resume();KeeFetchMachine.resume();KeeFetchMachine.stop();KeeFetchMachine.stop()')
            idle(page);frozen(page)
            assert page.evaluate('Object.isFrozen(KeeFetchMachine)')
        case('public-controls-are-idempotent-and-use-the-real-comparison',api)
        browser.close()
    server.shutdown(); server.server_close(); thread.join(timeout=5); temporary.cleanup()
    checkpoint()
    print(f"{report['passed']} passed; {report['failed']} failed",flush=True)
    return bool(report['failed'])

if __name__=='__main__':
    raise SystemExit(main())
