namespace HakutaiKagami.Core.Contracts;

// 全国の開始で有効にする仕組み（2026-11-13）。地方の版で作った仕組みを、全国の開始（NationalStart）でも動かす。
// 新規開始のときに今の一覧（Current）を NewGameState.NationalSystems に写して保存する。保存済みの世界は、そのときの一覧のまま変わらない。
// 仕組みを足すときは Current に名前を足し、その仕組みの Enabled に「|| NationalSystems.On(w,"名前")」を加える。
// 担当：生活・健康・人間関係・婚姻・養子・家督・時間・天候・賊・治安・同行者・密書・和睦と同盟交渉は Claude、戦・兵站・輸送・航海・道は Codex。
public static class NationalSystems
{
    public static readonly string[] Current=
    [
        // Claude の担当
        "PersonalLife",
        "HouseSuccession",
        // Codex の担当
        "OfficerBattleHealth",
        "FieldPresence", "FieldSupply",
        "RiverRoads", "HistoricalRoads", "NationalRoutes",
        "MilitaryEconomy", "HouseLogistics", "HouseFreightReceiving", "FreightOperations", "NpcFreight", "FieldConvoy", "ArmyRoadMarch", "RoadConvoy",
        "NpcArmies", "WorldClashes", "BattlefieldRules", "CastleSiege", "CastleWorks", "HostageCustody", "VoyageExpense",
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
