"""ドット絵武将の見た目の型を作る（甲冑アニメ工房の部品の組み合わせ）。

- 有名武将は固有の一式。一般の武将は身分×年齢×顔立ちの型と、家ごとの甲冑の色。
- 有名武将専用の前立て・赤備え・金箔は一般の型に使わない。
- 平時は身分に合った和服、戦時は甲冑。兵は足軽と僧兵。
出力の looks.json はゲーム側（Core/Appearance/DotOfficerLooks.cs）と同じ名前の決まりで型を指す。
"""
import json,sys,hashlib
from pathlib import Path

ARMORS=["黒漆","紺糸","鉄錆","萌黄","紫","銀白","朱"]          # 家の色（赤備え・金箔は固有の一式だけ）
LACES={"黒漆":"紺","紺糸":"白","鉄錆":"橙","萌黄":"白","紫":"金","銀白":"紺","朱":"黒"}
EYES=["p_eye_maru","p_eye_tsuri","p_eye_ito","p_eye_surudo","p_eye_rin","p_eye_kirenaga","p_eye_fushi","p_eye_meimoku"]
MOUTHS=["p_mouth_ichi","p_mouth_he","p_mouth_futeki","p_mouth_smile","p_mouth_kui"]
HAIR_YOUNG=["p_hair_wakamusha","p_hair_takayui","p_hair_chasen"]
HAIR_PRIME=["p_hair_mage","p_hair_chasen","p_hair_sou","p_hair_tanpatsu"]
HAIR_OLD=["p_hair_mage","p_hair_sou"]
HIGE_PRIME=[None,"p_hige_kuchi","p_hige_busho","p_hige_ago",None]
HIGE_OLD=["p_hige_shiro","p_hige_kuchi","p_hige_yagi"]
FRONTS_LORD=["kuwagata","nichirin","suigyu"]                    # 一般向けの前立て・脇立
TIERS={
 # 身分: (戦時の一式, 兜, 前立て候補, 面頬, 武器候補, 平時の和服候補, 平時のかぶり物)
 "lord":   (["set_tosei","set_std"],["kabuto2"],FRONTS_LORD,True,["katana"],["w_hitatare","w_daimon"],["samurai_eboshi"]),
 "samurai":(["set_std","set_tosei"],["kabuto","suji"],["kuwagata",None,"kuwagata"],False,["katana","yari","yumi"],["w_kamishimo","w_haori","w_hakama"],[]),
 "ronin":  (["set_std"],["zunari","kabuto"],[None],False,["katana","yari"],["w_kosode","w_hakama"],[]),
 "courtier":(["set_std"],["kabuto"],["kuwagata"],False,["katana"],["w_kariginu"],["tate_eboshi"]),
 "monk":   (["set_sohei"],None,None,False,["naginata"],["w_hoe"],[]),
}
KIMONO={"lord":["紫","萌黄","藍","茶"],"samurai":["藍","墨","茶","鼠","浅葱"],"ronin":["藍","茶","鼠","墨"],"courtier":["浅葱","白","山吹"],"monk":["墨"]}
HAKAMA={"lord":["茶","紺","黒"],"samurai":["紺","鼠","黒","茶"],"ronin":["鼠","茶","紺"],"courtier":["紫","紺"],"monk":["金茶","白"]}
BANDS={"young":3,"prime":4,"old":2}
SKINS=[None,None,"小麦",None,"日焼け"]

def pick(seq,key):
    return seq[int(hashlib.sha256(key.encode()).hexdigest(),16)%len(seq)]

def generic(tier,band,n):
    war_set,helms,fronts,menpo,weapons,peace,hats=TIERS[tier]
    k=f"{tier}/{band}/{n}"
    hair=pick({"young":HAIR_YOUNG,"prime":HAIR_PRIME,"old":HAIR_OLD}[band],k+"/hair")
    if tier=="monk":hair="p_hair_teihatsu"
    hige=None if band=="young" else pick(HIGE_OLD if band=="old" else HIGE_PRIME,k+"/hige")
    face={"eye":pick(EYES,k+"/eye"),"mouth":pick(MOUTHS,k+"/mouth"),"hair":hair,"hige":hige,
          "hairColor":"白銀" if band=="old" and n%2==0 else "黒","eyeColor":"焦茶","skin":pick(SKINS,k+"/skin")}
    head=None
    if helms:
        head=[pick(helms,k+"/helm")]
        f=pick(fronts,k+"/front")
        if f:head.append(f)
        if menpo:head.append("menpo")
    war={**face,"set":pick(war_set,k+"/set"),"head":head,"hand":pick(weapons,k+"/weapon"),"back":["sashimono"] if tier in ("lord","samurai") else []}
    peace={**face,"set":pick(peace,k+"/peace"),"headAdd":hats,"hand":None,
           "kimono":pick(KIMONO[tier],k+"/kimono"),"hakama":pick(HAKAMA[tier],k+"/hakama"),"back":[]}
    return war,peace

