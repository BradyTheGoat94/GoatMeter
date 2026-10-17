import importlib.util,json,pathlib,unittest,sys
sys.path.insert(0,str(pathlib.Path(__file__).parents[2]/'tools'))
spec=importlib.util.spec_from_file_location('calibration',pathlib.Path(__file__).parents[2]/'tools/calibrate_skill_hits.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class CalibrationTest(unittest.TestCase):
 def fixture(self):
  rows=[]
  for i in range(40):
   stamp=f'2026-10-01T00:00:{i:02d}Z'
   rows += [f'{stamp}|tag=damage|src=privateActor|tgt=privateTarget|skill=SyntheticSkill|amount=100|raw={i}|type=uninterpreted',f'{stamp}|tag=resolvedCombat|entity=privateActor|target=privateTarget|confirmed=True|class=Templar|name=PrivateName']
  return rows
 def test_holdout_and_privacy(self):
  r=m.calibrate(self.fixture());self.assertEqual(r['cohortCount'],1);self.assertEqual(r['cohorts'][0]['heldOutMedianAbsolutePercentError'],0);self.assertFalse(r['gameFormulaValidated']);s=json.dumps(r)
  for private in ['privateActor','privateTarget','PrivateName']:self.assertNotIn(private,s)
 def test_unconfirmed_excluded(self):
  self.assertEqual(m.calibrate([s.replace('confirmed=True','confirmed=False') for s in self.fixture()])['cohortCount'],0)
