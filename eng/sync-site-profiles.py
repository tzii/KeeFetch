"""Synchronize catalog and stable-release HTML without network or dependencies."""
import argparse
import html
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
# Immutable source of the completed 19-candidate study described by this preview.
# Update deliberately when the selected study changes; master still has older evidence.
EVIDENCE_REF = '19a0c224ca4dbf7042d22c3497e7599f61937c0b'
PROVIDERS = {'direct-site':'Direct Site','twenty-icons':'Twenty Icons','duckduckgo':'DuckDuckGo','google':'Google','yandex':'Yandex','favicone':'Favicone','icon-horse':'Icon Horse'}

def profile_markup(data):
    rows = []
    for p in data['profiles']:
        if not p['isVisible']:
            continue
        e = html.escape
        chain = ' → '.join(PROVIDERS[x] for x in p['providerIds'])
        disclosure = 'Enabled resolvers receive domains' if any(x != 'direct-site' for x in p['providerIds']) else 'No favicon resolvers; site-linked hosts possible'
        synth = 'Allowed' if p['allowSyntheticFallbacks'] else 'Disabled'
        stop = 'Stops on a strong resolver hit' if p['stopAfterStrongResolved'] else 'Queries the full chain'
        default = '<span class="badge">Default</span>' if p['id'] == 'everyday' else ''
        evidence = f'https://github.com/tzii/KeeFetch/blob/{EVIDENCE_REF}/' + p['evidenceReport']
        rows.append(f'<tr data-profile-id="{e(p["id"],quote=True)}"><th scope="row">{e(p["displayName"])}{default}</th><td>{e(p["intendedUse"])}</td><td>{e(chain)}<small>{p["primaryTimeoutMs"]/1000:g} s primary / {p["fallbackTimeoutMs"]/1000:g} s fallback / {p["cumulativeTimeoutMs"]/1000:g} s total. {stop}.</small></td><td>{e(disclosure)}<small>Synthetic: {synth}. Android store lookup: {"Enabled" if p["allowAndroidStoreLookup"] else "Disabled"}.</small><a href="{e(evidence,quote=True)}">Study evidence</a></td></tr>')
    return '<div class="table-wrap" tabindex="0" role="region" aria-label="Profile comparison"><table><caption>Managed fetch profiles</caption><thead><tr><th scope="col">Profile</th><th scope="col">Intended use</th><th scope="col">Provider order and budgets</th><th scope="col">Disclosure and fallbacks</th></tr></thead><tbody>' + ''.join(rows) + '</tbody></table></div>'

def profile_cards_markup(data):
    """Readable summary cards share the comparison table's catalog source."""
    cards = []
    for number, p in enumerate((p for p in data['profiles'] if p['isVisible']), 1):
        e = html.escape
        default = '<span class="card-default">Default</span>' if p['id'] == 'everyday' else ''
        count = len(p['providerIds'])
        sources = f'{count} icon source' + ('s' if count != 1 else '')
        cards.append(
            f'<article class="profile-summary" data-profile-key="{e(p["id"],quote=True)}">'
            f'<div class="card-top"><span aria-hidden="true">0{number}</span>{default}</div>'
            f'<h3>{e(p["displayName"])}</h3><p>{e(p["intendedUse"])}</p>'
            f'<div class="card-policy"><strong>{p["cumulativeTimeoutMs"]/1000:g}<small> s</small></strong>'
            f'<span>per-entry time budget</span></div><div class="card-sources">{sources}</div></article>'
        )
    return '<div class="profile-summaries">' + ''.join(cards) + '</div>'


def replace_block(text, name, content):
    start, end = f'<!-- {name}_START -->', f'<!-- {name}_END -->'
    if text.count(start) != 1 or text.count(end) != 1:
        raise ValueError(f'Expected exactly one {name} marker pair')
    before, rest = text.split(start)
    _, after = rest.split(end)
    return before + start + '\n' + content + '\n' + end + after

