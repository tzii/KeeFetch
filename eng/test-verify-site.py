"""Regression fixtures for failures that the offline website gate must catch."""
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import unittest

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('verifier',ROOT/'eng/verify-site.py')
verifier=importlib.util.module_from_spec(spec); spec.loader.exec_module(verifier)

class WebsiteGateTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.site=Path(self.temp.name)/'site'; shutil.copytree(ROOT/'site',self.site)
    def mutate(self,old,new,filename='index.html'):
        p=self.site/filename; text=p.read_text(encoding='utf-8')
        self.assertIn(old,text); p.write_text(text.replace(old,new,1),encoding='utf-8')
    def assert_rejected(self,expected):
        self.assertTrue(any(expected in e for e in verifier.verify(self.site)),verifier.verify(self.site))
    def test_actual_site_passes(self): self.assertEqual([],verifier.verify(self.site))
    def test_missing_title(self):
        self.mutate('<title>','<not-title>'); self.assert_rejected('missing title')
    def test_missing_description(self):
        self.mutate('name="description"','name="wrong"'); self.assert_rejected('missing description')
    def test_wrong_canonical(self):
        self.mutate('rel="canonical"','rel="alternate"'); self.assert_rejected('canonical')
    def test_duplicate_id(self):
        self.mutate('<footer>','<footer id="main">'); self.assert_rejected('duplicate ID')
    def test_broken_link(self):
        self.mutate('href="getting-started.html"','href="missing.html"'); self.assert_rejected('broken local link')
    def test_cross_page_fragment(self):
        self.mutate('getting-started.html#first-run','getting-started.html#missing'); self.assert_rejected('broken fragment')
    def test_missing_alt(self):
        self.mutate('alt=""',''); self.assert_rejected('image missing alt')
    def test_insecure_external_url(self):
        self.mutate('https://github.com/tzii/KeeFetch','http://github.com/tzii/KeeFetch'); self.assert_rejected('non-HTTPS')
    def test_external_runtime_asset(self):
        self.mutate('src="assets/js/site.js"','src="https://example.org/site.js"'); self.assert_rejected('external runtime asset')
    def test_missing_asset(self):
        self.mutate('src="assets/icons/keefetch-app.png"','src="assets/icons/missing.png"'); self.assert_rejected('broken local link')
    def test_navigation_drift(self):
        self.mutate('href="privacy.html"','href="profiles.html"'); self.assert_rejected('inconsistent primary navigation')
    def test_profile_drift(self):
        self.mutate('data-profile-id="bulk-fast"','data-profile-id="stale"'); self.assert_rejected('profile ID/order mismatch')
    def test_stale_generated_claim(self):
        self.mutate(' s total',' s invented total'); self.assert_rejected('generated content mismatch')
    def test_stale_study_evidence_link(self):
        self.mutate('/blob/19a0c224ca4dbf7042d22c3497e7599f61937c0b/docs/benchmarks/', '/blob/master/docs/benchmarks/', filename='profiles.html')
        self.assert_rejected('generated content mismatch')
    def test_release_tag_mismatch(self):
        path=self.site/'data/release.json'; data=json.loads(path.read_text(encoding='utf-8'))
        data['tag']='v999.0.0'; path.write_text(json.dumps(data),encoding='utf-8')
        self.assert_rejected('release tag/version mismatch')
    def test_escaping_local_link(self):
        self.mutate('href="profiles.html"','href="../private.txt"'); self.assert_rejected('local link escapes site')
    def test_checksum_from_wrong_release(self):
        path=self.site/'data/release.json'; data=json.loads(path.read_text(encoding='utf-8'))
        data['plgxChecksumUrl']=data['plgxChecksumUrl'].replace('/'+data['tag']+'/', '/v0.0.0/')
        path.write_text(json.dumps(data),encoding='utf-8')
        self.assert_rejected('incorrect PLGX checksum URL')
    def test_malformed_checksum(self):
        path=self.site/'data/release.json'; data=json.loads(path.read_text(encoding='utf-8'))
        data['plgxSha256']='not-a-sha256'
        path.write_text(json.dumps(data),encoding='utf-8')
        self.assert_rejected('invalid PLGX checksum')
    def test_preview_profile_snapshot_drift(self):
        path=self.site/'data/profiles-v1.3.json'
        path.write_text(path.read_text(encoding='utf-8').replace('22000','99999'),encoding='utf-8')
        self.assert_rejected('preview profile snapshot checksum mismatch')

if __name__=='__main__': unittest.main()
