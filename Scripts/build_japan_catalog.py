"""Offline deterministic extraction of pinned Natural Earth and Wikidata snapshots."""
from pathlib import Path
import json,hashlib,decimal
ROOT=Path(__file__).resolve().parents[1];SRC=ROOT/'Docs/Design/JapanMapV1/runtime-local/sources';OUT=ROOT/'Game/Data/Geography'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def e7(value):return int((decimal.Decimal(str(value))*10000000).quantize(decimal.Decimal(1),rounding=decimal.ROUND_HALF_UP))
def point(p):return {'LatitudeE7':e7(p[1]),'LongitudeE7':e7(p[0])}
def ring(values):
 result=[]
 for value in values:
  p=point(value)
  if not result or result[-1]!=p:result.append(p)
 if result[0]==result[-1]:result.pop()
 assert len(result)>=3
 return result
def main():
 commit=json.loads((SRC/'ne-commit.json').read_bytes())[0]['sha'];assert commit=='9380cca83db5f9aef52d5e762765100745f84b27'
 earth=json.loads((SRC/'ne-pinned.geojson').read_bytes());feature=next(f for f in earth['features'] if f['properties']['ADMIN']=='Japan')
 polygons=[{'Outer':ring(poly[0]),'Holes':[ring(r) for r in poly[1:]]} for poly in feature['geometry']['coordinates']]
 pages=json.loads((SRC/'castle-pages.json').read_bytes())['query']['pages'].values();entities=json.loads((SRC/'castle-entities.json').read_bytes())['entities'];landmarks=[];omitted=[]
 aliases={'岐阜城':'稲葉山城（表示候補）','若松城':'黒川城（表示候補）','駿府城':'駿府の城館（表示候補）','一乗谷朝倉氏遺跡':'一乗谷（表示候補）','大友氏遺跡':'大友氏館（表示候補）'}
 for page in sorted(pages,key=lambda p:p['title']):
  q=page.get('pageprops',{}).get('wikibase_item');entity=entities.get(q,{});coords=[c for c in entity.get('claims',{}).get('P625',[]) if c['mainsnak'].get('datavalue',{}).get('value',{}).get('globe')=='http://www.wikidata.org/entity/Q2' and c.get('rank')!='deprecated']
  if not coords:omitted.append({'Name':page['title'],'Reason':'No Wikidata Earth coordinate; not invented'});continue
  coords.sort(key=lambda c:c.get('rank')!='preferred');value=coords[0]['mainsnak']['datavalue']['value'];lat=value['latitude'];lon=value['longitude'];assert 24<=lat<=46 and 122<=lon<=146
  landmarks.append({'Id':'geo:'+q,'Name':aliases.get(page['title'],page['title']+'（表示候補）'),'Coordinate':{'LatitudeE7':e7(lat),'LongitudeE7':e7(lon)},'SourceUrl':'https://www.wikidata.org/wiki/'+q,'SourceRevision':str(entity['lastrevid']),'CoordinatePrecision':str(value.get('precision')),'HistoricalStatus':'DisplayCandidateOnly; modern representative point; 1560 footprint/ownership/unverified'})
 assert len(landmarks)==30
 sources=[{'Name':'Natural Earth 1:10m admin-0 Japan geometry','Url':f'https://raw.githubusercontent.com/nvkelso/natural-earth-vector/{commit}/geojson/ne_10m_admin_0_countries.geojson','License':'Public domain: https://www.naturalearthdata.com/about/terms-of-use/','Revision':commit,'Sha256':sha(SRC/'ne-pinned.geojson')}, {'Name':'Wikidata P625 coordinate subset','Url':'https://www.wikidata.org/wiki/Wikidata:Data_access','License':'CC0; structured data only','Revision':'Per landmark SourceRevision; retrieved 2026-10-20','Sha256':sha(SRC/'castle-entities.json')}]
 result={'Schema':1,'Projection':'display-equirectangular-36N-138E-R6371000-v1','Polygons':polygons,'Landmarks':landmarks,'Sources':sources,'Notices':['Modern generalized coastline; not reconstructed 1560 shoreline.','Static public geographic candidates; no troops, ownership or active scenario assignments.','Display projection only; not terrain height, geodesic route length or travel time.']}
 OUT.mkdir(parents=True,exist_ok=True);payload=(json.dumps(result,ensure_ascii=False,separators=(',',':'))+'\n').encode();(OUT/'japan-map-v1.json').write_bytes(payload)
 report={'date':'2026-10-20','catalog_sha256':hashlib.sha256(payload).hexdigest(),'polygons':len(polygons),'vertices':sum(len(p['Outer'])+sum(map(len,p['Holes'])) for p in polygons),'landmarks':len(landmarks),'omitted':omitted,'sources':sources}
 target=ROOT/'Docs/Design/JapanMapV1';target.mkdir(parents=True,exist_ok=True);(target/'source-manifest.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode())
 print('Built',len(polygons),'polygons and',len(landmarks),'source-linked candidates; SHA',report['catalog_sha256'])
if __name__=='__main__':main()
