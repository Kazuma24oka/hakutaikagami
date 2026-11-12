// 百代鏡：地図の陸地を国・郡に必ず1つずつ割り当て、そこから境界線を作る（2026-11-12 ユーザーの指摘：
// どの郡に属するか分からない線があると勢力の色で塗れない）。
//
//   dotnet run --project Tools/DistrictRaster -c Release -- [郡地図の zip]
//     既定：D:/01_資料/論文/戦国時代/戦国DB/MAPDATA/郡地図_Ver_1_1_11.zip
//     → Game/Data/Geography/borders-v2.json（国・郡の名前と位置、境界線、湖）と district-raster-v1.bin（郡の範囲の升目、gzip）
//
// 決まり：
//  1. 地図の陸地（japan-terrain-mask-v1 と北方領土の島。湖は除く）を約0.002度の升目に分ける（北緯30〜45.7度・東経128.4〜149度）。
//  2. 国は「国地図」、郡は「郡地図」で決める。郡の国と国地図の国が食い違う升目は国を優先し、その升目は同じ国の隣の郡に取り込む。
//  3. どの郡にも入らない陸（明治の市・区、埋め立て地、海岸のずれ）は、同じ国の隣の郡に取り込む（近い升目から順に広げる）。
//     国も決まらない陸は、隣の升目の国と郡に取り込む。島などで隣がなければ、一番近い升目の国と郡。
//  4. 境界線は、隣り合う陸の升目の郡が違う所。国が違えば国の線、同じ国なら郡の線。だからどの線も、両側の郡が決まっていて、
//     必ず海岸か別の線まで続く。線は角をなめらかにし、海岸の端は地図の海岸まで伸ばす。
//  「郡地図 Ver 1.1」（郡地図研究会、CC BY 4.0）を加工して作成。北海道は国「蝦夷」、渡島国の範囲は郡「渡島」。
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using HakutaiKagami.Core.World;

static class Program
{
    const double S=30.0,N=45.7,W=128.4,E=149.0,C=0.002;
    static readonly int Rows=(int)Math.Round((N-S)/C),Cols=(int)Math.Round((E-W)/C);
    static readonly HashSet<string> Hokkaido=["渡島国","後志国","石狩国","天塩国","北見国","胆振国","日高国","十勝国","釧路国","根室国","千島国"];

