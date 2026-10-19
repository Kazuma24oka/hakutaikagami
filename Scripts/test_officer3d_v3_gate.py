from pathlib import Path
import tempfile,unittest
import officer3d_v3_pipeline as g
class GateTests(unittest.TestCase):
 def test_dependency_mutation_and_stage(self):
  parent=Path(__file__).resolve().parent
  with tempfile.TemporaryDirectory(prefix='v3-gate-',dir=parent) as d:
   root=Path(d);assert root.resolve().parent==parent;(root/'Art/Officer3D/TrialV3').mkdir(parents=True)
   names=['contract','source-mesh','reference-photo','producer','runtime']
   for n in names:(root/n).write_bytes(b'fixed')
   g.bind(root,'runtime',names);self.assertEqual(len(g.require(root,'runtime')),64)
   for n in names:
    p=root/n;p.write_bytes(b'changed')
    with self.assertRaises(AssertionError):g.require(root,'runtime')
    p.write_bytes(b'fixed')
   plan=root/'Art/Officer3D/TrialV3/runtime-plan.json';plan.write_text(plan.read_text().replace('"stage": "runtime"','"stage": "export"'))
   with self.assertRaises(AssertionError):g.require(root,'runtime')
if __name__=='__main__':unittest.main()
