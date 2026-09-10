import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import hashlib
from PIL import Image

spec=importlib.util.spec_from_file_location('collector',Path(__file__).with_name('collect-machine-census.py'))
collector=importlib.util.module_from_spec(spec); spec.loader.exec_module(collector)

class CollectorBoundaryTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup); self.batch=Path(self.temp.name)
        image=self.batch/'icon.png'; Image.new('RGBA',(2,2),(0,0,0,0)).save(image)
        self.key='pub-test|'+hashlib.sha256(image.read_bytes()).hexdigest()
        self.manifest={'images':[{'image':'icon.png','artifact_hash':self.key.split('|')[1],'pixel':{'opaque_pct':0},'units':[{'key':self.key}]}]}
        self.label={'key':self.key,'label':'blank','reason':'Fully transparent image.','verified_live':False,'image_seen':True}
        self.result={'worker':'antigravity','status':'ok','run_id':'fixture','command':{'argv':['agy','--model','fixture-model','--effort','high']},'result':{'summary':None,'summary_truncated':False}}
        (self.batch/'execution.json').write_text(json.dumps({'agy_version':'test','utc_start':'2026-09-08T00:00:00Z','utc_end':'2026-09-08T00:01:00Z','exit_code':0}),encoding='utf-8')
    def save(self,labels=None,status='DONE'):
        self.result['result']['summary']=json.dumps({'status':status,'response':json.dumps(labels if labels is not None else [self.label])})
        (self.batch/'manifest.json').write_text(json.dumps(self.manifest),encoding='utf-8')
        (self.batch/'output.json').write_text(json.dumps({'results':[self.result]}),encoding='utf-8')
        path=self.batch/'execution.json'; execution=json.loads(path.read_text(encoding='utf-8'))
        execution.update(inputs_unchanged=True,input_hashes={n:hashlib.sha256((self.batch/n).read_bytes()).hexdigest() for n in ('manifest.json','icon.png')})
        path.write_text(json.dumps(execution),encoding='utf-8')
    def test_accepts_complete_inspected_batch(self):
        self.save(); labels,_=collector.parse_batch(self.batch)
        self.assertEqual(labels[self.key]['reviewer'],'machine:antigravity-test/fixture-model')
    def test_rejects_unseen_image(self):
        self.label['image_seen']=False; self.save()
        with self.assertRaisesRegex(ValueError,'inspection'): collector.parse_batch(self.batch)
    def test_rejects_duplicate_key(self):
        self.save([self.label,self.label])
        with self.assertRaisesRegex(ValueError,'duplicate'): collector.parse_batch(self.batch)
    def test_rejects_missing_key(self):
        self.save([])
        with self.assertRaisesRegex(ValueError,'incomplete'): collector.parse_batch(self.batch)
    def test_rejects_missing_model(self):
        self.result['command']['argv']=['agy']; self.save()
        with self.assertRaisesRegex(ValueError,'model'): collector.parse_batch(self.batch)
    def test_rejects_worker_error_even_with_labels(self):
        self.save(status='ERROR')
        with self.assertRaisesRegex(ValueError,'ERROR'): collector.parse_batch(self.batch)
    def test_rejects_changed_image(self):
        self.save(); (self.batch/'icon.png').write_bytes(b'changed')
        with self.assertRaisesRegex(ValueError,'hash mismatch'): collector.parse_batch(self.batch)
    def test_rejects_transparency_contradiction(self):
        self.label['label']='correct'; self.save()
        with self.assertRaisesRegex(ValueError,'transparent'): collector.parse_batch(self.batch)
    def test_rounded_opacity_does_not_force_blank(self):
        image=self.batch/'icon.png'
        sparse=Image.new('RGBA',(256,256),(0,0,0,0)); sparse.putpixel((0,0),(255,0,0,255)); sparse.save(image)
        digest=hashlib.sha256(image.read_bytes()).hexdigest()
        self.key='pub-test|'+digest; self.label.update(key=self.key,label='unusable',reason='Only one visible red pixel remains.')
        self.manifest['images'][0].update(artifact_hash=digest,units=[{'key':self.key}])
        self.save(); labels,_=collector.parse_batch(self.batch)
        self.assertEqual(labels[self.key]['label'],'unusable')
    def test_collection_refuses_any_existing_derived_output(self):
        for name in ('review-queue.machine.csv','labels.validated.json','spot-check.html','spot-check-keys.json','census-validation.json'):
            path=self.batch/name; path.write_text('preserve')
            with self.assertRaisesRegex(ValueError,'overwrite'): collector.collect(self.batch,self.batch/'absent.csv')
            self.assertEqual('preserve',path.read_text()); path.unlink()
    def test_rejects_failed_invocation(self):
        self.save(); path=self.batch/'execution.json'; execution=json.loads(path.read_text())
        execution['exit_code']=1; path.write_text(json.dumps(execution))
        with self.assertRaisesRegex(ValueError,'exit successfully'): collector.parse_batch(self.batch)
    def test_rejects_invalid_time_order(self):
        self.save(); path=self.batch/'execution.json'; execution=json.loads(path.read_text())
        execution['utc_end']='2026-09-07T00:00:00Z'; path.write_text(json.dumps(execution))
        with self.assertRaisesRegex(ValueError,'timestamps'): collector.parse_batch(self.batch)
    def test_accepts_transport_progress_before_exact_array(self):
        labels=[self.label]
        response='Checking live site...\nWaiting for verification to complete.\n'+json.dumps(labels)
        self.assertEqual(labels,collector.decode_labels(response))
    def test_rejects_failure_in_transport_progress(self):
        with self.assertRaisesRegex(ValueError,'Failure'):
            collector.decode_labels('Checking failed image access...\n'+json.dumps([self.label]))
    def test_accepts_exact_live_request_transport_messages(self):
        prefix='I have launched the live verification request for `httpstat.us` and will proceed once the background command completes.\nI am waiting for the live verification request to finish.\n'
        self.assertEqual([self.label],collector.decode_labels(prefix+json.dumps([self.label])))
    def test_rejects_repository_search_even_after_checking(self):
        with self.assertRaisesRegex(ValueError,'Evidence-scope'):
            collector.decode_labels('Checking existing labels in the repository.\n'+json.dumps([self.label]))
    def test_rejects_unrecognized_preamble(self):
        with self.assertRaisesRegex(ValueError,'Unrecognized'):
            collector.decode_labels('I could only inspect some images.\n'+json.dumps([self.label]))
    def test_rejects_trailing_prose_after_array(self):
        with self.assertRaises(json.JSONDecodeError):
            collector.decode_labels('Checking live site...\n'+json.dumps([self.label])+'\nMore notes')

if __name__=='__main__': unittest.main()
