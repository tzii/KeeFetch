import hashlib,importlib.util,json,tempfile,unittest
from pathlib import Path
from PIL import Image
s=importlib.util.spec_from_file_location('v',Path(__file__).with_name('validate-zcode-review.py'));v=importlib.util.module_from_spec(s);s.loader.exec_module(v)
class ZCodeReviewTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.b=Path(self.temp.name)
        Image.new('RGBA',(2,2),(255,0,0,255)).save(self.b/'icon.png');h=v.sha(self.b/'icon.png');self.key='fixture|'+h
        self.write('manifest.json',{'images':[{'image':'icon.png','artifact_hash':h,'units':[{'key':self.key}]}]})
        hashes={n:v.sha(self.b/n) for n in ('manifest.json','icon.png')};self.write('input-hashes.json',hashes)
        self.review={'schema_version':1,'harness':'ZCode','actual_model_id':v.MODEL,'model_identity_source':'Test fixture harness report','utc_start':'2026-09-09T00:00:00Z','utc_end':'2026-09-09T00:01:00Z','input_hashes':hashes,'inputs_unchanged':True,'labels':[{'key':self.key,'label':'ambiguous','reason':'Visible red square without identifiable brand.','image_seen':True,'verified_live':False}]}
    def write(self,n,x):(self.b/n).write_text(json.dumps(x),encoding='utf-8')
    def check(self):self.write('review.json',self.review);return v.validate(self.b)
    def test_valid_evidence_still_requires_inspection(self):self.assertEqual('structurally_valid_requires_direct_inspection',self.check()[1]['status'])
    def test_different_model_rejected(self):
        self.review['actual_model_id']='guessed'
        with self.assertRaisesRegex(ValueError,'model'):self.check()
    def test_changed_input_rejected(self):
        (self.b/'icon.png').write_bytes(b'changed')
        with self.assertRaisesRegex(ValueError,'input hash'):self.check()
    def test_incomplete_labels_rejected(self):
        self.review['labels']=[]
        with self.assertRaisesRegex(ValueError,'Incomplete'):self.check()
    def test_unseen_image_rejected(self):
        self.review['labels'][0]['image_seen']=False
        with self.assertRaisesRegex(ValueError,'inspection'):self.check()
    def test_duplicate_label_rejected(self):
        self.review['labels']*=2
        with self.assertRaisesRegex(ValueError,'duplicate'):self.check()
    def test_invalid_time_rejected(self):
        self.review['utc_end']='2026-09-08T00:01:00Z'
        with self.assertRaisesRegex(ValueError,'timestamps'):self.check()
    def hash_layouts(self,hashes):
        return [
            {'before':dict(hashes),'after':dict(hashes)},
            {n:{'before':h,'after':h} for n,h in hashes.items()},
            {n:{'sha256_before':h,'sha256_after':h} for n,h in hashes.items()},
            [{'path':n,'sha256_before':h,'sha256_after':h} for n,h in hashes.items()]]
    def test_delivered_hash_layouts_preserve_raw_evidence(self):
        hashes=self.review['input_hashes']
        for layout in self.hash_layouts(hashes):
            with self.subTest(layout=layout):
                self.review['input_hashes']=layout;self.check()
                self.assertEqual(layout,v.load(self.b/'review.json')['input_hashes'])
    def test_before_and_after_must_both_match(self):
        hashes=self.review['input_hashes']
        for layout in self.hash_layouts(hashes):
            for field in ('before','after'):
                bad=json.loads(json.dumps(layout))
                if isinstance(bad,list):bad[0]['sha256_'+field]='0'*64
                elif set(bad)=={'before','after'}:bad[field]['manifest.json']='0'*64
                else:
                    entry=bad['manifest.json'];entry[field if field in entry else 'sha256_'+field]='0'*64
                self.review['input_hashes']=bad
                with self.subTest(layout=bad),self.assertRaisesRegex(ValueError,'input identity'):self.check()
    def test_hash_layouts_reject_missing_extra_duplicate_or_unknown_fields(self):
        hashes=self.review['input_hashes'];layouts=self.hash_layouts(hashes)
        malformed=[[],layouts[3]+[layouts[3][0]],{'before':hashes},
                   {**hashes,'extra':'0'*64},
                   {'before':hashes,'after':{**hashes,'extra':'0'*64}},
                   {n:{'before':h,'after':h,'ignored':h} for n,h in hashes.items()}]
        for bad in malformed:
            self.review['input_hashes']=bad
            with self.subTest(layout=bad),self.assertRaises(ValueError):self.check()
    def test_separate_after_inspection_hash_cannot_contradict_inputs(self):
        self.review['input_hashes_recheck_after_inspection']={**self.review['input_hashes'],'manifest.json':'0'*64}
        with self.assertRaisesRegex(ValueError,'input identity'):self.check()
    def test_named_metadata_hashes_are_verified(self):
        (self.b/'prompt.txt').write_text('Assigned prompt',encoding='utf-8')
        self.review['input_hashes']={**self.review['input_hashes'],**{n:v.sha(self.b/n) for n in ('prompt.txt','input-hashes.json')}}
        self.check()
        (self.b/'prompt.txt').write_text('Changed',encoding='utf-8')
        with self.assertRaisesRegex(ValueError,'input identity'):self.check()
    def test_annotated_timestamps_retain_disclosure(self):
        self.review['utc_start']+=' (approximate; reconstructed from local clock offsets, not captured at first tool call)'
        self.review['utc_end']+=' (captured at the post-write hash re-check)'
        self.assertEqual(self.review['utc_start'],self.check()[1]['reported_timestamps']['utc_start'])
    def test_malformed_or_reversed_annotated_time_rejected(self):
        for value in ('2026-09-08T00:00:00Z (reconstructed)','2026-09-09T00:01:00Z unrelated','2026-09-09T00:01:00Z (unclosed'):
            self.review['utc_end']=value
            with self.subTest(value=value),self.assertRaises(ValueError):self.check()
if __name__=='__main__':unittest.main()
