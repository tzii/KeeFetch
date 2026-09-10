import hashlib,json,tempfile,unittest
from pathlib import Path
from machine_review_policy import validate_prompts

class PromptPolicyTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        # Match real batch generation, including Windows runner short TEMP paths.
        self.kit=Path(self.temp.name).resolve();self.batch=self.kit/'batch-01';self.batch.mkdir()
        self.template='Inspect BATCH_DIRECTORY/manifest.json.\n'
        self.write('kit.json',{'tooling_sha256':{'machine-census-prompt.txt':hashlib.sha256(self.template.encode()).hexdigest()}})
        (self.batch/'prompt.txt').write_text(self.template.replace('BATCH_DIRECTORY',str(self.batch)),encoding='utf-8')
    def write(self,path,value): (self.kit/path).write_text(json.dumps(value),encoding='utf-8')
    def policy(self):
        (self.kit/'template.txt').write_text(self.template,encoding='utf-8',newline='')
        self.write('acceptance.json',{'status':'accepted_to_resume_census'})
        self.value={'status':'accepted','accepted_by':'test','reason':'New inspected pilots','batches':{'batch-01':'v2'},'templates':{'v2':{'path':'template.txt','sha256':hashlib.sha256(self.template.encode()).hexdigest()}},'acceptance_files':{'v2':'acceptance.json'}}
        self.write('census-prompt-policy.json',self.value)
    def test_original_template_matches(self): validate_prompts(self.kit,[self.batch])
    def test_windows_template_line_endings_keep_recorded_identity(self):
        self.write('kit.json',{'tooling_sha256':{'machine-census-prompt.txt':hashlib.sha256(self.template.replace('\n','\r\n').encode()).hexdigest()},'prompt_template_sha256_normalized':hashlib.sha256(self.template.encode()).hexdigest()})
        validate_prompts(self.kit,[self.batch])
    def test_unapproved_drift_rejected(self):
        (self.batch/'prompt.txt').write_text('Different rubric')
        with self.assertRaisesRegex(ValueError,'drift'): validate_prompts(self.kit,[self.batch])
    def test_explicit_amendment_matches(self):
        self.policy();validate_prompts(self.kit,[self.batch])
    def test_rejects_modified_prompt_despite_valid_local_hashes(self):
        self.policy();(self.batch/'prompt.txt').write_text('Different rubric')
        with self.assertRaisesRegex(ValueError,'accepted version'): validate_prompts(self.kit,[self.batch])
    def test_rejects_incomplete_assignments(self):
        self.policy();self.value['batches']['batch-02']='v2';self.write('census-prompt-policy.json',self.value)
        with self.assertRaisesRegex(ValueError,'exact census'): validate_prompts(self.kit,[self.batch])
    def test_rejects_unaccepted_pilot(self):
        self.policy();self.write('acceptance.json',{'status':'pending'})
        with self.assertRaisesRegex(ValueError,'accepted pilot'): validate_prompts(self.kit,[self.batch])
    def test_rejects_template_mutation(self):
        self.policy();(self.kit/'template.txt').write_text('Changed')
        with self.assertRaisesRegex(ValueError,'hash mismatch'): validate_prompts(self.kit,[self.batch])
    def test_pending_invocation_uses_accepted_version(self):
        self.policy()
        validate_prompts(self.kit,[self.batch],complete=False,pending_prompt=self.template.replace('BATCH_DIRECTORY',str(self.batch)))
        with self.assertRaisesRegex(ValueError,'accepted version'): validate_prompts(self.kit,[self.batch],complete=False,pending_prompt='changed')

if __name__=='__main__':unittest.main()
