"""Run one authorized agy image batch through crew, preserving full provenance."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
from machine_review_policy import validate_prompts

ROOT=Path(__file__).resolve().parents[2]

def now(): return datetime.datetime.now(datetime.timezone.utc).isoformat()

def run(batch):
    batch=batch.resolve()
    if not (batch/'manifest.json').is_file(): raise ValueError('Missing batch manifest')
    if (batch/'output.json').exists(): raise ValueError('Refusing to overwrite an earlier review invocation')
    prompt=(ROOT/'eng/benchmark/machine-census-prompt.txt').read_text(encoding='utf-8').replace('BATCH_DIRECTORY',str(batch))
    if batch.name.startswith('batch-'):
        validate_prompts(batch.parent,[batch],complete=False,pending_prompt=prompt)
    crew=Path(os.environ['USERPROFILE'])/'.crew/bin/crew.py'
    environment={**os.environ,'PYTHONIOENCODING':'utf-8'}
    doctor=subprocess.run([sys.executable,str(crew),'doctor'],cwd=ROOT,env=environment,capture_output=True,text=True,encoding='utf-8',timeout=120)
    if doctor.returncode: raise ValueError('crew doctor failed: '+doctor.stderr)
    report=json.loads(doctor.stdout)
    worker=next(w for w in report['doctor'] if w['worker']=='antigravity')
    if worker['state']!='ready': raise ValueError('agy is unavailable; no configuration changes attempted')
    version=worker['version_hint']
    (batch/'prompt.txt').write_text(prompt,encoding='utf-8')
    (batch/'doctor.json').write_text(doctor.stdout,encoding='utf-8')
    command=[sys.executable,str(crew),'review','--workers','google','--prompt',prompt,'--cwd',str(ROOT),'--timeout','2100','--pretty']
    manifest=json.loads((batch/'manifest.json').read_text(encoding='utf-8'))
    inputs=['manifest.json','prompt.txt']+[image['image'] for image in manifest['images']]
    hashes={name:hashlib.sha256((batch/name).read_bytes()).hexdigest() for name in inputs}
    execution={'utc_start':now(),'agy_version':version,'cwd':str(ROOT),'batch':str(batch),'interface':'crew review --workers google','timeout_seconds':2100,'input_hashes':hashes}
    try:
        with (batch/'output.json').open('wb') as output, (batch/'output.err').open('wb') as error:
            process=subprocess.run(command,cwd=ROOT,env=environment,stdout=output,stderr=error,timeout=2220)
        execution['exit_code']=process.returncode
    finally:
        execution['utc_end']=now()
        execution['inputs_unchanged']=all((batch/name).is_file() and hashlib.sha256((batch/name).read_bytes()).hexdigest()==digest for name,digest in hashes.items())
        (batch/'execution.json').write_text(json.dumps(execution,indent=2)+'\n',encoding='utf-8')
    if not execution['inputs_unchanged']: raise RuntimeError('Reviewer modified its inputs; results rejected')
    if process.returncode: raise RuntimeError(f'crew batch failed ({process.returncode}); inspect {batch}')
    print(f'Review invocation complete: {batch}. Labels require validation.')

if __name__=='__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('batch',type=Path); args=parser.parse_args(); run(args.batch)
