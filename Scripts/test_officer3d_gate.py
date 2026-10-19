"""Negative tests: changed contract, source, input, tool and decisions must close the gate."""
from pathlib import Path
import json,sys,tempfile,unittest,shutil
import officer3d_pipeline as gate
ROOT=Path(sys.argv[1]);sys.argv=sys.argv[:1]
class GateTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory(prefix='officer3d-gate-',dir=Path(__file__).parent);self.r=Path(self.temp.name);assert self.r.resolve().parent==Path(__file__).resolve().parent;d=self.r/'Art/Officer3D/TrialV1/plans';d.mkdir(parents=True)
  shutil.copyfile(ROOT/'Art/Officer3D/TrialV1/contract.json',d.parent/'contract.json')
  self.status={'options_status':'USER_SELECTED_ALL_A','selected_options':{str(i):'A' for i in range(1,11)}}
  self.statuspath=d.parent.parent/'production-status.json';self.write(self.statuspath,self.status)
  self.paths=[]
  for name in ['source','input','tool']:
   f=self.r/name;f.write_text('fixed');self.paths.append({'path':name,'sha256':gate.sha(f)})
  self.plan={'allowed_stages':['runtime'],'contract_sha256':gate.sha(d.parent/'contract.json'),'source':self.paths[0],'inputs':[self.paths[1]],'tools':[self.paths[2]]};self.write(d/'runtime.json',self.plan)
 def tearDown(self):self.temp.cleanup()
 def write(self,p,v):p.write_text(json.dumps(v),encoding='utf-8')
 def test_unchanged_allowed(self):self.assertEqual(len(gate.require(self.r,'runtime')[0]),64)
 def test_each_mutation_rejected(self):
  for name in ['source','input','tool','Art/Officer3D/TrialV1/contract.json']:
   with self.subTest(name=name):
    f=self.r/name;old=f.read_bytes();f.write_bytes(old+b' ')
    with self.assertRaises(AssertionError):gate.require(self.r,'runtime')
    f.write_bytes(old)
 def test_decision_rejected(self):
  self.status['selected_options']['5']='B';self.write(self.statuspath,self.status)
  with self.assertRaises(AssertionError):gate.require(self.r,'runtime')
 def test_wrong_stage_rejected(self):
  self.plan['allowed_stages']=[];self.write(self.r/'Art/Officer3D/TrialV1/plans/runtime.json',self.plan)
  with self.assertRaises(AssertionError):gate.require(self.r,'runtime')
unittest.main()
