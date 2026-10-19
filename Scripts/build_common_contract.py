"""Generate the adopted common-contract document; does not implement game rules."""
from pathlib import Path
import hashlib, html, json

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / 'Docs/Design/CommonContractV1'
def esc(x): return html.escape(str(x), quote=True)
def write(p, s): p.write_bytes((s.rstrip()+'\n').encode('utf-8'))
def main():
 d=json.loads((BASE/'common-contract-v1.json').read_text(encoding='utf-8'))
 assert len(d['items'])==24 and [i['id'] for i in d['items']]==[f'CC-{n:02}' for n in range(1,25)]
 assert len(d['state_axes'])==8 and len(d['api'])==6
 cases=[dict(id=i['id']+'-'+a['kind'],contract=i['id'],status='PLANNED_NOT_EXECUTED',**a) for i in d['items'] for a in i['acceptance']]
 write(BASE/'acceptance-plan.json',json.dumps(dict(schema=1,count=len(cases),cases=cases),ensure_ascii=False,indent=2))
 assert len(cases)==48
 blocks=[];md=['# '+d['title'],'',d['authority'],'',d['scope'],'']
 for i in d['items']:
  refs=' '.join(f'<a href="../basic-design-v1.html#{esc(ref)}">{esc(ref)}</a>' if not ref.startswith('PW') else '<a href="../../Proposals/PLAYER_PEACE_WAR_FLOW.md">'+esc(ref)+'</a>' for ref in i['refs'])
  sources=' '.join(f'<a href="../../../{esc(p)}">{esc(Path(p).name)}</a>' for p in i['source'])
  fields=''.join(f'<dt>{label}</dt><dd>{esc(i[key])}</dd>' for key,label in [('decision','採用する仕様'),('fields','データ・型'),('rules','境界と不変条件'),('current','既存から活かす'),('change','必要な変更')])
  checks=''.join('<div class="case"><b>'+('正常例' if a['kind']=='normal' else '失敗・競合例')+'</b><p>'+esc(a['given_when_then'])+'</p></div>' for a in i['acceptance'])
  blocks.append(f'<details class="contract" id="{i["id"]}" data-contract><summary><span class="id">{i["id"]}</span><strong>{esc(i["title"])}</strong><span class="tag">採用</span></summary><div class="body"><dl>{fields}</dl><div class="cases">{checks}</div><p class="muted">整合先 {refs}</p><p class="sources">照合した既存コード {sources}</p></div></details>')
  md.extend(['## '+i['id']+' '+i['title'],'']+[f'**{label}**：{i[key]}\n' for key,label in [('decision','採用'),('fields','データ'),('rules','境界'),('current','既存'),('change','変更')]]+[f'**{a["kind"]}**：{a["given_when_then"]}\n' for a in i['acceptance']])
 def table(headers,rows):return '<div class="scroll"><table><thead><tr>'+''.join('<th>'+esc(x)+'</th>' for x in headers)+'</tr></thead><tbody>'+''.join('<tr>'+''.join('<td>'+esc(x)+'</td>' for x in row)+'</tr>' for row in rows)+'</tbody></table></div>'
 states=table(['状態の軸','遷移','成立条件・禁止','終端と履歴'],[[x[k] for k in ['axis','states','guard','terminal']] for x in d['state_axes']])
 apis=''.join('<article><h3>'+esc(x['name'])+'</h3><dl>'+''.join('<dt>'+label+'</dt><dd>'+esc(x[k])+'</dd>' for k,label in [('input','入力'),('output','出力'),('mutation','更新境界')])+'</dl></article>' for x in d['api'])
 wire=d['wire_contract'];wirehtml='<dl>'+''.join('<dt>'+esc(label)+'</dt><dd>'+esc(wire[k])+'</dd>' for k,label in [('encoding','符号化'),('canonical_hash','ハッシュと数値'),('version_policy','版と必須検査'),('empty_sections','欠損と空欄'),('retention','履歴の保持')])+'</dl><p class="muted">必須のPayload区分</p><div class="chips">'+''.join('<code>'+esc(x)+'</code>' for x in wire['required_payload_sections'])+'</div>'
 sources='<ul class="source-list">'+''.join('<li><a href="../../../'+esc(p)+'">'+esc(p)+'</a><code>'+h+'</code></li>' for p,h in d['source_hashes_normalized_lf'].items())+'</ul>'
 template=(ROOT/'Scripts/common_contract_template.html').read_text(encoding='utf-8')
 values=dict(TITLE=esc(d['title']),CARDS=''.join(blocks),STATES=states,APIS=apis,WIRE=wirehtml,SOURCES=sources,TEMPORAL=table(['時点','起きたこと','Coreでの扱い','意味'],d['temporal_examples']),EXAMPLE=esc(json.dumps(d['envelope_example'],ensure_ascii=False,indent=2)),SOURCE_SHA=hashlib.sha256((BASE/'common-contract-v1.json').read_bytes()).hexdigest())
 for k,v in values.items():template=template.replace('{{'+k+'}}',v)
 assert '{{' not in template
 write(BASE/'common-contract-v1.html',template)
 md.extend(['## 状態遷移','',json.dumps(d['state_axes'],ensure_ascii=False,indent=2),'','## 公開API','',json.dumps(d['api'],ensure_ascii=False,indent=2),'','## 保存のwire契約','',json.dumps(wire,ensure_ascii=False,indent=2),'','## 例示用ヘッダー（ロード不可）','',json.dumps(d['envelope_example'],ensure_ascii=False,indent=2)])
 write(BASE/'COMMON_CONTRACT_V1.md','\n'.join(md))
 print('BUILT: contracts=24 state_axes=8 APIs=6 acceptance=48 PLANNED_NOT_EXECUTED')
if __name__=='__main__':main()
