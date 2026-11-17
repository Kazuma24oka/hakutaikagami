namespace HakutaiKagami.Core.Contracts;

// 全国1560年の開始の版（2026-11-12、v110。設計 Docs/Proposals/NATIONAL_PLACEMENT_DESIGN_V1.md 第4節4）。
// 百代鏡DBの家・登場武将（HakutaiDbStart の名簿：DBの人物と地域の名のモブ）・城の持ち主を、城の移動網の城に置く。
// 世界は城の移動網・調べる権利の軽い作り方・続けての日課・遠方の休眠で作る（Create）。琉球の家と城は置かない。
public static class NationalStart
{
    public const string Edition="hakutai-1560-national-v1";
    // v2（2026-11-12）：京の朝廷（百代鏡DBの家 G-000241、城なし）の人々を二条御所の拠点に置き、京の取次・将軍家との往来を全国で使う。新規開始だけ。
    public const string CourtEdition="hakutai-1560-national-v2";
    public const string PersonEdition="hakutai-1560-national-v3";
    public const string RevenueEdition="hakutai-1560-national-v4";
    public static bool IsNational(string? edition)=>edition is Edition or CourtEdition or PersonEdition or RevenueEdition;
    public const string CourtHouse="G-000241";
    public static readonly string KyotoSite="site:"+CastleRoutes.NodeOf("L-500755");
    public static string PersonId(string officer)=>"person:hdb/"+Safe(officer.Replace(':','/'));
    public static string FactionId(string house)=>"org:hdb/"+Safe(house);
    // DBの ID に英数字以外（日本語の家の名など）があれば、その ID の SHA-256 の先頭12桁にする（同じ ID からは同じ値）。
    static string Safe(string id)=>id.All(c=>char.IsAsciiLetterOrDigit(c)||c is '/' or '-' or '_')?id:"x"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(id))[..12];
    static readonly Lazy<HashSet<string>> placed=new(()=>CastleRoutes.Catalog.Nodes.Select(n=>n.Castle).ToHashSet(StringComparer.Ordinal));
    static string? Site(string? castle)=>castle!=null&&placed.Value.Contains(castle)?"site:"+CastleRoutes.NodeOf(castle):null;
    // 1560年以外の開始（S03）：城の移動網に無い城は、同じ国の網の城のうち一番近い城に置く（推定）。国に網の城がなければ置かない。
    static readonly Lazy<Dictionary<string,HakutaiDbCastle[]>> netByProvince=new(()=>CastleRoutes.Catalog.Nodes.Select(n=>HakutaiDb.Castle(n.Castle)!).Where(c=>c.Province!=null)
        .GroupBy(c=>HakutaiDb.Kuni(c.Province),StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray(),StringComparer.Ordinal));
    static string? SiteIn(string? castle,HakutaiDbEra db)
    {
        if(castle==null)return null;if(Site(castle) is {} s)return s;
        if(db.Castle(castle) is not {} c||c.Province==null||!netByProvince.Value.TryGetValue(HakutaiDb.Kuni(c.Province),out var near))return null;
        var best=near.MinBy(n=>{double dx=(n.LongitudeE7-c.LongitudeE7)*0.82,dy=n.LatitudeE7-c.LatitudeE7;return dx*dx+dy*dy;})!;
        return Site(best.Id);
    }

    // 世界の人物 ID から百代鏡DBの人物（DBの人物に対応する登場武将だけ。モブはなし）。
    static Dictionary<string,HakutaiDbPerson> PeopleOf(HakutaiDbEra db)=>db.HouseList.SelectMany(h=>HakutaiDbStart.Roster(h.Id,db).Officers).Where(o=>o.Person!=null&&db.Person(o.Person) is not null)
        .GroupBy(o=>PersonId(o.Id),StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>db.Person(g.First().Person!)!,StringComparer.Ordinal);
    static readonly Lazy<Dictionary<string,HakutaiDbPerson>> dbPeople=new(()=>PeopleOf(HakutaiDb.Era1560)),dbPeople1467=new(()=>PeopleOf(HakutaiDb.Era(1467))),dbPeople1600=new(()=>PeopleOf(HakutaiDb.Era(1600)));
    // 1560年の名簿を先に、なければ1467年・1600年の名簿から（年によらない名前・生没年・血縁に使う）。モブは引かない。
    public static HakutaiDbPerson? DbPerson(string person)=>!person.StartsWith("person:hdb/",StringComparison.Ordinal)||person.StartsWith("person:hdb/mob/",StringComparison.Ordinal)?null:
        dbPeople.Value.GetValueOrDefault(person)??dbPeople1467.Value.GetValueOrDefault(person)??dbPeople1600.Value.GetValueOrDefault(person);
    // DBに生年と没年の両方がある人物の生没年（自然死の判定に使う）。
    public static (int Born,int Died)? Life(string person)=>DbPerson(person) is {Birth:int born,Death:int died}&&born<died?(born,died):null;
    static readonly Lazy<Dictionary<string,string>> dbHouses=new(()=>HakutaiDb.StartYears.SelectMany(y=>HakutaiDb.Era(y).HouseList).GroupBy(h=>FactionId(h.Id),StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.First().Id,StringComparer.Ordinal));
    static readonly Lazy<Dictionary<string,string>> dbHouses1560=new(()=>HakutaiDb.Houses.GroupBy(h=>FactionId(h.Id),StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.First().Id,StringComparer.Ordinal));
    // 世界の家の ID から百代鏡DBの家の ID（どの開始年の家も同じ ID の決め方）。
    public static string? DbHouse(string faction)=>!faction.StartsWith("org:hdb/",StringComparison.Ordinal)?null:dbHouses1560.Value.GetValueOrDefault(faction)??dbHouses.Value.GetValueOrDefault(faction);
    static readonly Lazy<SetupScenario> scenario=new(()=>Build(false,false)),courtScenario=new(()=>Build(true,false)),personScenario=new(()=>Build(true,true)),kinScenario=new(()=>Build(true,true,true));
    // 新規の開始は母の血縁も入れた版（一覧の DbMothers）。保存の開始定義は、その世界の一覧に合わせて作り直す。
    public static SetupScenario Scenario(string edition=RevenueEdition)=>Scenario(edition,NationalSystems.Current);
    // 一覧（新規開始で世界に保存するもの）に DbMothers があれば、母を実の親に入れた開始。一覧を保存しない世界は空の一覧で作る。
    static readonly Lazy<SetupScenario> scenario1467=new(()=>Build(true,true,true,HakutaiDb.Era(1467))),scenario1600=new(()=>Build(true,true,true,HakutaiDb.Era(1600)));
    public static SetupScenario Scenario(string edition,IReadOnlyCollection<string> systems)=>NationalSystems.StartYear(systems) is var year&&year!=1560&&edition==RevenueEdition?ContractJson.Copy(year==1467?scenario1467.Value:scenario1600.Value):ContractJson.Copy(edition==Edition?scenario.Value:edition==CourtEdition?courtScenario.Value:edition==RevenueEdition&&systems.Contains("DbMothers")?kinScenario.Value:personScenario.Value);
    internal static SetupScenario ScenarioFor(NewGameState data)=>Scenario(data.HistoricalCatalog==CourtEdition&&data.NationalPersonDataPolicy==NationalPersonEstimates.Policy?PersonEdition:data.HistoricalCatalog!,data.NationalSystems??[]);
    static SetupScenario Build(bool court,bool personData,bool mothers=false,HakutaiDbEra? era=null)
    {
        var db=era??HakutaiDb.Era1560;bool other=era!=null&&era.Year!=1560;court&=db.House(CourtHouse)!=null;
        var people=new List<SetupPerson>();var seen=new HashSet<string>(StringComparer.Ordinal);var factions=new List<SetupFaction>();var homes=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var h in db.HouseList.OrderBy(h=>h.Id,StringComparer.Ordinal))
        {
            var home=other?SiteIn(h.Home,db)??db.CastlesOf(h.Id).Select(c=>SiteIn(c.Id,db)).FirstOrDefault(s=>s!=null):Site(h.Home)??HakutaiDb.CastlesOf(h.Id).Select(c=>Site(c.Id)).FirstOrDefault(s=>s!=null);
            if(home==null)continue; // 城の移動網に城のない家（琉球など）は置かない
            // 複数の家の名簿に出る人物は、先の家（ID の順）にだけ置く。
            var roster=HakutaiDbStart.Roster(h.Id,db).Officers.Where(o=>!seen.Contains(PersonId(o.Id))).DistinctBy(o=>o.Id).ToArray();
            if(roster.Length==0)continue;
            var head=roster.FirstOrDefault(o=>o.Person!=null&&o.Person==h.Head)??roster[0];
            string faction=FactionId(h.Id),headId=PersonId(head.Id);
            factions.Add(new(faction,h.Name,headId,home));homes[faction]=home;
            foreach(var o in roster){seen.Add(PersonId(o.Id));people.Add(new(PersonId(o.Id),o.Name,(other?SiteIn(o.Castle,db):Site(o.Castle))??home,faction,o.Id==head.Id?null:headId,false,false));}
        }
        // 朝廷の人々は家に属さず（家中・兵を持たない）、京（二条御所）にいる。
        if(court)foreach(var o in HakutaiDbStart.Roster(CourtHouse,db).Officers.Where(o=>!seen.Contains(PersonId(o.Id))).DistinctBy(o=>o.Id)){seen.Add(PersonId(o.Id));people.Add(new(PersonId(o.Id),o.Name,KyotoSite,null,null,false,false));}
        // DBの父がこの世界にいれば、実の親として記録する（血縁。操作の引き継ぎの候補に使う）。
        var dbIds=people.Where(p=>DbPerson(p.Id)!=null).GroupBy(p=>DbPerson(p.Id)!.Id,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.First().Id,StringComparer.Ordinal);
        people=people.Select(p=>{
            var dp=DbPerson(p.Id);var person=dp?.Father is {} father&&dbIds.TryGetValue(father,out var parent)&&parent!=p.Id?p with{BirthParents=[parent]}:p;
            if(!personData||dp==null)return person;
            var estimate=NationalPersonEstimates.ForDbPerson(dp.Id,db.Year);var a=estimate.Abilities;
            if(!estimate.BirthParentAccepted)person=person with{BirthParents=null};
            // 母（DbMothers）：DBの母がこの世界にいて、推定の生年で13歳以上年上なら、実の親に加える。
            if(mothers&&dp.Mother is {} mother&&dbIds.TryGetValue(mother,out var m)&&m!=p.Id&&NationalPersonEstimates.ForDbPerson(mother,db.Year).BirthYear<=estimate.BirthYear-13)
                person=person with{BirthParents=[..(person.BirthParents??[]),m]};
            return person with{BirthYear=estimate.BirthYear,Attributes=new OfficerAttributes(a.Leadership,a.Valor,a.Intelligence,a.Politics){Charm=a.Charm}};
        }).ToList();
        // 1560年以外の版：親の推定の生年が子より13年以上前の親子だけを結ぶ（記録の循環や食い違いで親子が輪にならないように）。
        if(other){var born=people.ToDictionary(p=>p.Id,p=>p.BirthYear,StringComparer.Ordinal);
            people=people.Select(p=>p.BirthParents is {} ps&&p.BirthYear is int b?p with{BirthParents=ps.Where(x=>born.GetValueOrDefault(x) is int pb&&pb<=b-13).ToArray() is {Length:>0} ok?ok:null}:p with{BirthParents=null}).ToList();}
        var owners=factions.Select(f=>f.Id).ToHashSet(StringComparer.Ordinal);
        var castles=new List<SetupCastle>();var nodes=CastleRoutes.Catalog.Nodes.ToDictionary(n=>"site:"+n.Id,StringComparer.Ordinal);var homeOf=homes.GroupBy(x=>x.Value).ToDictionary(g=>g.Key,g=>g.Select(x=>x.Key).OrderBy(x=>x,StringComparer.Ordinal).First());
        foreach(var s in WorldNetwork.Sites(new NewGameState(1,new(1,"1560-01-01",[],[],[]),[],new("person:none"),0,15){NetworkPolicy=WorldNetwork.CastlePolicy}))
        {
            string id=s.Id.Value;string? owner=null;
            var node=nodes.GetValueOrDefault(id);
            if(node!=null&&(other?OwnerIn(node.Castle,db):HakutaiDb.Castle(node.Castle)?.Owner) is {} house&&owners.Contains(FactionId(house)))owner=FactionId(house);
            var home=homeOf.GetValueOrDefault(id);
            castles.Add(new(id,home??owner));
        }
        // 本拠を同じ城に持つ家が複数あれば、後の家は自分の別の城を本拠にする（なければ置かない）。
        var result=new SetupScenario(1,db.Year+"-01-01",people.ToArray(),castles.ToArray(),factions.ToArray());
        var badFactions=result.Factions.Where(f=>result.Castles.Single(c=>c.Id==f.Home).Faction!=f.Id).Select(f=>f.Id).ToHashSet(StringComparer.Ordinal);
        if(badFactions.Count>0)
        {
            var fixedFactions=new List<SetupFaction>();var dropped=new HashSet<string>(StringComparer.Ordinal);
            foreach(var f in result.Factions)
            {
                if(!badFactions.Contains(f.Id)){fixedFactions.Add(f);continue;}
                var own=result.Castles.FirstOrDefault(c=>c.Faction==f.Id);
                if(own!=null)fixedFactions.Add(f with{Home=own.Id});else dropped.Add(f.Id);
            }
            result=result with{Factions=fixedFactions.ToArray(),People=result.People.Where(p=>p.Faction==null||!dropped.Contains(p.Faction)).ToArray()};
        }
        NewGameSetup.Validate(result);
        return result;
    }

    // 1560年以外の開始：網の城の持ち主は、その年の版の同じ城の持ち主。版に無ければ、網の城へ寄せたその年の城の持ち主（ID の順で先のもの）。
    static string? OwnerIn(string castle,HakutaiDbEra db)=>db.Castle(castle)?.Owner??ownerNear.GetValue(db,BuildOwnerNear).GetValueOrDefault(castle);
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<HakutaiDbEra,Dictionary<string,string>> ownerNear=new();
    static Dictionary<string,string> BuildOwnerNear(HakutaiDbEra db)
    {
        var bySite=CastleRoutes.Catalog.Nodes.ToDictionary(n=>"site:"+n.Id,n=>n.Castle,StringComparer.Ordinal);
        var r=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var c in db.Edition.Castles.Where(c=>c.Owner!=null&&!placed.Value.Contains(c.Id)).OrderBy(c=>c.Id,StringComparer.Ordinal))
            if(SiteIn(c.Id,db) is {} s&&bySite.TryGetValue(s,out var n)&&db.Castle(n)?.Owner==null)r.TryAdd(n,c.Owner!);
        return r;
    }
    // 全国1560年の世界を作る（城の移動網・調べる権利の軽い作り方・続けての日課・遠方の休眠）。
    public static ContractWorld Create(string selected,ulong seed=20261112,string? timePolicy=null,HistoryMode? history=null,int year=1560,Knobs? knobs=null)
    {
        var systems=NationalSystems.ForYear(year);var raw=Scenario(RevenueEdition,systems);var request=new WorldSetupRequest([],[]);var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        return NewGameWorld.Create(setup,[],selected,seed,NewGameWorld.CompactPersonalAuthority,historicalCatalog:RevenueEdition,economyPolicy:RegionalDomains.Policy,npcRoutinePolicy:NpcJudgment.Policy,judgment:Judgment(history,knobs),inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,demandPolicy:LocalWorkDemand.Policy,timePolicy:timePolicy,networkPolicy:WorldNetwork.CastlePolicy,dormancyPolicy:NpcDormancy.Policy,controlPolicy:ControlSuccession.Policy,comingOfAge:FamilyKin.ComingOfAge,strategyPolicy:NationalStrategy.Policy,districtControlPolicy:NationalDistrictControl.Policy,developmentPolicy:CharacterDevelopment.Policy32,nationalPersonDataPolicy:NationalPersonEstimates.Policy,nationalSystems:systems,companionPolicy:PersonalCompanions.RetainerReleasePolicy,socialPolicy:CharacterRelations.Policy,marriagePolicy:AdultMarriage.Policy,adoptionPolicy:AdultAdoption.Policy,informationPolicy:PrivateInformation.Policy,weatherPolicy:RegionalWeather.Policy,banditPolicy:LocalSecurity.Policy,abilityPolicy:AbilityEffects.Policy,healthPolicy:ClinicalHealth.Policy,trucePolicy:TruceNegotiations.Policy,history:history);
    }
    // 新規ゲームの画面から始める（保存の開始定義も同じ版で作る）。
    // NPC の日課は判断の土台の版（v4：性格と知っていることによる選択、空き時間の修行。地方の新規開始と同じ設定）。2026-11-14 から新規の全国開始だけ。
    static NpcJudgmentSettings Judgment(HistoryMode? history)=>new(NpcJudgment.Policy,history??HistoryMode.Fictional,NpcAttention.Beginner){UseInspectionReferences=true,Challenge=NpcChallenge.Standard,HealthEdition=PersonalHealth.Edition};
    // 新規開始の画面で選んだつまみ（S03）：声のかかりやすさ・手強さ・判断支援を世界に保存する。選ばなければ前と同じ既定。
    public sealed record Knobs(NpcAttention Attention,NpcChallenge Challenge,DecisionSupport? Support);
    static NpcJudgmentSettings Judgment(HistoryMode? history,Knobs? knobs)=>knobs==null?Judgment(history):Judgment(history) with{Attention=knobs.Attention,Challenge=knobs.Challenge,Support=knobs.Support};
    public static NewCampaignProfile New(NewCampaignStore store,SetupScenario setup,string selected,ulong seed,string? timePolicy,bool previewOnly,HistoryMode? history=null,int year=1560,Knobs? knobs=null)
        =>store.New(setup,[],selected,seed,NewGameWorld.CompactPersonalAuthority,RevenueEdition,NpcJudgment.Policy,LocalInspectionFindings.Policy,WorkFollowup.Policy,LocalWorkDemand.Policy,timePolicy:timePolicy,previewOnly:previewOnly,economyPolicy:RegionalDomains.Policy,comingOfAge:FamilyKin.ComingOfAge,networkPolicy:WorldNetwork.CastlePolicy,dormancyPolicy:NpcDormancy.Policy,controlPolicy:ControlSuccession.Policy,strategyPolicy:NationalStrategy.Policy,districtControlPolicy:NationalDistrictControl.Policy,developmentPolicy:CharacterDevelopment.Policy32,nationalPersonDataPolicy:NationalPersonEstimates.Policy,nationalSystems:NationalSystems.ForYear(year),companionPolicy:PersonalCompanions.RetainerReleasePolicy,socialPolicy:CharacterRelations.Policy,marriagePolicy:AdultMarriage.Policy,adoptionPolicy:AdultAdoption.Policy,informationPolicy:PrivateInformation.Policy,weatherPolicy:RegionalWeather.Policy,banditPolicy:LocalSecurity.Policy,abilityPolicy:AbilityEffects.Policy,healthPolicy:ClinicalHealth.Policy,trucePolicy:TruceNegotiations.Policy,history:history,judgment:Judgment(history,knobs));
}
