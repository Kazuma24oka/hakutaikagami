"""全国1560年の移動網に標高と川線の移動負荷を記録する（再生成可能な派生版）。

入力：国土地理院「地球地図日本」標高30秒ラスタ・河川線、同出典から作った水面ポリゴン、v1城拠点網、v1全国経路網。
川・湖沼の横断は根拠のある橋や渡しを意味せず、線分とデータ上の推定横断負荷。
道の形は測量済み史料から得られない限り推定連絡路とし、現代道路形状は入力しない。
"""
from __future__ import annotations
import hashlib, io, json, math, struct, sys, zipfile
from pathlib import Path
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'Core'/'Data'
OUT=DATA/'castle-routes-1560-v2.json'
SRC=Path(sys.argv[1]) if len(sys.argv)>1 else Path('D:/01_資料/論文/戦国時代/戦国DB/MAPDATA/gm-japan')
PIXEL_DEG=1/120
WEST,NORTH=120.0,50.0

def point_in_ring(lat,lon,ring):
    inside=False; j=len(ring)-1
    for i,(yi,xi) in enumerate(ring):
        yj,xj=ring[j]
        if (yi>lat)!=(yj>lat) and lon<(xj-xi)*(lat-yi)/(yj-yi)+xi: inside=not inside
        j=i
    return inside

def orient(a,b,c):
    return (b[1]-a[1])*(c[0]-a[0])-(b[0]-a[0])*(c[1]-a[1])

def segment_hits_ring(a,b,ring):
    if point_in_ring(a[0],a[1],ring) or point_in_ring(b[0],b[1],ring): return True
    def on(p,q,r): return min(p[1],q[1])-1e-10<=r[1]<=max(p[1],q[1])+1e-10 and min(p[0],q[0])-1e-10<=r[0]<=max(p[0],q[0])+1e-10
    j=len(ring)-1
    for c in ring:
        d=ring[j]; ab1,ab2=orient(a,b,c),orient(a,b,d); cd1,cd2=orient(c,d,a),orient(c,d,b)
        if (ab1==0 and on(a,b,c)) or (ab2==0 and on(a,b,d)) or (cd1==0 and on(c,d,a)) or (cd2==0 and on(c,d,b)) or (ab1>0)!=(ab2>0) and (cd1>0)!=(cd2>0): return True
        j=(j+1)%len(ring)
    return False

def read_shp(z,name):
    b=z.read(f'gm-jpn-all_u_2_2/{name}.shp'); out=[]; i=100
    while i<len(b):
        _,length=struct.unpack('>II',b[i:i+8]); rec=io.BytesIO(b[i+8:i+8+length*2]); i+=8+length*2
        st=struct.unpack('<i',rec.read(4))[0]
        if st==0: out.append([]); continue
        rec.read(32); np_,npt=struct.unpack('<ii',rec.read(8)); parts=list(struct.unpack(f'<{np_}i',rec.read(4*np_)))+[npt]
        pts=struct.unpack(f'<{2*npt}d',rec.read(16*npt))
        out.append([[(pts[2*k+1],pts[2*k]) for k in range(a,c)] for a,c in zip(parts,parts[1:])])
    return out

def km(a,b):
    la1,lo1,la2,lo2=map(math.radians,(a[0],a[1],b[0],b[1])); h=math.sin((la2-la1)/2)**2+math.cos(la1)*math.cos(la2)*math.sin((lo2-lo1)/2)**2
    return 12742*math.asin(math.sqrt(h))

