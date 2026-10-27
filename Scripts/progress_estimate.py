"""Subjective product-scope estimates; deliberately separate from acceptance/test counts."""
from pathlib import Path
import json,html,math
R=Path(__file__).resolve().parents[1]
CSS="""main{overflow-wrap:anywhere}.estimate-top{border-top:5px solid #397867}.estimate-total{font-size:54px;line-height:1.2;color:#285e4d;font-weight:750}.estimate-range{font-size:16px;font-weight:400;color:#52616b}.estimate-bar{display:block;height:7px;background:#e7ece9;border-radius:5px;overflow:hidden;margin-top:8px}.estimate-bar span{height:100%;display:block;background:#4d8772}.estimate-node{border:1px solid #d6dfda;border-radius:8px;margin:10px 0;background:#fff}.estimate-node>summary{cursor:pointer;display:flex;align-items:center;gap:12px;padding:13px;list-style:none}.estimate-node>summary:before{content:'▸';color:#48705e}.estimate-node[open]>summary:before{content:'▾'}.estimate-name{flex:1;min-width:0}.estimate-name small{display:block;font-size:12px}.estimate-pct{font-size:23px;font-weight:700;white-space:nowrap;color:#285e4d}.estimate-body{padding:0 16px 14px 27px}.estimate-body .estimate-node{background:#f8faf8}.estimate-node.leaf .estimate-pct{font-size:18px}.estimate-node.leaf p{margin:8px 0}.estimate-ref{background:#f0eee8!important}.estimate-ref .estimate-pct{color:#746749}.estimate-tools{display:flex;flex-wrap:wrap;gap:8px;margin:12px 0}.estimate-tools button{font:inherit;padding:7px 12px;background:#f3f7f4;color:#254e40;border:1px solid #b9ccc0;border-radius:5px;cursor:pointer}.estimate-tools button:focus-visible,.estimate-node summary:focus-visible{outline:3px solid #b88131;outline-offset:2px}.estimate-focus{background:#edf3ee;border-left:4px solid #48765d;padding:12px}.estimate-roadmap{display:grid;grid-template-columns:1fr 1fr;gap:12px;padding-left:0;list-style:none;counter-reset:steps}.estimate-roadmap li{border:1px solid #d6dfda;border-radius:7px;padding:14px;counter-increment:steps}.estimate-roadmap li:before{content:counter(steps);display:inline-block;background:#e2ece4;padding:0 9px;border-radius:4px;margin-right:8px}.estimate-roadmap h3{font-size:17px;display:inline}.estimate-roadmap p{margin:7px 0}.estimate-roadmap small{display:block}.estimate-meta{font-size:14px}.estimate-scope{color:#52616b;font-size:14px}@media(max-width:600px){.estimate-roadmap{grid-template-columns:1fr}.estimate-body{padding-left:12px;padding-right:10px}.estimate-name{font-size:14px}.estimate-pct{font-size:20px}.estimate-total{font-size:44px}.estimate-node>summary{padding:10px;gap:8px}}"""
JS="""document.querySelectorAll('[data-estimate-action]').forEach(b=>b.addEventListener('click',()=>{let mode=b.dataset.estimateAction;document.querySelectorAll('#estimate-tree details').forEach(d=>d.open=mode==='all'||mode==='medium'&&d.classList.contains('domain'));if(mode==='focus'){const leaf=document.getElementById('estimate-'+b.dataset.focus);for(let p=leaf;p;p=p.parentElement)if(p.tagName==='DETAILS')p.open=true;leaf.scrollIntoView({block:'center'});}}));"""
def load():return json.loads((R/'Docs/Development/ProgressV1/estimate.json').read_bytes())
def calculate(a,d):
    excluded=set(a['excluded_from_rollup']);groups={g['id']:g for g in d['groups']}
    def avg(ids):
        values=[a['leaves'][i]['percent'] for i in ids if i not in excluded]
        return sum(values)/len(values) if values else 0
    medium={g['id']:avg([x['id'] for x in g['items']]) for g in d['groups']}
    large={x['id']:avg([i['id'] for gid in x['groups'] for i in groups[gid]['items']]) for x in a['domains']}
    total=sum(large[x['id']]*x['weight']/100 for x in a['domains'])
    step=a['rounding_step'];rounded=lambda v:int(math.floor(v/step+.5)*step)
    return dict(raw_total=total,total=rounded(total),medium={k:rounded(v) for k,v in medium.items()},large={k:rounded(v) for k,v in large.items()})
