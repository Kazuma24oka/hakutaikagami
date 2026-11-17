using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

// S09B（2026-11-17）：全国の軍勢の編成・指揮・攻城。一覧（NationalSystems）に Armies2 のある新規の全国開始だけ（全国の家の内政 Domestic2・水運と補給 Logistics2・人物 Persons2 を使う）。
// ・作戦線（陣形）：城10以上で、目標が天下か国の統一の家は、主の線（兵の6割）と別の国への二の線（4割）を同じ月に出せる。二の線は軍団長が率いる。
// ・軍団と一族の分隊：城15以上の家は、本拠の国の外の国ごとに軍団を置き、一族（当主の子・兄弟）の成人を先に、ほかは統率の高い家臣を軍団長にする。
// ・兵種の編成：家の鉄砲・軍馬・武具から鉄砲足軽・騎馬・槍の割合を決め、攻城では鉄砲、野戦では騎馬が効き、率いる人の対応する技能が効き目を変える。
// ・募兵：軍備の方針で銭のある家は、市で雇い兵を雇う（3か月。負けると逃げる）。
// ・現場の裁量：別の線の軍団長は、着いて相手が自分より強ければ、当主の命を待たずに兵を退く（戦わず、城も落ちない）。
// ・区画の攻城：山城・平山城は区画を一つずつ取る。落とせなかった攻めでも取った区画は残り、次の攻めで守りが弱い。半分を取られた城は、城兵の退去を条件に開城しやすい。
// ・第三者の救援：同盟の家の援軍は、本拠から城までの日数が15日を超えれば合戦に間に合わない（区画の残る山城は30日まで待てる）。
// ・競合する命令：従う家（国人）は、主の家の攻めへの加勢と、守る家との約束が重なれば、約束を守るか主に従うかを関係で決め、記録に残す。
// ・逃げた人の帰還：捕縛から逃げた人は、逃げた拠点から自分の家の本拠まで道のりの日数をかけて戻る。
// ・追跡補給：実際に動く軍勢（本人の近く）への兵糧は、目標の城ではなく、その時の軍勢の位置まで運ぶ。
public sealed record ArmyCorps(EntityId House,string Province,EntityId Commander,bool Kin,long At,long Revision);
public sealed record ArmyEvent(string Id,string Kind,string? Site,EntityId? House,EntityId? Other,EntityId? Person,long At,long Revision,long Amount,string Text)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public long? Until {get;init;}
}
public sealed record SiegeProgress(string Site,EntityId Attacker,int Taken,int Sections,long At);

