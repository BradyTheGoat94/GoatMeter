import pathlib,sys,unittest
sys.path.insert(0,str(pathlib.Path(__file__).parents[2]/'tools'))
from calibration_events import confirmed_hits,logged_flags
from calibrate_skill_hits import calibrate
class FlagCalibrationTest(unittest.TestCase):
 def flags(self,back=False):
  return {'rawType':'3','mods':'0x04','direction':'0x01' if back else '0x00','crit':'True','perfect':'True','double':'False','parry':'False','back':str(back),'front':'False'}
 def test_crit_and_perfect_and_back_are_preserved(self):
  self.assertEqual(logged_flags(self.flags(True)),(True,False,True,False,True,False))
  f=self.flags();f['crit']='False';self.assertIsNone(logged_flags(f))
 def fixture(self):
  rows=[]
  for i in range(80):
   stamp=f'2026-10-01T00:{i//60:02d}:{i%60:02d}Z';f=self.flags(i%2==0)
   rows += [stamp+'|tag=damageFlags|actor=privateActor|target=privateTarget|skill=SyntheticSkill|'+ '|'.join(k+'='+v for k,v in f.items()),stamp+f'|tag=damage|src=privateActor|tgt=privateTarget|skill=SyntheticSkill|amount={200 if i%2==0 else 100}|type=Crit|raw={i}',stamp+'|tag=resolvedCombat|entity=privateActor|target=privateTarget|scope=privateScope|class=Templar|confirmed=True']
  return rows
 def test_combined_flags_split_same_primary_type(self):
  hits=list(confirmed_hits(self.fixture()));self.assertEqual(len(hits),80)
  r=calibrate(self.fixture());self.assertEqual(r['cohortCount'],2);self.assertEqual({x['trainingMedianHit'] for x in r['cohorts']},{100,200});self.assertTrue(all(x['heldOutTotalDamageErrorPercent']==0 for x in r['cohorts']))
 def test_missing_flags_are_not_assumed_normal(self):
  r=calibrate([s for s in self.fixture() if '|tag=damageFlags|' not in s]);self.assertEqual(r['cohortCount'],1);self.assertIsNone(r['cohorts'][0]['loggedFlags'])
 def test_scope_separates_reused_actor_ids(self):
  rows=self.fixture();rows=[s.replace('scope=privateScope','scope=secondScope') if i>=len(rows)//2 else s for i,s in enumerate(rows)]
  r=calibrate(rows,minimum_hits=15);self.assertEqual(r['cohortCount'],4)
