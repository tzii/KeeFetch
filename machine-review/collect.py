#!/usr/bin/env python
"""Post-pass for the machine census labeling run.

Validates every batch output, cross-checks labels against pixel ground truth
and flags, assembles the labeled review queue, and builds the stratified
human spot-check sheet. Writes NOTHING into the evidence root until all
checks pass; the labeled queue is written to machine-review/review-queue.machine.csv
first so a human can diff/approve before it replaces the real queue.
"""
import csv, glob, json, os, sys, datetime, collections

REVIEWER = 'machine:antigravity-1.1.18/gemini-3.7-flash-high'
QUEUE = 'eng/benchmark-runs/profile-candidates-v13/review-queue.csv'
ALLOWED = {'correct','acceptable-synthetic','generic','wrong-brand','blank','unusable','ambiguous'}

def parse_batch(path):
    """crew wrapper JSON -> list of label objects."""
    with open(path, encoding='utf-8') as f:
        wrap = json.load(f)
    out = []
    for res in wrap.get('results', []):
        summary = res.get('result', {}).get('summary', '')
        try:
            outer = json.loads(summary)
            inner = outer.get('response', summary) if isinstance(outer, dict) else summary
        except json.JSONDecodeError:
            inner = summary
        try:
            start, end = inner.index('['), inner.rindex(']')
            out.extend(json.loads(inner[start:end+1]))
        except (ValueError, json.JSONDecodeError) as e:
            out.append({'_error': f'{path}: unparseable response: {e}'})
    return out

