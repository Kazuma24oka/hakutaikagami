namespace HakutaiKagami.Core.Contracts;

// S06（2026-11-17）：外交・婚姻の残り。一覧（NationalSystems）に Diplomacy2 のある新規の全国開始だけ。
// ・百代鏡DBの配偶者・養親（名簿の中の人どうし）を、開始時の婚姻・養子の事実として扱う（原本から導く。保存しない）。
// ・縁：二つの家の人どうしに婚姻（DBの配偶者、または婚姻による同盟）があれば、その家どうしは縁がある。縁のある家は同盟を結びやすく（20%→40%）、破りにくい（3%→1%）。
// ・婚姻による同盟：NPCの家どうしが同盟を結ぶとき、両家に独身の成人の男女がいれば婚姻で結ぶ。同盟が破られた日に離縁する。
// ・破約への制裁：同盟を破って攻めた家とは、攻められた家と同盟している家が同盟を解く。破った家の当主に悪名（攻められた家と制裁した家の当主はすぐ、ほかは道のりに応じて噂で）。
// ・約束の引き継ぎ：当主を継いだ人は、その時に家として続いている同盟・和睦を知らせとして知る。
public sealed record StrategyMarriage(EntityId Pact,EntityId Man,EntityId Woman,long At,long Revision);
public sealed record HouseCommitment(EntityId Pact,EntityId Other,string Kind,long Until,string Basis);
public sealed record KnowledgeIndexRow(string Province,string House,int Count,long LastLearnedAt,string LastText);

public static class Diplomacy2
{
    public const string Name=NationalSystems.Diplomacy2;
    public static bool On(ContractWorld w)=>NationalSystems.On(w,Name)&&NationalStrategy.Enabled(w);
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SetupScenario,Dictionary<string,string>> worldIdOfDb=new();
    static Dictionary<string,string> WorldIds(ContractWorld w)=>worldIdOfDb.GetValue(w.NewGame!.Start,s=>s.People.Select(p=>(p.Id,Db:NationalStart.DbPerson(p.Id)?.Id)).Where(x=>x.Db!=null).GroupBy(x=>x.Db!,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.First().Id,StringComparer.Ordinal));
    static IEnumerable<EntityId> Map(ContractWorld w,IEnumerable<string>? db)=>(db??[]).Select(d=>WorldIds(w).GetValueOrDefault(d)).Where(x=>x!=null).Select(x=>new EntityId(x!));

    // 開始時の婚姻（DBの配偶者）と、婚姻による同盟の、その時点で続いている夫婦。
    public static EntityId[] Spouses(ContractWorld w,EntityId person,long at)
    {
        if(!On(w))return [];
        var rows=new List<EntityId>();
        if(NationalStart.DbPerson(person.Value) is {} me)
        {
            rows.AddRange(Map(w,me.Spouses));
            rows.AddRange(w.NewGame!.Start.People.Where(p=>NationalStart.DbPerson(p.Id)?.Spouses?.Contains(me.Id)==true).Select(p=>new EntityId(p.Id)));
        }
        foreach(var m in Marriages(w,at))if(m.Man==person)rows.Add(m.Woman);else if(m.Woman==person)rows.Add(m.Man);
        return rows.Where(x=>x!=person).Distinct().OrderBy(x=>x.Value,StringComparer.Ordinal).ToArray();
    }
    // 開始時の養子（DBの養親）：その人を養親とする名簿の中の人。
    public static EntityId[] AdoptedChildren(ContractWorld w,EntityId parent)
    {
        if(!On(w)||NationalStart.DbPerson(parent.Value) is not {} me)return [];
        return w.NewGame!.Start.People.Where(p=>p.Id!=parent.Value&&NationalStart.DbPerson(p.Id)?.Adoptive?.Contains(me.Id)==true).Select(p=>new EntityId(p.Id)).ToArray();
    }
    // 続いている婚姻による同盟の夫婦（同盟が破られた日に離縁）。
    public static StrategyMarriage[] Marriages(ContractWorld w,long at)=>(w.Strategy?.Marriages??[]).Where(m=>m.At<=at&&(w.Strategy!.Pacts??[]).FirstOrDefault(p=>p.Id==m.Pact) is {} p&&(p.Broken==null||p.Broken>at)).ToArray();
    public static StrategyMarriage[] Divorced(ContractWorld w,long at)=>(w.Strategy?.Marriages??[]).Where(m=>(w.Strategy!.Pacts??[]).FirstOrDefault(p=>p.Id==m.Pact) is {Broken:{} b}&&b<=at).ToArray();

