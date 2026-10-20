from pathlib import Path
import hashlib,json,subprocess,sys
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'Docs/Design/SurveyRoadV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 OUT.mkdir(parents=True,exist_ok=True)
 subprocess.run([sys.executable,'-X','utf8','Scripts/check_historical_binding.py'],cwd=ROOT,check=True)
 manifest=json.loads((BASE/'source-manifest.json').read_bytes())
 for group in ['photos','gsi_tiles']:
  for name,digest in manifest[group].items():assert sha(OUT/'sources'/name)==digest,'Changed source image '+name
 # Uses the actual C# adoption evaluator; no duplicated Python adoption policy.
 p=subprocess.run(['dotnet','run','--project','Tools/CommonContractProbe/CommonContractProbe.csproj','--no-restore','--','survey',str(OUT/'probe')],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf-8',errors='replace',check=True)
 (OUT/'probe.log').write_bytes(p.stdout.encode())
 d=json.loads(p.stdout[p.stdout.index('{'):]);assert not d['Decision']['CanReplace'] and d['ReplacementCount']==0 and d['Refused'] and d['GeometryUnchanged'] and d['DefinitionUnchanged'],d
 evidence=json.loads((ROOT/'Core/Data/futamura-survey-review-v1.json').read_bytes())
 for s in evidence['Sources']:
  name='bunka-6.jpg' if s['Id']=='bunka-designation-map' else 'bunka-1.jpg'
  assert sha(OUT/'sources'/name)==s['Sha256'],'Evidence binding differs'
 g=json.loads((ROOT/'Docs/Design/HistoricalRoadV1/verification.json').read_bytes());assert g['status']=='PASSED' and isinstance(g['visual_review'],dict),'Run actual Godot verification and inspect final images'
 for name,digest in g['sources'].items():assert sha(ROOT/name)==digest,'Stale Godot source '+name
 files=['Core/Contracts/SurveyRoadReview.cs','Core/Data/futamura-survey-review-v1.json','Tests/SurveyRoadReviewTests.cs','Tools/CommonContractProbe/Program.cs','Scripts/review_survey_roads.py','Docs/Design/SurveyRoadV1/source-manifest.json']
 result={'status':'PASSED_WITH_NO_ADOPTION','decision':d,'inputs':{name:sha(ROOT/name) for name in files},'source_manifest_sha256':sha(BASE/'source-manifest.json'),'godot_verification_file':'Docs/Design/HistoricalRoadV1/verification.json','godot_verification_sha256':sha(ROOT/'Docs/Design/HistoricalRoadV1/verification.json'),'gpu_pixel_comparisons':g['pixel_checks'],'limitations':'No georeferenced/surveyed replacement accepted. Existing200m preview geometry retained. Source map inspected; actual geographic endpoints and1560 centerline not confirmed.'}
 (BASE/'verification.json').write_bytes((json.dumps(result,ensure_ascii=False,indent=2)+'\n').encode());print('PASS: actual C# review refused incomplete survey; existing movement/recovery unchanged')
if __name__=='__main__':main()
