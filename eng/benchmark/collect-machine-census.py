"""Validate new crew labeling outputs and create a candidate queue/spot-check.

Does not replace the benchmark queue, publish winners or reuse historical labels.
"""
import argparse
import csv
import datetime
import hashlib
import html
import json
import re
from collections import defaultdict
from pathlib import Path
from urllib.parse import urlsplit
from PIL import Image
from machine_review_policy import validate_prompts

LABELS={'correct','acceptable-synthetic','generic','wrong-brand','blank','unusable','ambiguous'}

def load(path): return json.loads(path.read_text(encoding='utf-8-sig'))

def decode_labels(response):
    if not isinstance(response,str): return response
    try: return json.loads(response)
    except json.JSONDecodeError:
        # agy's JSON transport can concatenate progress messages before its
        # final answer. Accept only a narrow plain-text progress preamble;
        # never discard errors, another structured response, or trailing prose.
        prefix,separator,tail=response.strip().partition('\n[')
        if not separator or any(char in prefix for char in '[{'):
            raise ValueError('Expected a JSON label array')
        lines=[line.strip() for line in prefix.splitlines() if line.strip()]
        def progress(line):
            return (re.match(r'^(Checking|Downloading|Waiting)\b',line) or
                    re.fullmatch(r'I have launched the live verification request for `[a-zA-Z0-9.-]+` and will proceed once the background command completes\.',line) or
                    line=='I am waiting for the live verification request to finish.')
        if not lines or any(not progress(line) for line in lines):
            raise ValueError('Unrecognized response preamble')
        if re.search(r'\b(error|failed|failure|unable|cannot|inaccessible|denied|truncated|not)\b',prefix,re.I):
            raise ValueError('Failure in response preamble')
        if re.search(r'\b(repository|historical|labels|labeling|preparation|other batches)\b',prefix,re.I):
            raise ValueError('Evidence-scope violation in response preamble')
        return json.loads('['+tail)

def parse_batch(batch):
    manifest=load(batch/'manifest.json'); raw=load(batch/'output.json'); execution=load(batch/'execution.json')
    if execution.get('exit_code') != 0: raise ValueError('Review invocation did not exit successfully')
    started=datetime.datetime.fromisoformat(execution['utc_start'].replace('Z','+00:00'))
    ended=datetime.datetime.fromisoformat(execution['utc_end'].replace('Z','+00:00'))
    if started.tzinfo is None or ended.tzinfo is None or ended < started: raise ValueError('Invalid review timestamps')
    if execution.get('inputs_unchanged') is not True or 'manifest.json' not in execution.get('input_hashes',{}): raise ValueError('Missing frozen review-input identity')
    for name,digest in execution['input_hashes'].items():
        path=(batch/name).resolve()
        if not path.is_relative_to(batch.resolve()) or hashlib.sha256(path.read_bytes()).hexdigest()!=digest: raise ValueError('Reviewed input hash mismatch')
    result=raw['results']
    if len(result)!=1 or result[0]['worker']!='antigravity' or result[0]['status']!='ok': raise ValueError(f'{batch.name}: worker failed')
    result=result[0]
    if result['result'].get('summary_truncated'): raise ValueError(f'{batch.name}: truncated labels')
    argv=result['command']['argv']
    if '--model' not in argv or '--effort' not in argv: raise ValueError('Missing actual invocation model/effort')
    model=argv[argv.index('--model')+1]; effort=argv[argv.index('--effort')+1]
    if not model.strip() or model.startswith('--') or not effort.strip() or effort.startswith('--'): raise ValueError('Invalid actual invocation model/effort')
    summary=json.loads(result['result']['summary'])
    if isinstance(summary,dict):
        # A response with an ERROR status must be investigated before acceptance.
        if summary.get('status') in ('ERROR','FAILED'): raise ValueError(f'{batch.name}: agy response status {summary["status"]}')
        summary=summary.get('response',summary)
    labels=decode_labels(summary)
    if not isinstance(labels,list): raise ValueError(f'{batch.name}: expected label array')
    expected={u['key']:(u,img) for img in manifest['images'] for u in img['units']}
    seen={}; provenance=f'machine:antigravity-{execution["agy_version"]}/{model}'
    for label in labels:
        key=label.get('key')
        if key not in expected or key in seen: raise ValueError(f'{batch.name}: unknown or duplicate key {key}')
        if label.get('label') not in LABELS or not isinstance(label.get('reason'),str) or not label['reason'].strip(): raise ValueError('Invalid label/reason')
        if type(label.get('verified_live')) is not bool or label.get('image_seen') is not True: raise ValueError('Image inspection/live verification missing')
        _,img=expected[key]
        path=batch/img['image']
        if hashlib.sha256(path.read_bytes()).hexdigest()!=img['artifact_hash']: raise ValueError('Reviewed image hash mismatch')
        with Image.open(path) as source:
            fully_transparent=source.convert('RGBA').getchannel('A').getextrema()[1]==0
        if fully_transparent and label['label']!='blank': raise ValueError('Fully transparent image not labeled blank')
        seen[key]={**label,'reviewer':provenance,'reviewed_at_utc':execution['utc_end'],'effort':effort,'crew_run_id':result['run_id']}
    if set(seen)!=set(expected): raise ValueError(f'{batch.name}: incomplete census labels')
    return seen,expected