def profile_presets_markup(data):
    profiles = [p for p in data['profiles'] if p['isVisible']]
    buttons, panels = [], []
    e = html.escape
    for p in profiles:
        key = e(p['id'], quote=True)
        name = e(p['displayName'])
        buttons.append(f'<button type="button" data-preset="{key}" aria-controls="preset-{key}" aria-pressed="{str(p["id"] == "everyday").lower()}">{name}</button>')
        chain = ' → '.join(PROVIDERS[x] for x in p['providerIds'])
        policy = 'No favicon resolvers or Android store lookup. Site-linked hosts and redirects remain possible.' if p['id'] == 'privacy' else 'Enabled favicon resolvers may receive the entry’s domain.'
        panels.append(f'<article class="preset-panel" id="preset-{key}" data-profile-id="{key}"><h3>{name}{" · Recommended" if p["id"] == "everyday" else ""}</h3><p class="preset-hook">{e(p["intendedUse"])}</p><p>{e(chain)} · {p["cumulativeTimeoutMs"]/1000:g} s total per-entry budget.</p><p>{policy} Synthetic fallbacks are disabled.</p></article>')
    return '<div class="preset-controls" role="group" aria-label="Explore fetch profiles" id="preset-controls" hidden>' + ''.join(buttons) + '</div><div id="preset-panels">' + ''.join(panels) + '</div>'


def preview_profiles(site):
    # Pages can publish before the plugin merges. Keep the advertised candidate
    # snapshot separate from profiles.json, which still follows the built branch.
    release = json.loads((site/'data/release.json').read_text(encoding='utf-8-sig'))
    snapshot = release['previewProfiles']
    if snapshot['file'] != 'profiles-v1.3.json':
        raise ValueError('unexpected preview profile filename')
    data = (site/'data'/snapshot['file']).read_bytes()
    if hashlib.sha256(data).hexdigest() != snapshot['sha256']:
        raise ValueError('preview profile snapshot checksum mismatch')
    return json.loads(data.decode('utf-8-sig'))


def expected_pages(site):
    data = preview_profiles(site)
    release = json.loads((site/'data/release.json').read_text(encoding='utf-8-sig'))
    e = html.escape
    version = e(release['version'])
    blocks = {
        'RELEASE_STATUS': (f'v{e(release["previewVersion"])} source preview · ' if release.get('previewVersion') else '') + f'Stable download: <a href="{e(release["releaseUrl"],quote=True)}">v{version}</a>',
        'RELEASE_FOOTER': f'<a href="{e(release["releaseUrl"],quote=True)}">Stable v{version}</a>',
        'RELEASE_DOWNLOAD': f'<a class="button" href="{e(release["plgxUrl"],quote=True)}">Download PLGX · v{version}</a>',
        'PROFILE_FALLBACK': profile_markup(data),
        'PROFILE_CARDS': profile_cards_markup(data),
        'RELEASE_HERO': f'<a class="button primary" href="{e(release["plgxUrl"],quote=True)}">Get KeeFetch <span class="button-version">v{version}</span><span aria-hidden="true">↓</span></a>',
        'RELEASE_NOTES': f'<a href="{e(release["releaseUrl"],quote=True)}">Stable v{version} · Release notes ↗</a>',
        'RELEASE_VERIFY': (f'<details class="verification"><summary>Verify your download</summary><p>Compare the PLGX file’s SHA-256 with the published checksum for v{version}.</p><div class="checksum-row"><code id="checksum">{e(release["plgxSha256"])}</code><button class="copy-button" id="copy-checksum" type="button" hidden>Copy SHA-256</button></div><p id="copy-status" role="status" aria-live="polite"></p><a href="{e(release["plgxChecksumUrl"],quote=True)}">Published checksum file ↗</a></details>' if release.get('plgxSha256') and release.get('plgxChecksumUrl') else f'<p><a href="{e(release["releaseUrl"],quote=True)}">Release assets and verification details ↗</a></p>'),
        'PROFILE_PRESETS': profile_presets_markup(data)
    }
    expected = {}
    for path in sorted(site.glob('*.html')):
        text = path.read_text(encoding='utf-8')
        for name, markup in blocks.items():
            if f'<!-- {name}_START -->' in text:
                text = replace_block(text, name, markup)
        expected[path] = text
    return expected

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--site', type=Path, default=ROOT/'site')
    args = parser.parse_args()
    changed = []
    for path, expected in expected_pages(args.site).items():
        if path.read_text(encoding='utf-8') != expected:
            changed.append(path.name)
            if not args.check:
                path.write_text(expected, encoding='utf-8', newline='\n')
    if args.check and changed:
        raise SystemExit('Generated site content is stale: ' + ', '.join(changed))
    print('Site catalog/release content ' + ('verified.' if args.check else 'synchronized.'))

if __name__ == '__main__': main()
