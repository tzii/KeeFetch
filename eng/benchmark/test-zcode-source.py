"""Boundary and integration tests for combining independently attributed reviews."""
import csv
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import unittest
from PIL import Image
import zcode_review_source as source

spec=importlib.util.spec_from_file_location('collector',Path(__file__).with_name('collect-machine-census.py'))
collector=importlib.util.module_from_spec(spec);spec.loader.exec_module(collector)

class ZCodeSourceTests(unittest.TestCase):
    def write(self,path,value):path.write_text(json.dumps(value),encoding='utf-8')
    def make_batch(self,path,name,color):
        path.mkdir(parents=True)
        Image.new('RGBA',(2,2),color).save(path/'icon.png');digest=source.sha(path/'icon.png')
        unit={'key':name+'|'+digest,'fixture_id':name,'artifact_hash':digest,'input_url':'https://'+name+'.example/','category':'test'}
        self.write(path/'manifest.json',{'batch':path.name,'images':[{'image':'icon.png','artifact_hash':digest,'units':[unit]}]})
        self.write(path/'input-hashes.json',{n:source.sha(path/n) for n in ('manifest.json','icon.png')})
        return unit
    def make_review(self,path):
        manifest=source.load(path/'manifest.json');key=manifest['images'][0]['units'][0]['key']
        review={'schema_version':1,'harness':'ZCode','actual_model_id':source.validator.MODEL,'model_identity_source':'Fixture harness declaration','utc_start':'2026-09-09T01:00:00Z','utc_end':'2026-09-09T01:01:00Z','input_hashes':source.load(path/'input-hashes.json'),'inputs_unchanged':True,'labels':[{'key':key,'label':'ambiguous','reason':'Visible red square without an identifiable mark.','image_seen':True,'verified_live':False}]}
        self.write(path/'review.json',review);(path/'transcript.txt').write_text('Reconstructed fixture transcript',encoding='utf-8')
        return review
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        root=Path(self.temp.name);self.parent=root/'parent';self.recovery=root/'recovery';self.parent.mkdir();self.recovery.mkdir()
        self.original=self.parent/'batch-02';self.batch=self.recovery/'batch-02'
        self.unit=self.make_batch(self.original,'red',(255,0,0,255));shutil.copytree(self.original,self.batch)
        (self.parent/'prompt-template-restricted.txt').write_text('Rubric\n',encoding='utf-8')
        template='Assigned BATCH_DIRECTORY\nRubric\n';(self.recovery/'census-prompt-template.txt').write_text(template,encoding='utf-8')
        (self.batch/'prompt.txt').write_text(template.replace('BATCH_DIRECTORY',str(self.batch.resolve())),encoding='utf-8')
        self.review=self.make_review(self.batch);self.review['assignment_prompt_sha256']=source.sha(self.batch/'prompt.txt');self.write(self.batch/'review.json',self.review)
        reports={};input_files={}
        for name in ('pilot-a','pilot-b'):
            pilot=self.recovery/name;self.make_batch(pilot,'pilot',(255,0,0,255));self.make_review(pilot)
            (self.recovery/(name+'-prompt.txt')).write_text('Pilot '+name,encoding='utf-8')
            _,report=source.validator.validate(pilot)
            reports[name]={**report,'transcript_sha256':source.sha(pilot/'transcript.txt'),'prompt_sha256':source.sha(self.recovery/(name+'-prompt.txt'))}
            input_files[name]=source.sha(pilot/'input-hashes.json')
        self.write(self.recovery/'pilot-comparison.json',{'pilots':reports})
        self.write(self.recovery/'pilot-acceptance.json',{'status':'accepted_to_start_census','reported_model':source.validator.MODEL,'comparison_sha256':source.sha(self.recovery/'pilot-comparison.json'),'input_hash_files':input_files})
        hashes=source.load(self.batch/'input-hashes.json')
        self.write(self.recovery/'recovery.json',{'parent_kit':str(self.parent),'pending_batches':['batch-02'],'pending_units':1,'input_hashes':{'batch-02':hashes}})
        self.policy={'status':'accepted','accepted_by':'Fixture inspector','reason':'Fixture direct inspection','utc':'2026-09-09T00:00:00Z','model':source.validator.MODEL,'recovery_sha256':source.sha(self.recovery/'recovery.json'),'pilot_acceptance_sha256':source.sha(self.recovery/'pilot-acceptance.json'),'template_sha256':source.sha(self.recovery/'census-prompt-template.txt'),'rubric_sha256':source.sha(self.parent/'prompt-template-restricted.txt'),'batches':{'batch-02':{'prompt_sha256':source.sha(self.batch/'prompt.txt'),'input_hashes':hashes}}}
        self.write(self.recovery/'census-assignment-policy.json',self.policy)
    def test_valid_assignment_preserves_distinct_provenance(self):
        policy,_=source.validate_policy(self.recovery,self.parent);labels,_=source.parse_batch(self.batch,self.original,policy)
        item=labels[self.unit['key']]
        self.assertEqual('machine:zcode-owner-reported/'+source.validator.MODEL,item['reviewer'])
        self.assertNotIn('crew_run_id',item);self.assertNotIn('effort',item)
    def test_changed_accepted_pilot_rejected(self):
        path=self.recovery/'pilot-a/review.json';review=source.load(path);review['labels'][0]['reason']='Different observation';self.write(path,review)
        with self.assertRaisesRegex(ValueError,'pilot evidence'):source.validate_policy(self.recovery,self.parent)
    def test_prompt_drift_rejected_even_if_individual_hash_updated(self):
        (self.batch/'prompt.txt').write_text('Changed rubric',encoding='utf-8')
        self.policy['batches']['batch-02']['prompt_sha256']=source.sha(self.batch/'prompt.txt');self.write(self.recovery/'census-assignment-policy.json',self.policy)
        with self.assertRaisesRegex(ValueError,'assigned ZCode prompt'):source.validate_policy(self.recovery,self.parent)
    def test_original_manifest_drift_rejected(self):
        path=self.original/'manifest.json';manifest=source.load(path);manifest['images'][0]['units'][0]['input_url']='https://other.example/';self.write(path,manifest)
        with self.assertRaisesRegex(ValueError,'original census manifest'):source.validate_policy(self.recovery,self.parent)
    def test_changed_recovery_rejected(self):
        path=self.recovery/'recovery.json';raw=source.load(path);raw['pending_units']=2;self.write(path,raw)
        with self.assertRaisesRegex(ValueError,'recovery identity'):source.validate_policy(self.recovery,self.parent)
    def test_duplicate_source_rejected(self):
        (self.original/'output.json').write_text('{}',encoding='utf-8')
        with self.assertRaisesRegex(ValueError,'Both agy and ZCode'):source.parse_batch(self.batch,self.original,self.policy)
    def test_review_before_assignment_rejected(self):
        self.review['utc_start']='2026-09-08T23:00:00Z';self.write(self.batch/'review.json',self.review)
        with self.assertRaisesRegex(ValueError,'predates'):source.parse_batch(self.batch,self.original,self.policy)
    def test_review_must_identify_prompt(self):
        self.review.pop('assignment_prompt_sha256');self.write(self.batch/'review.json',self.review)
        with self.assertRaisesRegex(ValueError,'identify its assigned'):source.parse_batch(self.batch,self.original,self.policy)
    def test_missing_transcript_rejected(self):
        (self.batch/'transcript.txt').unlink()
        with self.assertRaisesRegex(ValueError,'transcript'):source.parse_batch(self.batch,self.original,self.policy)
    def test_complete_combined_collection_retains_both_sources(self):
        agy=self.parent/'batch-01';unit=self.make_batch(agy,'blue',(0,0,255,255));(agy/'prompt.txt').write_text('Rubric\n',encoding='utf-8')
        self.write(self.parent/'agy-pilot-acceptance.json',{'status':'accepted_to_start_census'})
        self.write(self.parent/'census-prompt-policy.json',{'status':'accepted','accepted_by':'Fixture inspector','reason':'Fixture','batches':{'batch-01':'restricted','batch-02':'restricted'},'templates':{'restricted':{'path':'prompt-template-restricted.txt','sha256':source.sha(self.parent/'prompt-template-restricted.txt')}},'acceptance_files':{'restricted':'agy-pilot-acceptance.json'}})
        self.write(agy/'execution.json',{'agy_version':'fixture','exit_code':0,'utc_start':'2026-09-09T01:00:00Z','utc_end':'2026-09-09T01:01:00Z','inputs_unchanged':True,'input_hashes':{n:source.sha(agy/n) for n in ('manifest.json','icon.png','prompt.txt')}})
        self.write(agy/'output.json',{'results':[{'worker':'antigravity','status':'ok','run_id':'fixture','command':{'argv':['agy','--model','gemini-3.8-flash-high','--effort','high']},'result':{'summary':json.dumps({'status':'DONE','response':json.dumps([{'key':unit['key'],'label':'ambiguous','reason':'Visible blue square without an identifiable mark.','image_seen':True,'verified_live':False}])})}}]})
        queue=self.parent/'queue.csv'
        with queue.open('w',encoding='utf-8',newline='') as file:
            writer=csv.DictWriter(file,fieldnames=['fixture_id','artifact_hash','review_label','reviewer','reviewed_at_utc']);writer.writeheader()
            for u in (unit,self.unit):writer.writerow({'fixture_id':u['fixture_id'],'artifact_hash':u['artifact_hash']})
        collector.collect(self.parent,queue,self.recovery)
        labels=source.load(self.parent/'labels.validated.json');record=source.load(self.parent/'census-validation.json')
        self.assertEqual(2,len(labels));self.assertTrue(labels[unit['key']]['reviewer'].startswith('machine:antigravity-'))
        self.assertTrue(labels[self.unit['key']]['reviewer'].startswith('machine:zcode-owner-reported/'))
        self.assertEqual({'crew/antigravity','owner-run ZCode'},{item['harness'] for item in record['batch_sources'].values()})
        self.assertTrue((self.parent/'review-queue.machine.csv').exists())

if __name__=='__main__':unittest.main()
