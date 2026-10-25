"""Short local HTML report; never embeds or publishes restricted 3D imagery."""
from pathlib import Path
import json,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/RouteWorldV1'
def main():
 r=json.loads((BASE/'verification.json').read_text(encoding='utf-8'))
 assert r['status']=='PASSED' and isinstance(r['visual_review'],dict)
 counters=ET.parse(BASE/'runtime-local/tests/route.trx').find('.//{*}Counters').attrib
 assert counters['failed']=='0' and counters['total']==counters['passed']
 count=counters['passed'];checks=len(r['checks'])+len(r['restore_checks'])
 html='''<!doctype html><html lang="ja"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>百代鏡 — 新Coreと3Dの経路接続</title><style>
:root{color-scheme:dark;font:17px/1.8 "Yu Gothic UI",Meiryo,sans-serif;background:#101a22;color:#e9e9df}body{max-width:1040px;margin:auto;padding:32px 20px}h1{font-size:clamp(25px,4vw,38px);line-height:1.4}h2{font-size:23px;color:#dfc38d;margin-top:36px}p{max-width:900px}a{color:#9ed8e7}section,.metric{background:#1c2a34;border:1px solid #394c58;border-radius:10px;padding:20px}.metrics{display:grid;grid-template-columns:repeat(3,1fr);gap:12px}.metric strong{display:block;font-size:31px;color:#a8dfbf}table{border-collapse:collapse;width:100%}td,th{padding:10px;text-align:left;border-bottom:1px solid #40525d}.images{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0}img{display:block;width:100%;height:auto;border-radius:5px}figcaption,.muted{color:#afc2cb;font-size:14px}code{overflow-wrap:anywhere}.route{display:flex;gap:8px;flex-wrap:wrap;align-items:center;padding:14px 0}.route span{background:#344b50;padding:10px;border-radius:7px}.tag{color:#a8dfbf} @media(max-width:600px){body{padding:20px 12px}.metrics,.images{grid-template-columns:1fr}td,th{padding:6px;font-size:14px}}
</style><header><p class="tag">2026-10-20 / 実装・実測済みの範囲</p><h1>新Coreの移動が、<br>3D表示と保存復旧につながりました。</h1><p>既存の3D武将と秒基準のCoreを接続しました。途中の所在・動作位相と、本人が知る任務・返報が、別プロセスで復旧しても一致しています。</p></header>
<div class="metrics"><div class="metric"><strong>CORE_COUNT件</strong>Core試験・全件成功</div><div class="metric"><strong>GPU_COUNT件</strong>実Godotの操作・復旧検査</div><div class="metric"><strong>0画素</strong>復旧3比較の3D領域差分</div></div>
<h2>今回固めたこと</h2><section><div class="route"><span>城</span>→<span>平道40m<br>徒歩20秒</span>→<span>峠道80m<br>徒歩80秒</span>→<span>村</span></div><p>距離はmm、時間は整数秒。区間ごとの距離÷速度を切り上げて加算します。使者は各区間で徒歩の2倍、全区間の返報は50秒。同じ場所でも書状の受渡しは1秒です。</p><p class="muted">この120mの道と速度・4秒の城内作業は架空の検証入力です。史実地理や製品バランスの確定値とは分けています。経路と係数の版を保存し、違う版を勝手に読み替えません。</p></section>
<h2>実際に通した一巡</h2><table><thead><tr><th>世界時刻</th><th>Coreと画面で確認したこと</th></tr></thead><tbody>
<tr><td>0 → 1秒</td><td>移動命令と書役への委任を発送。到達後に歩き始める。</td></tr><tr><td>5 → 6秒</td><td>書役が作業完了。主君は返報を受けて初めて完了を認識。</td></tr><tr><td>51秒</td><td>経路70m地点で保存。表示の小数秒位相も保持。</td></tr><tr><td>復旧</td><td>同じ／別Godotプロセスで位置・向き・クリップ・位相・任務文言が一致。世界は停止して復旧。</td></tr><tr><td>101 → 151秒</td><td>村へ到着して待機。主君は150秒では結果未確認、151秒の返報で完了を知る。</td></tr></tbody></table>
<h2>実画面</h2><p class="muted">20枚のGPU原本から抜粋。連続12枚の動作も取得しました。既存の素材管理方針に従い、画像・動画・GLBはこのPC内だけに保管し、Gitには追加していません。</p>
<div class="images"><figure><a href="runtime-local/motion.gif"><img src="runtime-local/motion.gif" alt="経路の曲がり角から歩く実Godotの連続描画"></a><figcaption>曲がり角からの歩行（実GPU12フレーム、確認用短縮再生）</figcaption></figure><figure><img src="runtime-local/delegation.png" alt="委任の完了だけを知る主君の画面"><figcaption>主君：委任の完了は既知、移動結果は未確認。</figcaption></figure><figure><img src="runtime-local/midpoint.png" alt="移動途中の保存前"><figcaption>51秒、保存前。</figcaption></figure><figure><img src="runtime-local/restored-process.png" alt="別プロセスで復旧した同一位置と姿勢"><figcaption>別プロセス復旧。3D領域の差分0画素。</figcaption></figure></div>
<h2>起動と限界</h2><p><code>Scripts/Show-RouteWorld.ps1</code>で専用の3D画面を開けます。「移動と書役への委任を発送」→「自動進行 切替」で進みます。停止・保存・復旧・視点切替も操作できます。マウス操作ツールやカーソル設定は使っていません。</p><p>通常Mainと旧セーブは維持しています。今回の接続は架空の一本道まで。全国の実地理、分岐探索、通行権・封鎖・天候、移動中の相手への追跡配送、通常Mainの新Core移行は未実施です。</p><p>根拠：<a href="verification.json">ソース・画像・DLLのSHAと実行ログ</a> ／ <a href="../../Decisions/2026-10-20_ROUTE_WORLD_V1.md">契約・測定範囲</a>。入力が変われば起動前検査を失効させ、再検証します。自己点検であり、新しいユーザー承認を意味しません。</p>
<h2>次の作業</h2><section><p><b>分岐経路と、既知の通行条件による迂回・再計画。</b><br>完了条件：複数経路の所要時間、未知の封鎖情報を漏らさないこと、途中停止・復旧・3D表示の一致を確認すること。</p></section><footer><p class="muted">ブランチ：prototype/godot-foundation。main / design/foundationは変更していません。</p></footer></html>'''.replace('CORE_COUNT',count).replace('GPU_COUNT',str(checks))
 (BASE/'report.html').write_bytes(html.encode('utf-8'))
 print('Built concise route report with local GPU references')
if __name__=='__main__':main()
