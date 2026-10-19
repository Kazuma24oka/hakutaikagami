"""Build a short user report from executed TRX, process and Godot evidence."""
from pathlib import Path
import base64,hashlib,html,json,subprocess,xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/CommonContractV1'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(n):return json.loads((BASE/n).read_text(encoding='utf-8'))
def write(n,text):(BASE/n).write_bytes((text.rstrip()+'\n').encode('utf-8'))
def esc(v):return html.escape(str(v),quote=True)
def main():
 trx=BASE/'runtime-local/tests/core.trx';doc=ET.parse(trx);ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
 counters=doc.find('.//t:Counters',ns).attrib
 assert int(counters['failed'])==0 and int(counters['notExecuted'])==0
 methods={x.attrib['id']:x.find('t:TestMethod',ns).attrib for x in doc.findall('.//t:UnitTest',ns)}
 cases=[{'name':r.attrib['testName'],'class':methods[r.attrib['testId']]['className'],'outcome':r.attrib['outcome']} for r in doc.findall('.//t:UnitTestResult',ns)]
 assert all(c['outcome']=='Passed' for c in cases)
 new=[c for c in cases if c['class'].endswith(('CommonContractTests','CommonSaveTests'))]
 process=read('process-verification.json');gpu=read('godot-verification.json')
 assert process['status']==gpu['status']=='PASSED' and len(gpu['checks'])==9
 for p,h in process['sources'].items():assert sha(ROOT/p)==h,p
 for p,h in gpu['code_sha256'].items():assert sha(ROOT/p)==h,p
 for p,h in gpu['binary_sha256'].items():assert sha(ROOT/p)==h,p
 for p,h in gpu['images'].items():assert sha(BASE/'Evidence'/p)==h,p
 # Ensure the pre-existing Core/game source is unchanged, including normal Main and user save handlers.
 baseline='f3c22ebefb380790b458c83ae92d212ec2541c2f'
 oldpaths=subprocess.check_output(['git','ls-tree','-r','--name-only',baseline,'Core','Game'],cwd=ROOT).decode().splitlines()
 changed=subprocess.check_output(['git','diff','--name-only',baseline,'--','Core','Game'],cwd=ROOT).decode().splitlines()
 assert not set(oldpaths).intersection(changed),'Existing Core/Game source changed'
 result={'schema':1,'date':'2026-10-20','status':'VERIFIED_FIXTURE_SCOPE','baseline_commit':baseline,
  'g1':{'status':'COMPLETED','four_items':['共通データ型','参照・期間・資源・知識・因果の検査','旧データの読取対応・製品への混用拒否','不正変更の拒否と回帰試験']},
  'further_progress':[{'gate':'G2','status':'FIXTURE_SUBSET_VERIFIED','scope':'秒時計・権限・本人別表示・密書の受領/開封・停止'}, {'gate':'G3','status':'FIXTURE_SUBSET_VERIFIED','scope':'閲覧・物資移管・移動・取消・任意受諾、既存配送の再利用、効果台帳'}, {'gate':'G4','status':'FOUNDATION_VERIFIED','scope':'厳密JSON・世代保存・背景保存API・隔離読込・別プロセス復旧・旧形式拒否'}, {'gate':'G5','status':'SYNTHETIC_LOOP_VERIFIED','scope':'重要任務→準備→本人移動と書役委任→途中復旧→返報/検証用評価。通常ゲームの平時一巡完成ではない。'}],
  'tests':{'total':int(counters['total']),'passed':int(counters['passed']),'failed':int(counters['failed']),'skipped':int(counters['notExecuted']),'new_cases':len(new),'legacy_cases':len(cases)-len(new),'trx_sha256':sha(trx),'new_case_results':new},
  'godot':{'checks':len(gpu['checks']),'captures':list(gpu['images']),'version':gpu['gpu_log'].splitlines()[0],'ui_entry':'Scripts/Show-CommonContract.ps1','visual_self_review':gpu['visual_review']},
  'foreign_process':process['cross_process'],'abrupt_exit':process['abrupt_process_exit'],
  'next':{'task':'移動経路・行動所要時間の契約を具体化し、新しい秒基準セッションを既存3D表示へ接続する。','done_when':'準備・移動・委任・返報を3D表示と本人認識から操作でき、途中保存・別プロセス復旧後も一致する。'},
  'not_completed':['全国の実地理・史料データ、製品用の移動/経済/戦闘係数','通常3D Mainの新セッション移行、全制度の状態遷移、交渉・人事・継承等の実行サービス','補償の実行・部分消費の工程・本格的な任務評価やNPC計画AI','設計の全48受入ケース、物理電源断、全国負荷/性能、全自動世代整理'],
  'protected':['旧Core/Gameの既存追跡ファイルは基準コミットと一致','通常Main・旧Tick保存・既存3D資産は変更していない','main/design/foundation/Confirmed/sourcesは変更しない','既存プレビューを終了せず、OSマウス入力・カーソル設定を使用していない'],
  'proof_sha256':{'process-verification.json':sha(BASE/'process-verification.json'),'godot-verification.json':sha(BASE/'godot-verification.json')},
  'source_sha256':{str(p.relative_to(ROOT)).replace('\\','/'):sha(p) for folder in ['Core/Contracts','Tools/CommonContractProbe'] for p in (ROOT/folder).glob('*.cs')}}
 result['source_sha256'].update({str(p.relative_to(ROOT)).replace('\\','/'):sha(p) for p in [ROOT/'Tests/CommonContractTests.cs',ROOT/'Tests/CommonSaveTests.cs',ROOT/'Tests/HakutaiKagami.Tests.csproj',ROOT/'Scripts/verify_common_contract_process.py',ROOT/'Scripts/verify_common_contract_gpu.py',ROOT/'Scripts/verify_common_contract_inputs.py',ROOT/'Scripts/Show-CommonContract.ps1']})
 result['test_binary_sha256']={str(p.relative_to(ROOT)).replace('\\','/'):sha(p) for p in [ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Tests.dll',ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll']}
 shared=[p for p in (ROOT/'Core').rglob('*.cs') if not {'bin','obj'}.intersection(p.parts)]+list((ROOT/'Game/Scripts').glob('*.cs'))+[ROOT/'Core/HakutaiKagami.Core.csproj',ROOT/'Game/HakutaiKagami.Game.csproj',ROOT/'Game/project.godot',ROOT/'global.json']
 result['shared_source_sha256_normalized_lf']={str(p.relative_to(ROOT)).replace('\\','/'):hashlib.sha256(p.read_text(encoding='utf-8').encode('utf-8')).hexdigest() for p in shared}
 core_hashes=[sha(ROOT/p) for p in ['Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tools/CommonContractProbe/bin/Debug/net8.0/HakutaiKagami.Core.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll']]
 assert len(set(core_hashes))==1,'Tests/probe/Godot used different Core binaries'
 assert result['tests']['legacy_cases']==317 and result['tests']['new_cases']==95
 write('implementation-report.json',json.dumps(result,ensure_ascii=False,indent=2))
 timeline=process['peace_fixture']['Timeline']
 steps=''.join('<li><span>'+esc(x['Seconds'])+'秒</span>'+esc(x['Stage'])+'</li>' for x in timeline)
 gallery=''.join('<figure><figcaption>'+esc(title)+'</figcaption><img loading="lazy" alt="'+esc(title)+'・実Godot画面" src="data:image/png;base64,'+base64.b64encode((BASE/'Evidence'/name).read_bytes()).decode()+'"></figure>' for name,title in [('sealed.png','未開封で停止'),('in-progress.png','本人は移動、書役は委任実行中'),('restored.png','途中復旧・同じ経路進捗'),('complete.png','城主が3件の返報を認識')])
 rows=''.join('<tr><td>'+esc(x['gate'])+'</td><td>'+esc(x['scope'])+'</td><td>検証用の範囲で確認</td></tr>' for x in result['further_progress'])
 page='''<!doctype html><html lang="ja"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>共通Core 実装結果 — 百代鏡</title><style>
 *{box-sizing:border-box}body{margin:0;background:#111b20;color:#eee9dc;font:16px/1.85 "Yu Gothic UI",Meiryo,sans-serif}.wrap{max-width:1080px;padding:38px 5vw 60px;margin:auto}h1{font-size:clamp(26px,4vw,40px);line-height:1.5;margin:14px 0 18px}h2{font-size:22px;margin:32px 0 14px}a{color:#dfbe80;text-underline-offset:4px}.muted{color:#b9c0bd;font-size:14px}.label{color:#dfbe80;font-size:13px;letter-spacing:.08em}.stats{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin:24px 0}.stat{padding:16px;background:#1b2b32;border-top:2px solid #dfbe80}.stat b{display:block;font-size:29px;color:#a9d8bf}.stat small{color:#c3c9c5}.note{padding:18px 22px;background:#1b2b32;border-left:3px solid #dfbe80}.grid{display:grid;grid-template-columns:1fr 1fr;gap:18px}.grid>*{min-width:0}ul,ol{padding-left:24px}li{margin:8px 0}.timeline{list-style:none;padding:0}.timeline li{border-left:2px solid #638d7d;padding:9px 0 9px 18px;margin:0}.timeline span{display:inline-block;color:#dfbe80;min-width:60px}.scroll{overflow:auto}table{border-collapse:collapse;width:100%;min-width:600px;font-size:14px}th,td{text-align:left;vertical-align:top;border-bottom:1px solid #3b5053;padding:12px}th{color:#dfbe80}details{border:1px solid #3b5053;margin:22px 0;padding:16px 20px}summary{cursor:pointer;color:#dfbe80}.gallery{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0;min-width:0}img{display:block;max-width:100%;height:auto;border:1px solid #3b5053}figcaption{font-size:14px;padding:10px 0}.next{border:1px solid #638d7d;padding:18px 22px}code{overflow-wrap:anywhere}footer{border-top:1px solid #3b5053;margin-top:32px;padding-top:16px;font-size:12px;color:#aebbb4}@media(max-width:650px){.stats{grid-template-columns:1fr 1fr}.grid,.gallery{grid-template-columns:1fr}.wrap{padding:24px 5vw}details{padding:14px}.stat b{font-size:25px}}@media print{body{background:white;color:#111}.stat,.note{background:#eee;color:#111}.muted,footer{color:#333}a,.label,.timeline span,th{color:#69502b}.stats{break-inside:avoid}details{break-inside:avoid}}
 </style></head><body><main class="wrap"><div class="label">百代鏡 / 実装結果 / 2026.10.20</div><h1>共通Coreの土台を実装し、<br>保存復旧を挟んだ一巡まで確認しました。</h1><p>依頼された<strong>G1の4項目は完了</strong>。続けて、採用済み仕様の範囲で時計・命令・資源・保存を実装し、独立したGodot検証画面につなぎました。</p>
 <div class="stats"><div class="stat"><b>412 / 412</b><small>Core試験・全件成功</small></div><div class="stat"><b>95</b><small>今回追加した試験</small></div><div class="stat"><b>9</b><small>実Godotの操作検査</small></div><div class="stat"><b>成功</b><small>別プロセス復旧・途中終了</small></div></div>
 <div class="note"><strong>既存試作は維持しています。</strong><br>新しい時計は秒基準、旧時計は旧入口のまま。通常3D Main・旧保存・原素材は変更していません。今回の一巡は架空地域の検証で、製品の平時画面全体の完成ではありません。</div>
 <div class="grid"><section><h2>実装したこと</h2><ul><li>人物・所属・役職・権限・所在・資源の共通型と整合検査</li><li>命令の受付、密書の開封、任意依頼の受諾、取消と期限</li><li>既存配送を使った再送・遅着、二重実行防止、本人別の表示</li><li>世代保存・背景保存API・隔離読込・破損拒否・乱数の継続</li></ul></section><section><h2>実際に通した流れ</h2><ol class="timeline">'''+steps+'''</ol></section></div>
 <details id="images"><summary>実際に動かしたGodot画面を見る（4枚）</summary><p class="muted">説明用の生成画像ではなく、同じ共通Coreを操作した実画面です。データ検証画面であり、製品UIの完成案ではありません。</p><div class="gallery">'''+gallery+'''</div></details>
 <h2>どこまで進んだか</h2><div class="scroll"><table><thead><tr><th>段階</th><th>今回の到達点</th><th>扱い</th></tr></thead><tbody><tr><td>G1</td><td>共通型・整合検査・旧データ対応・回帰確認の4項目</td><td>完了</td></tr>'''+rows+'''</tbody></table></div>
 <p class="muted">既存317件＋新規95件＝412件成功、失敗・スキップ0。途中終了は保存参照を切り替える前に別プロセスを終了して確認しました。物理電源断や全国規模性能の保証ではありません。</p>
 <details><summary>未実施と、再利用するための記録</summary><ul>'''+''.join('<li>'+esc(x)+'</li>' for x in result['not_completed'])+'''</ul><p>起動入口：<code>Scripts/Show-CommonContract.ps1</code>（独立検証画面）。読取・表示は本人別の情報に限定しています。</p><p><a href="implementation-report.json">実行結果と新規試験一覧</a> ／ <a href="process-verification.json">別プロセス検証</a> ／ <a href="godot-verification.json">Godot検証と画像の対応</a> ／ <a href="common-contract-v1.html">採用した共通契約</a></p></details>
 <section class="next"><h2 style="margin-top:0">次にやること</h2><p>'''+esc(result['next']['task'])+'''</p><p class="muted"><strong>完了条件：</strong>'''+esc(result['next']['done_when'])+'''</p></section>
 <footer>prototype/godot-foundation ／ 仕様採用・試験用実装・製品完成を区別して記録。HTMLは実行証拠から生成し、画像を内蔵。OSのマウス入力・カーソル設定は使っていません。</footer></main></body></html>'''
 write('implementation-report.html',page)
 print('BUILT short report: 412 tests, 95 new, 9 Godot checks, 4 GPU images embedded')
if __name__=='__main__':main()
