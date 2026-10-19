"""Build the offline basic-design document and traceable acceptance plans from one source.

This validates design coverage, not game behavior. No art generation or runtime mutation.
"""
from pathlib import Path
import argparse,hashlib,html,json,re

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'Docs/Design'
def read(name):return json.loads((BASE/name).read_text(encoding='utf-8'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,text):p.write_bytes((text.rstrip()+'\n').encode('utf-8'))
def dump(p,data):write(p,json.dumps(data,ensure_ascii=False,indent=2))
def esc(s):return html.escape(str(s),quote=True)

def build():
 d=read('basic-design-v1.json');r=read('basic-design-rules.json');items=[x for g in d['groups'] for x in g['items']]
 assert len(d['groups'])==15 and [x['id'] for x in items]==[f'BD-{i:02}' for i in range(1,61)]
 assert len(r['rules'])==10 and len(r['role_matrix'])==20
 refids={x['id'] for x in d['invariants']}|{x['id'] for x in r['references']}
 for item in items:
  for key in ['title','decision','boundary','normal','failure','acceptance','refs']:assert item[key],(item['id'],key)
  assert set(item['refs'])<=refids,item['id']
 cases=[]
 for item in items:
  for kind,key in [('normal','normal'),('failure','failure')]:cases.append({'id':item['id']+'-'+kind,'design_id':item['id'],'kind':kind,'scenario':item[key],'acceptance':item['acceptance'],'status':'PLANNED_NOT_EXECUTED'})
 for i,row in enumerate(r['role_matrix'],1):
  for kind,key in [('normal','allowed'),('boundary','restricted')]:cases.append({'id':f'ROLE-{i:02}-{kind}','design_id':'BD-10','kind':kind,'scenario':row['role']+' / '+row['category']+' / '+row[key],'acceptance':'権限・知識・伝達・資源条件を確認し、実行／申請／越権／不可を期待経路へ分類する。','status':'PLANNED_NOT_EXECUTED'})
 for rule in r['rules']:
  assert all(x in {i['id'] for i in items}|refids for x in rule['refs'])
  cases.append({'id':rule['id'],'design_id':rule['refs'][0],'kind':'cross-system','scenario':rule['expected'],'acceptance':rule['rule'],'status':'PLANNED_NOT_EXECUTED'})
 assert len(cases)==170 and len({x['id'] for x in cases})==170
 dump(BASE/'basic-design-acceptance.json',{'schema':1,'status':'DESIGN_EXPECTATIONS_ONLY','count':len(cases),'note':'170は実行済みテスト数ではなく、設計から導出した予定シナリオ数。既存Core317件とは別。','cases':cases})
 md=['# 百代鏡 基本設計書 V1.0','',f"{d['date']} / {d['status']}",'',d['authority'],'',d['completion_scope'],'','## 製品範囲']
 for key in ['release','scenario','life','target']:md+=['',d['scope'][key]]
 md+=['','### 明示的な後続版・対象外']+['- '+s for s in d['scope']['deferred']]
 md+=['','## 維持する原則']+['- '+x['id']+'：'+x['text'] for x in d['invariants']]
 for group in d['groups']:
  md+=['','## '+group['title']]
  for x in group['items']:
   md+=['','### '+x['id']+' '+x['title'],'','**採用**：'+x['decision'],'','**境界**：'+x['boundary'],'','**正常例**：'+x['normal'],'','**失敗・競合例**：'+x['failure'],'','**受入条件**：'+x['acceptance'],'','参照：'+', '.join(x['refs'])]
 md+=['','## 共通の境界規則']
 for x in r['rules']:md+=['','### '+x['id']+' '+x['title'],'',x['rule'],'','期待例：'+x['expected']]
 md+=['','## 代表行動可否表','','| 立場 | 分類 | 実行候補 | 制限・別経路 |','|---|---|---|---|']
 md+=['| '+' | '.join(row[k] for k in ['role','category','allowed','restricted'])+' |' for row in r['role_matrix']]
 md+=['','## 参考作品：観察と採用判断を分ける']
 for x in r['references']:md+=['',f"### {x['id']} {x['title']}",'',x['type'],'',x['observation'],'','採用する考え方：'+x['adopt'],'','移植しないもの：'+x['exclude']]+(['',x['url']] if x['url'] else [])
 md+=['','## 詳細設計への引継ぎ']
 for x in r['detail_handoff']:md+=['',f"- {x['id']} / {x['owner']}：{x['scope']}"]
 md+=['','## 現在の実装と基本設計完了の意味','','基本判断60/60を閉じ、共通境界10件、代表権限20組、予定受入シナリオ170件を定義した。170件はゲームに対して未実行。詳細設計・史料採録・実装・最適化・素材制作は残る。現実装は3武将1城・一地域3部隊等の試作であり、全国・自由カメラ・全制度は未実装。既存Core317件や実GPU28枚の証拠を新機能の合格へ転用しない。','','V1の範囲・境界・方式は今回の委任採用で固定する。調整係数・API・史料レコードは詳細設計で具体化するが、設計方針を変更する場合は版・根拠・影響・再検査範囲を記録する。','','機械可読正本：[basic-design-v1.json](basic-design-v1.json)、共通規則：[basic-design-rules.json](basic-design-rules.json)、受入予定：[basic-design-acceptance.json](basic-design-acceptance.json)。HTMLは同じデータから生成する。']
 write(BASE/'BASIC_DESIGN_V1.md','\n'.join(md))
 cards=[];nav=[]
 for group in d['groups']:
  nav.append(f'<a href="#{group["id"]}">{esc(group["id"][1:])} {esc(group["title"])}</a>')
  articles=[]
  for x in group['items']:
   fields=''.join(f'<div class="field"><dt>{label}</dt><dd>{esc(x[key])}</dd></div>' for label,key in [('採用する方針','decision'),('範囲と例外の境界','boundary'),('正常な進み方','normal'),('失敗・競合時','failure'),('実装後の受入条件','acceptance')])
   refs=' '.join(f'<a href="#{esc(ref)}">{esc(ref)}</a>' for ref in x['refs'])
   articles.append(f'<article id="{x["id"]}" data-decision><div class="id">{x["id"]} · 委任採用</div><h3>{esc(x["title"])}</h3><dl>{fields}</dl><p class="refs">参照 {refs}</p></article>')
  cards.append(f'<section id="{group["id"]}" class="group"><details><summary><span class="group-no">{group["id"][1:]}</span><span>{esc(group["title"])}<small>{" ／ ".join(esc(x["title"]) for x in group["items"])}</small></span><span class="count">4 / 4</span></summary><div class="group-content">{"".join(articles)}</div></details></section>')
 rulehtml=''.join(f'<article id="{x["id"]}"><div class="id">{x["id"]} · 領域をまたぐ裁定</div><h3>{esc(x["title"])}</h3><p>{esc(x["rule"])}</p><div class="example"><strong>期待する結果</strong><p>{esc(x["expected"])}</p></div></article>' for x in r['rules'])
 refshtml=''.join(f'<article id="{x["id"]}"><h3>{esc(x["id"])} · '+(f'<a href="{esc(x["url"])}">{esc(x["title"])}</a>' if x['url'] else esc(x['title']))+f'</h3><p class="muted">{esc(x["type"])}</p><p>{esc(x["observation"])}</p><p><strong>百代鏡に活かす：</strong>{esc(x["adopt"])}</p><p><strong>持ち込まない：</strong>{esc(x["exclude"])}</p></article>' for x in r['references'])
 rolehtml=''.join('<tr>'+''.join(f'<td>{esc(row[k])}</td>' for k in ['role','category','allowed','restricted'])+'</tr>' for row in r['role_matrix'])
 detailshtml=''.join(f'<li><strong>{x["id"]} · {esc(x["owner"])}</strong><br>{esc(x["scope"])}</li>' for x in r['detail_handoff'])
 template=(ROOT/'Scripts/basic_design_template.html').read_text(encoding='utf-8')
 values={'TITLE':esc(d['title']),'NAV':''.join(nav),'GROUPS':''.join(cards),'RULES':rulehtml,'REFERENCES':refshtml,'ROLE_ROWS':rolehtml,'DETAILS':detailshtml,'SOURCE_SHA':sha(BASE/'basic-design-v1.json'),'INVARIANTS':''.join(f'<li id="{x["id"]}"><strong>{x["id"]}</strong> {esc(x["text"])}</li>' for x in d['invariants']),'SCOPE':''.join(f'<p>{esc(d["scope"][key])}</p>' for key in ['release','scenario','life','target']),'DEFERRED':''.join(f'<li>{esc(x)}</li>' for x in d['scope']['deferred'])}
 for k,v in values.items():template=template.replace('{{'+k+'}}',v)
 assert not re.search(r'\{\{[A-Z_]+\}\}',template)
 write(BASE/'basic-design-v1.html',template)
 print('BASIC_DESIGN_BUILT groups=15 decisions=60 rules=10 role_pairs=20 acceptance_plans=170')

if __name__=='__main__':build()
