using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

public enum DotTier { Lord, Samurai, Ronin, Courtier, Monk }
public sealed record DotLook(string Key,string Look,bool War,DotTier? Tier,string? Armor);

// ドット絵の武将の型を、人物の身分・年齢・家から決める（甲冑アニメ工房の型の名前と同じ決まり）。
// 見た目だけの導出で、世界の原本や保存には何も足さない。有名武将は固有の一式を使う。
public static class DotOfficerLooks
{
    public static readonly string[] Armors=["黒漆","紺糸","鉄錆","萌黄","紫","銀白","朱"];
    static readonly Dictionary<string,int> Bands=new(){["young"]=3,["prime"]=4,["old"]=2};
    // 固有の一式：人物IDと名前（改名前の名も）で引く。
    static readonly (string Look,string[] Match)[] Famous=[
        ("nobunaga",["person:nobunaga","織田信長"]),("shingen",["person:shingen","武田信玄"]),
        ("ieyasu",["徳川家康","松平元康"]),("kenshin",["上杉謙信","長尾景虎"]),("masamune",["伊達政宗"]),
        ("yukimura",["真田幸村","真田信繁"]),("tadakatsu",["本多忠勝"]),("naomasa",["井伊直政"]),
        ("hideyoshi",["豊臣秀吉","羽柴秀吉","木下藤吉郎"]),("motonari",["毛利元就"]),("yoshihiro",["島津義弘"])];
    // 家の甲冑の色。決めていない家は家IDから固定で選ぶ。
    static readonly Dictionary<string,string> HouseArmor=new(){["org:oda"]="黒漆",["org:takeda"]="朱",["org:hojo"]="萌黄",["org:imagawa"]="紫",["org:otomo"]="銀白",["org:shimazu"]="鉄錆"};
    static readonly Dictionary<string,DotTier> Fixed=new(){["person:yamashina-tokitsugu"]=DotTier.Courtier,["person:konoe-sakihisa"]=DotTier.Courtier,["person:ashikaga-yoshiteru"]=DotTier.Lord};

    static int Hash(string s)=>(int)(BitConverter.ToUInt32(SHA256.HashData(Encoding.UTF8.GetBytes(s)),0)&0x7fffffff);
    public static string ArmorOf(string? house)=>house==null?"紺糸":HouseArmor.TryGetValue(house,out var a)?a:Armors[Hash("house/"+house)%Armors.Length];
    public static string Band(int? age)=>age is null?"prime":age<25?"young":age<50?"prime":"old";

    public static DotTier TierOf(SetupPerson p,bool head)
    {
        if(Fixed.TryGetValue(p.Id,out var t))return t;
        if(p.Id.StartsWith("person:temple-representative/",StringComparison.Ordinal))return DotTier.Monk;
        if(head||p.InitialRole is StartTemplate.Daimyo or StartTemplate.LocalLord)return DotTier.Lord;
        if(p.Faction!=null||p.Lord!=null)return DotTier.Samurai;
        return DotTier.Ronin;
    }

    public static DotLook For(string id,string name,SetupPerson? person,bool head,string? house,int? age,bool war)
    {
        var famous=Famous.FirstOrDefault(f=>f.Match.Contains(id)||f.Match.Contains(name));
        if(famous.Look!=null)return new("famous/"+famous.Look+(war?"/war":"/peace"),"famous/"+famous.Look,war,null,null);
        var tier=person==null?DotTier.Ronin:TierOf(person,head);string band=Band(age);
        string look=$"generic/{tier.ToString().ToLowerInvariant()}/{band}/{Hash("look/"+id)%Bands[band]}";
        string? armor=war?ArmorOf(house):null;
        return new(war?$"{look}/war/{armor}":$"{look}/peace",look,war,tier,armor);
    }

    // 世界の人物から型を決める。出生した子は名簿の人物として扱う。
    public static DotLook For(ContractWorld w,EntityId person,bool war)
    {
        var data=w.NewGame!;var p=FamilyBirth.People(w).SingleOrDefault(x=>x.Id==person.Value);
        string name=w.Definitions.Characters.SingleOrDefault(c=>c.Id==person)?.Name??p?.Name??person.Value;
        string? house=p?.Faction;bool head=data.Start.Factions.Any(f=>f.Head==person.Value);
        int? age=p?.BirthYear is int born?WorldCalendar.At(w.EpochDate,w.ElapsedSeconds).Year-born:null;
        return For(person.Value,name,p,head,house,age,war);
    }

    public static string Soldier(string? house,string weapon)=>$"soldier/ashigaru/{weapon}/war/{ArmorOf(house)}";
    public const string WarriorMonk="soldier/sohei/war";
}