def famous():
    # 固有の一式（ツールの一式名）と平時の和服。ゲームの人物IDと名前の両方で引けるようにする。
    rows=[
     ("nobunaga",["person:nobunaga","織田信長"],"set_nobunaga",{"hair":"p_hair_chasen","eye":"p_eye_surudo","mouth":"p_mouth_futeki","hige":"p_hige_kuchi"},"w_dofuku","緋","黒"),
     ("shingen",["person:shingen","武田信玄"],"set_shingen",{"hair":"p_hair_teihatsu","eye":"p_eye_surudo","mouth":"p_mouth_ichi","hige":"p_hige_ago"},"w_hoe","墨","金茶"),
     ("ieyasu",["徳川家康","松平元康"],"set_ieyasu",{"hair":"p_hair_mage","eye":"p_eye_maru","mouth":"p_mouth_ichi","hige":"p_hige_kuchi"},"w_haori","墨","茶"),
     ("kenshin",["上杉謙信","長尾景虎"],"set_kenshin",{"hair":"p_hair_teihatsu","eye":"p_eye_rin","mouth":"p_mouth_ichi","hige":None},"w_hoe","白","白"),
     ("masamune",["伊達政宗"],"set_masamune",{"hair":"p_hair_takayui","eye":"p_eye_gantai","mouth":"p_mouth_futeki","hige":None},"w_haori","藍","紺"),
     ("yukimura",["真田幸村","真田信繁"],"set_yukimura",{"hair":"p_hair_wakamusha","eye":"p_eye_rin","mouth":"p_mouth_ichi","hige":None},"w_kamishimo","緋","紺"),
     ("tadakatsu",["本多忠勝"],"set_tadakatsu",{"hair":"p_hair_mage","eye":"p_eye_surudo","mouth":"p_mouth_kui","hige":"p_hige_tora"},"w_kamishimo","墨","鼠"),
     ("naomasa",["井伊直政"],"set_naomasa",{"hair":"p_hair_takayui","eye":"p_eye_kirenaga","mouth":"p_mouth_ichi","hige":None},"w_kamishimo","緋","紺"),
     ("hideyoshi",["豊臣秀吉","羽柴秀吉","木下藤吉郎"],"set_hideyoshi",{"hair":"p_hair_mage","eye":"p_eye_maru","mouth":"p_mouth_smile","hige":"p_hige_yagi"},"w_dofuku","山吹","金茶"),
     ("motonari",["毛利元就"],"set_motonari",{"hair":"p_hair_mage","eye":"p_eye_ito","mouth":"p_mouth_ichi","hige":"p_hige_shiro"},"w_hitatare","萌黄","茶"),
     ("yoshihiro",["島津義弘"],"set_yoshihiro",{"hair":"p_hair_mage","eye":"p_eye_surudo","mouth":"p_mouth_kui","hige":"p_hige_ago"},"w_hitatare","墨","黒"),
    ]
    out=[]
    for key,names,s,face,peace,kim,hak in rows:
        f={"eye":face["eye"],"mouth":face["mouth"],"hair":face["hair"],"hige":face["hige"],"hairColor":"黒","eyeColor":"焦茶","skin":None}
        out.append({"id":"famous/"+key,"match":names,
                    "war":{**f,"set":s},
                    "peace":{**f,"set":peace,"kimono":kim,"hakama":hak,"headAdd":[],"hand":None,"back":[]}})
    return out

def soldiers():
    rows=[]
    for w in ["yari","teppo","yumi"]:
        rows.append({"id":"soldier/ashigaru/"+w,"war":{"set":"set_ashigaru","hand":w,"eye":"p_eye_maru","mouth":"p_mouth_ichi","hair":"p_hair_tanpatsu","hige":None,"hairColor":"黒","eyeColor":"焦茶","skin":"小麦"}})
    rows.append({"id":"soldier/sohei","war":{"set":"set_sohei","eye":"p_eye_surudo","mouth":"p_mouth_kui","hair":"p_hair_teihatsu","hige":"p_hige_busho","hairColor":"黒","eyeColor":"焦茶","skin":"日焼け"}})
    return rows

def build():
    looks=famous()
    for tier in TIERS:
        for band,count in BANDS.items():
            for n in range(count):
                war,peace=generic(tier,band,n)
                looks.append({"id":f"generic/{tier}/{band}/{n}","war":war,"peace":peace})
    looks+=soldiers()
    return {"schema":1,"armors":ARMORS,"laces":LACES,"looks":looks}

if __name__=="__main__":
    out=Path(sys.argv[1] if len(sys.argv)>1 else Path(__file__).with_name("looks.json"))
    data=build();out.write_text(json.dumps(data,ensure_ascii=False,indent=1)+"\n",encoding="utf-8")
    print(len(data["looks"]),"looks")
