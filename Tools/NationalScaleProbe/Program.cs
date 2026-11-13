using System.Diagnostics;
using System.Text.Json;
using HakutaiKagami.Core.Contracts;

// 全国の実在拠点と人物に検査用500家を追加し、既存の正式な軍引受を使う。
// 検査用の家は製品シナリオへ保存しない。試作値の軍用米を持つ旧軍事規則の負荷を測る。
var output=Path.GetFullPath(args.FirstOrDefault()??"Docs/Design/NationalControlProjectionV1/runtime-local/scale");
Directory.CreateDirectory(output);
if(args.Contains("--day",StringComparer.Ordinal))
{
    var rawNational=NationalStart.Scenario();var selected=rawNational.People.First(p=>p.Name.Contains("信長",StringComparison.Ordinal)).Id;
    var initial=NationalStart.Create(selected);var sessionDay=new WorldSession(initial);var sim=Stopwatch.StartNew();
    void Until(WorldSession session,long target)
    {
        session.SetPaused(false);
        while(session.Seconds<target){long before=session.Seconds;session.AdvanceTo(Math.Min(target,session.Seconds+3600),100000);Console.WriteLine($"進行 {session.Seconds}秒");if(session.Seconds==before)throw new InvalidDataException("世界時計が進みません");session.SetPaused(false);}
        session.SetPaused(true);
    }
    Until(sessionDay,43200);double first=sim.Elapsed.TotalSeconds;
    var middle=sessionDay.DiagnosticSnapshot();var saves=new GenerationSaveStore(Path.Combine(output,"saves"),middle.Definitions);
    if(!saves.Save(sessionDay,"midday",middle.WorldId).Published)throw new InvalidDataException("途中保存を発行できません");
    var host=new WorldSessionHost(new WorldSession(initial));if(host.CommitLoad(saves.Prepare("midday"),host.Current,host.Current.Revision)!=ResultCode.Accepted)throw new InvalidDataException("途中保存を復旧できません");
    sim.Restart();Until(sessionDay,86400);double latter=sim.Elapsed.TotalSeconds;
    sim.Restart();Until(host.Current,86400);double restoredSeconds=sim.Elapsed.TotalSeconds;
    var after=sessionDay.DiagnosticSnapshot();bool equal=ContractJson.CanonicalHash(after)==ContractJson.CanonicalHash(host.Current.DiagnosticSnapshot());
    var result=new{Status=equal?"同じ未来":"復旧不一致",People=after.Characters.Length,Sites=after.Definitions.Sites.Length,Systems=after.NewGame!.NationalSystems,FirstHalfSeconds=first,SecondHalfSeconds=latter,FullDaySeconds=first+latter,RestoredHalfSeconds=restoredSeconds,Under60Seconds=first+latter<=60,SameFuture=equal,Scope="現在の全国開始で1日と途中保存復旧。軍500の負荷・実機FPSは別途未実施。"};
    File.WriteAllText(Path.Combine(output,"day-result.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(result));
    if(!equal)throw new InvalidDataException("途中保存の未来が一致しません");
    return;
}

var watch=Stopwatch.StartNew();var raw=NationalStart.Scenario(NationalStart.PersonEdition);
var homes=raw.Factions.Select(f=>f.Home).ToHashSet(StringComparer.Ordinal);
var sites=raw.Castles.Where(c=>!homes.Contains(c.Id)&&raw.People.Count(p=>p.Site==c.Id)<8).Take(500).ToArray();
if(sites.Length!=500)throw new InvalidDataException("検査用の既存拠点が不足しています");
var officers=sites.Select((s,n)=>new RegisteredOfficer("created:scale-"+n,null,"検査武将"+n,1520,new(10,10,10,10),StartTemplate.Daimyo,s.Id,null,"検査家"+n)).ToArray();
var placement=new WorldSetupRequest(officers,officers.Select((p,n)=>new DaimyoPlacement(p.Id,"org:scale-"+n,[p.Site],false)).ToArray());
var review=NewGameSetup.Preview(raw,placement);if(!review.IsEligible())throw new InvalidDataException(string.Join("/",review.Errors));
var setup=NewGameSetup.Confirm(raw,placement,review);
var player=raw.People.First(p=>p.Name.Contains("信長",StringComparison.Ordinal)).Id;
var w=NewGameWorld.Create(setup,officers,player,20261113,NewGameWorld.CompactPersonalAuthority,NationalStart.PersonEdition,
    npcRoutinePolicy:NewGameNpcRoutine.ContinuingPolicy,inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,demandPolicy:LocalWorkDemand.Policy,
    networkPolicy:WorldNetwork.CastlePolicy,dormancyPolicy:NpcDormancy.Policy,comingOfAge:FamilyKin.ComingOfAge,worldArmyPolicy:NpcArmies.CompactCombatPolicy,
    developmentPolicy:CharacterDevelopment.Policy,nationalPersonDataPolicy:NationalPersonEstimates.Policy,districtControlPolicy:NationalDistrictControl.Policy);
var creation=watch.Elapsed.TotalSeconds;
watch.Restart();
foreach(var p in officers){w.CommitSequence++;NpcArmies.Muster(w,new("decision:scale-muster/"+Array.IndexOf(officers,p)),new(p.Id),new("org:scale-"+Array.IndexOf(officers,p)));}
var mustering=watch.Elapsed.TotalSeconds;watch.Restart();var session=new WorldSession(w);var validation=watch.Elapsed.TotalSeconds;
var snapshot=session.DiagnosticSnapshot();File.WriteAllBytes(Path.Combine(output,"world.json"),ContractJson.Write(snapshot));
var prepare=new{Status="PREPARED",People=snapshot.Characters.Length,Sites=snapshot.Definitions.Sites.Length,Armies=NpcArmies.Armies(snapshot).Length,Units=snapshot.Units.Length,CreationSeconds=creation,MusteringSeconds=mustering,ValidationSeconds=validation,Scope="検査用500家の旧軍事規則での実軍引受。全国の全戦機能の有効化後の測定は別途必要。"};
File.WriteAllText(Path.Combine(output,"prepare.json"),JsonSerializer.Serialize(prepare,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(prepare));
