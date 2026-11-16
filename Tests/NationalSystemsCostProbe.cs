using HakutaiKagami.Core.Contracts;
using Xunit;
using Xunit.Abstractions;
namespace HakutaiKagami.Tests;
// 全国の開始で仕組みを足したときの重さを、仕組みごとに測る（HAKUTAI_NS_COST=1 のときだけ）。
[Trait("Tier","Probe")]
public sealed class NationalSystemsCostProbe(ITestOutputHelper o)
{
    static ContractWorld Make(string? extra)
    {
        var raw=NationalStart.Scenario();var request=new WorldSetupRequest([],[]);var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        bool on(string x)=>extra=="all"||extra==x||extra=="social"&&x is "companion" or "social"||extra=="marriage"&&x is "companion" or "social" or "marriage"||extra=="adoption"&&x is "companion" or "social" or "adoption";
        return NewGameWorld.Create(setup,[],NationalStartTests.Nobunaga(),20261112,NewGameWorld.CompactPersonalAuthority,historicalCatalog:NationalStart.RevenueEdition,economyPolicy:RegionalDomains.Policy,npcRoutinePolicy:NewGameNpcRoutine.ContinuingPolicy,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,demandPolicy:LocalWorkDemand.Policy,networkPolicy:WorldNetwork.CastlePolicy,dormancyPolicy:NpcDormancy.Policy,controlPolicy:ControlSuccession.Policy,comingOfAge:FamilyKin.ComingOfAge,strategyPolicy:NationalStrategy.Policy,districtControlPolicy:NationalDistrictControl.Policy,developmentPolicy:CharacterDevelopment.Policy,nationalPersonDataPolicy:NationalPersonEstimates.Policy,
            nationalSystems:on("life")&&on("succession")?["PersonalLife","HouseSuccession"]:on("life")?["PersonalLife"]:on("succession")?["HouseSuccession"]:null,abilityPolicy:on("ability")?AbilityEffects.Policy:null,
            companionPolicy:on("companion")?PersonalCompanions.RetainerReleasePolicy:null,socialPolicy:on("social")?CharacterRelations.Policy:null,marriagePolicy:on("marriage")?AdultMarriage.Policy:null,adoptionPolicy:on("adoption")?AdultAdoption.Policy:null,
            informationPolicy:on("information")||on("companion")?PrivateInformation.Policy:null,weatherPolicy:on("weather")?RegionalWeather.Policy:null,banditPolicy:on("bandit")?LocalSecurity.Policy:null);
    }
    static ContractWorld Old(string edition,bool strategy,bool control)
    {
        var raw=NationalStart.Scenario(edition);var request=new WorldSetupRequest([],[]);var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        var who=raw.People.First(p=>p.Name.Contains("信長")&&raw.Factions.Any(f=>f.Head==p.Id)).Id;
        return NewGameWorld.Create(setup,[],who,20261112,NewGameWorld.CompactPersonalAuthority,historicalCatalog:edition,npcRoutinePolicy:NewGameNpcRoutine.ContinuingPolicy,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,demandPolicy:LocalWorkDemand.Policy,networkPolicy:WorldNetwork.CastlePolicy,dormancyPolicy:NpcDormancy.Policy,controlPolicy:control?ControlSuccession.Policy:null,comingOfAge:FamilyKin.ComingOfAge,strategyPolicy:strategy?NationalStrategy.Policy:null);
    }
    [Fact]
    public void Measure()
    {
        if(Environment.GetEnvironmentVariable("HAKUTAI_NS_COST")!="1")return;
        var lines=new List<string>();
        bool guard=WorldSession.GuardTurnSnapshots;WorldSession.GuardTurnSnapshots=false; // 実際のゲームと同じ（検査だけの写しの書き換え確認を切る）
        foreach(var extra in (Environment.GetEnvironmentVariable("HAKUTAI_NS_SET")??"base,all").Split(',').Select(x=>x=="base"?null:x))
        {
            var s=new WorldSession(extra switch{"now"=>NationalStart.Create(NationalStartTests.Nobunaga()),"v3"=>NationalStartTests.BeforeRevenue(NationalStartTests.Nobunaga()),"v1bare"=>Old(NationalStart.Edition,false,false),"v1"=>Old(NationalStart.Edition,true,true),"v2"=>Old(NationalStart.CourtEdition,true,true),_=>Make(extra)});var sw=System.Diagnostics.Stopwatch.StartNew();
            NewGameWorldTests.At(s,s.Seconds+long.Parse(Environment.GetEnvironmentVariable("HAKUTAI_NS_SECONDS")??"21600"));
            lines.Add($"{extra??"base"} {sw.ElapsedMilliseconds}ms {ContractJson.CanonicalHash(s.DiagnosticSnapshot())}");o.WriteLine(lines[^1]);
            File.WriteAllLines(Environment.GetEnvironmentVariable("HAKUTAI_NS_OUT")!,lines.Append(Prof.Report()));
        }
        WorldSession.GuardTurnSnapshots=guard;
    }
}
