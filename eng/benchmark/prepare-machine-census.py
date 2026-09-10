"""Validate a complete non-resumed study and prepare an isolated icon-review kit.

Uses Pillow only for local image measurements. Never imports historical labels.
The selector remains the final independent row/metric/winner gate.
"""
import argparse
import csv
import datetime
import hashlib
import json
import shutil
import platform
from collections import Counter, defaultdict
from pathlib import Path
from PIL import Image, __version__ as pillow_version

ROOT=Path(__file__).resolve().parents[2]
FIELDS=('experiment_fingerprint','corpus_fingerprint','binary_hash','execution_harness_fingerprint')

def read_json(path): return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()

def validate(run_root,cold_only=False):
    schedule=read_json(run_root/'schedule.json')
    expected=schedule['cells']
    if len(expected)!=133 or len(set(expected))!=133: raise ValueError('Expected 133 unique scheduled cells')
    experiment=ROOT/'eng/benchmark/experiments/profile-candidates-v13-complete.json'
    definition=read_json(experiment)
    corpus=ROOT/definition['corpus']
    if sha(experiment)!=schedule['experiment_fingerprint'] or sha(corpus)!=schedule['corpus_fingerprint']:
        raise ValueError('Experiment/corpus differs from scheduled evidence')
    if sha(ROOT/'bin/Release/net48/KeeFetch.dll')!=schedule['binary_hash']: raise ValueError('Built binary changed')
    payload=b'keefetch-exec-harness-v1\n'
    for name in ('eng/benchmark-presets.ps1','eng/benchmark/BenchmarkHarness.psm1'):
        payload+=name.encode()+b'\n'+(ROOT/name).read_text(encoding='utf-8').encode()+b'\n'
    if hashlib.sha256(payload).hexdigest()!=schedule['execution_harness_fingerprint']: raise ValueError('Harness changed')
    fixtures={r['fixture_id']:r for r in csv.DictReader(corpus.open(encoding='utf-8-sig',newline=''))}
    if len(fixtures)!=300: raise ValueError('Expected 300 unique fixtures')
    policies={}
    for c in definition['candidates']:
        canonical=f'v2|providers={",".join(c["providerIds"])}|primaryMs={c["primaryTimeout"]}|fallbackMs={c["fallbackTimeout"]}|cumulativeMs={c["cumulativeTimeout"]}|synthetic={int(c["allowSynthetic"])}|stopAfterStrongResolved={int(c["stopAfterStrongResolved"])}|androidStore={int(c["allowAndroidStoreLookup"])}'
        policies[c['id']]=hashlib.sha256(canonical.encode()).hexdigest()
    seen=[]; environments=set(); commits=set(); units={}; counts=Counter()
    directories=sorted(p.parent for p in run_root.glob('*/run.json'))
    required=133
    if cold_only:
        expected=[cell for cell in expected if '|cold|' in cell]
        directories=[d for d in directories if read_json(d/'run.json')['cache_mode']=='cold']
        required=57
    if len(directories)!=required: raise ValueError(f'Incomplete matrix: {len(directories)}/{required} cell directories')
    for directory in directories:
        m=read_json(directory/'run.json')
        identity=f'{m["candidate_id"]}|{m["cache_mode"]}|{m["repetition"]}|{m["run_kind"]}'
        if m['status']!='complete' or m.get('resumed') is not False: raise ValueError(f'Incomplete/resumed cell: {identity}')
        if m.get('total_records')!=300: raise ValueError(f'Wrong row count: {identity}')
        if any(m.get(f)!=schedule[f] for f in FIELDS): raise ValueError(f'Mixed fingerprints: {identity}')
        if m.get('schedule_seed')!=schedule['schedule_seed'] or m.get('concurrency')!=8: raise ValueError('Execution settings mismatch')
        if m['experiment_id']!=definition['experiment_id'] or m['policy_fingerprint']!=policies[m['candidate_id']]: raise ValueError('Effective policy mismatch')
        environments.add(json.dumps(m['harness_environment'],sort_keys=True)); commits.add(m['commit'])
        rows=[json.loads(line) for line in (directory/'results.ndjson').read_text(encoding='utf-8-sig').splitlines() if line.strip()]
        if len(rows)!=300 or Counter(r['fixture_id'] for r in rows)!=Counter(fixtures.keys()): raise ValueError(f'Fixture identity mismatch: {identity}')
        seen.append(identity); counts[m['run_kind']]+=1
        for row in rows:
            for field,actual in [('run_id',m['run_id']),('experiment_id',m['experiment_id']),('profile',m['candidate_id']),('cache_mode',m['cache_mode']),('repetition',m['repetition']),('concurrency',8),('commit',m['commit'])]:
                if row.get(field)!=actual: raise ValueError(f'Row {field} mismatch in {identity}')
            if row['input_url']!=fixtures[row['fixture_id']]['input_url']: raise ValueError('Input URL changed')
            if m['run_kind']!='measured' or m['cache_mode']!='cold' or not row.get('artifact_hash'): continue
            artifact=(directory/row['artifact_path']).resolve()
            if not artifact.is_relative_to(directory.resolve()) or sha(artifact)!=row['artifact_hash']: raise ValueError('Artifact path/hash mismatch')
            key=row['fixture_id']+'|'+row['artifact_hash']
            if key not in units:
                units[key]={'key':key,'fixture_id':row['fixture_id'],'artifact_hash':row['artifact_hash'],'input_url':row['input_url'],'category':row['category'],'artifact':str(artifact),'flags':set(),'occurrences':0}
            u=units[key]; u['occurrences']+=1
            for flag in ('is_synthetic','blank_suspected','placeholder_suspected'):
                if row.get(flag): u['flags'].add(flag)
    expected_counts=Counter(measured=57) if cold_only else Counter(measured=114,warmup=19)
    if Counter(seen)!=Counter(expected) or counts!=expected_counts: raise ValueError('Scheduled matrix mismatch')
    if len(environments)!=1 or len(commits)!=1: raise ValueError('Mixed environment/source')
    queue=list(csv.DictReader((run_root/'review-queue.csv').open(encoding='utf-8-sig',newline='')))
    keys=[r['fixture_id']+'|'+r['artifact_hash'] for r in queue]
    if len(keys)!=len(set(keys)) or set(keys)!=set(units): raise ValueError('Queue does not equal cold-artifact census')
    for u in units.values(): u['flags']=sorted(u['flags'])
    return {'schedule':schedule,'environment':json.loads(next(iter(environments))),'source':next(iter(commits)),'units':units,'validation_scope':'complete-cold-census' if cold_only else 'complete-matrix'}

