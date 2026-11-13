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

    // 世界の人物 ID から百代鏡DBの人物（DBの人物に対応する登場武将だけ。モブはなし）。
    static readonly Lazy<Dictionary<string,HakutaiDbPerson>> dbPeople=new(()=>HakutaiDb.Houses.SelectMany(h=>HakutaiDbStart.Roster(h.Id).Officers).Where(o=>o.Person!=null&&HakutaiDb.Person(o.Person) is not null)
        .GroupBy(o=>PersonId(o.Id),StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>HakutaiDb.Person(g.First().Person!)!,StringComparer.Ordinal));
    public static HakutaiDbPerson? DbPerson(string person)=>person.StartsWith("person:hdb/",StringComparison.Ordinal)?dbPeople.Value.GetValueOrDefault(person):null;
    // DBに生年と没年の両方がある人物の生没年（自然死の判定に使う）。
    public static (int Born,int Died)? Life(string person)=>DbPerson(person) is {Birth:int born,Death:int died}&&born<died?(born,died):null;
    static readonly Lazy<Dictionary<string,string>> dbHouses=new(()=>HakutaiDb.Houses.GroupBy(h=>FactionId(h.Id),StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.First().Id,StringComparer.Ordinal));
    // 世界の家の ID から百代鏡DBの家の ID。
    public static string? DbHouse(string faction)=>dbHouses.Value.GetValueOrDefault(faction);
    static readonly Lazy<SetupScenario> scenario=new(()=>Build(false,false)),courtScenario=new(()=>Build(true,false)),personScenario=new(()=>Build(true,true));
    public static SetupScenario Scenario(string edition=RevenueEdition)=>ContractJson.Copy(edition==Edition?scenario.Value:edition==CourtEdition?courtScenario.Value:personScenario.Value);
    internal static SetupScenario ScenarioFor(NewGameState data)=>Scenario(data.HistoricalCatalog==CourtEdition&&data.NationalPersonDataPolicy==NationalPersonEstimates.Policy?PersonEdition:data.HistoricalCatalog!);
    static SetupScenario Build(bool court,bool personData)
    {
        var people=new List<SetupPerson>();var seen=new HashSet<string>(StringComparer.Ordinal);var factions=new List<SetupFaction>();var homes=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var h in HakutaiDb.Houses.OrderBy(h=>h.Id,StringComparer.Ordinal))
        {
            var home=Site(h.Home)??HakutaiDb.CastlesOf(h.Id).Select(c=>Site(c.Id)).FirstOrDefault(s=>s!=null);
            if(home==null)continue; // 城の移動網に城のない家（琉球など）は置かない
            // 複数の家の名簿に出る人物は、先の家（ID の順）にだけ置く。
            var roster=HakutaiDbStart.Roster(h.Id).Officers.Where(o=>!seen.Contains(PersonId(o.Id))).DistinctBy(o=>o.Id).ToArray();
            if(roster.Length==0)continue;
            var head=roster.FirstOrDefault(o=>o.Person!=null&&o.Person==h.Head)??roster[0];
            string faction=FactionId(h.Id),headId=PersonId(head.Id);
            factions.Add(new(faction,h.Name,headId,home));homes[faction]=home;
            foreach(var o in roster){seen.Add(PersonId(o.Id));people.Add(new(PersonId(o.Id),o.Name,Site(o.Castle)??home,faction,o.Id==head.Id?null:headId,false,false));}
        }
        // 朝廷の人々は家に属さず（家中・兵を持たない）、京（二条御所）にいる。
        if(court)foreach(var o in HakutaiDbStart.Roster(CourtHouse).Officers.Where(o=>!seen.Contains(PersonId(o.Id))).DistinctBy(o=>o.Id)){seen.Add(PersonId(o.Id));people.Add(new(PersonId(o.Id),o.Name,KyotoSite,null,null,false,false));}
        // DBの父がこの世界にいれば、実の親として記録する（血縁。操作の引き継ぎの候補に使う）。
        var dbIds=people.Where(p=>DbPerson(p.Id)!=null).GroupBy(p=>DbPerson(p.Id)!.Id,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.First().Id,StringComparer.Ordinal);
        people=people.Select(p=>{
            var db=DbPerson(p.Id);var person=db?.Father is {} father&&dbIds.TryGetValue(father,out var parent)&&parent!=p.Id?p with{BirthParents=[parent]}:p;
            if(!personData||db==null)return person;
            var estimate=NationalPersonEstimates.ForDbPerson(db.Id);var a=estimate.Abilities;
            if(!estimate.BirthParentAccepted)person=person with{BirthParents=null};
            return person with{BirthYear=estimate.BirthYear,Attributes=new OfficerAttributes(a.Leadership,a.Valor,a.Intelligence,a.Politics){Charm=a.Charm}};
        }).ToList();
        var owners=factions.Select(f=>f.Id).ToHashSet(StringComparer.Ordinal);
        var castles=new List<SetupCastle>();var nodes=CastleRoutes.Catalog.Nodes.ToDictionary(n=>"site:"+n.Id,StringComparer.Ordinal);var homeOf=homes.GroupBy(x=>x.Value).ToDictionary(g=>g.Key,g=>g.Select(x=>x.Key).OrderBy(x=>x,StringComparer.Ordinal).First());
        foreach(var s in WorldNetwork.Sites(new NewGameState(1,new(1,"1560-01-01",[],[],[]),[],new("person:none"),0,15){NetworkPolicy=WorldNetwork.CastlePolicy}))
        {
            string id=s.Id.Value;string? owner=null;
            var node=nodes.GetValueOrDefault(id);
            if(node!=null&&HakutaiDb.Castle(node.Castle)?.Owner is {} house&&owners.Contains(FactionId(house)))owner=FactionId(house);
            var home=homeOf.GetValueOrDefault(id);
            castles.Add(new(id,home??owner));
        }
        // 本拠を同じ城に持つ家が複数あれば、後の家は自分の別の城を本拠にする（なければ置かない）。
        var result=new SetupScenario(1,"1560-01-01",people.ToArray(),castles.ToArray(),factions.ToArray());
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

    // 全国1560年の世界を作る（城の移動網・調べる権利の軽い作り方・続けての日課・遠方の休眠）。
    public static ContractWorld Create(string selected,ulong seed=20261112,string? timePolicy=null)
    {
        var raw=Scenario();var request=new WorldSetupRequest([],[]);var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        return NewGameWorld.Create(setup,[],selected,seed,NewGameWorld.CompactPersonalAuthority,historicalCatalog:RevenueEdition,economyPolicy:RegionalDomains.Policy,npcRoutinePolicy:NewGameNpcRoutine.ContinuingPolicy,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,demandPolicy:LocalWorkDemand.Policy,timePolicy:timePolicy,networkPolicy:WorldNetwork.CastlePolicy,dormancyPolicy:NpcDormancy.Policy,controlPolicy:ControlSuccession.Policy,comingOfAge:FamilyKin.ComingOfAge,strategyPolicy:NationalStrategy.Policy,districtControlPolicy:NationalDistrictControl.Policy,developmentPolicy:CharacterDevelopment.Policy,nationalPersonDataPolicy:NationalPersonEstimates.Policy,healthPolicy:ClinicalHealth.Policy);
    }
    // 新規ゲームの画面から始める（保存の開始定義も同じ版で作る）。
    public static NewCampaignProfile New(NewCampaignStore store,SetupScenario setup,string selected,ulong seed,string? timePolicy,bool previewOnly)
        =>store.New(setup,[],selected,seed,NewGameWorld.CompactPersonalAuthority,RevenueEdition,NewGameNpcRoutine.ContinuingPolicy,LocalInspectionFindings.Policy,WorkFollowup.Policy,LocalWorkDemand.Policy,timePolicy:timePolicy,previewOnly:previewOnly,economyPolicy:RegionalDomains.Policy,comingOfAge:FamilyKin.ComingOfAge,networkPolicy:WorldNetwork.CastlePolicy,dormancyPolicy:NpcDormancy.Policy,controlPolicy:ControlSuccession.Policy,strategyPolicy:NationalStrategy.Policy,districtControlPolicy:NationalDistrictControl.Policy,developmentPolicy:CharacterDevelopment.Policy,nationalPersonDataPolicy:NationalPersonEstimates.Policy,healthPolicy:ClinicalHealth.Policy);
}
