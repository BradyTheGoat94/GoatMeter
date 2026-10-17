import importlib.util,pathlib,unittest
spec=importlib.util.spec_from_file_location('build_hits',pathlib.Path('tools/analyze_build_hits.py'));module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
class HitAuditTests(unittest.TestCase):
 def test_confirmed_pairs_dedup_and_no_macro_attribution(self):
  lines=[]
  for sec in [0,0,5]:
   time=f'2026-10-01T00:00:{sec:02d}Z';lines += [f'{time}|tag=damage|src=1|tgt=2|skill=Synthetic Hit|amount=100|raw=AB',f'{time}|tag=resolvedCombat|entity=1|target=2|confirmed=True|class=Templar|name=Synthetic']
  report=module.analyze(lines);self.assertEqual(report['confirmedHitEventsByClass'],{'Templar':2});self.assertEqual(report['skills'][0]['capturedDamage'],200);self.assertEqual(report['skills'][0]['positiveHitIntervalMedianSeconds'],5)
  self.assertNotIn('name',report['skills'][0]);self.assertTrue(any('not establish cooldown efficiency' in x for x in report['limits']))
 def test_unknown_identity_cannot_become_class_evidence(self):
  report=module.analyze(['2026-10-01T00:00:00Z|tag=damage|src=1|tgt=2|skill=Hit|amount=100','2026-10-01T00:00:00Z|tag=resolvedCombat|entity=1|target=2|confirmed=False|class=Templar']);self.assertEqual(report['skills'],[])
if __name__=='__main__':unittest.main()
