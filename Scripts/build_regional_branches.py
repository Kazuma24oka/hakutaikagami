from pathlib import Path
import json,math,hashlib,re
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/RegionalBranchV1';SRC=BASE/'runtime-local/sources'
def main():
 old=json.loads((ROOT/'Core/Data/owari-route-v1.json').read_bytes());raw=(SRC/'tange-codh.html').read_bytes();text=raw.decode();m=re.search(r'緯度経度.*?(35\.[0-9]+),\s*(136\.[0-9]+)',text,re.S);assert m and m.groups()==('35.086879','136.950983')
 t=dict(Id='CODH230000107700',Name='丹下砦跡',LatitudeE7=round(float(m[1])*1e7),LongitudeE7=round(float(m[2])*1e7),Revision='retrieved-2026-10-20;sha256:'+hashlib.sha256(raw).hexdigest(),Precision='not supplied; decimal digits are not accuracy')
 e=json.loads((SRC/'kasadera.json').read_bytes())['entities']['Q3193655'];assert e['lastrevid']==2552800098;v=e['claims']['P625'][0]['mainsnak']['datavalue']['value'];assert v['globe']=='http://www.wikidata.org/entity/Q2'
 k=dict(Id='Q3193655',Name='笠寺観音（笠覆寺）',LatitudeE7=round(v['latitude']*1e7),LongitudeE7=round(v['longitude']*1e7),Revision=str(e['lastrevid']),Precision=str(v['precision']))
 nodes=[dict(Id=id,Stop=stop) for id,stop in zip(['castle','fork','village','tange','kasadera'],[*old['Stops'],t,k])];by={n['Id']:n['Stop'] for n in nodes}
 def distance(a,b):
  x,y=map(math.radians,[a['LatitudeE7']/1e7,b['LatitudeE7']/1e7]);dl=math.radians((b['LongitudeE7']-a['LongitudeE7'])/1e7);return round(2*6371000*1000*math.asin(math.sqrt(math.sin((y-x)/2)**2+math.cos(x)*math.cos(y)*math.sin(dl/2)**2)))
 edges=[dict(Id=id,From=a,To=b,LengthMm=str(distance(by[a],by[b])),FootSpeed=1000,CourierSpeed=2000) for id,a,b in [('road','castle','fork'),('pass','fork','village'),('kasadera-access','fork','kasadera'),('kasadera-tange','kasadera','tange'),('tange-fort','tange','village')]]
 c=dict(Schema=1,Nodes=nodes,Edges=edges,Algorithm='known-evidence-dijkstra-time-ordinal/haversine-R6371000-mm-v1',Sources=old['SourceUrls']+['https://www.city.nagoya.jp/midori/miryoku/1024446/1036842/1024461.html','https://geoshape.ex.nii.ac.jp/nrct-poi/resource/23/230000107700.html','https://www.city.nagoya.jp/minami/miryoku/1023909/1023959/1023965.html'],Credit='『日本歴史地名大系』施設・地点項目データセット（CODH作成） doi:10.20676/00000456 / CC BY 4.0 / 丹下砦跡の座標をE7化・距離導出。精度未提示。',Caveat='Public representative anchors and coarse inferred links, not reconstructed historical roads. Closed pass is synthetic test condition, not a historical event. No bridge location asserted.')
 payload=(json.dumps(c,ensure_ascii=False,indent=2)+'\n').encode();target=ROOT/'Core/Data/owari-branches-v1.json';target.write_bytes(payload)
 manifest=dict(date='2026-10-20',catalog_sha256=hashlib.sha256(payload).hexdigest(),sources=c['Sources'],credit=c['Credit'],license_url='https://creativecommons.org/licenses/by/4.0/',tange=t,caveat=c['Caveat'],nodes=nodes)
 (BASE/'source-manifest.json').write_bytes((json.dumps(manifest,ensure_ascii=False,indent=2)+'\n').encode());print('Built 5 source-linked anchors and 5 coarse links')
if __name__=='__main__':main()
