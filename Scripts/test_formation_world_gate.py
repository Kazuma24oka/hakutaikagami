import unittest,tempfile,json
from pathlib import Path
from validate_formation_world import sha,require
class FormationGateTests(unittest.TestCase):
 def test_changed_source_contract_assets_and_rendering_reject_stale_plan(self):
  parent=Path(__file__).resolve().parent
  with tempfile.TemporaryDirectory(prefix='formation-world-gate-',dir=parent) as d:
   root=Path(d);assert root.resolve().parent==parent;base=root/'Art/Officer3D/FormationWorldV1';base.mkdir(parents=True)
   names=['core','line-contract','renderer','normal-save-ui','assets','shader','scene','validation']
   for name in names:(root/name).write_bytes(b'fixed')
   (base/'runtime-plan.json').write_text(json.dumps({'schema':1,'dependencies':[{'path':name,'sha256':sha(root/name)} for name in names]}))
   self.assertEqual(64,len(require(root)))
   for name in names:
    (root/name).write_bytes(b'changed')
    with self.assertRaises(AssertionError):require(root)
    (root/name).write_bytes(b'fixed')
if __name__=='__main__':unittest.main()
