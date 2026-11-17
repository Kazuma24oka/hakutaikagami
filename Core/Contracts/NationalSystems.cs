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
        "PersonalLife", "NpcSelfCare", "NpcHouseCare", "AbilityReputation",
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
        "NpcArmies", "ArmyCadence", "RetainerCommand", "StrategyBattle", "StrategyRestoration", "StrategyDefection", "StrategyRealArmies", "StrategyLosses", "ArmyRout", "DefectionLetters", "StrategyCapture", "NpcCaptors", "StrategyIndependence", "RestorationSupport", "ConquestDomains", "NpcTeachers", "NpcSkirmish", "PlayerCaptiveLetters", "PlayerUnitLeaders", "SkirmishPassing", "IndependentHouses", "LocalLords", "CaptiveEscort", "WorkExperience", "TroopSkills", "NpcDoctor", "DbMothers", "WorldClashes", "BattleCaptivity", "PlayerCapture", "BattlefieldRules", "CastleSiege", "CastleWorks", "HostageCustody", "VoyageExpense",
        // 止めるもの：家中の書状の受け渡しで付き合いの記録ができると、地方の版のNPCの日課の付き合い（訪問・噂・贈り物・寄進・茶会）が全員で動き、記録が1日に数千件増える。これを入れた世界では動かさない（入れる前の保存は今までどおり）。
        QuietNpcSocial,
        // 1560年の官位（百代鏡DBの追加の版、就いた年の記録のあるものだけ）を名乗りと朝廷の序列の始まりに入れる。
        DbOffices1560,
        // 史実人物の開始時の技能に、百代鏡DBの技能・専門の候補（経歴の手がかり）を入れる。
        DbSkillHints,
        // 史実人物の家系（百代鏡DBの本姓）と信仰（宗派・信仰の候補）を、作成武将の家系・信仰と同じ一覧で引けるようにする。
        DbLineageFaith,
        // 危篤・意思表示不能、家督争いの決着と兄弟姉妹の家督、遺産の相続、本人の手当の医術と後遺症、操作の引き継ぎの兄弟姉妹（S02）。
        MortalSuccession,
        // 史実の事件の第2版（1467年〜1615年の出陣と史実の同盟、S03）。
        HistoryEvents2,
        // 個人の目標の追加（名声・一門の繁栄・城の奪取）と、1650年の総括の歩み（S03）。
        LifeGoals2,
        // 京の政治の効果（全国の守護・将軍の栄典と偏諱・守護の実権・名乗りと席次・補任と栄典の噂・将軍の継承、S04）。
        CentralStanding,
        // 遠方の合戦の天候と地形・NPCの開城・負傷兵・捕縛からの逃亡・落城の勲功と知行（S05）。
        Warfare2,
        // DBの配偶者・養親の事実、婚姻による同盟と離縁、破約への制裁、約束の引き継ぎの知らせ、情報の索引（S06）。
        Diplomacy2,
        // 拠点ごとの人口・実名の寺社と門徒・家中職・拠点の値の初期データ・全国の賊の巣（S07）。
        Territory2,
        // 全国の家の内政：方針と専門職・家計・生産と職人・災害・徴募と逃散・一揆と檄・寺社の権益・破壊の計略・幕府職と奏請（S07B）。
        Domestic2,
        // 全国の水運・交易・補給と名物：名物の目録と移り変わり・探物、水軍と海を渡る出陣、交易船、陣への補給と消耗、隊商と道中の引継ぎ（S08）。
        Logistics2,
        // NPCの判断と人物の一生：志の推定と兵科適性、家の目標と計画、断る条件、自発登用と役の目標、実務の技能と教わる分と衰え、隠居、元服の希望、各年代の改名の台帳（S09A）。
        Persons2,
        // 全国の軍勢の編成・指揮・攻城：作戦線と優先度、兵種の編成と募兵、軍団と一族の分隊、競合する命令と現場の裁量、区画の攻城と開城の条件、救援の到着、逃げた人の帰還、追跡補給（S09B）。
        Armies2,
        // 外交・家中・国人の交渉：約束の条項と条件の変更、本人への申し入れ、多者の和睦と官位の願い、制裁の履行、従属の条項と違反、婚約と輿入れと養子、独立の伝達、派閥、辞去と転籍、捕虜の交換、偵察と流言（S09C）。
        Politics2,
        // 領地と寺社と朝廷の個別の案件：水利の争いの裁定と普請、寺社への願いとキリスト教、惣国と徳政、客将と任命の再承認、国司・京官の効き目、幕府への申請と奉行人、中央の仲裁と兵を退くこと（S09D）。
        Cases2,
        // 任務・配送・命令の全般：併用の表と代わり・戻り・補償、書状の道と継ぎと混み・代理の開封、命令の競合、子の形成、手強さの先読み、役以外の技能、後見、代理の決裁と追認、国人連合、配下の攻撃の責任、婚姻と養子の確かめ、鞍替えの通知、恩と忠誠、身代金と医療、兵糧の見聞（S09E）。
        Duties2,
        // NPCの城主の報告が届かなかったと知れば、同じ観測の報告を送り直す（S10）。
        CastellanResend,
        // 画面と文面の仕上げ（S11）：書状の帳面・自動の停止・人物帳・判断の比較・ルールのつまみ・史実の機会・軍団への命
        Screens2,
    ];
    public const string QuietNpcSocial="QuietNpcSocial";
    public const string DbOffices1560="DbOffices1560";
    public const string DbSkillHints="DbSkillHints";
    public const string DbLineageFaith="DbLineageFaith";
    public const string MortalSuccession="MortalSuccession";
    public const string HistoryEvents2="HistoryEvents2";
    public const string LifeGoals2="LifeGoals2";
    public const string CentralStanding="CentralStanding";
    public const string Warfare2="Warfare2";
    public const string Diplomacy2="Diplomacy2";
    public const string Territory2="Territory2";
    public const string Domestic2="Domestic2";
    public const string Logistics2="Logistics2";
    public const string Persons2="Persons2";
    public const string Armies2="Armies2";
    public const string Politics2="Politics2";
    public const string Cases2="Cases2";
    public const string Duties2="Duties2";
    public const string CastellanResend="CastellanResend";
    public const string Screens2="Screens2";
    // これまでに一覧に入ったことのある名前（保存の検証に使う）。名前を消さない。
    // 開始年の印（2026-11-17、S03）：1560年以外の開始の世界だけが一覧に持つ。印のない世界は1560年。
    public const string StartYear1467="StartYear1467",StartYear1600="StartYear1600";
    public static int StartYear(IReadOnlyCollection<string>? list)=>list==null?1560:list.Contains(StartYear1467)?1467:list.Contains(StartYear1600)?1600:1560;
    public static int StartYear(ContractWorld w)=>StartYear(w.NewGame?.NationalSystems);
    public static string[] ForYear(int year)=>year switch{1560=>[..Current],1467=>[..Current,StartYear1467],1600=>[..Current,StartYear1600],_=>throw new ArgumentOutOfRangeException(nameof(year))};
    static readonly HashSet<string> known=new([..Current,StartYear1467,StartYear1600],StringComparer.Ordinal);
    // 一覧ごとに一度だけ引ける形にする（判定は多くの場所で何度も呼ばれる）。
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<string[],HashSet<string>> sets=new();
    public static bool On(ContractWorld w,string name)=>w.NewGame?.NationalSystems is {} list&&sets.GetValue(list,l=>new HashSet<string>(l,StringComparer.Ordinal)).Contains(name);
    internal static void Validate(NewGameState data)
    {
        if(data.NationalSystems is not {} list)return;
        if(!NationalStart.IsNational(data.HistoricalCatalog)||list.Distinct(StringComparer.Ordinal).Count()!=list.Length||list.Any(n=>!known.Contains(n)))
            throw new InvalidDataException("全国の開始で有効にする仕組みの一覧が不正です");
    }
}
