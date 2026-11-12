using Godot;
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text.Json;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.World;

public sealed record NationalDistrictControlTexture(Texture2D Texture,RoutePoint NorthWest,RoutePoint SouthEast,int MappedPixels);

public static class NationalDistrictControlOverlay
{
    static RoutePoint P(GeoCoordinate g){var p=JapanGeography.Project(g);return new(p.EastMm,p.NorthMm);}
    public static NationalDistrictControlTexture Build(NationalDistrictControlState state)
    {
        if(state.Schema!=1||state.Policy!=NationalDistrictControl.Policy)throw new InvalidDataException("郡支配の版が違います");
        using var border=JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://Data/Geography/borders-v2.json"));
        var root=border.RootElement;var raster=root.GetProperty("Raster");int rows=raster.GetProperty("Rows").GetInt32(),cols=raster.GetProperty("Cols").GetInt32();
        int factor=4,width=cols/factor,height=rows/factor;float south=raster.GetProperty("South").GetSingle(),west=raster.GetProperty("West").GetSingle(),cell=raster.GetProperty("Cell").GetSingle();
        var northWest=P(new((int)Math.Round((south+rows*cell)*1e7),(int)Math.Round(west*1e7)));var southEast=P(new((int)Math.Round(south*1e7),(int)Math.Round((west+cols*cell)*1e7)));
        var districts=new Dictionary<int,NationalDistrictAssignment?>();foreach(var d in root.GetProperty("Districts").EnumerateArray()){int id=d.GetProperty("Id").GetInt32();districts[id]=NationalDistrictControl.Resolve(state,id.ToString(),d.GetProperty("Province").GetString()!,d.GetProperty("Name").GetString()!);}
        var colorByFaction=new Dictionary<string,Color>(StringComparer.Ordinal);
        Color Faction(string id){if(colorByFaction.TryGetValue(id,out var c))return c;uint h=2166136261;foreach(char ch in id)h=unchecked((h^ch)*16777619);c=Color.FromHsv((h%360)/360f,.46f,.88f);colorByFaction[id]=c;return c;}
        var data=new byte[width*height*4];int mapped=0;
        using var file=System.IO.File.OpenRead(ProjectSettings.GlobalizePath("res://Data/Geography/district-raster-v1.bin"));using var gzip=new GZipStream(file,CompressionMode.Decompress);using var reader=new BinaryReader(gzip);
        if(reader.ReadInt32()!=rows||reader.ReadInt32()!=cols)throw new InvalidDataException("郡の範囲の大きさが合いません");
        for(int row=0;row<rows;row++)
        {
            int count=reader.ReadInt32();var runs=new (int Start,int End,int District)[count];for(int i=0;i<count;i++){int start=reader.ReadUInt16(),length=reader.ReadUInt16(),id=reader.ReadUInt16();if(length==0||start+length>cols)throw new InvalidDataException("郡の範囲が不正です");runs[i]=(start,start+length,id);}
            if(row%factor!=factor/2)continue;int y=(rows-1-row)/factor,run=0;
            for(int x=0;x<width;x++)
            {
                int col=x*factor+factor/2;while(run<runs.Length&&runs[run].End<=col)run++;
                int id=run<runs.Length&&runs[run].Start<=col?runs[run].District:0;if(id==0)continue;
                Color color=new("55595d");
                if(districts.TryGetValue(id,out var assignment)&&assignment is {Owners.Length:>0} a)
                {
                    var owner=a.Owners.Length==1?a.Owners[0]:a.Owners[(x+y)%a.Owners.Length];color=Faction(NationalStart.FactionId(owner.Faction));
                    if(a.NameInferred&&(x+y)%8==0)color=color.Lightened(.35f);mapped++;
                }
                int offset=(y*width+x)*4;data[offset]=(byte)Math.Clamp(Math.Round(color.R*255),0,255);data[offset+1]=(byte)Math.Clamp(Math.Round(color.G*255),0,255);data[offset+2]=(byte)Math.Clamp(Math.Round(color.B*255),0,255);data[offset+3]=255;
            }
        }
        var image=Image.CreateEmpty(width,height,false,Image.Format.Rgba8);image.SetData(width,height,false,Image.Format.Rgba8,data);
        return new(ImageTexture.CreateFromImage(image),northWest,southEast,mapped);
    }
}
