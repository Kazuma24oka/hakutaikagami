"""Refuse to show an unreviewed common-core screen after source changes."""
from pathlib import Path
import hashlib,json

ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/CommonContractV1'
def main():
 report=json.loads((BASE/'implementation-report.json').read_text(encoding='utf-8'))
 gpu=json.loads((BASE/'godot-verification.json').read_text(encoding='utf-8'))
 assert isinstance(gpu['visual_review'],dict),'GPU images have not been visually reviewed'
 shared=[p for p in (ROOT/'Core').rglob('*.cs') if not {'bin','obj'}.intersection(p.parts)]+list((ROOT/'Game/Scripts').glob('*.cs'))+[ROOT/'Core/HakutaiKagami.Core.csproj',ROOT/'Game/HakutaiKagami.Game.csproj',ROOT/'Game/project.godot',ROOT/'global.json']
 actual={str(p.relative_to(ROOT)).replace('\\','/') for p in shared}
 assert actual==set(report['shared_source_sha256_normalized_lf']),'Compiled input set changed; revalidate added/removed sources'
 for name,digest in report['shared_source_sha256_normalized_lf'].items():
  assert hashlib.sha256((ROOT/name).read_text(encoding='utf-8').encode('utf-8')).hexdigest()==digest,'Changed shared input: '+name
 for name,digest in {**report['source_sha256'],**gpu['code_sha256']}.items():
  assert hashlib.sha256((ROOT/name).read_bytes()).hexdigest()==digest,'Changed input requires relevant tests and review: '+name
 for name,digest in gpu['images'].items():
  assert hashlib.sha256((BASE/'Evidence'/name).read_bytes()).hexdigest()==digest,'Changed GPU image: '+name
 print('COMMON_CONTRACT_INPUTS_VERIFIED (source binding; informational DLL rebuild metadata is not treated as a rule change)')
if __name__=='__main__':main()
