"""Enforce recorded census prompt versions without changing historical evidence."""
import hashlib
import json
from pathlib import Path

def sha(data): return hashlib.sha256(data).hexdigest()

def inside(kit,name):
    path=(kit/name).resolve()
    if not path.is_relative_to(kit.resolve()): raise ValueError('Prompt policy path outside kit')
    return path

def validate_prompts(kit,batches,complete=True,pending_prompt=None):
    policy_path=kit/'census-prompt-policy.json'
    if policy_path.exists():
        policy=json.loads(policy_path.read_text(encoding='utf-8'))
        if policy.get('status')!='accepted' or not policy.get('accepted_by') or not policy.get('reason'):
            raise ValueError('Missing prompt policy acceptance')
        assignments=policy['batches']
        if complete and set(assignments)!={batch.name for batch in batches}:
            raise ValueError('Prompt policy does not cover exact census batch set')
        templates={}
        for version,item in policy['templates'].items():
            data=inside(kit,item['path']).read_bytes()
            if sha(data)!=item['sha256']: raise ValueError('Prompt template hash mismatch')
            acceptance=json.loads(inside(kit,policy['acceptance_files'][version]).read_text(encoding='utf-8'))
            if acceptance.get('status') not in ('accepted_to_start_census','accepted_to_resume_census'):
                raise ValueError('Prompt version lacks accepted pilot')
            templates[version]=data.decode('utf-8').replace('\r\n','\n')
        for batch in batches:
            if batch.name not in assignments: raise ValueError('Batch missing from prompt policy')
            expected=templates[assignments[batch.name]].replace('BATCH_DIRECTORY',str(batch.resolve()))
            actual=pending_prompt if pending_prompt is not None else (batch/'prompt.txt').read_text(encoding='utf-8')
            if actual!=expected: raise ValueError('Census prompt differs from accepted version: '+batch.name)
        return {'policy_sha256':sha(policy_path.read_bytes()),'versions':assignments}
    # An unamended kit must use exactly the template recorded when created.
    metadata=json.loads((kit/'kit.json').read_text(encoding='utf-8'))
    original=metadata.get('prompt_template_sha256_normalized',metadata['tooling_sha256']['machine-census-prompt.txt'])
    for batch in batches:
        actual=pending_prompt if pending_prompt is not None else (batch/'prompt.txt').read_text(encoding='utf-8')
        normalized=actual.replace(str(batch.resolve()),'BATCH_DIRECTORY')
        if sha(normalized.encode('utf-8'))!=original: raise ValueError('Unapproved census prompt drift: '+batch.name)
    return {'original_template_sha256':original}
