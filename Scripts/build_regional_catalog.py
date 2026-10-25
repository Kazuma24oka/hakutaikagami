"""Offline extraction of pinned structured coordinates; no road geometry is invented."""
from pathlib import Path
import json,math,hashlib
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/RegionalRouteV1'
def main():
 src=BASE/'runtime-local/sources/entities.json';entities=json.loads(src.read_bytes())['entities'];stops=[]
 for q,revision in [('Q1357421',2449829991),('Q482065',2554524499),('Q11418956',2493037870)]:
  e=entities[q];assert e['lastrevid']==revision
  v=e['claims']['P625'][0]['mainsnak']['datavalue']['value'];assert v['globe']=='http://www.wikidata.org/entity/Q2'
  stops.append(dict(Id=q,Name=e['labels']['ja']['value'],LatitudeE7=round(v['latitude']*1e7),LongitudeE7=round(v['longitude']*1e7),Revision=str(revision),Precision=str(v['precision'])))
 def distance(a,b):
  la,lb=map(math.radians,[a['LatitudeE7']/1e7,b['LatitudeE7']/1e7]);dlon=math.radians((b['LongitudeE7']-a['LongitudeE7'])/1e7);return round(2*6371000*math.asin(math.sqrt(math.sin((lb-la)/2)**2+math.cos(la)*math.cos(lb)*math.sin(dlon/2)**2))*1000)
 c=dict(Schema=1,Stops=stops,LegLengthsMm=[str(distance(a,b)) for a,b in zip(stops,stops[1:])],FootSpeed=1000,CourierSpeed=2000,Algorithm='haversine-R6371000-mm-round-v1/corridor-ceiling-v1',SourceUrls=['https://rekishi-kanko.pref.aichi.jp/place/place5.html','https://www.city.nagoya.jp/naka/miryoku/1021146/1033230/1049391.html'],Caveat='Historical stop sequence; modern representative coordinates; straight stop-to-stop corridor, NOT reconstructed roads. Distances are great-circle lower bounds. Speeds are prototype coefficients; no terrain/water crossing reconstruction.')
 data=(json.dumps(c,ensure_ascii=False,indent=2)+'\n').encode();(ROOT/'Core/Data/owari-route-v1.json').write_bytes(data)
 manifest=dict(date='2026-10-20',entity_snapshot_sha256=hashlib.sha256(src.read_bytes()).hexdigest(),catalog_sha256=hashlib.sha256(data).hexdigest(),sources=c['SourceUrls'],stops=stops,caveat=c['Caveat'])
 (BASE/'source-manifest.json').write_bytes((json.dumps(manifest,ensure_ascii=False,indent=2)+'\n').encode());print('Built 3 real anchors, 2 coarse links; lengths mm:',c['LegLengthsMm'])
if __name__=='__main__':main()