def collect(kit,queue,zcode_kit=None):
    outputs=('review-queue.machine.csv','labels.validated.json','spot-check.html','spot-check-keys.json','census-validation.json')
    if any((kit/name).exists() for name in outputs): raise ValueError('Refusing to overwrite existing census outputs')
    batches=sorted(kit.glob('batch-*'))
    zcode_policy=None;zcode_identity=None
    if zcode_kit is not None:
        import zcode_review_source
        zcode_kit=zcode_kit.resolve()
        zcode_policy,zcode_identity=zcode_review_source.validate_policy(zcode_kit,kit)
        if not set(zcode_policy['batches']).issubset({b.name for b in batches}):raise ValueError('ZCode assignment is outside the parent census')
        parent_policy=load(kit/'census-prompt-policy.json')
        if set(parent_policy['batches'])!={b.name for b in batches}:raise ValueError('Parent prompt policy differs from census batch set')
        prompt_identity=validate_prompts(kit,[b for b in batches if b.name not in zcode_policy['batches']],complete=False)
    else:
        prompt_identity=validate_prompts(kit,batches)
    batch_outputs={};batch_sources={}
    all_labels={}; units={}; same_origin=defaultdict(set)
    for batch in batches:
        if zcode_policy is not None and batch.name in zcode_policy['batches']:
            source=zcode_kit/batch.name
            labels,expected=zcode_review_source.parse_batch(source,batch,zcode_policy)
            output=source/'review.json';batch_sources[batch.name]={'harness':'owner-run ZCode','output_path':str(output),'transcript_sha256':hashlib.sha256((source/'transcript.txt').read_bytes()).hexdigest()}
        else:
            labels,expected=parse_batch(batch)
            output=batch/'output.json';batch_sources[batch.name]={'harness':'crew/antigravity','output_path':str(output)}
        batch_outputs[batch.name]=hashlib.sha256(output.read_bytes()).hexdigest()
        for key,item in labels.items():
            if key in all_labels: raise ValueError('Duplicate key across batches')
            all_labels[key]=item
            unit,img=expected[key]; units[key]=(unit,img,batch)
            same_origin[(img['artifact_hash'],urlsplit(unit['input_url']).hostname)].add(item['label'])
    inconsistent=[key for key,labels in same_origin.items() if len(labels)>1]
    if inconsistent: raise ValueError(f'Inconsistent labels for identical image/domain: {inconsistent}')
    with queue.open(encoding='utf-8-sig',newline='') as file:
        reader=csv.DictReader(file); fields=reader.fieldnames; rows=list(reader)
    keys=[r['fixture_id']+'|'+r['artifact_hash'] for r in rows]
    if len(keys)!=len(set(keys)) or set(keys)!=set(all_labels): raise ValueError('Batch labels do not equal the complete review queue')
    for row,key in zip(rows,keys):
        for field in ('review_label','reviewer','reviewed_at_utc'):
            row[field]=all_labels[key]['label' if field=='review_label' else field]
    out=kit/'review-queue.machine.csv'
    with out.open('x',encoding='utf-8-sig',newline='') as file:
        writer=csv.DictWriter(file,fieldnames=fields); writer.writeheader(); writer.writerows(rows)
    (kit/'labels.validated.json').write_text(json.dumps(all_labels,indent=2)+'\n',encoding='utf-8')
    # Cover every exceptional outcome plus deterministic category/label examples.
    chosen=[]; strata=set()
    for key in sorted(keys):
        unit,img,batch=units[key]; label=all_labels[key]; stratum=(unit['category'],label['label'])
        if label['label'] in {'wrong-brand','blank','unusable','ambiguous'} or stratum not in strata:
            chosen.append(key); strata.add(stratum)
    sections=[]
    for key in chosen:
        unit,img,batch=units[key]; label=all_labels[key]
        image=(batch/img['image']).relative_to(kit).as_posix(); e=html.escape
        sections.append(f'<article><img width="96" height="96" src="{e(image)}" alt="Icon for {e(unit["input_url"],quote=True)}"><h2>{e(unit["input_url"])}</h2><p>{e(key)}</p><p><strong>{e(label["label"])}</strong> — {e(label["reason"])}</p><p>{e(unit["category"])} · live verified: {label["verified_live"]}</p></article>')
    markup='<!doctype html><html lang="en"><meta charset="utf-8"><title>New study machine-review spot-check</title><style>body{font:16px system-ui;margin:2rem;background:#f5f5f5}article{display:inline-block;vertical-align:top;width:340px;padding:1rem;margin:.5rem;background:white;border:1px solid #555;overflow-wrap:anywhere}img{image-rendering:pixelated;background:repeating-conic-gradient(#ddd 0% 25%,white 0% 50%) 0/16px 16px}h2{font-size:1rem}</style><h1>Expanded study — machine-review spot-check</h1><p>Generated sheet; inspection and observations must be recorded separately. No human review is implied.</p>'+''.join(sections)+'</html>'
    (kit/'spot-check.html').write_text(markup,encoding='utf-8')
    (kit/'spot-check-keys.json').write_text(json.dumps(chosen,indent=2)+'\n',encoding='utf-8')
    validation={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'units':len(keys),'prompt_identity':prompt_identity,'zcode_identity':zcode_identity,'collector_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'prompt_policy_tool_sha256':hashlib.sha256(Path(__file__).with_name('machine_review_policy.py').read_bytes()).hexdigest(),'queue_sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'batch_outputs':batch_outputs,'batch_sources':batch_sources}
    (kit/'census-validation.json').write_text(json.dumps(validation,indent=2)+'\n',encoding='utf-8')
    print(f'Validated {len(keys)} machine labels; candidate queue and {len(chosen)}-unit spot-check prepared. Source queue untouched.')

if __name__=='__main__':
    p=argparse.ArgumentParser(); p.add_argument('--kit',required=True,type=Path); p.add_argument('--queue',type=Path); p.add_argument('--batch',type=Path)
    p.add_argument('--zcode-kit',type=Path,help='Explicit accepted owner-run ZCode recovery assignments; never inferred from directory contents')
    a=p.parse_args()
    if a.batch:
        labels,_=parse_batch(a.batch); print(f'{len(labels)} batch labels validated')
    elif a.queue: collect(a.kit,a.queue,a.zcode_kit)
    else: p.error('--queue or --batch is required')