def main():
    labels, errors = {}, []          # key -> label object
    hash_labels = collections.defaultdict(set)  # artifact_hash -> {label}
    hash_domains = {}                # artifact_hash -> set of eTLD+1-ish domains

    def domain(url):
        if not url: return ''
        s = url.split('://',1)[-1]
        return s.split('/',1)[0].lower().rstrip(':443').rstrip(':80')

    manifests = sorted(glob.glob('machine-review/batch-*/manifest.json'))
    for mf in manifests:
        b = os.path.basename(os.path.dirname(mf))
        with open(mf, encoding='utf-8') as f:
            man = json.load(f)
        outp = os.path.join(os.path.dirname(mf), 'output.json')
        if not os.path.isfile(outp):
            errors.append(f'{b}: missing output.json'); continue
        objs = parse_batch(outp)
        bad = [o for o in objs if '_error' in o]
        for o in bad: errors.append(o['_error'])
        seen = set()
        for o in objs:
            if '_error' in o: continue
            key, lab = o.get('key'), o.get('label')
            if not key or key in seen:
                errors.append(f'{b}: duplicate/missing key {key}'); continue
            seen.add(key)
            if lab not in ALLOWED:
                errors.append(f'{b}: {key} label outside set: {lab}'); continue
            if not isinstance(o.get('verified_live'), bool):
                errors.append(f'{b}: {key} verified_live not boolean'); continue
            labels[key] = o
        for img in man['images']:
            for u in img['units']:
                if u['key'] not in seen:
                    errors.append(f'{b}: manifest key not labeled: {u["key"]}')
                hash_labels[img['artifact_hash']].add(labels.get(u['key'], {}).get('label'))
                hash_domains.setdefault(img['artifact_hash'], set()).add(domain(u['input_url']))

    # cross-image consistency: same hash + same site context -> same label.
    # Different sites (web domains vs android app packages, or different apps)
    # may legitimately get different labels for identical bytes - the label
    # belongs to the (fixture, hash) unit, and e.g. the Google G is correct
    # for google.com but wrong-brand for the Gmail app package.
    hash_domain_labels = collections.defaultdict(lambda: collections.defaultdict(set))
    for h, doms in hash_domains.items():
        pass
    for mf in manifests:
        with open(mf, encoding='utf-8') as f:
            man = json.load(f)
        for img in man['images']:
            for u in img['units']:
                lab = labels.get(u['key'], {}).get('label')
                hash_domain_labels[img['artifact_hash']][domain(u['input_url'])].add(lab)
    for h, bydom in hash_domain_labels.items():
        for d, labs in bydom.items():
            if len(labs) > 1:
                errors.append(f'hash {h[:12]} domain {d}: inconsistent labels {sorted(labs)}')

    # pixel cross-checks
    contradictions = []
    for mf in manifests:
        with open(mf, encoding='utf-8') as f:
            man = json.load(f)
        for img in man['images']:
            px = img['pixel']
            is_blank = px['opaque_pct'] == 0 or (px['near_white_pct'] > 98 and px['unique_colors'] < 40)
            for u in img['units']:
                lab = labels.get(u['key'], {}).get('label')
                if lab == 'blank' and not is_blank:
                    contradictions.append((u['key'], 'label=blank but pixels have content'))
                if lab in ('correct','acceptable-synthetic','generic','wrong-brand') and is_blank:
                    contradictions.append((u['key'], f'label={lab} but pixels say blank'))
    for c in contradictions:
        errors.append('PIXEL: ' + c[0] + ' ' + c[1])

    if errors:
        print('VALIDATION ERRORS:', len(errors))
        for e in errors[:40]: print(' -', e)
        sys.exit(1)

    # assemble labeled queue (side copy first)
    with open(QUEUE, encoding='utf-8-sig', newline='') as f:
        qrows = list(csv.DictReader(f))
        fields = list(qrows[0].keys())
    now = datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0).isoformat().replace('+00:00','Z')
    dist = collections.Counter()
    for r in qrows:
        o = labels[r['fixture_id'] + '|' + r['artifact_hash']]
        r['review_label'] = o['label']; dist[o['label']] += 1
        r['reviewer'] = REVIEWER; r['reviewed_at_utc'] = now
        note = (r['notes'] or '').strip()
        reason = (o.get('reason') or '').strip()
        r['notes'] = (note + ' | ' if note and note != 'census' else '') + f'machine-review: {reason}'
    outq = 'machine-review/review-queue.machine.csv'
    with open(outq, 'w', encoding='utf-8-sig', newline='') as f:
        w = csv.DictWriter(f, fieldnames=fields, quoting=csv.QUOTE_ALL)
        w.writeheader(); w.writerows(qrows)
    print('labeled queue:', outq, '| distribution:', dict(dist))
    print('all 456 labeled:', sum(dist.values()) == 456)

    # stratified spot-check sheet: 25 units, embedded images
    bylabel = collections.defaultdict(list)
    for r in qrows: bylabel[r['review_label']].append(r)
    import base64, random
    random.seed(20260822)
    sample = []
    for lab in sorted(bylabel):
        random.shuffle(bylabel[lab])
        sample.extend(bylabel[lab][:max(1, round(25*len(bylabel[lab])/456))])
    imgdir = {}
    for mf in manifests:
        with open(mf, encoding='utf-8') as f: man = json.load(f)
        for img in man['images']:
            imgdir[img['artifact_hash']] = os.path.join(os.path.dirname(mf), img['image'])
    rows_html = []
    for r in sample:
        p = imgdir.get(r['artifact_hash'])
        b64 = base64.b64encode(open(p,'rb').read()).decode() if p and os.path.isfile(p) else ''
        rows_html.append(f'<tr><td>{r["fixture_id"]}<br><small>{r["categories"]}</small></td>'
                         f'<td><img src="data:image/png;base64,{b64}" width="64" height="64"></td>'
                         f'<td><b>{r["review_label"]}</b><br><small>{r["notes"]}</small></td></tr>')
    html = ('<html><head><meta charset="utf-8"><title>Machine census spot-check</title>'
            '<style>body{font-family:sans-serif}table{border-collapse:collapse}td,th{border:1px solid #999;padding:6px}img{image-rendering:pixelated}</style>'
            '</head><body><h1>Machine census spot-check - 25 stratified units</h1>'
            f'<p>Reviewer: {REVIEWER}</p><table><tr><th>fixture</th><th>artifact</th><th>machine label</th></tr>'
            + ''.join(rows_html) + '</table></body></html>')
    with open('machine-review/spot-check.html', 'w', encoding='utf-8') as f:
        f.write(html)
    print('spot-check sheet:', 'machine-review/spot-check.html,', len(sample), 'units')

if __name__ == '__main__':
    main()
