"""Validate explicit owner-run ZCode assignments without inventing crew evidence."""
import datetime
import hashlib
import importlib.util
import json
from pathlib import Path

spec=importlib.util.spec_from_file_location('zcode_validator',Path(__file__).with_name('validate-zcode-review.py'))
validator=importlib.util.module_from_spec(spec);spec.loader.exec_module(validator)
load=validator.load
sha=validator.sha

def validate_policy(recovery,parent):
    recovery=recovery.resolve();parent=parent.resolve()
    policy=load(recovery/'census-assignment-policy.json');prepared=load(recovery/'recovery.json')
    if policy.get('status')!='accepted' or not policy.get('accepted_by') or not policy.get('reason'):
        raise ValueError('Missing ZCode assignment acceptance')
    if policy.get('model')!=validator.MODEL:raise ValueError('Unexpected assigned ZCode model')
    if Path(prepared['parent_kit']).resolve()!=parent:raise ValueError('ZCode recovery belongs to a different parent kit')
    if sha(recovery/'recovery.json')!=policy['recovery_sha256']:raise ValueError('Changed ZCode recovery identity')
    acceptance=load(recovery/'pilot-acceptance.json')
    if sha(recovery/'pilot-acceptance.json')!=policy['pilot_acceptance_sha256'] or acceptance.get('status')!='accepted_to_start_census' or acceptance.get('reported_model')!=validator.MODEL:
        raise ValueError('Missing or changed ZCode pilot acceptance')
    if sha(recovery/'pilot-comparison.json')!=acceptance['comparison_sha256']:raise ValueError('Changed ZCode pilot comparison')
    comparison=load(recovery/'pilot-comparison.json')
    for name in ('pilot-a','pilot-b'):
        _,report=validator.validate(recovery/name)
        prior=comparison['pilots'][name]
        if any(report[field]!=prior[field] for field in ('review_sha256','manifest_sha256')):
            raise ValueError('Changed accepted ZCode pilot evidence')
        if sha(recovery/name/'transcript.txt')!=prior['transcript_sha256'] or sha(recovery/(name+'-prompt.txt'))!=prior['prompt_sha256'] or sha(recovery/name/'input-hashes.json')!=acceptance['input_hash_files'][name]:
            raise ValueError('Changed accepted ZCode pilot provenance')
    template_path=recovery/'census-prompt-template.txt'
    if sha(template_path)!=policy['template_sha256']:raise ValueError('Changed ZCode census template')
    rubric_path=parent/'prompt-template-restricted.txt'
    if sha(rubric_path)!=policy['rubric_sha256']:raise ValueError('Changed accepted label rubric')
    template=template_path.read_text(encoding='utf-8')
    if rubric_path.read_text(encoding='utf-8') not in template:raise ValueError('ZCode template does not retain accepted rubric')
    names=prepared['pending_batches']
    if len(names)!=len(set(names)) or set(names)!=set(policy['batches']) or set(names)!=set(prepared['input_hashes']):raise ValueError('ZCode assignment set differs from prepared recovery')
    if set(names)!={p.name for p in recovery.glob('batch-*')}:raise ValueError('Unexpected ZCode batch directories')
    keys=[]
    for name,item in policy['batches'].items():
        if Path(name).name!=name or name in ('.','..'):raise ValueError('Invalid ZCode batch path')
        batch=recovery/name;original=parent/name
        if item['input_hashes']!=prepared['input_hashes'][name] or load(batch/'input-hashes.json')!=item['input_hashes']:
            raise ValueError('Changed ZCode assignment input identity')
        for relative,digest in item['input_hashes'].items():
            path=(batch/relative).resolve()
            if not path.is_relative_to(batch.resolve()) or sha(path)!=digest:raise ValueError('Changed assigned ZCode input')
        if sha(batch/'prompt.txt')!=item['prompt_sha256'] or (batch/'prompt.txt').read_text(encoding='utf-8')!=template.replace('BATCH_DIRECTORY',str(batch.resolve())):
            raise ValueError('Changed assigned ZCode prompt')
        manifest=load(batch/'manifest.json');original_manifest=load(original/'manifest.json')
        if manifest['images']!=original_manifest['images']:raise ValueError('ZCode assignment differs from original census manifest')
        for image in manifest['images']:
            path=(original/image['image']).resolve()
            if not path.is_relative_to(original.resolve()) or sha(path)!=image['artifact_hash']:raise ValueError('Original census image changed')
            keys.extend(unit['key'] for unit in image['units'])
    if len(keys)!=len(set(keys)) or len(keys)!=prepared['pending_units']:raise ValueError('ZCode assignment key count changed')
    return policy,{'policy_sha256':sha(recovery/'census-assignment-policy.json'),'recovery_sha256':sha(recovery/'recovery.json'),'pilot_acceptance_sha256':sha(recovery/'pilot-acceptance.json'),'source_adapter_sha256':sha(Path(__file__)),'validator_sha256':sha(Path(validator.__file__))}

def parse_batch(batch,original,policy):
    if any((original/name).exists() for name in ('output.json','execution.json','output.err','doctor.json')):
        raise ValueError('Both agy and ZCode evidence assigned to one batch; reconcile explicitly')
    labels,_=validator.validate(batch)
    raw=load(batch/'review.json');assignment=policy['batches'][batch.name]
    if raw.get('assignment_prompt_sha256')!=assignment['prompt_sha256']:
        raise ValueError('Review does not identify its assigned ZCode prompt')
    started=validator.review_time(raw['utc_start'])
    assigned=datetime.datetime.fromisoformat(policy['utc'].replace('Z','+00:00'))
    if started<assigned:raise ValueError('ZCode census review predates accepted assignment')
    transcript=batch/'transcript.txt'
    if not transcript.is_file() or not transcript.read_text(encoding='utf-8-sig').strip():raise ValueError('Missing supplied ZCode transcript')
    manifest=load(batch/'manifest.json')
    expected={u['key']:(u,image) for image in manifest['images'] for u in image['units']}
    seen={key:{**item,'reviewer':'machine:zcode-owner-reported/'+validator.MODEL,'reviewed_at_utc':validator.review_time(raw['utc_end']).isoformat(),'reported_timestamps':{k:raw[k] for k in ('utc_start','utc_end')},'review_source':'owner-run ZCode','model_identity_source':raw['model_identity_source'],'review_sha256':sha(batch/'review.json'),'assignment_prompt_sha256':assignment['prompt_sha256'],'transcript_sha256':sha(transcript)} for key,item in labels.items()}
    return seen,expected
