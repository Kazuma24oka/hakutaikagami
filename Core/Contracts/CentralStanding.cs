namespace HakutaiKagami.Core.Contracts;

// S04（2026-11-17）：京の政治の効果。一覧（NationalSystems）に CentralStanding のある新規の全国開始だけ。
// ・全国の守護：在職の将軍（操作する人物でない）が、月の初めに国の城の6割以上（2城以上）を持つ家の当主を、その国の守護に補任する。
// ・将軍の栄典：在職の将軍が京にいる月の初めに、京にいる当主へ御供衆（位階が従五位下以上）、白傘袋・毛氈鞍覆（守護）、偏諱（御供衆を受け、将軍と親しい当主）を一度ずつ授ける。
// ・守護の実権：その国の城を攻めるとき、その国の小さな家（攻める家の3分の1未満の力で、守る家に従わない家）が加勢する。その国に本拠を持つ従う家は離反しない。
// ・名乗りと席次：本人の名乗りは開封した返書と1560年の在職、他人の名乗りは1560年の在職と噂で聞いたもの。席次は正式な位階、次に栄典の格。
// ・噂：補任・栄典・叙任は京から道のりに応じて遅れて届く（史実の事件の噂と同じ決め方。誤報はない）。
public sealed record StrategyShugo(EntityId House,EntityId Head,string Province,EntityId Shogun,long At,long Revision);
public sealed record StrategyHonor(EntityId House,EntityId Person,string Kind,EntityId Shogun,string? Character,long At,long Revision);
public sealed record CentralRumor(string Id,string Text,long OccurredAt,long LearnedAt,EntityId Person,EntityId? House);

public static class CentralStanding
{
    public const string Name=NationalSystems.CentralStanding;
    public static bool On(ContractWorld w)=>NationalSystems.On(w,Name)&&NationalStrategy.Enabled(w);
    public const string Companion="御供衆",WhiteUmbrella="白傘袋",SaddleCloth="毛氈鞍覆",NameGift="偏諱";
    static int HonorWeight(string kind)=>kind switch{Companion=>3,NameGift=>2,WhiteUmbrella=>1,SaddleCloth=>1,_=>0};

    static readonly Lazy<Dictionary<string,string>> provinces=new(()=>CastleRoutes.Catalog.Nodes.Where(n=>n.Province!=null).ToDictionary(n=>"site:"+n.Id,n=>HakutaiDb.Kuni(n.Province),StringComparer.Ordinal));
    public static string? ProvinceOf(EntityId site)=>provinces.Value.GetValueOrDefault(site.Value);

    // 守護：その国の最後の補任がその家なら在職（家の当主が代わっても家の職として続く）。
    public static string[] ShugoProvinces(ContractWorld w,string house,long at)=>
        (w.Strategy?.Shugo??[]).Where(x=>x.At<=at).GroupBy(x=>x.Province,StringComparer.Ordinal).Select(g=>g.Last()).Where(x=>x.House.Value==house).Select(x=>x.Province).ToArray();
    public static bool IsShugo(ContractWorld w,string house,string? province,long at)=>On(w)&&province!=null&&ShugoProvinces(w,house,at).Contains(province);

