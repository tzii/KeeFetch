"""Offline semantic, link, catalog and release gate for the static site."""
import argparse
import importlib.util
import json
import re
from collections import Counter
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
PAGES = ('index.html','getting-started.html','profiles.html','privacy.html','troubleshooting.html','benchmarks.html','contributing.html')
NAV = list(PAGES) + ['https://github.com/tzii/KeeFetch']

class Page(HTMLParser):
    def __init__(self, text):
        super().__init__(convert_charrefs=True)
        self.ids=[]; self.links=[]; self.images=[]; self.meta={}; self.canonical=[]
        self.title=''; self.in_title=False; self.headings=[]; self.mains=[]
        self.nav=[]; self.nav_depth=0; self.current=[]; self.profiles=[]; self.html_lang=''
        self.errors=[]; self.first_link=None
        self.feed(text)
    def handle_starttag(self, tag, attrs):
        a=dict(attrs)
        if tag=='html': self.html_lang=a.get('lang','')
        if 'id' in a: self.ids.append(a['id'])
        if tag=='title': self.in_title=True
        if tag=='h1': self.headings.append(a)
        if tag=='main': self.mains.append(a)
        if tag=='meta': self.meta[a.get('name',a.get('property',''))]=a.get('content','')
        if tag=='nav' and a.get('aria-label')=='Primary': self.nav_depth+=1
        if tag=='a':
            if self.first_link is None: self.first_link=a.get('href','')
            if self.nav_depth:
                self.nav.append(a.get('href',''))
                if a.get('aria-current')=='page': self.current.append(a.get('href'))
        if tag=='img': self.images.append(a)
        if tag=='link' and a.get('rel')=='canonical': self.canonical.append(a.get('href',''))
        if 'data-profile-id' in a: self.profiles.append(a['data-profile-id'])
        for attr in ('href','src'):
            if attr in a: self.links.append((tag,attr,a[attr]))
        if tag=='base': self.errors.append('base element is not allowed')
        if tag in ('iframe','object','embed'): self.errors.append('embedded external content is not allowed')
        if any(k.startswith('on') for k in a): self.errors.append('inline event handler is not allowed')
    def handle_endtag(self, tag):
        if tag=='title': self.in_title=False
        if tag=='nav' and self.nav_depth: self.nav_depth-=1
    def handle_data(self, data):
        if self.in_title: self.title+=data

