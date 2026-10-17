import pathlib,sys,unittest,json
sys.path.insert(0,str(pathlib.Path(__file__).parents[2]/'tools'))
from audit_empirical_contexts import audit_contexts
from build_empirical_replay import build_context
import test_empirical_bundle
from calibration_events import confirmed_hits

class ContextTest(unittest.TestCase):
 def test_separate_contexts_never_pool_damage(self):
  rows=test_empirical_bundle.BundleTest().fixture()
  other=[x.replace('privateActor','secondPrivate').replace('amount=100','amount=900') for x in rows]
  r=audit_contexts(rows+other)
  self.assertEqual(r['partitionCount'],2);self.assertEqual(len(r['usableContexts']),2)
  self.assertTrue(all(x['coveredTotalErrorPercent']==0 for x in r['usableContexts']))
  for identifier in ['privateActor','privateTarget','secondPrivate','PrivateScope','PrivateName']:self.assertNotIn(identifier,json.dumps(r))
 def test_mixed_context_rejected_and_input_not_mutated(self):
  hits=list(confirmed_hits(test_empirical_bundle.BundleTest().fixture()));before=list(hits)
  build_context(hits);self.assertEqual(hits,before)
  hits[-1]={**hits[-1],'partition':('different','actor','target')}
  self.assertRaises(ValueError,build_context,hits)
  self.assertRaises(ValueError,build_context,before,'Templar',1)
 def test_absent_class_is_explicit(self):
  r=audit_contexts(test_empirical_bundle.BundleTest().fixture(),'Assassin');self.assertEqual(r['partitionCount'],0);self.assertEqual(r['usableContexts'],[])
