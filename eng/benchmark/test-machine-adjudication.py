"""Check that candidate relabeling preserves evidence and rejects unsafe inputs."""
import csv
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from PIL import Image

spec=importlib.util.spec_from_file_location('adjudicator',Path(__file__).with_name('apply-machine-adjudication.py'))
adjudicator=importlib.util.module_from_spec(spec); spec.loader.exec_module(adjudicator)

class AdjudicationTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.kit=Path(self.temp.name); self.key='fixture|hash'
        self.old={'label':'acceptable-synthetic','reason':'Original observation','reviewer':'first','reviewed_at_utc':'2026-09-08T00:00:00Z'}
        self.new={**self.old,'label':'correct','reason':'Verified brand mark','reviewer':'second'}
        self.write('labels.validated.json',{self.key:self.old})
        self.write('batch-01/manifest.json',{'images':[{'image':'icon.png','artifact_hash':'hash','units':[{'key':self.key,'category':'android-app','input_url':'androidapp://test.app'}]}]})
        self.write('reask/acceptance.json',{'status':'accepted_for_candidate_adjudication','accepted_by':'machine spot-check','reason':'Verified original image','units':1})
        self.write('reask/output.json',{}); self.write('reask/execution.json',{})
        with (self.kit/'review-queue.machine.csv').open('w',newline='',encoding='utf-8') as file:
            writer=csv.DictWriter(file,fieldnames=['fixture_id','artifact_hash','review_label','reviewer','reviewed_at_utc'])
            writer.writeheader(); writer.writerow({'fixture_id':'fixture','artifact_hash':'hash','review_label':self.old['label']})
        self.mock=patch.object(adjudicator.collector,'parse_batch',return_value=({self.key:self.new},{}))
        self.mock.start(); self.addCleanup(self.mock.stop)
    def write(self,name,value):
        path=self.kit/name; path.parent.mkdir(parents=True,exist_ok=True); path.write_text(json.dumps(value),encoding='utf-8')
    def test_preserves_base_and_records_acceptance_and_android_spot(self):
        before=(self.kit/'labels.validated.json').read_bytes()
        adjudicator.apply(self.kit,['reask'],'candidate')
        self.assertEqual(before,(self.kit/'labels.validated.json').read_bytes())
        report=json.loads((self.kit/'candidate.json').read_text())
        self.assertEqual('correct',report['changes'][0]['after']['label'])
        self.assertEqual(self.key,report['spot_check'][0]['key'])
        self.assertIn('acceptance_sha256',report['accepted_reasks'][0])
        self.assertEqual(64,len(report['collector_sha256']))
    def test_rejects_unaccepted_reask_without_output(self):
        self.write('reask/acceptance.json',{'status':'pending'})
        with self.assertRaisesRegex(ValueError,'acceptance'): adjudicator.apply(self.kit,['reask'],'candidate')
        self.assertFalse((self.kit/'candidate.csv').exists())
    def test_rejects_duplicate_reask(self):
        with self.assertRaisesRegex(ValueError,'Duplicate'): adjudicator.apply(self.kit,['reask','reask'],'candidate')
    def test_rejects_unknown_census_key(self):
        adjudicator.collector.parse_batch.return_value=({'unknown':self.new},{})
        with self.assertRaisesRegex(ValueError,'non-census'): adjudicator.apply(self.kit,['reask'],'candidate')
    def test_rejects_path_escape(self):
        with self.assertRaisesRegex(ValueError,'outside kit'): adjudicator.apply(self.kit,['../outside'],'candidate')
    def test_refuses_overwrite(self):
        self.write('candidate.json',{'preserved':True})
        with self.assertRaisesRegex(ValueError,'overwrite'): adjudicator.apply(self.kit,['reask'],'candidate')

class DirectReviewTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.kit=Path(self.temp.name)
        batch=self.kit/'batch-01';batch.mkdir();Image.new('RGBA',(4,4),(255,0,0,255)).save(batch/'icon.png')
        digest=adjudicator.sha(batch/'icon.png');self.key='fixture|'+digest
        self.current={self.key:{'label':'generic','reviewer':'original'}}
        self.write('labels.validated.json',self.current)
        self.write('batch-01/manifest.json',{'images':[{'image':'icon.png','artifact_hash':digest,'units':[{'key':self.key,'category':'test','input_url':'https://example.test/'}]}]})
        with (self.kit/'review-queue.machine.csv').open('w',newline='',encoding='utf-8') as file:
            writer=csv.DictWriter(file,fieldnames=['fixture_id','artifact_hash','review_label','reviewer','reviewed_at_utc']);writer.writeheader();writer.writerow({'fixture_id':'fixture','artifact_hash':digest})
        self.record={'schema_version':1,'status':'accepted_for_candidate_adjudication','accepted_by':'Codex','reason':'Disclosed fixture adjudication','reviewer':'machine:codex-direct-adjudication','utc':'2026-09-10T00:00:00Z','base_labels_sha256':adjudicator.sha(self.kit/'labels.validated.json'),'input_hashes':{p.relative_to(self.kit).as_posix():adjudicator.sha(p) for p in (batch/'manifest.json',batch/'icon.png')},'labels':[{'key':self.key,'from_label':'generic','label':'ambiguous','reason':'Cannot identify brand','observation':'Visible red square','image_seen':True,'verified_live':False,'references':[]}]}
    def write(self,name,value):(self.kit/name).write_text(json.dumps(value),encoding='utf-8')
    def parse(self):
        self.write('direct.json',self.record);return adjudicator.parse_direct_review(self.kit,self.kit/'direct.json',self.current)
    def test_direct_candidate_retains_distinct_provenance_and_originals(self):
        self.parse();before=(self.kit/'labels.validated.json').read_bytes()
        adjudicator.apply(self.kit,[],'candidate',['direct.json'])
        report=json.loads((self.kit/'candidate.json').read_text());item=report['changes'][0]['after']
        self.assertEqual('machine:codex-direct-adjudication',item['reviewer']);self.assertNotIn('crew_run_id',item)
        self.assertEqual(before,(self.kit/'labels.validated.json').read_bytes());self.assertEqual(1,report['accepted_direct_reviews'][0]['units'])
    def test_rejects_stale_base_or_prior_label(self):
        self.record['base_labels_sha256']='0'*64
        with self.assertRaisesRegex(ValueError,'base labels'):self.parse()
        self.record['base_labels_sha256']=adjudicator.sha(self.kit/'labels.validated.json');self.record['labels'][0]['from_label']='correct'
        with self.assertRaisesRegex(ValueError,'prior label'):self.parse()
    def test_rejects_missing_or_changed_input_and_path_escape(self):
        del self.record['input_hashes']['batch-01/icon.png']
        with self.assertRaisesRegex(ValueError,'omitted'):self.parse()
        self.record['input_hashes']['batch-01/icon.png']='0'*64
        with self.assertRaisesRegex(ValueError,'hash/path'):self.parse()
        self.record['input_hashes']={'../outside':'0'*64}
        with self.assertRaisesRegex(ValueError,'hash/path'):self.parse()
    def test_requires_actual_inspection_reason_and_live_reference(self):
        original=json.loads(json.dumps(self.record))
        for field,value in [('image_seen',False),('reason',''),('observation',''),('verified_live',True),('references',['file:///private'])]:
            self.record=json.loads(json.dumps(original));self.record['labels'][0][field]=value
            with self.subTest(field=field),self.assertRaises(ValueError):self.parse()
    def test_rejects_duplicate_and_non_census_labels(self):
        self.record['labels']*=2
        with self.assertRaisesRegex(ValueError,'duplicate'):self.parse()
        self.record['labels']=[{**self.record['labels'][0],'key':'unknown'}]
        with self.assertRaisesRegex(ValueError,'Unknown'):self.parse()
    def test_requires_accepted_source_and_aware_timestamp(self):
        self.record['status']='pending'
        with self.assertRaisesRegex(ValueError,'acceptance'):self.parse()
        self.record['status']='accepted_for_candidate_adjudication';self.record['utc']='2026-09-10T00:00:00'
        with self.assertRaisesRegex(ValueError,'timezone'):self.parse()

if __name__=='__main__': unittest.main()