    // 月の初め（NationalStrategy.Apply から）：守護の補任と将軍の栄典。
    internal static StrategyState Month(ContractWorld w,StrategyState s,Dictionary<string,string?> owners)
    {
        if(!On(w)||SupremeOffice.ShogunAt(w,w.ElapsedSeconds,w.CommitSequence) is not {} shogun||shogun==w.PlayerControl)return s;
        long now=w.ElapsedSeconds,rev=w.CommitSequence;
        var shugo=(s.Shugo??[]).ToList();
        foreach(var g in owners.Where(x=>ProvinceOf(new(x.Key)) is not null).GroupBy(x=>ProvinceOf(new(x.Key))!,StringComparer.Ordinal).OrderBy(g=>g.Key,StringComparer.Ordinal))
        {
            var top=g.Where(x=>x.Value!=null).GroupBy(x=>x.Value!,StringComparer.Ordinal).OrderByDescending(h=>h.Count()).ThenBy(h=>h.Key,StringComparer.Ordinal).FirstOrDefault();
            if(top==null||top.Count()<2||top.Count()*10<g.Count()*6)continue;
            var current=shugo.Where(x=>x.Province==g.Key).LastOrDefault();
            if(current?.House.Value==top.Key)continue;
            var head=HouseSuccession.Head(w,new(top.Key));if(w.Characters.SingleOrDefault(c=>c.Id==head)?.Life!=LifeState.Adult)continue;
            shugo.Add(new(new(top.Key),head,g.Key,shogun,now,rev));
        }
        var honors=(s.Honors??[]).ToList();
        var kyoto=CentralCourtCatalog.KyotoIn(w);
        if(SocialWorld.Site(w,shogun)==kyoto)
            foreach(var f in w.NewGame!.Start.Factions.OrderBy(f=>f.Id,StringComparer.Ordinal))
            {
                var head=HouseSuccession.Head(w,new(f.Id));
                if(head==shogun||SocialWorld.Site(w,head)!=kyoto||w.Characters.SingleOrDefault(c=>c.Id==head)?.Life!=LifeState.Adult)continue;
                bool Has(string kind)=>honors.Any(h=>h.Person==head&&h.Kind==kind);
                void Grant(string kind,string? ch=null){if(!Has(kind))honors.Add(new(new(f.Id),head,kind,shogun,ch,now,rev));}
                if(CentralTitles.FormalRank(w,head,now,rev)<=FiveLower)Grant(Companion);
                if(shugo.Any(x=>x.House.Value==f.Id))Grant(WhiteUmbrella);
                if(shugo.Any(x=>x.House.Value==f.Id))Grant(SaddleCloth);
                if(Has(Companion)&&SocialWorld.ConsentBonus(w,shogun,head,now,rev)>=2&&NationalStart.DbPerson(shogun.Value)?.Imina is {Length:>=2} imina)Grant(NameGift,imina[1..2]);
            }
        return s with{Shugo=shugo.Count==0?null:shugo.ToArray(),Honors=honors.Count==0?null:honors.ToArray()};
    }
    // 従五位下の序列（位階の表の順で、小さいほど上）。
    static readonly Lazy<int> fiveLower=new(()=>CentralTitles.Titles.Single(t=>t.Kind=="rank"&&t.Name=="従五位下").Order);
    static int FiveLower=>fiveLower.Value;

    // 名乗り：本人は開封した返書と1560年の在職と授かった栄典、他人は1560年の在職と噂で聞いた叙任・補任・栄典。
    public static string[] KnownTitles(ContractWorld w,EntityId viewer,EntityId person)
    {
        var rows=new List<string>();
        if(viewer==person)rows.AddRange(CentralTitles.Held(w,person).Select(h=>h.Name));
        else rows.AddRange(CentralTitles.Held1560(w,person).Select(h=>h.Office));
        // 家中職（一覧の Territory2）：百代鏡DBの家中職の候補のうち、開始年までに就いたもの。
        if(Territory2.On(w)&&NationalStart.DbPerson(person.Value) is {} db&&HakutaiDbSupplement.Person(db.Id)?.HousePosts is {} posts)
            rows.AddRange(posts.Where(p=>p.From is int y&&y<=NationalSystems.StartYear(w)).Select(p=>(p.House!=null&&p.House!="記事に記載なし"?p.House:"")+p.Post));
        if(Domestic2.Hokoshu1(w,person))rows.Add(Domestic2.Hokoshu);
        if(On(w))
        {
            long at=w.ElapsedSeconds;
            foreach(var r in Heard(w,viewer,at).Where(r=>r.Person==person))rows.Add(r.Id.StartsWith("honor:",StringComparison.Ordinal)?r.Text.Split('　')[^1]:r.Text.Split('　')[^1]);
        }
        return rows.Where(x=>x.Length>0).Distinct(StringComparer.Ordinal).ToArray();
    }
    // 席次：同じ拠点の人を、正式な位階、栄典の格、ID の順に並べる。
    public static EntityId[] Seating(ContractWorld w,IEnumerable<EntityId> people)
    {
        long at=w.ElapsedSeconds,rev=w.CommitSequence+1;var honors=w.Strategy?.Honors??[];
        return people.OrderBy(p=>CentralTitles.FormalRank(w,p,at,rev)).ThenByDescending(p=>honors.Where(h=>h.Person==p).Sum(h=>HonorWeight(h.Kind))).ThenBy(p=>p.Value,StringComparer.Ordinal).ToArray();
    }
    public static (int Seat,int Of)? SeatAtSite(ContractWorld w,EntityId person)
    {
        if(!On(w)||SocialWorld.Site(w,person) is not {} site)return null;
        var here=w.Characters.Where(c=>c.Life==LifeState.Adult&&SocialWorld.Site(w,c.Id)==site).Select(c=>c.Id).ToArray();
        var order=Seating(w,here);return (Array.IndexOf(order,person)+1,order.Length);
    }

