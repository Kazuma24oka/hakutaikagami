using System.Diagnostics;
using System.Text.Json;
using HakutaiKagami.Core.Contracts;

// 全国の実在拠点と人物に検査用500家を追加し、既存の正式な軍引受を使う。
// 検査用の家は製品シナリオへ保存しない。試作値の軍用米を持つ旧軍事規則の負荷を測る。
var output=Path.GetFullPath(args.FirstOrDefault()??"Docs/Design/NationalControlProjectionV1/runtime-local/scale");
Directory.CreateDirectory(output);
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