def verify(site):
    site=site.resolve(); errors=[]; parsed={}
    for name in PAGES:
        if not (site/name).is_file(): errors.append(f'{name}: missing page')
    for path in site.glob('*.html'):
        text=path.read_text(encoding='utf-8')
        page=Page(text); parsed[path.resolve()]=page
        def fail(message): errors.append(f'{path.name}: {message}')
        for error in page.errors: fail(error)
        if not page.title.strip(): fail('missing title')
        if not page.meta.get('description','').strip(): fail('missing description')
        expected='https://tzii.github.io/KeeFetch/'+('' if path.name=='index.html' else path.name)
        if page.canonical != [expected]: fail('missing or incorrect canonical')
        if not page.meta.get('viewport'): fail('missing viewport')
        if not page.html_lang: fail('missing document language')
        if len(page.headings)!=1: fail('expected exactly one h1')
        if len(page.mains)!=1 or page.mains[0].get('id')!='main': fail('expected main landmark with id main')
        if page.first_link!='#main': fail('skip link must be first')
        if page.nav!=NAV: fail('inconsistent primary navigation')
        if page.current!=[path.name]: fail('current page navigation mismatch')
        for id_,count in Counter(page.ids).items():
            if count>1: fail(f'duplicate ID: {id_}')
        for img in page.images:
            if 'alt' not in img: fail('image missing alt')
        for field in ('og:title','og:description','og:type','og:url','og:image'):
            if not page.meta.get(field): fail(f'missing {field}')
        if page.meta.get('og:url')!=expected: fail('Open Graph URL mismatch')
    for path,page in parsed.items():
        for tag,attr,url in page.links:
            target=urlsplit(url)
            if target.scheme or target.netloc:
                if target.scheme!='https' or not target.netloc:
                    errors.append(f'{path.name}: non-HTTPS external URL: {url}')
                if attr=='src' or tag=='link' and url not in page.canonical:
                    errors.append(f'{path.name}: external runtime asset: {url}')
                continue
            resolved=(path.parent/unquote(target.path)).resolve() if target.path else path
            if target.path.endswith('/'): resolved/= 'index.html'
            if not resolved.is_relative_to(site):
                errors.append(f'{path.name}: local link escapes site: {url}'); continue
            if not resolved.is_file(): errors.append(f'{path.name}: broken local link: {url}')
            elif target.fragment and resolved.suffix=='.html':
                other=parsed.get(resolved)
                if other is None or unquote(target.fragment) not in other.ids:
                    errors.append(f'{path.name}: broken fragment: {url}')
    try:
        spec=importlib.util.spec_from_file_location('site_sync',ROOT/'eng/sync-site-profiles.py')
        sync=importlib.util.module_from_spec(spec); spec.loader.exec_module(sync)
        profiles=sync.preview_profiles(site)
        ids=[p['id'] for p in profiles['profiles'] if p['isVisible']]
        if len(ids)!=len(set(ids)): errors.append('duplicate catalog profile ID')
        for name in ('index.html','profiles.html'):
            page=parsed.get(site/name)
            if page and page.profiles!=ids: errors.append(f'{name}: profile ID/order mismatch')
        release=json.loads((site/'data/release.json').read_text(encoding='utf-8-sig'))
        if not re.fullmatch(r'\d+\.\d+\.\d+',release['version']): errors.append('invalid release version')
        if release['tag']!='v'+release['version']: errors.append('release tag/version mismatch')
        base='https://github.com/tzii/KeeFetch/releases/'
        for key,suffix in [('releaseUrl','tag/'+release['tag']),('plgxUrl','download/'+release['tag']+'/KeeFetch.plgx'),('dllUrl','download/'+release['tag']+'/KeeFetch.dll')]:
            if release[key]!=base+suffix: errors.append(f'incorrect release URL: {key}')
        if release.get('checksumsUrl') not in (None,base+'download/'+release['tag']+'/SHA256SUMS.txt'):
            errors.append('incorrect checksums URL')
        if release.get('plgxSha256') and not re.fullmatch(r'[0-9a-f]{64}', release['plgxSha256']):
            errors.append('invalid PLGX checksum')
        if release.get('plgxChecksumUrl') not in (None,base+'download/'+release['tag']+'/KeeFetch.plgx.sha256'):
            errors.append('incorrect PLGX checksum URL')
        if bool(release.get('plgxSha256')) != bool(release.get('plgxChecksumUrl')):
            errors.append('PLGX checksum and source URL must be supplied together')
        for path,expected in sync.expected_pages(site).items():
            if path.read_text(encoding='utf-8')!=expected: errors.append(f'{path.name}: generated content mismatch')
    except (OSError,ValueError,KeyError,TypeError) as ex: errors.append(f'data contract: {ex}')
    for css in site.rglob('*.css'):
        text=css.read_text(encoding='utf-8')
        if re.search(r'@import\b',text,re.I): errors.append(f'{css.name}: CSS imports are not allowed')
        for url in re.findall(r'url\(\s*[\'"]?([^\)\'"\s]+)',text):
            parts=urlsplit(url)
            local=(css.parent/unquote(parts.path)).resolve()
            if parts.scheme or parts.netloc or not local.is_relative_to(site) or not local.is_file():
                errors.append(f'{css.name}: invalid CSS asset: {url}')
    return errors

def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--site',type=Path,default=ROOT/'site')
    args=parser.parse_args(); errors=verify(args.site)
    if errors: raise SystemExit('\n'.join(errors))
    print('Website verified: seven pages, semantics, local links/assets, catalog and release fallbacks.')

if __name__=='__main__': main()