def measurements(path):
    with Image.open(path) as source:
        source.load(); rgba=source.convert('RGBA'); pixels=list(rgba.getdata())
        return {'width':rgba.width,'height':rgba.height,'bytes':path.stat().st_size,'nonzero_alpha_pixels':sum(p[3]>0 for p in pixels),'opaque_pct':round(100*sum(p[3]>16 for p in pixels)/len(pixels),2)}

def create_kit(run_root,kit,cold_only=False):
    evidence=validate(run_root,cold_only)
    if kit.exists(): raise ValueError('Refusing to overwrite an existing review kit')
    grouped=defaultdict(list)
    for unit in evidence['units'].values(): grouped[unit['artifact_hash']].append(unit)
    images=[]
    for digest,units in sorted(grouped.items()):
        artifact=Path(units[0]['artifact'])
        images.append({'artifact_hash':digest,'source':str(artifact),'pixel':measurements(artifact),'units':sorted(units,key=lambda x:x['key'])})
    pilot=[]; covered=set()
    # Deterministic coverage of categories/flags and shared-image cases.
    for img in images:
        traits={u['category'] for u in img['units']} | {f for u in img['units'] for f in u['flags']}
        if len(img['units'])>1: traits.add('shared-image')
        if traits-covered:
            pilot.append(img); covered|=traits
    def write_batch(name,items):
        batch=kit/name; (batch/'images').mkdir(parents=True)
        out=[]
        for item in items:
            local='images/'+item['artifact_hash']+'.png'; shutil.copyfile(item['source'],batch/local)
            out.append({'artifact_hash':item['artifact_hash'],'image':local,'pixel':item['pixel'],'units':[{k:v for k,v in u.items() if k!='artifact'} for u in item['units']]})
        (batch/'manifest.json').write_text(json.dumps({'batch':name,'images':out},indent=2)+'\n',encoding='utf-8')
    write_batch('pilot-a',pilot); write_batch('pilot-b',pilot)
    for index in range(0,len(images),24): write_batch(f'batch-{index//24+1:02}',images[index:index+24])
    tooling={name:sha(ROOT/'eng/benchmark'/name) for name in ('prepare-machine-census.py','collect-machine-census.py','compare-machine-pilots.py','run-machine-batch.py','machine_review_policy.py','machine-census-prompt.txt','select-profiles.ps1')}
    template_hash=hashlib.sha256((ROOT/'eng/benchmark/machine-census-prompt.txt').read_text(encoding='utf-8').encode('utf-8')).hexdigest()
    (kit/'kit.json').write_text(json.dumps({'created_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'validation_scope':evidence['validation_scope'],'full_matrix_required_before_selection':True,'source':evidence['source'],'schedule':evidence['schedule'],'environment':evidence['environment'],'python_version':platform.python_version(),'pillow_version':pillow_version,'tooling_sha256':tooling,'prompt_template_sha256_normalized':template_hash,'unit_count':len(evidence['units']),'image_count':len(images),'pilot_images':len(pilot),'pilot_traits':sorted(covered),'historical_labels_imported':False},indent=2)+'\n',encoding='utf-8')
    print(f'Validated {evidence["validation_scope"]}; prepared {len(evidence["units"])} units / {len(images)} images; pilot {len(pilot)} images. Full matrix required before selection.')

if __name__=='__main__':
    p=argparse.ArgumentParser(); p.add_argument('--run-dir',required=True,type=Path); p.add_argument('--kit',type=Path); p.add_argument('--cold-only',action='store_true')
    a=p.parse_args()
    if a.kit: create_kit(a.run_dir.resolve(),a.kit.resolve(),a.cold_only)
    else:
        result=validate(a.run_dir.resolve(),a.cold_only); print(f'{result["validation_scope"]} verified: {len(result["units"])} units')
