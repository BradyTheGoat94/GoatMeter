import pathlib,sys,unittest,json
sys.path.insert(0,str(pathlib.Path(__file__).parents[2]/'tools'))
from build_empirical_replay import build_bundle
class BundleTest(unittest.TestCase):
 def fixture(self):
  rows=[]
  for i in range(50):
   t=f'2026-10-01T00:00:{i:02d}Z';rows += [t+f'|tag=damage|src=privateActor|tgt=privateTarget|skill=Synthetic|amount=100|type=Direct|raw={i}',t+'|tag=resolvedCombat|entity=privateActor|target=privateTarget|class=Templar|confirmed=True|scope=PrivateScope|name=PrivateName']
  return rows
 def test_bundle_no_identifiers_and_holdout(self):
  b=build_bundle(self.fixture());self.assertEqual(b['models'][0]['trainingHits'],35);self.assertEqual(len(b['timeline']),15);self.assertEqual(b['observed']['coveredDamage'],1500);self.assertFalse(b['restrictions']['allowNewRotation'])
  for private in ['privateActor','privateTarget','PrivateScope','PrivateName']:self.assertNotIn(private,json.dumps(b))
 def test_insufficient_or_wrong_class_fails(self):
  self.assertRaises(ValueError,build_bundle,[]);self.assertRaises(ValueError,build_bundle,self.fixture(),'Assassin')
 def test_absolute_error_does_not_cancel(self):
  rows=self.fixture()
  for i in range(35,49):rows[2*i]=rows[2*i].replace('amount=100',f'amount={0 if i%2 else 200}')
  b=build_bundle(rows);self.assertEqual(b['observed']['coveredDamage'],1500);self.assertEqual(b['diagnostics']['sumAbsoluteHitError'],1400);self.assertAlmostEqual(b['diagnostics']['coveredHitWeightedAbsoluteErrorPercent'],100*1400/1500)
  s=b['diagnostics']['skills'][0];self.assertEqual(s['coveredHits'],15);self.assertEqual(s['knownFlagCoveredHits'],0);self.assertEqual(s['sumAbsoluteHitError'],1400);self.assertEqual(s['coveredTotalErrorPercent'],0)
 def test_excluded_skill_is_not_reported_as_zero_error(self):
  rows=self.fixture();rows[-2]=rows[-2].replace('skill=Synthetic','skill=Unseen')
  b=build_bundle(rows);s=next(s for s in b['diagnostics']['skills'] if s['skill']=='Unseen')
  self.assertEqual(s['coveredHits'],0);self.assertIsNone(s['coveredTotalErrorPercent']);self.assertIsNone(s['coveredHitWeightedAbsoluteErrorPercent'])