    // 家どうしの縁。
    // 開始時の婚姻（DBの配偶者）でつながる家の組（開始の定義ごとに一度だけ数える）。
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SetupScenario,HashSet<string>> startTies=new();
    static HashSet<string> StartTies(ContractWorld w)=>startTies.GetValue(w.NewGame!.Start,start=>
    {
        var house=start.People.ToDictionary(p=>p.Id,p=>p.Faction,StringComparer.Ordinal);var ids=WorldIds(w);var pairs=new HashSet<string>(StringComparer.Ordinal);
        foreach(var p in start.People)
            foreach(var sp in NationalStart.DbPerson(p.Id)?.Spouses??[])
                if(ids.GetValueOrDefault(sp) is {} other&&house.GetValueOrDefault(p.Id) is {} x&&house.GetValueOrDefault(other) is {} y&&x!=y){pairs.Add(x+"|"+y);pairs.Add(y+"|"+x);}
        return pairs;
    });
    public static bool Tied(ContractWorld w,string a,string b,long at)
    {
        if(!On(w))return false;
        if(StartTies(w).Contains(a+"|"+b))return true;
        var house=w.NewGame!.Start.People.ToDictionary(p=>p.Id,p=>p.Faction,StringComparer.Ordinal);
        return Marriages(w,at).Any(m=>house.GetValueOrDefault(m.Man.Value) is {} x&&house.GetValueOrDefault(m.Woman.Value) is {} y&&(x==a&&y==b||x==b&&y==a));
    }
    public static int AllianceChance(ContractWorld w,string a,string b,long at)=>Tied(w,a,b,at)?400:200;
    public static int BetrayChance(ContractWorld w,string a,string b,long at)=>Tied(w,a,b,at)?10:30;

    static bool Single(ContractWorld w,EntityId p,long at)=>Spouses(w,p,at).Length==0;
    // 同盟を結んだとき：両家の独身の成人の男女（操作する人物と当主を除く、ID の順）で婚姻。
    internal static StrategyMarriage? MarryFor(ContractWorld w,StrategyPact pact)
    {
        if(!On(w))return null;long at=pact.From;
        IEnumerable<EntityId> Candidates(EntityId house,string sex)=>w.NewGame!.Start.People.Where(p=>p.Faction==house.Value&&new EntityId(p.Id)!=w.PlayerControl&&new EntityId(p.Id)!=HouseSuccession.Head(w,house)&&NationalStart.DbPerson(p.Id)?.Sex==sex&&w.Characters.SingleOrDefault(c=>c.Id.Value==p.Id)?.Life==LifeState.Adult)
            .Select(p=>new EntityId(p.Id)).Where(p=>Single(w,p,at)).OrderBy(p=>p.Value,StringComparer.Ordinal);
        foreach(var (x,y) in new[]{(pact.A,pact.B),(pact.B,pact.A)})
            if(Candidates(x,"男").FirstOrDefault() is {Value:not null} man&&Candidates(y,"女").FirstOrDefault() is {Value:not null} woman)return new(pact.Id,man,woman,at,pact.Revision);
        return null;
    }

    // 破約への制裁：攻められた家と同盟している家は、破った家との同盟を解く。
    internal static void Sanctions(ContractWorld w,List<StrategyPact> pacts,string betrayer,string victim,long now)
    {
        if(!On(w))return;
        var friends=pacts.Where(p=>p.Kind=="alliance"&&p.Broken==null&&p.From<=now&&now<p.Until&&(p.A.Value==victim||p.B.Value==victim)).Select(p=>p.A.Value==victim?p.B.Value:p.A.Value).Where(x=>x!=betrayer).ToHashSet(StringComparer.Ordinal);
        for(int n=0;n<pacts.Count;n++)
        {
            var p=pacts[n];
            if(p.Broken==null&&p.From<=now&&now<p.Until&&(p.A.Value==betrayer&&friends.Contains(p.B.Value)||p.B.Value==betrayer&&friends.Contains(p.A.Value)))pacts[n]=p with{Broken=now};
        }
    }
    // 悪名：同盟を破って攻めた家の当主。
    internal static IEnumerable<RenownFact> KnownInfamy(ContractWorld w,EntityId observer)
    {
        if(!On(w)||w.Strategy is not {} s)yield break;
        foreach(var c in s.Campaigns.Where(c=>c.Basis=="betrayal"&&c.At<=w.ElapsedSeconds))
        {
            var breaker=HouseSuccession.Head(w,c.Attacker,c.At,c.Revision);
            bool close=observer==breaker||HouseSuccession.Head(w,c.Defender,c.At,c.Revision)==observer||(s.Pacts??[]).Any(p=>p.Broken==c.At&&(p.A==c.Attacker||p.B==c.Attacker)&&HouseSuccession.Head(w,p.A==c.Attacker?p.B:p.A,c.At,c.Revision)==observer);
            long learned=close?c.At:c.At+HistoricalNews.DelayFrom(w,observer,c.Target,c.At);
            if(learned>w.ElapsedSeconds)continue;
            yield return new("betrayal:"+c.Id.Value,observer,breaker,RenownKind.Infamy,c.At,learned,c.Target,"同盟を破って攻めた",true);
        }
    }