def main():
    z=zipfile.ZipFile(SRC/'gm-jpn-all_u_2_2.zip')
    lines=[line for feature in read_shp(z,'riverl_jpn') for line in feature if len(line)>=2]
    # Elevation TIFF is 30 arc-second palette values at approximately 25m steps; 0 denotes no-data/water.
    elzip=zipfile.ZipFile(SRC/'gm-jpn-el_u_1_1.zip'); tif=next(n for n in elzip.namelist() if n.endswith('/el.tif'))
    elev=Image.open(io.BytesIO(elzip.read(tif))); ew,eh=elev.size
    river=Image.new('1',(ew,eh),0); draw=ImageDraw.Draw(river)
    for line in lines:
        pts=[]
        for lat,lon in line:
            x=round((lon-WEST)/PIXEL_DEG); y=round((NORTH-lat)/PIXEL_DEG)
            if 0<=x<ew and 0<=y<eh: pts.append((x,y))
            else:
                if len(pts)>1: draw.line(pts,fill=1,width=1)
                pts=[]
        if len(pts)>1: draw.line(pts,fill=1,width=1)
    water_path=ROOT/'Game'/'Data'/'Geography'/'terrain-v1.json'
    water_doc=json.loads(water_path.read_text(encoding='utf-8'))
    waterbodies=[]
    for ring in water_doc['Lakes']:
        points=[(lat/1e7,lon/1e7) for lat,lon in ring]
        if len(points)>=3:
            waterbodies.append((min(p[0] for p in points),max(p[0] for p in points),min(p[1] for p in points),max(p[1] for p in points),points))
    national=json.loads((DATA/'national-routes-v1.json').read_text(encoding='utf-8'))
    old=json.loads((DATA/'castle-routes-1560-v1.json').read_text(encoding='utf-8'))
    regional=json.loads((DATA/'regional-expansion-v1.json').read_text(encoding='utf-8'))
    owari=json.loads((DATA/'owari-branches-v1.json').read_text(encoding='utf-8'))
    outside={n['Id'] for n in national['Nodes'] if n['LatitudeE7']<289000000 and n['LongitudeE7']<1310000000}
    node={n['Id']:(n['LatitudeE7']/1e7,n['LongitudeE7']/1e7) for n in national['Nodes'] if n['Id'] not in outside}
    node.update({n['Id']:(n['Stop']['LatitudeE7']/1e7,n['Stop']['LongitudeE7']/1e7) for n in regional['Nodes']})
    node.update({n['Id']:(n['Stop']['LatitudeE7']/1e7,n['Stop']['LongitudeE7']/1e7) for n in owari['Nodes']})
    node.update({n['Id']:(n['LatitudeE7']/1e7,n['LongitudeE7']/1e7) for n in old['Nodes']})
    catalog_nodes={n['Id'] for n in national['Nodes']}
    straight={e['Id'] for e in national['Edges'] if e['Mode']=='land' and e['From'] in catalog_nodes and e['To'] in catalog_nodes}
    kept=[e.copy() for e in national['Edges'] if e['Id'] not in straight and e['From'] not in outside and e['To'] not in outside]
    edges=[e.copy() for e in owari['Edges']]+[e.copy() for e in regional['Edges']]+kept+[e.copy() for e in old['Edges']]
    factor_summary=[]; total_cross=0; total_water=0; adjusted=0; max_factor=1_000_000
    for e in edges:
        e.setdefault('Mode','land'); e.setdefault('Basis','既存の全国連絡路（元資料の確度表記を維持）')
        if e['Mode']!='land':
            e.update(BaseLengthMm=str(e['LengthMm']),TerrainFactorPpm=1_000_000,EstimatedRiverCrossings=0,EstimatedInlandWaterCrossings=0)
            continue
        a,b=node[e['From']],node[e['To']]; d=km(a,b); steps=max(1,math.ceil(d/0.5)); heights=[]; river_samples=[]
        for i in range(steps+1):
            f=i/steps; lat=a[0]+(b[0]-a[0])*f; lon=a[1]+(b[1]-a[1])*f
            x=round((lon-WEST)/PIXEL_DEG); y=round((NORTH-lat)/PIXEL_DEG)
            if 0<=x<ew and 0<=y<eh:
                v=elev.getpixel((x,y)); heights.append(None if v==0 else v*25)
                river_samples.append(bool(river.getpixel((x,y))))
            else: heights.append(None); river_samples.append(False)
        grades=[]
        for i in range(1,len(heights)):
            if heights[i-1] is not None and heights[i] is not None:
                segment=max(d/steps,0.05); grades.append(abs(heights[i]-heights[i-1])/(segment*1000))
        mean_grade=sum(grades)/len(grades) if grades else 0.0
        # Undirected travel: rolling terrain raises cost; no slope becomes a hard barrier.
        terrain=1.0 if d<1.0 else min(1.5,1.0+mean_grade)
        crossing=0; prev=False
        if d>=1.0:
            for wet in river_samples:
                if wet and not prev: crossing+=1
                prev=wet
        # Existing GSI waterbody polygons include lakes and broad river surfaces; count estimated chord intersections once per polygon.
        water=0
        if d>=1.0 and crossing==0:
            for minlat,maxlat,minlon,maxlon,ring in waterbodies:
                if max(min(a[0],b[0]),minlat)>min(max(a[0],b[0]),maxlat) or max(min(a[1],b[1]),minlon)>min(max(a[1],b[1]),maxlon): continue
                if segment_hits_ring(a,b,ring): water+=1
        # Crossing locations are inferred from source geometries; each adds a small explicit estimated burden, never a hard barrier.
        base=int(e['LengthMm']); ppm=round(terrain*1_000_000)
        adjusted_mm=round(base*ppm/1_000_000+(crossing+water)*250_000*1.25)
        e.update(BaseLengthMm=str(e['LengthMm']),LengthMm=str(adjusted_mm),TerrainFactorPpm=ppm,EstimatedRiverCrossings=crossing,EstimatedInlandWaterCrossings=water)
        factor_summary.append(ppm/1e6); total_cross+=crossing; total_water+=water; adjusted+=1; max_factor=max(max_factor,ppm)
    doc={"Schema":1,"Id":"castle-routes-1560-v2","WorldYear":1560,"BasedOn":old['Id'],
         "Database":old['Database'],"DatabaseSource":old['DatabaseSource'],
         "TerrainSource":{"Name":"地球地図日本（国土地理院）","Credit":"「地球地図日本」（国土地理院）を加工して作成","Terms":"国土地理院コンテンツ利用規約","Elevation":"標高30秒GeoTIFF。パレット値を約25m単位として読み、欠損・水面値0は斜面計算から除外。経路を約500m間隔で標本化して平均絶対標高差/距離を算出。斜面係数=min(1.5, 1+平均絶対勾配)。","Rivers":"河川線 riverl を標高ラスタと同じ30秒格子へラスタ化し、経路標本上の連続した線を推定横断として数え、1回あたり推定312.5mの追加距離。橋・渡しの史実主張ではない。","InlandWater":"既存 terrain-v1.json の湖・広い川面（地球地図日本由来）に推定線分が交わり、その辺で河川線を数えていない場合、1ポリゴンあたり312.5mを追加。線と面の二重計上を避ける。実際の道筋・渡河設備の史実主張ではない。","Roads":"national-routes-v1 の資料付き経路と castle-routes-1560-v1 の城間推定連絡路を維持。現代道路データは使わず、裏付けのない城間は推定のまま。","Raster":"gm-jpn-el_u_1_1.zip の jpn/el.tif と gm-jpn-all_u_2_2.zip の riverl_jpn.shp。","ElevationZipSha256":hashlib.sha256((SRC/'gm-jpn-el_u_1_1.zip').read_bytes()).hexdigest(),"VectorZipSha256":hashlib.sha256((SRC/'gm-jpn-all_u_2_2.zip').read_bytes()).hexdigest(),"InlandWaterSha256":hashlib.sha256(water_path.read_bytes()).hexdigest()},
         "Policy":"v1の拠点と経路を固定し、陸路の所要距離へ標高由来の遅延と推定河川・湖沼の小さな負荷を加えた全国開始専用版。橋・渡し・史実の道筋を新たに断定しない。","Nodes":old['Nodes'],"Edges":[{"Id":e['Id'],"From":e['From'],"To":e['To'],"LengthMm":str(e['LengthMm']),"FootSpeed":e['FootSpeed'],"CourierSpeed":e['CourierSpeed'],"Mode":e['Mode'],"Basis":e.get('HistoricalCertainty') or e.get('Basis') or "既存の地域経路（確度は対応する元カタログを参照）","BaseLengthMm":str(e['BaseLengthMm']),"TerrainFactorPpm":e['TerrainFactorPpm'],"EstimatedRiverCrossings":e['EstimatedRiverCrossings'],"EstimatedInlandWaterCrossings":e['EstimatedInlandWaterCrossings']} for e in sorted(edges,key=lambda e:e['Id'])]}
    OUT.write_text(json.dumps(doc,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8',newline='\n')
    print(f'{OUT.name}: nodes={len(doc["Nodes"])} edges={len(edges)} adjusted-land={adjusted} river-crossings={total_cross} inland-water-crossings={total_water} mean-factor={sum(factor_summary)/max(1,len(factor_summary)):.4f} max-factor={max_factor/1e6:.4f}')

if __name__=='__main__': main()