def render_estimate(p,d):
    a=load();c=calculate(a,d);e=html.escape;groups={g['id']:g for g in d['groups']};rows={x['id']:x for x in p['items']}
    def node(title,pct,kind,body,identifier,subtitle=''):
        return '<details class="estimate-node '+kind+'" id="estimate-'+identifier+'"><summary><span class="estimate-name"><small>'+e(subtitle)+'</small>'+e(title)+'<span class="estimate-bar"><span style="width:'+str(pct)+'%"></span></span></span><span class="estimate-pct">約'+str(pct)+'%</span></summary><div class="estimate-body">'+body+'</div></details>'
    audit=a.get('progress_audit');audit_html=''
    if audit:
        first=audit['history'][0]['raw_total'];last=audit['history'][-1]['raw_total'];delta=last-first
        history_rows=''.join('<tr><td>PR #'+str(v['pr'])+'</td><td>'+format(v['raw_total'],'.1f')+'%</td><td>'+str(v['display_total'])+'%</td></tr>' for v in audit['history'])
        audit_html='<div class="estimate-focus" role="note"><b>60区分の一次静的監査済み・製品受入は未完了</b><p>旧尺度のPR #13集計 '+format(last,'.1f')+'% ／ PR #4時点 '+format(first,'.1f')+'% から '+format(delta,'+.1f')+'ポイント。小区分自体が概算であり、小数表示は測定精度を意味しません。</p><p>'+e(audit['note'])+'</p><details><summary>進捗の算出履歴（PR #4〜#13）</summary><table><thead><tr><th>時点</th><th>丸め前</th><th>5%刻み表示</th></tr></thead><tbody>'+history_rows+'</tbody></table></details></div>'
    batch=a.get('approval_batch')
    if batch:
        audit_html+='<div class="estimate-focus"><b>今回の実装による増分 '+format(batch['earned_gain'],'+.3f')+'ポイント</b><p>固定基準 '+format(batch['baseline'],'.1f')+'% → 現在 '+format(batch['current'],'.1f')+'%。評価補正 '+format(batch['excluded_reassessment'],'.3f')+'ポイントは加点対象外。完成条件 '+str(batch['criteria_passed'])+'/'+str(batch['criteria_total'])+'。重み・分母・実装前の目標は固定。</p></div>'
    tree=''
    for domain in a['domains']:
        middle=''
        for gid in domain['groups']:
            group=groups[gid];leaves=''
            for item in group['items']:
                key=item['id'];row=rows[key];v=round(c['raw_total'],1) if key=='BD-01' else a['leaves'][key]['percent'];ref=key in a['excluded_from_rollup']
                body='<p class="estimate-scope">'+e(a['leaves'][key]['basis'])+'</p><p><b>現在：</b>'+e(row['existing'])+'</p><p><b>残り：</b>'+e(row['remaining'])+'</p><p><a href="../../../'+e(row['evidence'][0])+'">実装・設計の根拠</a></p>'
                leaves+=node(item['title'],v,'leaf'+(' estimate-ref' if ref else ''),body,key,'小区分 '+key+(' ／ 集計対象外の参照値' if ref else ''))
            middle+=node(group['title'],c['medium'][gid],'medium',leaves,gid,'中区分')
        tree+=node(domain['title'],c['large'][domain['id']],'domain','<p class="estimate-scope">'+e(domain['scope'])+'</p>'+middle,domain['id'],'大区分')
    focus=a['focus'];gid=next(g['id'] for g in d['groups'] if any(x['id']==focus['leaf'] for x in g['items']));domain=next(x for x in a['domains'] if gid in x['groups'])
    path=domain['title']+' → '+groups[gid]['title']+' → '+next(x['title'] for x in groups[gid]['items'] if x['id']==focus['leaf'])
    roadmap=''.join('<li><h3>'+e(x['title'])+'</h3><small>'+e(x['status']+' ／ '+x['domain'])+'</small><p><b>この工程の区切り：</b>'+e(x['finish'])+'</p><small>'+e(x['why'])+'</small></li>' for x in a['roadmap'])
    notes=''.join('<p><b>'+e(x['title'])+'：</b>'+e(x['text'])+'</p>' for x in a['crosscut_notes'])
    return '<section class="estimate-top" aria-label="全体と分野別の概算進捗"><h2>全国で武将の人生を遊べる最初の完成版まで</h2><div class="estimate-total">約'+format(c['raw_total'],'.1f')+'% <span class="estimate-range">概算の幅 '+str(a['overall_range'][0])+'〜'+str(a['overall_range'][1])+'%</span></div><div class="estimate-bar"><span style="width:'+str(c['raw_total'])+'%"></span></div>'+audit_html+'<p>完成版の遊びとして、どこまで実装・接続できているかの概算です。分野を開くと、中区分 → 小区分の％と、現在・残りが見られます。</p><small>記録更新日 '+e(a['as_of'])+'。設計書の完成やテスト件数による加点ではありません。</small><div class="estimate-tools"><button data-estimate-action="close">大区分だけ表示</button><button data-estimate-action="medium">中区分まで表示</button><button data-estimate-action="all">すべて開く</button><button data-estimate-action="focus" data-focus="'+e(focus['leaf'])+'">今の作業位置を開く</button></div><div id="estimate-tree">'+tree+'</div><details class="estimate-meta"><summary>概算％の集計方法と注意点</summary><p>'+e(a['meaning'])+'</p><p>'+e(a['aggregation'])+'</p><p>大区分の重み：'+e('、'.join(x['title']+' '+str(x['weight'])+'%' for x in a['domains']))+'。</p><p>100%は、その区分の完成版向けの適用範囲まで実装・接続・確認した状態。基本設計の採用は完了済みですが、製品受入（BD-60）の0%は管理上の別指標で、全体へ加点しません。全体接続の参照値（BD-01）も二重集計しません。</p><a href="estimate.json">概算と計画の元データ</a></details></section><section><h2>今どこにいて、どこで次の分野へ渡るか</h2><div class="estimate-focus"><small>'+e(path)+'</small><p><b>'+e(focus['title'])+'</b> ／ '+e(focus['status'])+'</p><b>この工程の終了条件</b><ul>'+''.join('<li>'+e(x)+'</li>' for x in focus['exit'])+'</ul><small>後続へ回すもの：'+e('、'.join(focus['deferred']))+'</small></div><ol class="estimate-roadmap">'+roadmap+'</ol><p class="estimate-meta">'+e(a['roadmap_note'])+'</p>'+notes+'</section>'
