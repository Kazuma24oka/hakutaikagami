from pathlib import Path
import json,hashlib
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'Docs/Design/RegionalCampaignV1'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 report=json.loads((BASE/'verification.json').read_bytes())
 assert report['status']=='PASSED' and isinstance(report.get('visual_review'),dict),'Actual image review required'
 for section in ['sources','assets','binaries','entry_tools']:
  for name,expected in report[section].items():
   p=ROOT/name;assert p.is_file() and sha(p)==expected,'Changed '+section+': '+name
 for name,expected in report['images'].items():assert sha(BASE/'runtime-local'/name)==expected,'Changed image: '+name
 assert sha(BASE/'runtime-local/tests/campaign.trx')==report['trx_sha256'],'Changed test results'
 assert report['visual_review']['images']==report['images'],'Review does not bind these images'
 print('PASS: current sources, DLLs, assets, test results and reviewed GPU images')
if __name__=='__main__':main()
