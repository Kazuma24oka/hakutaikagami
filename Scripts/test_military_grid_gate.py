from pathlib import Path
import tempfile,unittest,json
from validate_military_grid import sha,require

class MilitaryGateTests(unittest.TestCase):
 def test_source_contract_and_asset_changes_invalidate_proof(self):
  parent=Path(__file__).resolve().parent
  with tempfile.TemporaryDirectory(prefix='military-grid-gate-',dir=parent) as d:
   root=Path(d);assert root.resolve().parent==parent;stage=root/'Art/Officer3D/MilitaryGridV1';stage.mkdir(parents=True)
   names=['map','military-core','shared-transmission','knowledge-view','renderer','save','contract','asset']
   for n in names:(root/n).write_bytes(b'fixed')
   (stage/'runtime-plan.json').write_text(json.dumps({'schema':1,'dependencies':[{'path':n,'sha256':sha(root/n)} for n in names]}))
   self.assertEqual(len(require(root)),64)
   for n in names:
    (root/n).write_bytes(b'changed')
    with self.assertRaises(AssertionError):require(root)
    (root/n).write_bytes(b'fixed')
if __name__=='__main__':unittest.main()
