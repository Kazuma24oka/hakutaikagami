import unittest,tempfile,json
from pathlib import Path
from validate_officer_world import sha,require
class WorldGateTests(unittest.TestCase):
 def test_dependency_mutations_are_rejected(self):
  parent=Path(__file__).resolve().parent
  with tempfile.TemporaryDirectory(prefix='officer-world-gate-',dir=parent) as d:
   root=Path(d);assert root.resolve().parent==parent;stage=root/'Art/Officer3D/WorldIntegrationV1';stage.mkdir(parents=True)
   names=['core','appearance-contract','rendering','assets','normal-save-ui']
   for n in names:(root/n).write_bytes(b'fixed')
   plan={'schema':1,'dependencies':[{'path':n,'sha256':sha(root/n)} for n in names]};(stage/'runtime-plan.json').write_text(json.dumps(plan))
   self.assertEqual(len(require(root)),64)
   for n in names:
    (root/n).write_bytes(b'changed')
    with self.assertRaises(AssertionError):require(root)
    (root/n).write_bytes(b'fixed')
if __name__=='__main__':unittest.main()
