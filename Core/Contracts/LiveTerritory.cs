namespace HakutaiKagami.Core.Contracts;
// 新規開始の版付き入口。旧プロファイルと旧世界へ機能を勝手に追加しない。
public static class LiveTerritory
{
 public const string Policy="new-campaign-autonomous-territory-crafts-survey-council-v1";
 public static bool Enabled(ContractWorld w)=>w.NewGame?.TerritoryPolicy is Policy or CastleWorks.Policy or CastleSiege.Policy||NationalSystems.On(w,"LiveTerritory");
 // 当主・奉行が自分から領地の仕事をするか。休眠の版の世界（全国）では、毎時の区切り（起きる人が決まる時刻）に、操作する人物の近くで起きている人だけが動く（遠方の家は動かない。失敗する試みを変更のたびに繰り返さない）。
 internal static Func<EntityId,bool> Acting(ContractWorld w)=>!NpcDormancy.Enabled(w)?_=>true:w.ElapsedSeconds%NpcDormancy.Interval!=0?_=>false:NpcDormancy.AwakeAt(w,w.ElapsedSeconds);
 internal static void Initialize(ContractWorld w){if(!Enabled(w))return;if(!NewGameWork.Enabled(w)||!MaterialMarkets.Enabled(w))throw new InvalidDataException("地域の継続行動には仕事と実物経済の方針が必要です");w.PublicWorks=new(1,PublicWorks.Policy,[],[]);w.Crafts=new(1,CraftTrade.Policy,[]);w.Surveys=new(1,LandSurvey.Policy,[],[],[]);w.Administration=new(1,StandingAdministration.Policy,[],[]);}
}
