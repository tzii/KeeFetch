"""Apply explicitly accepted, validated re-asks to a new candidate queue."""
import argparse
import csv
import datetime
import hashlib
import importlib.util
import json
from collections import defaultdict
from pathlib import Path
from urllib.parse import urlsplit
from PIL import Image

spec=importlib.util.spec_from_file_location('collector',Path(__file__).with_name('collect-machine-census.py'))
collector=importlib.util.module_from_spec(spec); spec.loader.exec_module(collector)
load=lambda path:json.loads(path.read_text(encoding='utf-8-sig'))
sha=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()

def parse_direct_review(kit,path,current):
    """Import a disclosed Codex adjudication without inventing crew provenance."""
    record=load(path)
    if (record.get('schema_version')!=1 or record.get('status')!='accepted_for_candidate_adjudication'
            or not record.get('accepted_by') or not record.get('reason')
            or record.get('reviewer')!='machine:codex-direct-adjudication'):
        raise ValueError('Direct review lacks explicit acceptance/provenance')
    when=datetime.datetime.fromisoformat(record['utc'].replace('Z','+00:00'))
    if when.tzinfo is None:raise ValueError('Direct review timestamp lacks timezone')
    if record.get('base_labels_sha256')!=sha(kit/'labels.validated.json'):
        raise ValueError('Direct review base labels changed')
    hashes=record.get('input_hashes')
    if not isinstance(hashes,dict) or not hashes:raise ValueError('Direct review lacks input hashes')
    for relative,digest in hashes.items():
        source=(kit/relative).resolve()
        if not source.is_relative_to(kit.resolve()) or sha(source)!=digest:
            raise ValueError('Direct review input hash/path mismatch')
    expected={}
    for batch in sorted(kit.glob('batch-*')):
        manifest=batch/'manifest.json'
        for image in load(manifest)['images']:
            for unit in image['units']:
                if unit['key'] in expected:raise ValueError('Duplicate direct-review census key')
                expected[unit['key']]=(manifest,batch/image['image'],image['artifact_hash'])
    items=record.get('labels');seen={}
    if not isinstance(items,list) or not items:raise ValueError('Direct review has no labels')
    for item in items:
        key=item.get('key')
        if key not in expected or key not in current or key in seen:raise ValueError('Unknown or duplicate direct-review key')
        if item.get('from_label')!=current[key]['label']:raise ValueError('Direct review prior label changed')
        if item.get('label') not in collector.LABELS or item.get('image_seen') is not True or type(item.get('verified_live')) is not bool:
            raise ValueError('Invalid direct review label/inspection')
        if any(not isinstance(item.get(field),str) or not item[field].strip() for field in ('reason','observation')):
            raise ValueError('Direct review requires a reason and visible observation')
        references=item.get('references',[])
        if not isinstance(references,list) or any(not isinstance(url,str) or urlsplit(url).scheme not in ('https','http') or not urlsplit(url).hostname for url in references):
            raise ValueError('Invalid direct-review reference URL')
        if item['verified_live'] and not references:raise ValueError('Live direct review lacks references')
        manifest,image,digest=expected[key]
        for source in (manifest,image):
            if hashes.get(source.relative_to(kit).as_posix())!=sha(source):raise ValueError('Direct review omitted assigned input identity')
        if sha(image)!=digest:raise ValueError('Direct review artifact changed')
        with Image.open(image) as source:
            if source.convert('RGBA').getchannel('A').getextrema()[1]==0 and item['label']!='blank':raise ValueError('Transparent direct-review image must be blank')
        seen[key]={**item,'reviewer':record['reviewer'],'reviewed_at_utc':record['utc'],'review_source':'disclosed Codex direct adjudication','direct_review_sha256':sha(path)}
    return seen


