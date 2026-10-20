from pathlib import Path
import json,math,sys
ROOT=Path(__file__).resolve().parents[1]
def render():
 current=json.loads((ROOT/'Game/Data/Geography/japan-map-v1.json').read_bytes())
 old=json.loads((ROOT/'Core/Data/owari-branches-v1.json').read_bytes())
 result=json.loads(METADATA);nodes=[];edges=[]
 for node,name,start,edge_id in [('inabayama','稲葉山','castle','mino-concept'),('okazaki','岡崎','village','mikawa-concept')]:
  source=next(x for x in current['Landmarks'] if name in x['Name']);a=next(n['Stop'] for n in old['Nodes'] if n['Id']==start);b=source['Coordinate']
  expected={'inabayama':'2512270183','okazaki':'2440512260'}[node]
  assert source['SourceRevision']==expected,'Explicit source update required'
  lat1=math.radians(a['LatitudeE7']/1e7);lat2=math.radians(b['LatitudeE7']/1e7);dl=math.radians((b['LongitudeE7']-a['LongitudeE7'])/1e7)
  v=math.sin((lat2-lat1)/2)**2+math.cos(lat1)*math.cos(lat2)*math.sin(dl/2)**2
  mm=round(6371000000*2*math.atan2(math.sqrt(v),math.sqrt(1-v)))
  nodes.append({'Id':node,'Stop':{'Id':source['Id'][4:],'Name':source['Name'].replace('（表示候補）',''),**b,'Revision':source['SourceRevision'],'Precision':source['CoordinatePrecision']}})
  edges.append({'Id':edge_id,'From':start,'To':node,'LengthMm':str(mm),'FootSpeed':1000,'CourierSpeed':2000})
 ordered={'Schema':result['Schema'],'Nodes':nodes,'Edges':edges,**{k:v for k,v in result.items() if k!='Schema'}}
 return (json.dumps(ordered,ensure_ascii=False,indent=2)+'\n').encode()
def main():
 output=ROOT/'Core/Data/regional-expansion-v1.json';data=render()
 if '--check' in sys.argv:assert output.read_bytes()==data,'Catalog differs from pinned inputs'
 else:output.write_bytes(data)
 print('PASS: reproducible adjacent-region coordinates and geodesic lengths')
METADATA='{"Schema": 1, "Algorithm": "haversine-R6371000-round-mm-v1; abstract interregional links; historic roads/river crossings unverified", "Sources": ["https://www.city.gifu.lg.jp/kankoubunka/kankou/1013051/1005097/1034742/1034737.html", "https://okazaki-kanko.jp/feature/shiroatomeguri/okazaki1", "https://www.archives.go.jp/exhibition/digital/ieyasu/history.html", "https://www.wikidata.org/wiki/Q1367052?oldid=2512270183", "https://www.wikidata.org/wiki/Q1013445?oldid=2440512260"], "Credit": "Wikidata structured coordinates CC0; inherited CODH CC BY 4.0 credit remains mandatory", "Caveat": "Modern representative points. Interregional links are simulation-only geodesic lower bounds, NOT reconstructed roads; Kiso/Yahagi river crossings, bridges, slopes and 1560 access/ownership unverified. No historic bridge is invented."}'
if __name__=="__main__":main()