    // 家として続いている約束（同盟・和睦）。
    public static HouseCommitment[] Commitments(ContractWorld w,EntityId house,long at)=>!On(w)?[]:
        (w.Strategy?.Pacts??[]).Where(p=>p.Broken==null&&p.From<=at&&at<p.Until&&(p.A==house||p.B==house)).Select(p=>new HouseCommitment(p.Id,p.A==house?p.B:p.A,p.Kind,p.Until,p.Basis)).ToArray();
    // 当主を継いだ時の引き継ぎの知らせ（全国の家の家督の記録・操作の引き継ぎ）。
    internal static IEnumerable<PersonalChronicleRow> ChronicleRows(ContractWorld w,EntityId viewer)
    {
        if(!On(w))yield break;
        string HouseName(EntityId h)=>w.Definitions.Organizations.SingleOrDefault(o=>o.Id==h)?.Name??h.Value;
        foreach(var x in (w.Strategy?.Successions??[]).Where(x=>x.Heir==viewer&&x.At<=w.ElapsedSeconds))
        {
            var list=Commitments(w,x.House,x.At);
            string body=list.Length==0?"続いている約束なし":string.Join("・",list.Select(c=>HouseName(c.Other)+(c.Kind=="truce"?"と和睦":"と同盟")));
            yield return new("inherit:"+x.House.Value+"/"+x.At,"引き継ぎ",$"{HouseName(x.House)}を継ぐ　{body}",x.At,x.At,"家督",x.House.Value);
        }
        // 操作の引き継ぎで当主の人を継いだとき。
        foreach(var h in (w.ControlHandovers??[]).Where(h=>h.To==viewer&&h.At<=w.ElapsedSeconds))
            foreach(var f in w.NewGame!.Start.Factions.Where(f=>HouseSuccession.Head(w,new(f.Id),h.At,h.Revision)==viewer))
            {
                var list=Commitments(w,new(f.Id),h.At);
                string body=list.Length==0?"続いている約束なし":string.Join("・",list.Select(c=>HouseName(c.Other)+(c.Kind=="truce"?"と和睦":"と同盟")));
                yield return new("inherit:"+f.Id+"/"+h.At,"引き継ぎ",$"{HouseName(new(f.Id))}を継ぐ　{body}",h.At,h.At,"家督",f.Id);
            }
        // 家族の遺産（家の約束とは別）。
        foreach(var e in MortalSuccession.Estates(w,viewer))
            yield return new("estate:"+e.Deceased.Value+"/"+e.At,"遺産",$"{w.Definitions.Characters.SingleOrDefault(c=>c.Id==e.Deceased)?.Name??e.Deceased.Value}の遺産　{e.Amount}文",e.At,e.At,"相続");
        foreach(var m in Divorced(w,w.ElapsedSeconds).Where(m=>m.Man==viewer||m.Woman==viewer))
            yield return new("divorce:"+m.Pact.Value,"離縁","同盟が破られ離縁",null,(w.Strategy!.Pacts??[]).First(p=>p.Id==m.Pact).Broken!.Value,"家");
    }

    // 本人が知った情報の索引：年代記の記録を、家の本拠の国と家ごとにまとめる。
    public static KnowledgeIndexRow[] Index(ContractWorld w,PersonalChronicleRow[] rows)
    {
        string Province(string house)=>w.NewGame!.Start.Factions.FirstOrDefault(f=>f.Id==house)?.Home is {} home&&CentralStanding.ProvinceOf(new(home)) is {} p?p:"不明";
        string Name(string house)=>w.Definitions.Organizations.SingleOrDefault(o=>o.Id.Value==house)?.Name??house;
        return rows.SelectMany(r=>(r.Houses??(r.House!=null?[r.House]:[])).Select(h=>(House:h,Row:r))).GroupBy(x=>x.House,StringComparer.Ordinal)
            .Select(g=>{var last=g.OrderBy(x=>x.Row.LearnedAt).Last().Row;return new KnowledgeIndexRow(Province(g.Key),Name(g.Key),g.Count(),last.LearnedAt,last.Text);})
            .OrderBy(r=>r.Province,StringComparer.Ordinal).ThenBy(r=>r.House,StringComparer.Ordinal).ToArray();
    }
}

public sealed partial class WorldSession
{
    public KnowledgeIndexRow[] KnowledgeIndex(EntityId actor){var rows=PersonalChronicle(actor);lock(gate)return Diplomacy2.Index(state,rows);}
}