    static int Main(string[] args)
    {
        string zipPath=args.Length>0?args[0]:"D:/01_資料/論文/戦国時代/戦国DB/MAPDATA/郡地図_Ver_1_1_11.zip";
        string root=FindRoot();string geo=Path.Combine(root,"Game","Data","Geography");
        using var zip=ZipFile.OpenRead(zipPath);
        var provRows=ReadDbf(zip,"国地図Ver1_1_11");var provShapes=ReadShp(zip,"国地図Ver1_1_11");
        var distRows=ReadDbf(zip,"郡地図Ver1_1_11");var distShapes=ReadShp(zip,"郡地図Ver1_1_11");
        // 国と郡の番号（0 は未定）
        var provNames=new List<string>{""};var provIndex=new Dictionary<string,int>();
        int Prov(string kuni){string n=Hokkaido.Contains(kuni)?"蝦夷":kuni.TrimEnd('国');if(!provIndex.TryGetValue(n,out int i)){i=provNames.Count;provNames.Add(n);provIndex[n]=i;}return i;}
        var distNames=new List<(int Prov,string Name)>{(0,"")};var distIndex=new Dictionary<(int,string),int>();
        int Dist(string kuni,string gun)
        {
            int p=Prov(kuni);string n=Hokkaido.Contains(kuni)?(kuni=="渡島国"?"渡島":""):gun;
            if(!distIndex.TryGetValue((p,n),out int i)){i=distNames.Count;distNames.Add((p,n));distIndex[(p,n)]=i;}return i;
        }
        long cells=(long)Rows*Cols;Console.WriteLine($"升目 {Rows}x{Cols}");
        var land=new byte[cells];var prov=new ushort[cells];var dist=new ushort[cells];
        // 1. 陸地
        var mask=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,"Core","Data","japan-terrain-mask-v1.json"))).RootElement.GetProperty("Polygons");
        var north=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(geo,"northern-territories-v1.json"))).RootElement.GetProperty("Polygons");
        foreach(var poly in mask.EnumerateArray().Concat(north.EnumerateArray()))Fill(RingsOf(poly),(i,a,b)=>{for(long k=i+a;k<i+b;k++)land[k]=1;});
        // 湖（国の形の穴と、地球地図日本の湖・広い川面）は陸から外す
        var lakeRings=new List<(double,double)[]>();
        foreach(var shape in provShapes)foreach(var ring in shape)if(SignedArea(ring)>0.0002)lakeRings.Add(ring);  // 国の形の穴（この向きが穴）
        var terrain=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(geo,"terrain-v1.json"))).RootElement;
        foreach(var l in terrain.GetProperty("Lakes").EnumerateArray())lakeRings.Add(l.EnumerateArray().Select(q=>(q[0].GetInt32()/1e7,q[1].GetInt32()/1e7)).ToArray());
        foreach(var ring in lakeRings)Fill([ring],(i,a,b)=>{for(long k=i+a;k<i+b;k++)land[k]=0;});
        // 2. 国と郡
        for(int s=0;s<provShapes.Count;s++){int p=Prov(provRows[s]["KUNI"]);Fill(provShapes[s],(i,a,b)=>{for(long k=i+a;k<i+b;k++)prov[k]=(ushort)p;});}
        for(int s=0;s<distShapes.Count;s++)
        {
            var r=distRows[s];if(r["GUN"].EndsWith("市")||r["GUN"].EndsWith("区"))continue;
            int d=Dist(r["KUNI"],r["GUN"]);Fill(distShapes[s],(i,a,b)=>{for(long k=i+a;k<i+b;k++)dist[k]=(ushort)d;});
        }
        long conflict=0;
        for(long k=0;k<cells;k++){if(land[k]==0){prov[k]=0;dist[k]=0;continue;}if(dist[k]!=0&&prov[k]!=0&&distNames[dist[k]].Prov!=prov[k]){dist[k]=0;conflict++;}}
        Console.WriteLine($"国を優先して郡から外した升目 {conflict}");
        // 3a. 国の決まらない陸：隣の国から広げる
        Spread(land,prov,(from,to)=>true);
        // 3b. 郡の決まらない陸：同じ国の隣の郡から広げる
        Spread(land,dist,(from,to)=>prov[to]==0||distNames[dist[from]].Prov==prov[to]);
        // 3c. まだ決まらない陸（島など）：海もたどって一番近い升目の郡
        long left=0;for(long k=0;k<cells;k++)if(land[k]==1&&dist[k]==0)left++;
        if(left>0)SpreadThroughSea(land,dist);
        for(long k=0;k<cells;k++)if(land[k]==1&&dist[k]!=0)prov[k]=(ushort)distNames[dist[k]].Prov;
        long unassigned=0;for(long k=0;k<cells;k++)if(land[k]==1&&dist[k]==0)unassigned++;
        Console.WriteLine($"島などで海をたどって決めた升目 {left}・決まらない陸 {unassigned}");
        // 4. 境界線
        var landIndex=new LandMaskIndex(mask.EnumerateArray().Concat(north.EnumerateArray()).Select(ToGeo).ToArray());
        var (provLines,distLines,openEnds)=Borders(land,dist,distNames,landIndex);
        Console.WriteLine($"国の線 {provLines.Count}・郡の線 {distLines.Count}・海岸にも線にも届かない端 {openEnds}");
        // 名前の位置：その国・郡の升目の重心に一番近い、その国・郡の升目
        var labels=Labels(land,dist,distNames.Count);var provLabels=ProvinceLabels(land,dist,distNames,provNames.Count);
        // 郡の範囲の升目（色で塗るため）：行ごとに [始まりの列, 長さ, 郡] の並び、gzip
        string rasterFile=Path.Combine(geo,"district-raster-v1.bin");
        using(var fs=File.Create(rasterFile))using(var gz=new GZipStream(fs,CompressionLevel.SmallestSize))using(var bw=new BinaryWriter(gz))
        {
            bw.Write(Rows);bw.Write(Cols);
            for(int r=0;r<Rows;r++)
            {
                var runs=new List<(int,int,int)>();long b0=(long)r*Cols;
                for(int c=0;c<Cols;){int d=dist[b0+c];int c0=c;while(c<Cols&&dist[b0+c]==d)c++;if(d!=0)runs.Add((c0,c-c0,d));}
                bw.Write(runs.Count);foreach(var (c0,len,d) in runs){bw.Write((ushort)c0);bw.Write((ushort)len);bw.Write((ushort)d);}
            }
        }
        var oldLakes=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(geo,"borders-v1.json"))).RootElement.GetProperty("Lakes").Clone();
        var doc=new Dictionary<string,object>{
            ["Schema"]=1,["Id"]="borders-v2",
            ["Source"]=new Dictionary<string,string>{["Name"]="郡地図 Ver 1.1（郡地図研究会）",["License"]="CC BY 4.0",["Credit"]="「郡地図 Ver 1.1」を加工して作成",
                ["Edits"]="地図の陸地を約0.002度の升目に分け、国は国地図・郡は郡地図で決め（食い違いは国を優先）、どの郡にも入らない陸は同じ国の隣の郡に取り込み、境界線は隣り合う升目の郡が違う所から作った。明治の国（北海道は蝦夷、渡島国は郡「渡島」）。明治の市・区は郡にしない。日本測地系から世界測地系へ近似で変換"},
            ["Provinces"]=Enumerable.Range(1,provNames.Count-1).Where(p=>provLabels[p]!=null).Select(p=>new Dictionary<string,object>{["Id"]=p,["Name"]=provNames[p],["Label"]=provLabels[p]!}).ToArray(),
            ["Districts"]=Enumerable.Range(1,distNames.Count-1).Where(d=>labels[d]!=null&&distNames[d].Name.Length>0).Select(d=>new Dictionary<string,object>{["Id"]=d,["Province"]=provNames[distNames[d].Prov],["Name"]=distNames[d].Name,["Label"]=labels[d]!}).ToArray(),
            ["ProvinceLines"]=provLines,["DistrictLines"]=distLines,["Lakes"]=oldLakes,
            ["Raster"]=new Dictionary<string,object>{["File"]="district-raster-v1.bin",["South"]=S,["West"]=W,["Cell"]=C,["Rows"]=Rows,["Cols"]=Cols},
            ["Checks"]=new Dictionary<string,long>{["UnassignedLand"]=unassigned,["OpenEnds"]=openEnds}};
        File.WriteAllText(Path.Combine(geo,"borders-v2.json"),JsonSerializer.Serialize(doc,new JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping})+"\n",new UTF8Encoding(false));
        Console.WriteLine($"borders-v2.json {new FileInfo(Path.Combine(geo,"borders-v2.json")).Length/1024}KB・district-raster-v1.bin {new FileInfo(rasterFile).Length/1024}KB");
        return unassigned==0&&openEnds==0?0:1;
    }

    static string FindRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d!=null&&!File.Exists(Path.Combine(d.FullName,"global.json")))d=d.Parent;return d!.FullName;}

    // シェープファイル（国地図・郡地図）：日本測地系の緯度経度→世界測地系（近似の式）
    static (double,double) TokyoToWgs(double lat,double lon)=>(lat-0.00010695*lat+0.000017464*lon+0.0046017,lon-0.000046038*lat-0.000083043*lon+0.010040);
    static List<List<(double,double)[]>> ReadShp(ZipArchive zip,string name)
    {
        var e=zip.Entries.First(x=>x.FullName.EndsWith(name+".shp"));using var ms=new MemoryStream();e.Open().CopyTo(ms);var b=ms.ToArray();
        var outp=new List<List<(double,double)[]>>();int i=100;
        while(i<b.Length)
        {
            int len=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(i+4));int o=i+8;i+=8+len*2;
            int st=BitConverter.ToInt32(b,o);if(st==0){outp.Add([]);continue;}
            int np=BitConverter.ToInt32(b,o+36),npt=BitConverter.ToInt32(b,o+40);int po=o+44;int pt=po+4*np;
            var parts=Enumerable.Range(0,np).Select(k=>BitConverter.ToInt32(b,po+4*k)).Append(npt).ToArray();var rings=new List<(double,double)[]>();
            for(int k=0;k<np;k++){var ring=new (double,double)[parts[k+1]-parts[k]];for(int j=parts[k];j<parts[k+1];j++){double x=BitConverter.ToDouble(b,pt+16*j),y=BitConverter.ToDouble(b,pt+16*j+8);ring[j-parts[k]]=TokyoToWgs(y,x);}rings.Add(ring);}
            outp.Add(rings);
        }
        return outp;
    }
    static List<Dictionary<string,string>> ReadDbf(ZipArchive zip,string name)
    {
        var e=zip.Entries.First(x=>x.FullName.EndsWith(name+".dbf"));using var ms=new MemoryStream();e.Open().CopyTo(ms);var b=ms.ToArray();
        int n=BitConverter.ToInt32(b,4);int hl=BitConverter.ToUInt16(b,8),rl=BitConverter.ToUInt16(b,10);var fields=new List<(string,int)>();int i=32;
        while(b[i]!=0x0D){fields.Add((Encoding.ASCII.GetString(b,i,11).TrimEnd('\0'),b[i+16]));i+=32;}
        var rows=new List<Dictionary<string,string>>();
        for(int r=0;r<n;r++){int off=hl+r*rl+1;var row=new Dictionary<string,string>();foreach(var (f,len) in fields){row[f]=Encoding.UTF8.GetString(b,off,len).Trim();off+=len;}rows.Add(row);}
        return rows;
    }
    static double SignedArea((double,double)[] r){double a=0;for(int i=0;i<r.Length;i++){var (y1,x1)=r[i];var (y2,x2)=r[(i+1)%r.Length];a+=x1*y2-x2*y1;}return a/2;}
    static List<(double,double)[]> RingsOf(JsonElement poly)=>new[]{poly.GetProperty("Outer")}.Concat(poly.GetProperty("Holes").EnumerateArray()).Select(r=>r.EnumerateArray().Select(q=>(q.GetProperty("LatitudeE7").GetInt32()/1e7,q.GetProperty("LongitudeE7").GetInt32()/1e7)).ToArray()).ToList();
    static GeoPolygon ToGeo(JsonElement poly){GeoCoordinate[] R(JsonElement r)=>r.EnumerateArray().Select(q=>new GeoCoordinate(q.GetProperty("LatitudeE7").GetInt32(),q.GetProperty("LongitudeE7").GetInt32())).ToArray();return new(R(poly.GetProperty("Outer")),poly.GetProperty("Holes").EnumerateArray().Select(R).ToArray());}

    // 輪の組（偶奇）で塗る：升目の中心が内側の列の範囲を、行ごとに span(行の先頭, 始まりの列, 終わりの列) で返す
    static void Fill(List<(double,double)[]> rings,Action<long,int,int> span)
    {
        double lo=rings.SelectMany(r=>r).Min(p=>p.Item1),hi=rings.SelectMany(r=>r).Max(p=>p.Item1);
        int r0=Math.Max(0,(int)Math.Floor((lo-S)/C)),r1=Math.Min(Rows-1,(int)Math.Ceiling((hi-S)/C));if(r1<r0)return;
        var edges=new List<(double,double,double,double)>();foreach(var r in rings)for(int i=0;i<r.Length;i++){var a=r[i];var b=r[(i+1)%r.Length];if(a.Item1!=b.Item1)edges.Add((a.Item1,a.Item2,b.Item1,b.Item2));}
        var byRow=new List<int>[r1-r0+1];for(int k=0;k<byRow.Length;k++)byRow[k]=new();
        for(int ei=0;ei<edges.Count;ei++){var (y1,_,y2,_)=edges[ei];int a=Math.Max(r0,(int)Math.Floor((Math.Min(y1,y2)-S)/C-0.5)),b=Math.Min(r1,(int)Math.Ceiling((Math.Max(y1,y2)-S)/C-0.5));for(int r=a;r<=b;r++)byRow[r-r0].Add(ei);}
        var xs=new List<double>();
        for(int r=r0;r<=r1;r++)
        {
            double y=S+(r+0.5)*C;xs.Clear();
            foreach(int ei in byRow[r-r0]){var (y1,x1,y2,x2)=edges[ei];if((y1>y)!=(y2>y))xs.Add(x1+(x2-x1)*(y-y1)/(y2-y1));}
            if(xs.Count<2)continue;xs.Sort();
            for(int k=0;k+1<xs.Count;k+=2){int c0=Math.Max(0,(int)Math.Ceiling((xs[k]-W)/C-0.5)),c1=Math.Min(Cols,(int)Math.Ceiling((xs[k+1]-W)/C-0.5));if(c1>c0)span((long)r*Cols,c0,c1);}
        }
    }

    // 決まっている陸の升目から、決まらない陸の升目へ広げる（4方向、近い順）
    static void Spread(byte[] land,ushort[] value,Func<long,long,bool> allow)
    {
        var q=new Queue<int>();for(int k=0;k<value.Length;k++)if(land[k]==1&&value[k]!=0)q.Enqueue(k);
        while(q.Count>0){int k=q.Dequeue();foreach(long n in Neighbors(k))if(land[n]==1&&value[n]==0&&allow(k,n)){value[n]=value[k];q.Enqueue((int)n);}}
    }
    static void SpreadThroughSea(byte[] land,ushort[] dist)
    {
        var carry=(ushort[])dist.Clone();var q=new Queue<int>();for(int k=0;k<dist.Length;k++)if(land[k]==1&&dist[k]!=0)q.Enqueue(k);
        while(q.Count>0){int k=q.Dequeue();foreach(long n in Neighbors(k))if(carry[n]==0){carry[n]=carry[k];if(land[n]==1)dist[n]=carry[k];q.Enqueue((int)n);}}
    }
    static IEnumerable<long> Neighbors(long k){long r=k/Cols,c=k%Cols;if(r>0)yield return k-Cols;if(r<Rows-1)yield return k+Cols;if(c>0)yield return k-1;if(c<Cols-1)yield return k+1;}

    // 境界線：隣り合う陸の升目の郡が違う辺を、格子の点でつないだ線にする
    static (List<int[][]> Prov,List<int[][]> Dist,long Open) Borders(byte[] land,ushort[] dist,List<(int Prov,string Name)> names,LandMaskIndex coast)
    {
        var graphs=new[]{new Dictionary<long,List<long>>(),new Dictionary<long,List<long>>()};// 0=国 1=郡
        var all=new Dictionary<long,int>();
        long V(long r,long c)=>r*(Cols+1)+c;
        void Add(int kind,long a,long b){(graphs[kind].TryGetValue(a,out var la)?la:graphs[kind][a]=[]).Add(b);(graphs[kind].TryGetValue(b,out var lb)?lb:graphs[kind][b]=[]).Add(a);all[a]=all.GetValueOrDefault(a)+1;all[b]=all.GetValueOrDefault(b)+1;}
        for(long r=0;r<Rows;r++)for(long c=0;c<Cols;c++)
        {
            long k=r*Cols+c;if(land[k]==0)continue;int d=dist[k];
            if(c+1<Cols&&land[k+1]==1&&dist[k+1]!=d)Add(names[d].Prov!=names[dist[k+1]].Prov?0:1,V(r,c+1),V(r+1,c+1));
            if(r+1<Rows&&land[k+Cols]==1&&dist[k+Cols]!=d)Add(names[d].Prov!=names[dist[k+Cols]].Prov?0:1,V(r+1,c),V(r+1,c+1));
        }
        // 点のまわりの4升に陸でない升があれば海岸の点
        bool Shore(long v){long r=v/(Cols+1),c=v%(Cols+1);for(long dr=-1;dr<=0;dr++)for(long dc=-1;dc<=0;dc++){long rr=r+dr,cc=c+dc;if(rr<0||cc<0||rr>=Rows||cc>=Cols||land[rr*Cols+cc]==0)return true;}return false;}
        long open=0;var result=new List<int[][]>[]{new(),new()};
        for(int kind=0;kind<2;kind++)
        {
            var g=graphs[kind];var used=new HashSet<(long,long)>();
            bool Stop(long v)=>g[v].Count!=2||all[v]!=2;
            foreach(var start in g.Keys)foreach(var nb in g[start])
            {
                if(used.Contains((start,nb))||!Stop(start))continue;  // 線は端か分かれ目から始める
                var chain=new List<long>{start};long prev=start,cur=nb;used.Add((start,nb));used.Add((nb,start));
                while(true){chain.Add(cur);if(Stop(cur)||cur==start)break;long nx=g[cur][0]==prev?g[cur][1]:g[cur][0];if(used.Contains((cur,nx)))break;used.Add((cur,nx));used.Add((nx,cur));prev=cur;cur=nx;}
                result[kind].Add(Smooth(chain,v=>Shore(v),coast));
            }
            // 輪だけの線（中に分かれ目のない島の境など）
            foreach(var start in g.Keys)foreach(var nb in g[start])
            {
                if(used.Contains((start,nb)))continue;var chain=new List<long>{start};long prev=start,cur=nb;used.Add((start,nb));used.Add((nb,start));
                while(cur!=start){chain.Add(cur);long nx=g[cur][0]==prev?g[cur][1]:g[cur][0];if(used.Contains((cur,nx)))break;used.Add((cur,nx));used.Add((nx,cur));prev=cur;cur=nx;}
                chain.Add(start);result[kind].Add(Smooth(chain,v=>false,coast));
            }
        }
        // 端は、ほかの線とつながる点か海岸の点でなければならない
        foreach(var kind in new[]{0,1})foreach(var g in new[]{graphs[kind]})foreach(var (v,list) in g)if(list.Count==1&&all[v]==1&&!Shore(v))open++;
        return (result[0],result[1],open);
    }
    // 格子の点の列→緯度経度。角をなめらかにし（端は動かさない）、間引き、海岸の端は地図の海岸まで伸ばす
    static int[][] Smooth(List<long> chain,Func<long,bool> shore,LandMaskIndex coast)
    {
        var pts=chain.Select(v=>(S+(v/(Cols+1))*C,W+(v%(Cols+1))*C)).ToList();
        for(int it=0;it<3&&pts.Count>2;it++){var np=new List<(double,double)>{pts[0]};for(int i=0;i+1<pts.Count;i++){var a=pts[i];var b=pts[i+1];if(i>0)np.Add((a.Item1*0.75+b.Item1*0.25,a.Item2*0.75+b.Item2*0.25));if(i+1<pts.Count-1)np.Add((a.Item1*0.25+b.Item1*0.75,a.Item2*0.25+b.Item2*0.75));}np.Add(pts[^1]);pts=np;}
        pts=Simplify(pts,0.0002);
        (double,double) Extend((double,double) end,(double,double) prev)
        {
            double dy=end.Item1-prev.Item1,dx=end.Item2-prev.Item2,n=Math.Sqrt(dy*dy+dx*dx);if(n==0)return end;dy/=n;dx/=n;var cur=end;
            if(!coast.Contains(G(cur)))return end;
            for(int i=0;i<40;i++){var nx=(cur.Item1+dy*0.0002,cur.Item2+dx*0.0002);if(!coast.Contains(G(nx)))return nx;cur=nx;}
            return end;
        }
        if(shore(chain[0])&&pts.Count>1)pts[0]=Extend(pts[0],pts[1]);
        if(shore(chain[^1])&&pts.Count>1)pts[^1]=Extend(pts[^1],pts[^2]);
        return pts.Select(p=>new[]{(int)Math.Round(p.Item1*1e7),(int)Math.Round(p.Item2*1e7)}).ToArray();
    }
    static GeoCoordinate G((double,double) p)=>new((int)Math.Round(p.Item1*1e7),(int)Math.Round(p.Item2*1e7));
    static List<(double,double)> Simplify(List<(double,double)> p,double tol)
    {
        if(p.Count<3)return p;var keep=new bool[p.Count];keep[0]=keep[^1]=true;var st=new Stack<(int,int)>();st.Push((0,p.Count-1));
        while(st.Count>0){var (a,b)=st.Pop();double dy=p[b].Item1-p[a].Item1,dx=p[b].Item2-p[a].Item2,n=Math.Sqrt(dx*dx+dy*dy);double best=0;int idx=-1;
            for(int k=a+1;k<b;k++){double d=n==0?Math.Sqrt(Math.Pow(p[k].Item1-p[a].Item1,2)+Math.Pow(p[k].Item2-p[a].Item2,2)):Math.Abs(dy*(p[k].Item2-p[a].Item2)-dx*(p[k].Item1-p[a].Item1))/n;if(d>best){best=d;idx=k;}}
            if(best>tol&&idx>0){keep[idx]=true;st.Push((a,idx));st.Push((idx,b));}}
        return p.Where((_,i)=>keep[i]).ToList();
    }
    static int[]?[] Labels(byte[] land,ushort[] dist,int count)
    {
        var sy=new double[count];var sx=new double[count];var n=new long[count];
        for(long k=0;k<dist.LongLength;k++){int d=dist[k];if(d==0)continue;sy[d]+=k/Cols;sx[d]+=k%Cols;n[d]++;}
        var best=new double[count];Array.Fill(best,double.MaxValue);var at=new long[count];
        for(long k=0;k<dist.LongLength;k++){int d=dist[k];if(d==0)continue;double cy=sy[d]/n[d],cx=sx[d]/n[d];double dd=Math.Pow(k/Cols-cy,2)+Math.Pow(k%Cols-cx,2);if(dd<best[d]){best[d]=dd;at[d]=k;}}
        return Enumerable.Range(0,count).Select(d=>n[d]==0?null:new[]{(int)Math.Round((S+(at[d]/Cols+0.5)*C)*1e7),(int)Math.Round((W+(at[d]%Cols+0.5)*C)*1e7)}).ToArray();
    }
    static int[]?[] ProvinceLabels(byte[] land,ushort[] dist,List<(int Prov,string Name)> names,int count)
    {
        var sy=new double[count];var sx=new double[count];var n=new long[count];
        for(long k=0;k<dist.LongLength;k++){int d=dist[k];if(d==0)continue;int p=names[d].Prov;sy[p]+=k/Cols;sx[p]+=k%Cols;n[p]++;}
        var best=new double[count];Array.Fill(best,double.MaxValue);var at=new long[count];
        for(long k=0;k<dist.LongLength;k++){int d=dist[k];if(d==0)continue;int p=names[d].Prov;double dd=Math.Pow(k/Cols-sy[p]/n[p],2)+Math.Pow(k%Cols-sx[p]/n[p],2);if(dd<best[p]){best[p]=dd;at[p]=k;}}
        return Enumerable.Range(0,count).Select(p=>n[p]==0?null:new[]{(int)Math.Round((S+(at[p]/Cols+0.5)*C)*1e7),(int)Math.Round((W+(at[p]%Cols+0.5)*C)*1e7)}).ToArray();
    }
}
