namespace HakutaiKagami.Core.Contracts;
// 新規開始の版付き入口。旧プロファイルと旧世界へ機能を勝手に追加しない。
public static class LiveTerritory
{
 public const string Policy="new-campaign-autonomous-territory-crafts-survey-council-v1";
 public static bool Enabled(ContractWorld w)=>w.NewGame?.TerritoryPolicy==Policy;
 internal static void Initialize(ContractWorld w){if(!Enabled(w))return;if(!NewGameWork.Enabled(w)||!MaterialMarkets.Enabled(w))throw new InvalidDataException("地域の継続行動には仕事と実物経済の方針が必要です");w.PublicWorks=new(1,PublicWorks.Policy,[],[]);w.Crafts=new(1,CraftTrade.Policy,[]);w.Surveys=new(1,LandSurvey.Policy,[],[],[]);w.Administration=new(1,StandingAdministration.Policy,[],[]);}
}
