using HakutaiKagami.Core.Appearance;
using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

// Immutable authoring inputs are saved beside the live Core; they do not become the player's world knowledge.
public sealed record NewGameState(int Schema, SetupScenario Start, RegisteredOfficer[] Registered, EntityId Selected, ulong Seed, int AdultAge)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public VoyageExpenseRules? VoyageRules {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? WorldArmyPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? EconomyPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? CompanionPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? TrucePolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? InformationPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? StanddownPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? DevelopmentPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? SocialPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? AdoptionPolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? MarriagePolicy {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? CustodyPolicy {get;init;}


    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? PersonalAuthorityLayout { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? HistoricalCatalog { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? NpcRoutinePolicy { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? InspectionPolicy { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkPolicy { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? DemandPolicy { get; init; }


}
public static partial class NewGameWorld
{
    public const string Ruleset="new-game-personal-world-v1";
    public static readonly EntityId Basis=new("basis:new-game-start");
    public const int TrialAdultAge=18,TrialBudget=1000;
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok,string message){if(!ok)throw new InvalidDataException(message);}
    public static string RoleName(StartTemplate role)=>role switch{StartTemplate.Ronin=>"浪人",StartTemplate.Retainer=>"家臣・随行",StartTemplate.Subretainer=>"陪臣",StartTemplate.Castellan=>"城主",StartTemplate.LocalLord=>"国人領主",StartTemplate.Daimyo=>"大名",_=>"未設定"};
    public static string Node(string site)
    {
        if(site=="castle:kiyosu")return "castle";if(site=="castle:kofu")return "kofu";if(site=="castle:odawara")return "odawara";
        return NationalRoutes.Nodes.SingleOrDefault(n=>RegionalCampaign.Site(n.Id).Value==site)?.Id??throw new InvalidDataException("収録されていない登場拠点です");
    }
    public static string SiteName(string site)=>NationalRoutes.Sites.Single(s=>s.Id==RegionalCampaign.Site(Node(site))).Name;
    public static RegisteredOfficer[] Normalize(RegisteredOfficer[] records)
    {
        NewGameSetup.Validate(new OfficerLibrary(1,records,[]));
        // A group's copies may still name the ungrouped source as their lord. Resolve only an unambiguous in-group copy.
        return records.Select(p=>{
            string? lord=p.Lord;if(lord!=null&&!records.Any(q=>q.Id==lord)){
                var copies=records.Where(q=>q.CopiedFrom==lord).ToArray();Need(copies.Length<=1,"直属相手の複製が複数あります。グループ内で相手を指定してください");if(copies.Length==1)lord=copies[0].Id;
            }
            return ContractJson.Copy(p) with{Site=RegionalCampaign.Site(Node(p.Site)).Value,Lord=lord};
        }).ToArray();
    }
    public static SetupScenario Scenario(string? historicalCatalog=null,string? npcRoutinePolicy=null,string? inspectionPolicy=null,string? workPolicy=null)
    {
        if(historicalCatalog!=null)return HistoricalStartCatalog.Scenario(historicalCatalog);
        var catalog=NationalPersonnel.Catalog;
        var people=catalog.People.Select(p=>new SetupPerson(p.Id.Value,p.Name,RegionalCampaign.Site(p.HomeCandidate).Value,p.Faction.Value,null,false,false)).ToArray();
        var factions=catalog.Factions.Select(f=>{var head=people.Single(p=>p.Faction==f.Id.Value);return new SetupFaction(f.Id.Value,f.Name,head.Id,head.Site);}).ToArray();
        var castles=NationalRoutes.Sites.Select(s=>new SetupCastle(s.Id.Value,factions.SingleOrDefault(f=>f.Home==s.Id.Value)?.Id)).ToArray();
        // Only the sourced names/affiliations and home candidates are historical data. Territorial/head rights here are declared trial initialization.
        return new(1,"1560-01-01",people,castles,factions);
    }
    public static bool CanPlay(SetupScenario setup,SetupPerson p,int adultAge=TrialAdultAge)=>!p.Created||DateOnly.ParseExact(setup.EpochDate,"yyyy-MM-dd").Year-p.BirthYear>=adultAge;
    public static string InitialNode(ContractWorld world,EntityId actor)=>Node(world.NewGame!.Start.People.Single(p=>p.Id==actor.Value).Site);
    public static OfficerAppearance Look(NewGameState state,string person)
    {
        var p=state.Registered.SingleOrDefault(p=>p.Id==person);
        var look=p?.Look??AppearanceCatalog.Default(state.Start.People.Single(q=>q.Id==person).Created?1:0);
        return ContractJson.Copy(look);
    }
    public static string Version(NewGameState state)=>ContractJson.CanonicalHash(new[]{Ruleset,NationalRoutes.Version,ContractJson.CanonicalHash(state)});
    static TimedRelation Relation(string kind,string id,EntityId subject,EntityId target)=>new(new(kind+":"+id),subject,target,kind=="membership"?"membership":kind=="service"?"service":kind=="land"?"control":kind,Basis,0,long.MaxValue,0);
    internal static (TimedRelation[] Members,TimedRelation[] Bonds,TimedRelation[] Offices,TimedRelation[] Lands) InitialRelations(NewGameState data)
    {
        var members=new List<TimedRelation>();var bonds=new List<TimedRelation>();var offices=new List<TimedRelation>();var lands=new List<TimedRelation>();
        foreach(var p in data.Start.People){var id=new EntityId(p.Id);string suffix=ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(p.Id));
            if(p.Faction!=null)members.Add(Relation("membership",suffix,id,new(p.Faction)));
            if(p.Lord!=null)bonds.Add(Relation("service",suffix,id,new(p.Lord)));
            var head=data.Start.Factions.SingleOrDefault(f=>f.Head==p.Id);
            if(head!=null)offices.Add(Relation("house-head",suffix,id,new(head.Home)));
            else if(p.InitialRole is StartTemplate.Castellan or StartTemplate.LocalLord){offices.Add(Relation(p.InitialRole==StartTemplate.Castellan?"castellan":"house-head",suffix,id,new(p.Site)));if(p.InitialRole==StartTemplate.LocalLord)lands.Add(Relation("land",suffix,id,new(p.Site)));}
        }
        foreach(var c in data.Start.Castles.Where(c=>c.Faction!=null))lands.Add(Relation("land",ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(c.Id)),new(c.Faction!),new(c.Id)));
        return(members.ToArray(),bonds.ToArray(),offices.ToArray(),lands.ToArray());
    }
    static Grant[] Grants(NewGameState state)=>state.PersonalAuthorityLayout==CompactPersonalAuthority?CompactPersonalGrants(state):state.Start.People.SelectMany(p=>
        new[]{new Grant(new("grant:new-game/move/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(p.Id))),new(p.Id),new(p.Id),ContractFixture.Castle,[ActionKind.Move],Basis,0,long.MaxValue,0,[],false,0)}
        .Concat(NationalRoutes.Sites.Select(s=>new Grant(new("grant:new-game/inspect/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(p.Id+"/"+s.Id.Value))),new(p.Id),new(p.Id),s.Id,[ActionKind.Inspect],Basis,0,long.MaxValue,0,[],false,0)))).ToArray();
    static void ValidateStart(NewGameState data)
    {
        Need(data.CustodyPolicy is null or HostageCustody.Policy,"Unknown custody policy");Need(data.CustodyPolicy==null||data.SocialPolicy==CharacterRelations.Policy,"Custody requires personal consent policy");Need(data.MarriagePolicy is null or AdultMarriage.Policy,"Unknown marriage policy");Need(data.MarriagePolicy==null||data.SocialPolicy==CharacterRelations.Policy,"Marriage requires social policy");Need(data.AdoptionPolicy is null or AdultAdoption.Policy,"Unknown adoption policy");Need(data.AdoptionPolicy==null||data.SocialPolicy==CharacterRelations.Policy,"Adoption requires contextual personal consent");Need(data.SocialPolicy is null or CharacterRelations.Policy,"Unknown social policy");Need(data.SocialPolicy==null||data.CompanionPolicy!=null,"Social cooperation requires companion policy");Need(data.DevelopmentPolicy is null or CharacterDevelopment.Policy,"Unknown character development policy");Need(data.StanddownPolicy is null or PrivateStanddown.Policy,"Unknown private standdown policy");Need(data.StanddownPolicy==null||data.InformationPolicy==PrivateInformation.Policy&&data.WorldArmyPolicy==NpcArmies.CompactCombatPolicy,"Standdown requires sourced contacts and real military world");Need(data.InformationPolicy is null or PrivateInformation.Policy,"Unknown private information policy");Need(data.InformationPolicy==null||NewGameWork.Supported(data.WorkPolicy),"Information fee requires private wallets");Need(data.TrucePolicy is null or TruceNegotiations.Policy,"Unknown truce policy");Need(data.TrucePolicy==null||data.WorldArmyPolicy==NpcArmies.CompactCombatPolicy,"Truce requires versioned actual military world");Need(data.CompanionPolicy==null||PersonalCompanions.Supported(data.CompanionPolicy),"Unknown companion policy");Need(data.CompanionPolicy is not(PersonalCompanions.PrivateInspectionPolicy or PersonalCompanions.PersonalServicePolicy or PersonalCompanions.RetainerReleasePolicy)||data.InformationPolicy==PrivateInformation.Policy,"Private inspection requires sourced personal contact");Need(data.CompanionPolicy==null||NewGameWork.Supported(data.WorkPolicy),"Companions require private wallets");Need(data.EconomyPolicy is null or RegionalEconomy.Policy or CommunityEconomy.Policy or RegionalTrade.Policy or HouseSalary.Policy or VillageConsultation.Policy or NpcCommunityAdministration.Policy or CommunityStewardship.Policy or MaterialMarkets.Policy or RegionalDomains.Policy or HouseLogistics.Policy or HouseFreightReceiving.Policy or MilitaryEconomy.Policy or NpcFreight.Policy or FreightOperations.Policy or FieldConvoy.Policy or ArmyRoadMarch.Policy or RoadConvoy.Policy or PersonalLife.Policy or CastleHandover.Policy or CastleAdministration.Policy or HouseSuccession.Policy,"Unknown regional economy policy");Need(data.EconomyPolicy is not(RegionalTrade.Policy or HouseSalary.Policy or VillageConsultation.Policy or NpcCommunityAdministration.Policy or CommunityStewardship.Policy or MaterialMarkets.Policy or RegionalDomains.Policy or HouseLogistics.Policy or HouseFreightReceiving.Policy or MilitaryEconomy.Policy or NpcFreight.Policy or FreightOperations.Policy or FieldConvoy.Policy or ArmyRoadMarch.Policy or RoadConvoy.Policy or PersonalLife.Policy or CastleHandover.Policy or CastleAdministration.Policy or HouseSuccession.Policy)||NewGameWork.Supported(data.WorkPolicy),"Trade requires a private purse policy");Need(data.WorldArmyPolicy==null||NpcArmies.Supported(data.WorldArmyPolicy),"Unknown world army policy");VoyageExpense.ValidateRules(data.VoyageRules);Need(data.VoyageRules==null||NewGameWork.Supported(data.WorkPolicy),"Voyage fares require private wallets");Need(data!=null&&data.Schema==1&&data.Start!=null&&data.Registered!=null&&data.AdultAge==TrialAdultAge,"開始定義の版が不正です");Need(data.PersonalAuthorityLayout is null or CompactPersonalAuthority,"本人権限の保存形式が不正です");Need(data.DemandPolicy is null or LocalWorkDemand.Policy,"Unknown local demand policy");Need(data.DemandPolicy==null||NewGameWork.Supported(data.WorkPolicy),"Demand requires work and observation policies");Need(data.WorkPolicy==null||NewGameWork.Supported(data.WorkPolicy),"Unknown work cycle policy");Need(data.WorkPolicy==null||data.InspectionPolicy==LocalInspectionFindings.Policy,"Work requires local observation policy");Need(data.InspectionPolicy is null or LocalInspectionFindings.Policy,"Unknown local inspection policy");Need(data.NpcRoutinePolicy==null||NewGameNpcRoutine.Supported(data.NpcRoutinePolicy),"NPC routine policy is unknown");Need(data.NpcRoutinePolicy!=NewGameNpcRoutine.ContinuingPolicy||data.DemandPolicy==LocalWorkDemand.Policy&&data.InspectionPolicy==LocalInspectionFindings.Policy,"Continuing NPC work requires own local observations");Need(data.NpcRoutinePolicy==null||(data.NpcRoutinePolicy==NewGameNpcRoutine.ContinuingPolicy?data.Start.People.Count(p=>p.Id!=data.Selected.Value&&CanPlay(data.Start,p))<=NewGameNpcRoutine.ContinuingMaximumActors:data.Start.People.Count(p=>!p.Created&&p.Id!=data.Selected.Value)<=NewGameNpcRoutine.MaximumActors),"NPC routine candidate limit exceeded");NewGameSetup.Validate(data.Start);NewGameSetup.Validate(new OfficerLibrary(1,data.Registered,[]));
        Need(data.Start.EpochDate=="1560-01-01","この候補は1560年開始です");
        Need(data.Start.People.Where(p=>!p.Created).OrderBy(p=>p.Id).Select(p=>(p.Id,p.Name)).SequenceEqual(Scenario(data.HistoricalCatalog).People.OrderBy(p=>p.Id).Select(p=>(p.Id,p.Name))),"史実人物の定義を無断追加・変更できません");
        Need(data.Start.Castles.Select(c=>c.Id).Order().SequenceEqual(NationalRoutes.Sites.Select(s=>s.Id.Value).Order()),"収録拠点の構成が違います");
        var baseScenario=Scenario(data.HistoricalCatalog);var allocations=data.Start.Factions.Where(f=>data.Registered.Any(p=>p.Id==f.Head&&p.Role==StartTemplate.Daimyo)).Select(f=>new DaimyoPlacement(f.Head,f.Id,data.Start.Castles.Where(c=>c.Faction==f.Id).Select(c=>c.Id).ToArray(),baseScenario.Factions.Any(old=>data.Start.People.Single(p=>p.Id==old.Head).Lord==f.Head))).ToArray();
        var rebuilt=NewGameSetup.Preview(baseScenario,new(data.Registered,allocations));Need(rebuilt.IsEligible()&&ContractJson.CanonicalHash(rebuilt.World)==ContractJson.CanonicalHash(data.Start),"開始配置は選択した登録武将と領土・処遇から再現できません");

        Need(data.Start.People.Any(p=>p.Id==data.Selected.Value&&CanPlay(data.Start,p,data.AdultAge)),"操作できる成人武将を選んでください");
        Need(data.Start.People.Where(p=>p.Created).Select(p=>p.Id).Order().SequenceEqual(data.Registered.Select(p=>p.Id).Order()),"登録武将の定義が欠落しています");
        Need(data.Start.People.All(p=>NationalRoutes.Sites.Any(s=>s.Id.Value==p.Site)&&EntityId.IsValid(p.Id)&& (p.Faction==null||EntityId.IsValid(p.Faction))),"人物・登場場所のIDが不正です");
        foreach(var p in data.Registered){var live=data.Start.People.Single(q=>q.Id==p.Id);Need(live.Name==p.Name&&live.BirthYear==p.BirthYear&&live.Attributes==p.Attributes&&live.Site==p.Site&&live.InitialRole==p.Role,"登録武将の内容が配置と一致しません");Look(data,p.Id).Validate(AppearanceCatalog.Slots);
            if(p.Role==StartTemplate.Subretainer)Need(live.Lord!=null&&data.Start.People.Single(q=>q.Id==live.Lord).Lord!=null,"陪臣の直属相手には、その上の主君が必要です");
            long cost=(long)p.Attributes.Leadership+p.Attributes.Valor+p.Attributes.Intelligence+p.Attributes.Politics+(p.Attributes.Negotiation??0)+100L*data.Start.Castles.Count(c=>data.Start.Factions.Any(f=>f.Head==p.Id&&f.Id==c.Faction))+50L*data.Start.People.Count(q=>q.Lord==p.Id);
            Need(new[]{p.Attributes.Leadership,p.Attributes.Valor,p.Attributes.Intelligence,p.Attributes.Politics,p.Attributes.Negotiation??0}.All(n=>n<=100)&&cost<=TrialBudget,"登録武将の開始能力・領土・配下が試作予算を超えています（能力0～100、総予算1000）");
        }
        Need(data.Start.People.Where(p=>p.InitialRole==StartTemplate.Castellan).GroupBy(p=>p.Site).All(g=>g.Count()==1),"同じ城に複数の登録城主がいます");
    }
    public static ContractWorld Create(SetupScenario confirmed,RegisteredOfficer[] registered,string selected,ulong seed=20261022,string? personalAuthorityLayout=null,string? historicalCatalog=null,string? npcRoutinePolicy=null,string? inspectionPolicy=null,string? workPolicy=null,string? demandPolicy=null,VoyageExpenseRules? voyageRules=null,string? worldArmyPolicy=null,string? economyPolicy=null,string? companionPolicy=null,string? trucePolicy=null,string? informationPolicy=null,string? standdownPolicy=null,string? developmentPolicy=null,string? socialPolicy=null,string? adoptionPolicy=null,string? marriagePolicy=null,string? custodyPolicy=null)
    {
        var data=new NewGameState(1,ContractJson.Copy(confirmed),Normalize(registered),new(selected),seed,TrialAdultAge){PersonalAuthorityLayout=personalAuthorityLayout,HistoricalCatalog=historicalCatalog,NpcRoutinePolicy=npcRoutinePolicy,InspectionPolicy=inspectionPolicy,WorkPolicy=workPolicy,DemandPolicy=demandPolicy,VoyageRules=voyageRules,WorldArmyPolicy=worldArmyPolicy,EconomyPolicy=economyPolicy,CompanionPolicy=companionPolicy,TrucePolicy=trucePolicy,InformationPolicy=informationPolicy,StanddownPolicy=standdownPolicy,DevelopmentPolicy=developmentPolicy,SocialPolicy=socialPolicy,AdoptionPolicy=adoptionPolicy,MarriagePolicy=marriagePolicy,CustodyPolicy=custodyPolicy};ValidateStart(data);
        var w=TravelWorld.Create();w.NewGame=data;w.WorldClashes=worldArmyPolicy==NpcArmies.CompactCombatPolicy?new(1,WorldClashes.SparsePolicy,[]):worldArmyPolicy==NpcArmies.CombatPolicy?new(1,WorldClashes.Policy,[]):null;w.NpcArmies=worldArmyPolicy==null?null:new(1,NpcArmies.Policy,[],[],[]);if(voyageRules!=null||worldArmyPolicy!=null){w.TravelDecisions=[];w.TrafficStops=[];}w.NpcWork=data.NpcRoutinePolicy==null?null:new([]);w.WorldId=new("world:"+Ruleset);w.Seed=seed;w.Paused=true;w.PlayerControl=data.Selected;
        w.Definitions=w.Definitions with{Id=new("scenario:"+Ruleset),Ruleset=Ruleset,Version=Version(data),Characters=data.Start.People.Select(p=>new DefinitionEntry(new(p.Id),p.Name)).ToArray(),Organizations=data.Start.Factions.Select(f=>new DefinitionEntry(new(f.Id),f.Name)).ToArray(),AuthorityBases=[new(Basis,"新規ゲームの開始配置（役職・領土は試作設定）。個人の移動/自己点検以外の軍・徴収権を付与しない")],Armies=[]};
        w.Characters=data.Start.People.Select(p=>new CharacterState(new(p.Id),CanPlay(data.Start,p)?LifeState.Adult:LifeState.Minor,new("appearance:"+ContractJson.Hash(ContractJson.Write(Look(data,p.Id)))),0)).ToArray();
        w.Locations=data.Start.People.Select(p=>new LocationState(new(p.Id),LocationKind.AtSite,new(p.Site),null,0,0,null,null,null,null,0)).ToArray();
        var rel=InitialRelations(data);w.Memberships=rel.Members;w.ServiceBonds=rel.Bonds;w.Offices=rel.Offices;w.LandRights=rel.Lands;w.Grants=Grants(data);
        AdultAdoption.Initialize(w);AdultMarriage.Initialize(w);HostageCustody.Initialize(w);CharacterRelations.Initialize(w);CharacterDevelopment.Initialize(w);w.Units=[];w.ResourceLots=[];w.Evidence=[];w.Knowledge=[];NewGameWork.Initialize(w);RegionalEconomy.Initialize(w);MilitaryEconomy.Initialize(w);HouseLogistics.Initialize(w);FreightOperations.Initialize(w);FieldConvoy.Initialize(w);ArmyRoadMarch.Initialize(w);RegionalDomains.Initialize(w);CommunityEconomy.Initialize(w);RegionalTrade.Initialize(w);HouseSalary.Initialize(w);PersonalLife.Initialize(w);CastleHandover.Initialize(w);CastleAdministration.Initialize(w);CastellanReports.Initialize(w);HouseSuccession.Initialize(w);VillageConsultation.Initialize(w);NpcCommunityAdministration.Initialize(w);CommunityStewardship.Initialize(w);MaterialMarkets.Initialize(w);if(companionPolicy!=null)w.Companions=new(1,companionPolicy,[],[]);if(trucePolicy!=null)w.Truces=new(1,TruceNegotiations.Policy,[],[],[],[]);if(informationPolicy!=null)w.PrivateInformation=new(1,PrivateInformation.Policy,[],[],[]);if(standdownPolicy!=null)w.Standdown=new(1,PrivateStanddown.Policy,[],[]);
        foreach(var p in data.Start.People){var id=new EntityId("evidence:new-game/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(p.Id)));w.Evidence=[..w.Evidence,new(id,new(p.Id),new(p.Id),"initial-self-role:"+(p.Faction??"ronin")+"/"+(p.Lord??"none"),0,0,null,null)];w.Knowledge=[..w.Knowledge,new(new(p.Id),id,0)];}
        w.RandomStreams=w.RandomStreams.Select(s=>new RandomStream(s.Purpose,ContractRandom.InitialState(seed,s.Purpose),0)).ToArray();ContractValidation.Validate(w);return w;
    }
    public static Intent LocalWork(ContractWorld w,string id)
    {
        var site=w.Locations.Single(l=>l.Actor==w.PlayerControl);Need(site.Kind==LocationKind.AtSite,"到着した拠点で相談してください");Need(NewGameWork.ScopeSupported(w,site.Site!.Value),"この拠点の陸路は点検対象が未設定です。別の拠点で相談してください");
        Need(!w.Tasks.Any(t=>t.Executor==w.PlayerControl&&t.Execution is not(ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed)),"先に進行中の用件を片付けてください");
        var knownLord=NewGameService.View(w).Principal;var other=LocalWorkDemand.Candidates(w).FirstOrDefault()??throw new InvalidDataException("この拠点に新しい点検を依頼する相手はいません");
        return new(new(id),other.Actor,w.PlayerControl,site.Site!.Value,ActionKind.Inspect,null,null,0,null,new(DeadlineCriterion.Completion,w.ElapsedSeconds+120,false,null,w.ElapsedSeconds+120),w.ElapsedSeconds,1,4,1,true,knownLord==other.Actor&&NewGameService.BoundAt(w,other.Actor,w.ElapsedSeconds,w.CommitSequence)?TaskKind.BindingOrder:TaskKind.VoluntaryRequest);
    }
    public static string? Address(ContractWorld w,MessageEnvelope message)
    {
        if(!Enabled(w))return null;if(NewGameMilitary.RemoteAddress(w,message) is {} remoteMilitaryAddress)return remoteMilitaryAddress;if(TreatyDefense.Address(w,message) is {} defenseAddress)return defenseAddress;if(TreatySupply.Address(w,message) is {} supplyAddress)return supplyAddress;if(TreatyNegotiations.Address(w,message) is {} treatyAddress)return treatyAddress;if(TreatyNegotiations.OperationAddress(w,message) is {} operationAddress)return operationAddress;if(TruceConfirmations.Address(w,message) is {} confirmAddress)return confirmAddress;if(CastellanReports.Address(w,message) is {} castellanAddress)return castellanAddress;if(FreightOperations.Address(w,message) is {} freightAddress)return freightAddress;if(CommunityStewardship.Address(w,message) is {} stewardshipAddress)return stewardshipAddress;if(CommunityEconomy.Address(w,message) is {} communityAddress)return communityAddress;if(PersonalCompanions.ReleaseAddress(w,message) is {} releaseAddress)return releaseAddress;if(PrivateInformation.Address(w,message) is {} informationAddress)return informationAddress;if(TruceNegotiations.Address(w,message) is {} truceAddress)return truceAddress;if(PersonalCompanions.Address(w,message) is {} companionAddress)return companionAddress;if(FactionNotices.Address(w,message) is {} politicalAddress)return politicalAddress;if(NewGameMilitary.Address(w,message) is {} militaryAddress)return militaryAddress;if(NewGameService.Address(w,message) is {} address)return address;var request=w.Requests.SingleOrDefault(r=>r.Id.Value==message.CorrelationId);
        // Reply to the original meeting site, not the other person's current GPS.
        return request!=null&&request.Intent.Actor!=request.Intent.Executor?Node(request.Intent.Scope.Value):null;
    }
    static bool Same<T>(T a,T b)=>ContractJson.CanonicalHash(a)==ContractJson.CanonicalHash(b);
    public static void Validate(ContractWorld w)
    {
        if(!Enabled(w)){Need(w.NewGame==null,"他の開始に新規ゲーム定義を混在できません");return;}
        Need(w.NewGame!=null,"開始定義がありません");var d=w.NewGame;var projection=VerifiedStart(d);
        Need(w.PlayerControl==d.Selected&&w.Seed==d.Seed&&w.Definitions.Version==projection.Version&&w.EpochDate==d.Start.EpochDate,"開始人物・Seed・定義が一致しません");
        Need(ContractJson.CanonicalHash(w.Definitions.Characters)==projection.CharactersHash&&ContractJson.CanonicalHash(w.Definitions.Organizations)==projection.OrganizationsHash,"世界の人物・勢力が開始定義と一致しません");
        Need(w.Definitions.Armies.Length==0,"開始定義へ軍務の動的名簿を混在させることはできません");
        var rel=projection.Relations;Need(Same(w.Memberships,NewGameService.Relations(w,rel.Members,"membership"))&&Same(w.ServiceBonds,NewGameService.Relations(w,rel.Bonds,"service"))&&ContractJson.CanonicalHash(w.Offices)==ContractJson.CanonicalHash(HouseSuccession.Offices(w,rel.Offices))&&ContractJson.CanonicalHash(w.LandRights)==projection.LandsHash&&ContractJson.CanonicalHash(w.Grants)==projection.GrantsHash&&(w.NewMilitary!=null||w.Units.All(u=>NpcArmies.Armies(w).Any(a=>a.Id==u.Army))&&w.ResourceLots.All(l=>NewGameWork.IsWallet(w,l)||TransportWork.IsLot(l)||NpcArmies.IsLot(w,l))),"開始時の主従・土地・権限を無断変更できません");
        foreach(var p in d.Start.People){var c=w.Characters.Single(x=>x.Id.Value==p.Id);Need(c.AppearanceId==projection.People[p.Id].Appearance&&c.Life==projection.People[p.Id].Life,"人物の外見・成人資格が一致しません");if(!(c.Id==w.PlayerControl&&NewGameMilitary.Deployed(w))&&!w.Branches!.Journeys.Any(j=>j.Actor==c.Id)&&!FieldConvoy.Controls(w,c.Id,w.ElapsedSeconds)&&!HostageCustody.HasReturnHistory(w,c.Id))Need(w.Locations.Single(l=>l.Actor==c.Id).Site==new EntityId(p.Site),"移動記録なしに所在を変更できません");}
        foreach(var r in w.Requests)ValidateOrderAt(w,r.Intent,r.CreatedAt);
    }
    public static Intent Inspect(ContractWorld w,string id)=>InspectFor(w,w.PlayerControl,id,w.ElapsedSeconds);
    internal static Intent InspectFor(ContractWorld w,EntityId actor,string id,long at)=>new(new(id),actor,actor,w.Locations.Single(l=>l.Actor==actor).Site??throw new InvalidDataException("到着してから点検してください"),ActionKind.Inspect,null,null,0,null,new(DeadlineCriterion.Completion,at+60,false,null,at+60),at,1,4,1,false,TaskKind.BindingOrder);
    public static void ValidateOrder(ContractWorld w,Intent i)
    {
        if(HostageCustody.IsReturnTask(w,i))return;
        if(i.Action==ActionKind.Train){CharacterDevelopment.ValidatePractice(w,i,w.ElapsedSeconds,true);return;}if(PersonalCompanions.IsTask(w,i)){PersonalCompanions.ValidatePending(w,i);return;}if(i.Action==ActionKind.Move){TravelWorld.ValidateOrder(w,i);return;}
        if(i.Actor==i.Executor&&i.Executor!=w.PlayerControl){Need(NewGameNpcRoutine.IsActor(w,i.Actor)&&i==InspectFor(w,i.Actor,i.RequestId.Value,w.ElapsedSeconds),"NPC personal inspection lacks declared policy");return;}
        if(i.Actor!=i.Executor){Need(i==LocalWork(w,i.RequestId.Value),"現地で相談した任意依頼の条件が違います");return;}
        Need(i.Action==ActionKind.Inspect && i==Inspect(w,i.RequestId.Value),"本人の移動・到着した拠点の自己点検だけを扱います");
    }
    static void ValidateOrderAt(ContractWorld w,Intent i,long at)
    {
        if(i.Action==ActionKind.Train){CharacterDevelopment.ValidatePractice(w,i,at,false);return;}if(PersonalCompanions.IsTask(w,i))return;Need((i.Executor==w.PlayerControl||(ArmyRoadMarch.IsTrip(w,i)||RoadConvoy.IsTrip(w,i))||NpcFreight.IsTrip(w,i)||NpcArmies.IsMarch(w,i)||i.Actor==i.Executor&&i.Action==ActionKind.Inspect&&NewGameNpcRoutine.IsActor(w,i.Actor))&&i.Action is ActionKind.Move or ActionKind.Inspect,"本人以外の指揮・徴収は開始だけではできません");
        if(i.Actor!=i.Executor){Need(NewGameWork.ScopeSupported(w,i.Scope),"Unmapped local inspection offer");Need(LocalWorkDemand.At(w,i.Actor,i.Scope,at,w.Requests.Single(r=>r.Id==i.RequestId).CommitSequence-1).Needed,"Local inspection lacks known demand at issue");Need(i.Action==ActionKind.Inspect&&(i.Kind==TaskKind.VoluntaryRequest||i.Kind==TaskKind.BindingOrder&&NewGameService.BoundAt(w,i.Actor,at,w.Requests.Single(r=>r.Intent.RequestId==i.RequestId).CommitSequence-1)&&NewGameService.KnownBoundAt(w,i.Actor,at,w.Requests.Single(r=>r.Intent.RequestId==i.RequestId).CommitSequence-1))&&i.Important&&i.WorkSeconds==4&&i.DeliverySeconds==1&&i.ReportSeconds==1&&i.Scope.Value==dSite(w,i.Executor,at)&&i.Scope.Value==dSite(w,i.Actor,at)&&i.Deadline.At==at+120,"現地相談の物理所在・任意依頼・期限が不正です");}
        else if(i.Action==ActionKind.Inspect)Need(i.WorkSeconds==4&&i.DeliverySeconds==1&&i.ReportSeconds==1&&i.Scope.Value==dSite(w,i.Actor,at)&&i.Deadline.At==at+60,"自己点検の場所・期限が不正です");
    }
    static string dSite(ContractWorld w,EntityId actor,long at){var j=w.Branches!.Journeys.LastOrDefault(j=>j.Actor==actor&&w.Tasks.Single(t=>t.Id==j.Task).FinishedAt<=at);return j==null?w.NewGame!.Start.People.Single(p=>p.Id==actor.Value).Site:RegionalCampaign.Site(j.Legs[^1].To).Value;}
}
