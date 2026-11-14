namespace HakutaiKagami.Core.Contracts;

// 全国の開始で有効にする仕組み（2026-11-13）。地方の版で作った仕組みを、全国の開始（NationalStart）でも動かす。
// 新規開始のときに今の一覧（Current）を NewGameState.NationalSystems に写して保存する。保存済みの世界は、そのときの一覧のまま変わらない。
// 仕組みを足すときは Current に名前を足し、その仕組みの Enabled に「|| NationalSystems.On(w,"名前")」を加える。
// 専用の設定を持つ仕組み（健康・和睦など）は、この一覧ではなく NationalStart.Create・New で設定を渡す。
public static class NationalSystems
{
    public static readonly string[] Current=
    [
        // 生活・家
        "PersonalLife", "NpcSelfCare", "AbilityReputation",
        "HouseSuccession",
        // 領地（普請・職人と交易・検地と布告・常設の奉行）
        "LiveTerritory",
        // 城代と城の受け渡し（城主の任命・蔵の預かり・門の引渡し）
        "CastleAdministration",
        // 当主に銭を払って次の出陣を見合わせてもらう約束
        "PrivateStanddown",
        // 戦・兵站・輸送・道
        "OfficerBattleHealth", "ExpeditionFodder",
        "FieldPresence", "FieldSupply", "FieldWorld", "RegionalBattle", "RegionalScenery",
        "RiverRoads", "HistoricalRoads", "NationalRoutes",
        "MilitaryEconomy", "HouseLogistics", "HouseFreightReceiving", "FreightOperations", "NpcFreight", "FieldConvoy", "ArmyRoadMarch", "RoadConvoy",
        "NpcArmies", "ArmyCadence", "RetainerCommand", "StrategyBattle", "StrategyRestoration", "StrategyDefection", "StrategyRealArmies", "StrategyLosses", "WorldClashes", "BattleCaptivity", "PlayerCapture", "BattlefieldRules", "CastleSiege", "CastleWorks", "HostageCustody", "VoyageExpense",
    ];
    // これまでに一覧に入ったことのある名前（保存の検証に使う）。名前を消さない。
    static readonly HashSet<string> known=new(Current,StringComparer.Ordinal);
    public static bool On(ContractWorld w,string name)=>w.NewGame?.NationalSystems is {} list&&Array.IndexOf(list,name)>=0;
    internal static void Validate(NewGameState data)
    {
        if(data.NationalSystems is not {} list)return;
        if(!NationalStart.IsNational(data.HistoricalCatalog)||list.Distinct(StringComparer.Ordinal).Count()!=list.Length||list.Any(n=>!known.Contains(n)))
            throw new InvalidDataException("全国の開始で有効にする仕組みの一覧が不正です");
    }
}