    // 噂：京で起きた補任・栄典・叙任が、京からの道のりに応じて遅れて届く。
    public static CentralRumor[] Heard(ContractWorld w,EntityId viewer,long at)
    {
        if(!On(w))return [];var s=w.Strategy;var rows=new List<CentralRumor>();
        string HouseName(EntityId h)=>w.Definitions.Organizations.SingleOrDefault(o=>o.Id==h)?.Name??h.Value;
        string PersonName(EntityId p)=>w.Definitions.Characters.SingleOrDefault(c=>c.Id==p)?.Name??p.Value;
        var kyoto=CentralCourtCatalog.KyotoIn(w);
        foreach(var x in s?.Shugo??[])
        {
            long learned=x.Head==viewer?x.At:x.At+HistoricalNews.DelayFrom(w,viewer,kyoto,x.At);if(learned>at)continue;
            rows.Add(new("shugo:"+x.Province+"/"+x.At,$"{PersonName(x.Head)}（{HouseName(x.House)}）　{x.Province}守護",x.At,learned,x.Head,x.House));
        }
        foreach(var x in s?.Honors??[])
        {
            long learned=x.Person==viewer?x.At:x.At+HistoricalNews.DelayFrom(w,viewer,kyoto,x.At);if(learned>at)continue;
            rows.Add(new("honor:"+x.Kind+"/"+x.Person.Value,$"{PersonName(x.Person)}（{HouseName(x.House)}）　{(x.Kind==NameGift?$"偏諱「{x.Character}」":x.Kind=="与姓"?$"与姓「{x.Character}」":x.Kind)}",x.At,learned,x.Person,x.House));
        }
        var events=HouseholdCourt.Events(w);
        foreach(var e in events.Where(e=>e.Input.Action==CourtAction.IssueCourtRank&&e.Input.Person!=viewer))
        {
            long learned=e.At+HistoricalNews.DelayFrom(w,viewer,kyoto,e.At);if(learned>at)continue;
            var t=CentralTitles.Title(CentralRank.RequestedTitle(events,e)??"rank:14");
            rows.Add(new("rank:"+e.Input.Id.Value,$"{PersonName(e.Input.Person)}　{t?.Name}",e.At,learned,e.Input.Person,e.Input.Owner));
        }
        return rows.OrderBy(r=>r.LearnedAt).ThenBy(r=>r.Id,StringComparer.Ordinal).ToArray();
    }
    internal static IEnumerable<PersonalChronicleRow> ChronicleRows(ContractWorld w,EntityId viewer)=>
        Heard(w,viewer,w.ElapsedSeconds).Select(r=>new PersonalChronicleRow("central:"+r.Id,"噂",r.Text,r.OccurredAt,r.LearnedAt,"噂",r.House?.Value));

    // 守護の加勢：攻める家がその国の守護なら、その国の小さな家（守る家とその従う家・同盟を除く）が加勢する。
    internal static EntityId[] ShugoSupport(ContractWorld w,StrategyCampaign c,Dictionary<string,string?> owners,Func<string,long> strength,IEnumerable<string> excluded)
    {
        var province=ProvinceOf(c.Target);if(!IsShugo(w,c.Attacker.Value,province,w.ElapsedSeconds))return [];
        long mine=strength(c.Attacker.Value);var skip=excluded.ToHashSet(StringComparer.Ordinal);
        return owners.Where(x=>x.Value!=null&&ProvinceOf(new(x.Key))==province).Select(x=>x.Value!).Distinct(StringComparer.Ordinal)
            .Where(h=>h!=c.Attacker.Value&&h!=c.Defender.Value&&!skip.Contains(h)&&strength(h)*3<mine).OrderBy(h=>h,StringComparer.Ordinal).Select(h=>new EntityId(h)).ToArray();
    }
}
