from pathlib import Path
import unittest,tempfile,json
import officer3d_v2_pipeline as gate
class GateTests(unittest.TestCase):
 def test_stale_dependencies_and_stage_rejected(self):
  with tempfile.TemporaryDirectory(prefix='officer3d-v2-') as d:
   r=Path(d);(r/'Art/Officer3D/TrialV2').mkdir(parents=True);paths=['source','contract','producer','runtime','asset']
   for p in paths:(r/p).write_text('fixed')
   gate.bind(r,'runtime',paths);self.assertEqual(len(gate.require(r,'runtime')),64)
   for name in paths:
    p=r/name;p.write_text('changed')
    with self.assertRaises(AssertionError):gate.require(r,'runtime')
    p.write_text('fixed')
   p=r/'Art/Officer3D/TrialV2/runtime-plan.json';plan=gate.read(p);plan['stage']='export';p.write_text(json.dumps(plan))
   with self.assertRaises(AssertionError):gate.require(r,'runtime')
if __name__=='__main__':unittest.main()