def apply(kit,reasks,name,direct_reviews=None):
    if not name.replace('-','').replace('_','').isalnum(): raise ValueError('Output name must be a simple filename stem')
    output=kit/(name+'.csv')
    if output.exists() or (kit/(name+'.json')).exists(): raise ValueError('Refusing to overwrite adjudication evidence')
    if len(reasks)!=len(set(reasks)): raise ValueError('Duplicate accepted re-ask')
    direct_reviews=direct_reviews or []
    if len(direct_reviews)!=len(set(direct_reviews)):raise ValueError('Duplicate direct review')
    labels=load(kit/'labels.validated.json'); changed=[]; accepted=[]
    for reask in reasks:
        path=(kit/reask).resolve()
        if not path.is_relative_to(kit.resolve()): raise ValueError('Re-ask path outside kit')
        acceptance=load(path/'acceptance.json')
        if acceptance.get('status')!='accepted_for_candidate_adjudication' or not acceptance.get('accepted_by') or not acceptance.get('reason'):
            raise ValueError('Re-ask lacks explicit recorded acceptance')
        replacements,_=collector.parse_batch(path)
        if acceptance.get('units')!=len(replacements): raise ValueError('Acceptance unit count differs from re-ask')
        for key,item in replacements.items():
            if key not in labels: raise ValueError('Re-ask introduced a non-census key')
            changed.append({'key':key,'before':labels[key],'after':item,'reask':reask})
            labels[key]=item
        accepted.append({'reask':reask,'output_sha256':sha(path/'output.json'),'execution_sha256':sha(path/'execution.json'),'acceptance_sha256':sha(path/'acceptance.json')})
    direct_accepted=[]
    for relative in direct_reviews:
        path=(kit/relative).resolve()
        if not path.is_relative_to(kit.resolve()):raise ValueError('Direct review path outside kit')
        replacements=parse_direct_review(kit,path,labels)
        for key,item in replacements.items():
            changed.append({'key':key,'before':labels[key],'after':item,'direct_review':relative})
            labels[key]=item
        direct_accepted.append({'path':relative,'sha256':sha(path),'units':len(replacements)})
    # Recheck consistency across the final labels, including adjudicated units.
    groups=defaultdict(set); seen=set(); spots=[]; strata=set(); changed_keys={change['key'] for change in changed}
    for batch in sorted(kit.glob('batch-*')):
        manifest=load(batch/'manifest.json')
        for image in manifest['images']:
            for unit in image['units']:
                key=unit['key']; item=labels[key]
                if key in seen: raise ValueError('Duplicate census key in batch manifests')
                seen.add(key)
                groups[(image['artifact_hash'],urlsplit(unit['input_url']).hostname)].add(item['label'])
                stratum=(unit['category'],item['label'])
                if item['label']!='correct' or unit['category']=='android-app' or stratum not in strata or key in changed_keys:
                    spots.append({'key':key,'image':str(Path(batch.name)/image['image']),'input_url':unit['input_url'],'category':unit['category'],'label':item['label'],'reason':item['reason']})
                    strata.add(stratum)
    if seen!=set(labels) or any(len(values)>1 for values in groups.values()): raise ValueError('Final label identity/domain consistency failed')
    with (kit/'review-queue.machine.csv').open(encoding='utf-8-sig',newline='') as file:
        reader=csv.DictReader(file); fields=reader.fieldnames; rows=list(reader)
    keys=[row['fixture_id']+'|'+row['artifact_hash'] for row in rows]
    if len(keys)!=len(set(keys)) or set(keys)!=set(labels): raise ValueError('Base queue differs from validated census')
    for row,key in zip(rows,keys):
        row.update(review_label=labels[key]['label'],reviewer=labels[key]['reviewer'],reviewed_at_utc=labels[key]['reviewed_at_utc'])
    with output.open('w',encoding='utf-8-sig',newline='') as file:
        writer=csv.DictWriter(file,fieldnames=fields); writer.writeheader(); writer.writerows(rows)
    report={'status':'candidate_requires_spot_check_and_selection','tool_sha256':sha(Path(__file__)),'collector_sha256':sha(Path(collector.__file__)),'base_labels_sha256':sha(kit/'labels.validated.json'),'base_queue_sha256':sha(kit/'review-queue.machine.csv'),'accepted_reasks':accepted,'accepted_direct_reviews':direct_accepted,'changes':changed,'final_queue_sha256':sha(output),'spot_check':spots}
    (kit/(name+'.json')).write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(f'Wrote {len(rows)} candidate labels; {len(changed)} disclosed adjudications; {len(spots)} units selected for direct spot-check reconciliation. No publication.')

if __name__=='__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('--kit',required=True,type=Path); parser.add_argument('--accept-reask',action='append',default=[]); parser.add_argument('--output-name',default='review-queue-adjudicated')
    parser.add_argument('--accept-direct-review',action='append',default=[],help='Explicit accepted direct-adjudication record relative to the kit; never attributed to crew')
    args=parser.parse_args(); apply(args.kit.resolve(),args.accept_reask,args.output_name,args.accept_direct_review)
