using System;
using System.Linq;
using System.Collections.Generic;
namespace HakutaiKagami.Core.Appearance;

public class OfficerAppearance
{
    public int Schema { get; set; } = 3;
    public string Model { get; set; } = "v013";
    public string Body { get; set; } = "v013_base";
    public string Face { get; set; } = "v013_face";
    public string Hair { get; set; } = "none_under_helmet";
    public Dictionary<string,string> Equipment {get;set;} = new();
    public string Beard { get; set; } = "none";
    public float[] HairSrgb { get; set; } = {1,1,1};
    public Dictionary<string,float[]> MaterialSrgb { get; set; } = new() { ["cloth"]=new float[]{1,1,1}, ["lacquer"]=new float[]{1,1,1}, ["metal"]=new float[]{1,1,1} };
    public float[] SkinLinear { get; set; } = { .87f, .73f, .59f };
    public float[] GearSrgb { get; set; } = { 1, 1, 1 };
    public Dictionary<string,bool> Enabled { get; set; } = new();
    public Dictionary<string,float[]> SlotSrgb { get; set; } = new();
    public void Validate(IEnumerable<string> slots)
    {
        var keys = slots.Order().ToArray();
        if(!Equipment.Keys.Order().SequenceEqual(keys) || Equipment.Any(p=>p.Value!="silver" && (p.Value!="red_lamellar" || !AppearanceCatalog.RedSlots.Contains(p.Key))))throw new InvalidOperationException("Unknown equipment ID/slot");
        if (Schema != 3 || Model != "v013" || Body != "v013_base" || !new[]{"v013_face","mature_v1"}.Contains(Face) || !new[]{"none_under_helmet","tied_v1"}.Contains(Hair) || !new[]{"none","short_v1"}.Contains(Beard) || !MaterialSrgb.Keys.Order().SequenceEqual(new[]{"cloth","lacquer","metal"}) || !Enabled.Keys.Order().SequenceEqual(keys) || !SlotSrgb.Keys.Order().SequenceEqual(keys)) throw new InvalidOperationException("Unknown appearance schema/model/slots");
        foreach (var rgb in new[] { SkinLinear, GearSrgb, HairSrgb }.Concat(SlotSrgb.Values).Concat(MaterialSrgb.Values))
            if (rgb.Length != 3 || rgb.Any(v => !float.IsFinite(v) || v < 0 || v > 1)) throw new InvalidOperationException("Invalid RGB");
    }
}

public static class AppearanceCatalog
{
    public static readonly string[] Slots={"mask","weapon","underclothes","cloak","helmet","torso","neck_guard","shoulders","arms","feet","waist","thighs","shins"};
    public static readonly string[] RedSlots={"mask","helmet","torso","neck_guard","shoulders","arms","feet","waist","thighs","shins"};
    public static OfficerAppearance Default(int index)
    {
        var a=new OfficerAppearance();
        foreach(var slot in Slots){a.Enabled[slot]=true;a.Equipment[slot]=index==1&&RedSlots.Contains(slot)?"red_lamellar":"silver";a.SlotSrgb[slot]=[1,1,1];}
        if(index>0){a.Face="mature_v1";a.Hair="tied_v1";a.Beard="short_v1";a.Enabled["mask"]=false;a.Enabled["helmet"]=false;a.Enabled["cloak"]=false;}
        return a;
    }
}
