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