public static class Armies2
{
    public const string Name=NationalSystems.Armies2;
    public static bool On(ContractWorld w)=>NationalSystems.On(w,Name)&&Logistics2.On(w)&&Persons2.On(w);
    const long Day=86400;
    static int Draw(ContractWorld w,string key)=>(int)(BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"armies-v1/{w.Seed}/{key}")))%1000);
    static string Hash(string s)=>ContractJson.Hash(Encoding.UTF8.GetBytes(s))[..12];
    static string HouseName(ContractWorld w,EntityId h)=>w.Definitions.Organizations.SingleOrDefault(o=>o.Id==h)?.Name??h.Value;
    static string PersonName(ContractWorld w,EntityId p)=>w.Definitions.Characters.SingleOrDefault(c=>c.Id==p)?.Name??p.Value;
    static string SiteName(ContractWorld w,string site)=>w.Definitions.Sites.SingleOrDefault(s=>s.Id.Value==site)?.Name??site;
    static string HomeOf(ContractWorld w,string house)=>w.NewGame!.Start.Factions.FirstOrDefault(f=>f.Id==house)?.Home??"";

    public static ArmyCorps[] Corps(ContractWorld w,EntityId house)=>(w.Strategy?.Corps??[]).Where(c=>c.House==house).ToArray();
    public static ArmyEvent[] Events(ContractWorld w)=>w.Strategy?.Armies??[];
    public static SiegeProgress? Siege(ContractWorld w,string site)=>w.Strategy?.Sieges?.FirstOrDefault(x=>x.Site==site);

    // ---- 二の線（NationalStrategy の家ごとの出陣の後） ----
    // 主の線と別の国の、自分より弱い（1.5倍以上の差の）相手の城を、二の線の的にする。
    internal static (string Site,string Owner)? SecondLine(ContractWorld w,string house,string firstSite,(string Site,string? Owner)[] targets,Func<string,long> ratio,int castles,long now)
    {
        if(!On(w)||castles<10||Persons2.Goal(w,new(house))?.Goal is not(Persons2.Tenka or Persons2.Unify))return null;
        var first=CentralStanding.ProvinceOf(new(firstSite));
        var t=targets.Where(t=>t.Owner!=null&&t.Site!=firstSite&&CentralStanding.ProvinceOf(new(t.Site))!=first&&ratio(t.Owner!)*4/10>=150).OrderByDescending(t=>ratio(t.Owner!)).ThenBy(t=>t.Site,StringComparer.Ordinal).FirstOrDefault();
        return t.Site==null?null:(t.Site,t.Owner!);
    }
    public static EntityId? Commander(ContractWorld w,string house,string site)=>Corps(w,new(house)).FirstOrDefault(c=>c.Province==CentralStanding.ProvinceOf(new(site)))?.Commander;

    // ---- 合戦（NationalStrategy.Settle から） ----
    public sealed record Mix(int Guns,int Horse,int Spear);
    public static Mix Composition(ContractWorld w,StrategyState s,EntityId house,long soldiers)
    {
        var g=s.Houses?.FirstOrDefault(h=>h.House==house)?.Goods;if(g==null||soldiers<=0)return new(0,0,100);
        int guns=(int)Math.Min(30,g.Guns*100/Math.Max(1,soldiers)),horse=(int)Math.Min(30,g.Horses*100/Math.Max(1,soldiers));
        return new(guns,horse,100-guns-horse);
    }
    internal static (long A,long D) Effective(ContractWorld w,StrategyState s,StrategyCampaign c,long a,long d)
    {
        if(!On(w))return (a,d);
        // 線の割合。
        if(c.Share is int share)a=a*share/100;
        // 雇い兵。
        a+=Hired(s,c.Attacker,c.At);d+=Hired(s,c.Defender,c.At);
        // 兵種：攻城（山城・平山城）は鉄砲、野戦（平城・ほか）は騎馬。率いる人の技能（50を境に）で効き目が変わる。
        bool castle=Warfare2.CastleType(c.Target) is "山城" or "平山城";
        long Arms(EntityId house,long v,EntityId leader)
        {
            var m=Composition(w,s,house,v);var kind=castle?DevelopmentSkill.Gun:DevelopmentSkill.Cavalry;int share2=castle?m.Guns:m.Horse;
            int skill=Persons2.Skill(w,leader,kind,c.At);
            return v*(1000+share2*(castle?3:2)*(50+skill)/100)/1000;
        }
        var leader=c.Commander??HouseSuccession.Head(w,c.Attacker);
        a=Arms(c.Attacker,a,leader);d=Arms(c.Defender,d,HouseSuccession.Head(w,c.Defender));
        // 区画の攻城：取った区画の分だけ守りが弱い。
        if(Siege(w,c.Target.Value) is {} sg&&sg.Attacker==c.Attacker&&sg.Sections>0)d=d*(sg.Sections-sg.Taken)/sg.Sections;
        return (a,d);
    }
    static long Hired(StrategyState s,EntityId house,long at)=>(s.Armies??[]).Where(e=>e.Kind=="hire"&&e.House==house&&e.At<=at&&at<(e.Until??0)).Sum(e=>e.Amount);
    // 開城の条件：半分の区画を取られた城（操作する人物の家でない）は、城兵の退去を条件に開城する。
    internal static bool Yields(ContractWorld w,StrategyCampaign c)=>On(w)&&Siege(w,c.Target.Value) is {} sg&&sg.Attacker==c.Attacker&&sg.Taken*2>=sg.Sections&&HouseSuccession.Head(w,c.Defender)!=w.PlayerControl;
    // 現場の裁量：二の線の軍団長は、相手が自分より強ければ退く。
    internal static bool Withdraws(ContractWorld w,StrategyCampaign c,long a,long d)=>On(w)&&c.Line==2&&c.Commander!=null&&d>a;
    // 救援の到着：同盟の家の本拠から城までの日数が15日（区画の残る山城は30日）を超えれば間に合わない。
    internal static bool Late(ContractWorld w,StrategyCampaign c,EntityId ally)
    {
        if(!On(w))return false;long days=Domestic2.Days(HomeOf(w,ally.Value),c.Target.Value);
        return days>(Warfare2.CastleType(c.Target)=="山城"?30:15);
    }
    // 追跡補給：実際の軍勢の今の位置までの日数。
    internal static long? TrackingDays(ContractWorld w,StrategyCampaign c)
    {
        if(!On(w)||c.Real!=true||NationalStrategy.RealArmy(w,c) is not {} army)return null;
        var home=BranchRoutes.GraphNodes(w).FirstOrDefault(n=>"site:"+n.Id==HomeOf(w,c.Attacker.Value))?.Position;if(home==null)return null;
        // 軍勢の今の位置＝率いる大将のその時の道の上の位置。
        var p=RegionalCampaign.RoadActorPointAt(w,army.Commander,w.ElapsedSeconds);
        long de=p.EastMm-home.EastMm,dn=p.NorthMm-home.NorthMm;return 1+(long)Math.Sqrt((double)de*de+(double)dn*dn)/30_000_000;
    }

    // ---- 月の初め（NationalStrategy.Apply から） ----
    internal static StrategyState Month(ContractWorld w,StrategyState s,Dictionary<string,string?> owners)
    {
        if(!On(w))return s;
        long now=w.ElapsedSeconds,rev=w.CommitSequence;var date=WorldCalendar.At(w.EpochDate,now);
        var events=(s.Armies??[]).ToList();var corps=(s.Corps??[]).ToList();var sieges=(s.Sieges??[]).ToList();
        if(date.Month==1)events=events.Where(e=>now-e.At<365*Day||(e.Until??0)>now).ToList();
        void Add(string kind,string? site,EntityId? house,EntityId? other,EntityId? person,long amount,string text,long? until=null)=>
            events.Add(new($"army:{kind}/{date:yyyyMM}/{Hash(kind+"/"+site+"/"+house?.Value+"/"+other?.Value+"/"+person?.Value+"/"+events.Count)}",kind,site,house,other,person,now,rev,amount,text){Until=until});
        var alive=w.Characters.Where(c=>c.Life==LifeState.Adult).Select(c=>c.Id).ToHashSet();
        var houses=(s.Houses??[]).ToDictionary(h=>h.House.Value,h=>h,StringComparer.Ordinal);
        var pm=date.AddMonths(-1);long prevStart=NationalStrategy.MonthStart(w,pm.Year,pm.Month);
        var settled=s.Outcomes.Where(o=>o.At>prevStart&&o.At<=now).ToArray();

        // 軍団：城15以上の家は、本拠の国の外の国ごとに軍団長を置く（一族の成人を先に）。亡くなった軍団長は替える。
        foreach(var f in w.NewGame!.Start.Factions.OrderBy(f=>f.Id,StringComparer.Ordinal))
        {
            var own=owners.Where(x=>x.Value==f.Id).Select(x=>x.Key).ToArray();var house=new EntityId(f.Id);var head=HouseSuccession.Head(w,house);
            if(own.Length<15||head==w.PlayerControl||!alive.Contains(head))continue;
            var homeP=CentralStanding.ProvinceOf(new(f.Home));
            var members=FamilyBirth.People(w).Where(p=>p.Faction==f.Id&&p.Id!=head.Value&&alive.Contains(new EntityId(p.Id))).ToArray();
            bool KinOf(SetupPerson p)=>p.BirthParents?.Contains(head.Value)==true||FamilyBirth.People(w).SingleOrDefault(x=>x.Id==head.Value)?.BirthParents is {} hp&&p.BirthParents?.Intersect(hp).Any()==true;
            foreach(var prov in own.Select(x=>CentralStanding.ProvinceOf(new(x))).Where(p=>p!=null&&p!=homeP).Distinct().OrderBy(p=>p,StringComparer.Ordinal))
            {
                var cur=corps.LastOrDefault(c=>c.House==house&&c.Province==prov);if(cur!=null&&alive.Contains(cur.Commander))continue;
                var used=corps.Where(c=>c.House==house&&alive.Contains(c.Commander)).Select(c=>c.Commander).ToHashSet();
                var pick=members.Where(p=>!used.Contains(new EntityId(p.Id))).OrderByDescending(KinOf).ThenByDescending(p=>Persons2.Skill(w,new(p.Id),DevelopmentSkill.Tactics,now)+Persons2.Skill(w,new(p.Id),DevelopmentSkill.Drill,now)).ThenBy(p=>p.Id,StringComparer.Ordinal).FirstOrDefault();
                if(pick==null)continue;bool kin=KinOf(pick);
                corps.Add(new(house,prov!,new(pick.Id),kin,now,rev));Add("corps",null,house,null,new(pick.Id),0,$"{HouseName(w,house)}が{pick.Name}を{prov}の軍団長に任じた{(kin?"（一族の分隊）":"")}");
            }
        }
        // 募兵：軍備の方針で銭が千貫以上ある家（操作する人物の家でない）は、3か月の雇い兵を雇う（銭の1割で、千貫ごとに兵100）。
        foreach(var h in houses.Values.Where(h=>h.Policy==Domestic2.Arms&&h.Coin>=1000&&HouseSuccession.Head(w,h.House)!=w.PlayerControl&&owners.ContainsValue(h.House.Value)).OrderBy(h=>h.House.Value,StringComparer.Ordinal).ToArray())
        {
            if(events.Any(e=>e.Kind=="hire"&&e.House==h.House&&(e.Until??0)>now))continue;
            long men=h.Coin/1000*100;houses[h.House.Value]=h with{Coin=h.Coin-h.Coin/10};
            Add("hire",null,h.House,null,null,men,$"{HouseName(w,h.House)}が雇い兵{men}人を雇った",now+90*Day);
        }
        // 負けた家の雇い兵は逃げる。
        foreach(var o in settled)
        {
            var c=s.Campaigns.Single(x=>x.Id==o.Campaign);var loser=o.Captured?c.Defender:c.Attacker;
            for(int i=0;i<events.Count;i++)if(events[i] is var e&&e.Kind=="hire"&&e.House==loser&&(e.Until??0)>now){events[i]=e with{Until=now};Add("desert",null,loser,null,null,e.Amount,$"{HouseName(w,loser)}の雇い兵が負けて逃げた");}
        }
        // 区画の攻城：落ちなかった攻めの区画を一つ取る（攻めが守りより強いとき）。落ちた城の記録は消す。半分を取られた城は退去の条件で開城しやすい。
        foreach(var o in settled)
        {
            var c=s.Campaigns.Single(x=>x.Id==o.Campaign);
            if(o.Captured){sieges.RemoveAll(x=>x.Site==c.Target.Value);continue;}
            int sections=Warfare2.CastleType(c.Target) switch{"山城"=>4,"平山城"=>3,_=>0};if(sections==0)continue;
            bool won=o.Battle is {} b?b.DefenderLost>b.AttackerLost:false;if(!won)continue;
            var cur=sieges.FirstOrDefault(x=>x.Site==c.Target.Value&&x.Attacker==c.Attacker);
            int taken=Math.Min(sections-1,(cur?.Taken??0)+1);sieges.RemoveAll(x=>x.Site==c.Target.Value);sieges.Add(new(c.Target.Value,c.Attacker,taken,sections,now));
            Add("section",c.Target.Value,c.Attacker,c.Defender,null,taken,$"{HouseName(w,c.Attacker)}が{SiteName(w,c.Target.Value)}の区画を取った（{taken}/{sections}）");
            if(taken*2>=sections)Add("terms",c.Target.Value,c.Defender,c.Attacker,null,0,$"{SiteName(w,c.Target.Value)}の城兵は、退去を条件に開城を申し出た");
        }
        // 競合する命令：攻める家に従う家（国人）が、守る家と約束を結んでいれば、関係（約束の残りの長さ）で主に従うか約束を守るかを決める。
        foreach(var c in s.Campaigns.Where(c=>c.At==now||c.At>prevStart&&c.At<now))
            foreach(var v in owners.Values.Where(v=>v!=null&&NationalStrategy.DbOverlord(w,v!)==c.Attacker.Value).Distinct().OrderBy(v=>v,StringComparer.Ordinal))
            {
                var pact=(s.Pacts??[]).FirstOrDefault(p=>p.Broken==null&&p.From<=now&&now<p.Until&&(p.A.Value==v&&p.B==c.Defender||p.B.Value==v&&p.A==c.Defender));if(pact==null)continue;
                if(events.Any(e=>e.Kind=="conflict"&&e.Site==c.Id.Value&&e.House?.Value==v))continue;
                bool keep=pact.Until-now>365*Day;
                events.Add(new($"army:conflict/{Hash(c.Id.Value+"/"+v)}","conflict",c.Id.Value,new(v!),c.Attacker,null,now,rev,keep?1:0,$"{HouseName(w,new(v!))}は{HouseName(w,c.Attacker)}の加勢の命と{HouseName(w,c.Defender)}との約束が重なり、{(keep?"約束を守って加わらなかった":"主に従って加わった")}"));
            }
        // 現場の裁量で退いた二の線（戦わず、城も落ちない）を記録する。
        foreach(var o in settled.Where(o=>!o.Captured&&o.Battle==null&&o.Surrendered==null))
        {
            var c=s.Campaigns.Single(x=>x.Id==o.Campaign);if(c.Line!=2||c.Commander is not {} cm)continue;
            Add("withdraw",c.Target.Value,c.Attacker,c.Defender,cm,0,$"{PersonName(w,cm)}（軍団長）は{SiteName(w,c.Target.Value)}の守りが強いと見て、自分の判断で兵を退いた");
        }
        // 逃げた人の帰還：逃げた拠点から本拠まで道のりの日数をかけて戻る（記録が届く時刻）。
        foreach(var e in (s.Escapes??[]).Where(e=>e.At>prevStart&&e.At<=now))
        {
            var home=FamilyBirth.People(w).SingleOrDefault(p=>p.Id==e.Person.Value)?.Faction is {} fa?HomeOf(w,fa):null;if(home==null)continue;
            long days=Domestic2.Days(e.Site.Value,home);
            Add("return",home,null,null,e.Person,days,$"{PersonName(w,e.Person)}が{SiteName(w,e.Site.Value)}から逃れて{SiteName(w,home)}へ戻った",e.At+days*Day);
        }
        return s with{Armies=events.Count==0?null:events.ToArray(),Corps=corps.Count==0?null:corps.ToArray(),Sieges=sieges.Count==0?null:sieges.ToArray(),
            Houses=houses.Count==0?s.Houses:houses.Values.OrderBy(h=>h.House.Value,StringComparer.Ordinal).ToArray()};
    }

    public static DomesticRumor[] Heard(ContractWorld w,EntityId viewer,long at)
    {
        if(!On(w))return [];var rows=new List<DomesticRumor>();var mine=w.NewGame!.Start.People.SingleOrDefault(p=>p.Id==viewer.Value)?.Faction;
        foreach(var e in Events(w))
        {
            string? where=e.Site!=null&&e.Site.StartsWith("site:",StringComparison.Ordinal)?e.Site:e.House is {} h?HomeOf(w,h.Value):null;
            bool own=e.House?.Value==mine||e.Other?.Value==mine||e.Person==viewer;
            long learned=e.Kind=="return"&&e.Until is long u?u:own?e.At:where is {Length:>0}?e.At+HistoricalNews.DelayFrom(w,viewer,new(where),e.At):e.At+Day;
            if(learned<=at)rows.Add(new(e.Id,e.Text,e.At,learned,e.House));
        }
        return rows.OrderBy(r=>r.LearnedAt).ThenBy(r=>r.Id,StringComparer.Ordinal).ToArray();
    }
    internal static IEnumerable<PersonalChronicleRow> ChronicleRows(ContractWorld w,EntityId viewer)=>
        Heard(w,viewer,w.ElapsedSeconds).Select(r=>new PersonalChronicleRow("armies:"+r.Id,"軍勢",r.Text,r.OccurredAt,r.LearnedAt,"噂",r.House?.Value));

    internal static void Validate(ContractWorld w,StrategyState s,StrategyState? prior)
    {
        if(s.Armies==null&&s.Corps==null&&s.Sieges==null&&!s.Campaigns.Any(c=>c.Line!=null||c.Share!=null||c.Commander!=null))return;
        if(!On(w))throw new InvalidDataException("全国の軍勢の編成と指揮は一覧に Armies2 のある世界だけです");
        if(prior?.Corps is {} oc&&!(s.Corps??[]).Take(oc.Length).SequenceEqual(oc))throw new InvalidDataException("過去の軍団の記録が変更されています");
        foreach(var c in s.Campaigns)if(c.Share is int sh&&(sh<=0||sh>100)||c.Line is int l&&l is <1 or >2)throw new InvalidDataException("出陣の線の記録が不正です");
        foreach(var x in s.Sieges??[])if(x.Taken<0||x.Taken>=x.Sections)throw new InvalidDataException("攻城の区画の記録が不正です");
    }
}
