"""甲冑アニメ工房（ユーザー自作のツール）を画面なしのブラウザで動かし、型ごとのアニメを書き出す。

使い方: python bake.py <甲冑アニメ工房/index.html> <looks.json> <出力フォルダ>
- ツール本体は書き換えない。写しに一括処理のスクリプトを足して、作業用フォルダで動かす。
- 出力は型ごとの横並びの絵（行＝動き、列＝コマ）と index.json（大きさ・足元・コマの時間）。
- ちびコマの素体を含むので、出力はローカルに置き、規約の確認まで Git に入れない。
"""
import base64,json,os,re,subprocess,sys,tempfile
from pathlib import Path

BROWSERS=[r"C:\Program Files\Google\Chrome\Application\chrome.exe",r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"]
WAR_ANIMS=["idle","walk","attack","damage","win"]
PEACE_ANIMS=["idle","walk"]

BATCH=r"""
<script>
(async () => {
 const out = document.createElement("pre"); out.id = "bake-out";
 const log = [];
 try {
  const SPEC = __SPEC__;
  while (!parts.ready) await new Promise(r => setTimeout(r, 20));
  function applyLook(L){
    parts.pick = {base:["b_noppera"]};
    for (const s of ["eye","mouth","hair","hige"]) parts.pick[s] = L[s] ? [L[s]] : [];
    parts.hairColor = L.hairColor || "黒"; parts.eyeColor = L.eyeColor || "焦茶";
    parts.skin = L.skin ? SKINS[L.skin] : null;
    applyPartColors(); for (const p of parts.items) if (p.gen) p.px = partPx(p.gen);
    state.mode = "stand"; state.flip = false;
    togglePart(itemOf(L.set));
    if (L.head) parts.pick.head = [...L.head];
    if (L.headAdd) parts.pick.head = [...(parts.pick.head||[]), ...L.headAdd];
    if ("hand" in L) parts.pick.hand = L.hand ? [L.hand] : [];
    if (L.back) parts.pick.back = [...L.back];
    if (L.haori) parts.pick.haori = [...L.haori];
    for (const k of ["armor","lace","kimono","hakama","flag","mon"]) if (L[k]) state[k] = L[k];
    syncPick(); rebuildBuilt();
  }
  for (const job of SPEC){
    applyLook(job.look);
    const ch = state.chars.find(c => c.built);
    const rows = [], meta = {};
    for (const a of job.anims){ const fr = animFrames(ch, a); rows.push(fr.map(f => f.grid)); meta[a] = fr.map(f => f.ms); }
    const c = document.createElement("canvas"); const cols = Math.max(...rows.map(r => r.length));
    c.width = cols*W; c.height = rows.length*H; const x = c.getContext("2d");
    rows.forEach((r, ri) => r.forEach((g, ci) => { const id = x.createImageData(W, H); id.data.set(g); x.putImageData(id, ci*W, ri*H); }));
    log.push({key: job.key, w: W, h: H, ox: OX, oy: OY, anims: job.anims, ms: meta, png: c.toDataURL("image/png")});
  }
  out.textContent = JSON.stringify({ok:true, rows:log});
 } catch (e) { out.textContent = JSON.stringify({ok:false, error: String(e && e.stack || e), done: log.length}); }
 document.body.appendChild(out);
})();
</script>
"""

def jobs(looks):
    out=[]
    for L in looks["looks"]:
        if L["id"].startswith("generic/") or L["id"].startswith("soldier/ashigaru"):
            for armor in looks["armors"]:
                war={**L["war"],"armor":armor,"lace":looks["laces"][armor]}
                out.append({"key":f'{L["id"]}/war/{armor}',"look":war,"anims":WAR_ANIMS})
        else:
            out.append({"key":f'{L["id"]}/war',"look":L["war"],"anims":WAR_ANIMS})
        if "peace" in L:
            out.append({"key":f'{L["id"]}/peace',"look":L["peace"],"anims":PEACE_ANIMS})
    return out

def run(tool,looks_path,outdir,chunk=40):
    html=Path(tool).read_text(encoding="utf-8");looks=json.loads(Path(looks_path).read_text(encoding="utf-8"))
    browser=next(b for b in BROWSERS if Path(b).is_file());todo=jobs(looks);outdir=Path(outdir);outdir.mkdir(parents=True,exist_ok=True)
    index={"schema":1,"source":"甲冑アニメ工房","sheets":{}}
    work=Path(tempfile.mkdtemp(prefix="dotbake-"))
    for start in range(0,len(todo),chunk):
        part=todo[start:start+chunk];page=work/f"bake-{start}.html"
        page.write_text(html.replace("</body>",BATCH.replace("__SPEC__",json.dumps(part,ensure_ascii=False))+"</body>",1),encoding="utf-8")
        dom=subprocess.run([browser,"--headless=new","--disable-gpu","--no-first-run","--user-data-dir="+str(work/"profile"),"--virtual-time-budget=600000","--dump-dom",page.as_uri()],capture_output=True,timeout=900).stdout.decode("utf-8","replace")
        m=re.search(r'<pre id="bake-out">(.*?)</pre>',dom,re.S)
        if not m:raise SystemExit("no output for chunk %d"%start)
        res=json.loads(m.group(1).replace("&quot;",'"').replace("&lt;","<").replace("&gt;",">").replace("&amp;","&"))
        if not res["ok"]:raise SystemExit(res["error"])
        for r in res["rows"]:
            name=r["key"].replace("/","__")+".png"
            (outdir/name).write_bytes(base64.b64decode(r["png"].split(",",1)[1]))
            index["sheets"][r["key"]]={"file":name,"w":r["w"],"h":r["h"],"ox":r["ox"],"oy":r["oy"],"anims":r["anims"],"ms":r["ms"]}
        print("baked",start+len(part),"/",len(todo),flush=True)
    index["looks"]=[{"id":L["id"],"match":L.get("match",[])} for L in looks["looks"]]
    (outdir/"index.json").write_text(json.dumps(index,ensure_ascii=False,indent=1)+"\n",encoding="utf-8")

if __name__=="__main__":run(*sys.argv[1:4])
