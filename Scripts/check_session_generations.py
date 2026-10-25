from pathlib import Path
import re,hashlib,json
ROOT=Path(__file__).resolve().parents[1]
def check(root=ROOT):
 files=sorted((root/'Core/Contracts').glob('WorldSession*.cs'));sources={};assignments=[]
 # This guards the ownership rule used by opaque worker captures. It is not a
 # substitute for behavioral tests or review of newly added Core operations.
 writes=re.compile(r'\bstate(?:\.[A-Za-z_]\w*)+(?:\[[^;\r\n]*\])?\s*(?:=(?!=)|\+\+|--|\+=|-=|\*=|/=|\?\?=)')
 mutators=re.compile(r'\bstate(?:\.[A-Za-z_]\w*)+\.(?:Add|AddRange|Remove|RemoveAll|RemoveAt|Clear|Insert|Sort|Reverse|Enqueue|Dequeue|Push|Pop|TryAdd)\s*\(')
 for p in files:
  raw=p.read_bytes();text=raw.decode('utf-8-sig');rel=p.relative_to(root).as_posix();sources[rel]=hashlib.sha256(raw).hexdigest()
  assert not writes.search(text),(rel,'direct member mutation of private committed generation')
  assert not mutators.search(text),(rel,'direct collection mutation of private committed generation')
  assert not re.search(r'\breturn\s+state\s*;',text),(rel,'private world exposed without a copy')
  assert not re.search(r'\b(?:ref|out)\s+state\b',text),(rel,'private world escaped by reference')
  for match in re.finditer(r'\bstate\s*=(?!=)',text):
   tail=text[match.end():].lstrip();assert p.name=='WorldSession.cs' and (tail.startswith('ContractJson.Copy(initial);') or tail.startswith('next;')),(rel,'unreviewed generation assignment')
   assignments.append(dict(file=rel,line=text[:match.start()].count('\n')+1,value='initial deep copy' if tail.startswith('ContractJson') else 'validated new generation'))
 assert len(assignments)==2,assignments
 return dict(sources=sources,assignments=assignments,rule='Only constructor deep-copy and validated copy-on-write Change may replace state; no direct committed-member/collection writes or raw world escape.',limits='Static ownership guard plus behavioral worker tests; does not prove purity of every called Core helper.')
if __name__=='__main__':
 result=check();print(json.dumps(result,indent=2));print('SESSION_GENERATION_OWNERSHIP_PASSED')
