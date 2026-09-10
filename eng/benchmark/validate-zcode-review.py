"""Validate owner-run ZCode evidence without claiming independent model attestation.

This does not import labels, accept pilots, adjudicate judgments or select profiles.
"""
import argparse,datetime,hashlib,json,re
from pathlib import Path
from PIL import Image

MODEL='builtin:zai-coding-plan/GLM-5.3-Flash'
LABELS={'correct','acceptable-synthetic','generic','wrong-brand','blank','unusable','ambiguous'}
def load(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def review_time(raw):
    # Some delivered records append a disclosed reconstruction note. Retain
    # that raw value in provenance; parse only a full ISO timestamp prefix.
    if not isinstance(raw,str):raise ValueError('Invalid review timestamp')
    match=re.fullmatch(r'(.+?) \(([^()\r\n]+)\)',raw)
    value=match.group(1) if match else raw
    return datetime.datetime.fromisoformat(value.replace('Z','+00:00'))


def validate_input_hashes(recorded,prepared):
    """Check all delivered before/after hash layouts without rewriting evidence."""
    if isinstance(recorded,list):
        entries={}
        for item in recorded:
            if not isinstance(item,dict) or set(item)!={'path','sha256_before','sha256_after'}:
                raise ValueError('Invalid review input hash record')
            name=item['path']
            if not isinstance(name,str) or name in entries:raise ValueError('Duplicate or invalid review input hash path')
            entries[name]={k:item[k] for k in ('sha256_before','sha256_after')}
        recorded=entries
    if not isinstance(recorded,dict):raise ValueError('Invalid review input hashes')
    if set(recorded)=={'before','after'}:
        if recorded['before']!=prepared or recorded['after']!=prepared:
            raise ValueError('Review input identity differs from prepared hashes')
        return
    if set(recorded)!=set(prepared):raise ValueError('Review input identity differs from prepared hashes')
    for name,digest in prepared.items():
        value=recorded[name]
        if value==digest:continue
        if isinstance(value,dict) and set(value) in ({'before','after'},{'sha256_before','sha256_after'}) and all(v==digest for v in value.values()):continue
        raise ValueError('Review input identity differs from prepared hashes')


def validate(batch):
    batch=batch.resolve();manifest=load(batch/'manifest.json');prepared=load(batch/'input-hashes.json');review=load(batch/'review.json')
    if review.get('schema_version')!=1 or review.get('harness')!='ZCode':raise ValueError('Unexpected review schema/harness')
    if review.get('actual_model_id')!=MODEL or not str(review.get('model_identity_source','')).strip():raise ValueError('Missing/different reported model identity')
    if review.get('inputs_unchanged') is not True:raise ValueError('Reviewer did not confirm unchanged inputs')
    times=[review_time(review[k]) for k in ('utc_start','utc_end')]
    if any(t.tzinfo is None for t in times) or times[1]<times[0]:raise ValueError('Invalid review timestamps')
    names={'manifest.json'}|{i['image'] for i in manifest['images']}
    if set(prepared)!=names:raise ValueError('Review input identity differs from prepared hashes')
    recorded=review.get('input_hashes')
    # These two explicitly permitted inputs may also be hashed by the worker.
    # Bind them to disk here; the source adapter additionally binds the prompt
    # and input-hashes file to the accepted assignment policy.
    expected_hashes=dict(prepared)
    if isinstance(recorded,dict):
        for name in ('prompt.txt','input-hashes.json'):
            if name in recorded:expected_hashes[name]=sha(batch/name)
    validate_input_hashes(recorded,expected_hashes)
    if 'input_hashes_recheck_after_inspection' in review:
        validate_input_hashes(review['input_hashes_recheck_after_inspection'],prepared)
    for name,digest in prepared.items():
        path=(batch/name).resolve()
        if not path.is_relative_to(batch) or sha(path)!=digest:raise ValueError('Changed input hash or path outside batch')
    expected={};images={}
    for img in manifest['images']:
        if sha(batch/img['image'])!=img['artifact_hash']:raise ValueError('Image artifact hash mismatch')
        for unit in img['units']:
            if unit['key'] in expected:raise ValueError('Duplicate manifest key')
            expected[unit['key']]=unit;images[unit['key']]=img
    labels=review.get('labels');seen={}
    if not isinstance(labels,list):raise ValueError('Expected label array')
    for item in labels:
        key=item.get('key')
        if key not in expected or key in seen:raise ValueError('Unknown or duplicate review key')
        if item.get('label') not in LABELS or not isinstance(item.get('reason'),str) or not item['reason'].strip():raise ValueError('Invalid label/reason')
        if item.get('image_seen') is not True or type(item.get('verified_live')) is not bool:raise ValueError('Missing image inspection or live-verification Boolean')
        with Image.open(batch/images[key]['image']) as source:
            if source.convert('RGBA').getchannel('A').getextrema()[1]==0 and item['label']!='blank':raise ValueError('Transparent image must be blank')
        seen[key]=item
    if set(seen)!=set(expected):raise ValueError('Incomplete label census')
    return seen,{'status':'structurally_valid_requires_direct_inspection','units':len(seen),'reported_model':MODEL,'model_provenance':'owner-run ZCode harness report; not independently attested by this validator','review_sha256':sha(batch/'review.json'),'manifest_sha256':sha(batch/'manifest.json'),'reported_timestamps':{k:review[k] for k in ('utc_start','utc_end')}}

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('batch',type=Path);args=parser.parse_args()
    _,report=validate(args.batch);print(json.dumps(report,indent=2))
