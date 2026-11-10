using System.Linq;
using Godot;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    void AddJudgmentConditions()
    {
        page.AddChild(new Label{Text="歴史への寄せ方"});
        var history=new OptionButton{Name="HistoryMode"};
        foreach(string label in new[]{"史実尊重：史実に沿う好みを強める","準史実：史実の好みと現在の事情を併用","仮想：史実への加点を使わない"})history.AddItem(label);
        history.Select((int)historyMode);history.ItemSelected+=index=>historyMode=(HistoryMode)index;page.AddChild(history);
        page.AddChild(new Label{Text="あなたへの仕事や誘い"});
        var attention=new OptionButton{Name="NpcAttention"};
        foreach(string label in new[]{"入門：声をかけられやすい","標準：少し声をかけられやすい","対等：ほかの人物と同じ扱い"})attention.AddItem(label);
        attention.Select((int)npcAttention);attention.ItemSelected+=index=>npcAttention=(NpcAttention)index;page.AddChild(attention);
        page.AddChild(new Label{Text="本人が知っている事情と実行できる範囲で判断します。\n依頼は断れます。報酬や権限が増える設定ではありません。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
    }
    internal (NewCampaignStore Store,NewCampaignProfile Profile) CreateJudgmentReview(string root,bool owari=false)
    {
        if(TestKey!=null)throw new System.InvalidOperationException("通常の入口で検査してください");
        Conditions();
        var histories=GetNode<OptionButton>(page.GetPath()+"/HistoryMode");
        var mode=owari?HistoryMode.Historical:HistoryMode.Fictional;
        histories.Select((int)mode);histories.EmitSignal(OptionButton.SignalName.ItemSelected,(long)mode);
        var attention=GetNode<OptionButton>(page.GetPath()+"/NpcAttention");
        attention.Select((int)NpcAttention.Beginner);attention.EmitSignal(OptionButton.SignalName.ItemSelected,(long)NpcAttention.Beginner);
        chosen=NewGameWorld.Normalize([new("created:judgment-ui",null,"確認用の浪人",1520,new(60,50,70,40),StartTemplate.Ronin,"site:castle",null,null,HakutaiKagami.Core.Appearance.AppearanceCatalog.Default(1))]);
        var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);var setup=new WorldSetupRequest(chosen,[]);
        confirmed=NewGameSetup.Confirm(raw,setup,NewGameSetup.Preview(raw,setup));campaigns=new NewCampaignStore(root);
        return (campaigns,CreateSelectedCampaign(owari?"created:judgment-ui":"person:nobunaga"));
    }
    internal async System.Threading.Tasks.Task CheckImagawaStart(string root, bool fromMap)
    {
        campaigns = new NewCampaignStore(root);
        var raw = NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        var request = new WorldSetupRequest([], []);
        confirmed = NewGameSetup.Confirm(raw, request, NewGameSetup.Preview(raw, request));
        ChoosePerson();
        if (fromMap) map!.Pick("site:sunpu");
        else Press("all-people");
        foreach (var name in new[] { "person:imagawa-yoshimoto", "person:imagawa-ujizane" })
            Check(System.Array.Exists(visiblePeople, p => p.Id == name), "選択候補に父子が揃う");
        var id = fromMap ? "person:imagawa-ujizane" : "person:imagawa-yoshimoto";
        var index = System.Array.FindIndex(visiblePeople, p => p.Id == id);
        Check(index >= 0, "今川家の人物を選択可能");
        personRows.Select(index);
        personRows.EmitSignal(ItemList.SignalName.ItemSelected, (long)index);
        Check(personText.Text.Contains(fromMap ? "後継者" : "当主"), "開始時の立場の表示");
        Press("begin");
        await Frame();
        await WaitSave();
        game!.Advance(5);
        var council = game.Session;
        var peace = council.DiagnosticSnapshot();
        Check(!FactionStances.Truth(peace, council.Seconds).Any() && NpcArmies.Musters(peace).Length == 0,
            "判断を見送った間は未開戦・未出陣");
        var stores = MilitaryEconomy.LocalCampaignStores(peace, new("org:imagawa"));
        Check(fromMap ? stores == null : stores is { FreeRiceKg: >= 0, FreeWorkers: >= 0 },
            "義元のみ現地の動員原資を確認");
        var decision = new FactionStanceInput(new("decision:imagawa-start-check"),
            FactionStanceAction.DeclareHostile, new("org:imagawa"), new("org:oda"), council.Seconds,
            council.Seconds + 30L * 86400);
        var reply = council.ProclaimFactionStance(new(decision, new(new("request:imagawa-start-notice"), decision.Id)),
            council.PlayerControl, council.Revision);
        Check(reply.Code == (fromMap ? ResultCode.NotAvailable : ResultCode.Accepted), "現当主だけが決裁：" + reply.VisibleReason);
        var decided = council.DiagnosticSnapshot();
        Check(fromMap ? ContractJson.CanonicalHash(peace) == ContractJson.CanonicalHash(decided) :
            decided.FactionNotices!.Requests.Any(n=>n.Input.Original==decision.Id) &&
            !FactionStances.Known(decided,new("person:nobunaga")).Any(), "判断結果と未達通知の認識");
        if (!fromMap)
        {
            void Accepted(CommandResult result) => Check(result.Code == ResultCode.Accepted, result.VisibleReason);
            void Military(NewMilitaryAction action, long amount = 0) =>
                Accepted(council.DecideNewMilitary(new(new("request:imagawa-duty/" + council.Revision), action, amount),
                    council.PlayerControl, council.Revision));
            void ReadMail()
            {
                foreach (var mail in council.GetActorView(council.PlayerControl).Messages.Where(m => m.KnownBody == null))
                    Accepted(council.Open(council.PlayerControl, mail.Id, council.Revision));
            }

            Accepted(council.DecideNewMilitary(new(new("request:imagawa-duty"), NewMilitaryAction.Request) {
                AuthorityPolicy = NewGameMilitary.AuthorityContract, LedgerPolicy = NewGameMilitary.LedgerIdentityContract
            }, council.PlayerControl, council.Revision));
            game.Advance(council.Seconds + 2);
            ReadMail();
            Military(NewMilitaryAction.Accept);
            game.Advance(council.Seconds + 2);
            ReadMail();
            Military(NewMilitaryAction.Pickup);
            Accepted(council.ManageMilitaryRice(new(new("request:imagawa-food"), council.MilitaryEconomyView()!.Depot,
                MilitaryEconomy.NpcRiceKg), council.PlayerControl, council.Revision));
            Military(NewMilitaryAction.Load, NewGameMilitary.Capacity);
            Accepted(council.ManageArmyRoad(new(new("request:imagawa-road"), ArmyRoadAction.March, new("site:kakegawa")),
                council.PlayerControl, council.Revision));
            game.Advance(council.Seconds + 4);
            Check(council.ArmyRoadView()!.Marching, "掛川への街道を行軍中");
        }
        game.ReviewFraction(0);
        game.Press("save");
        await WaitSave();
        var world = game.Session.DiagnosticSnapshot();
        Check(world.PlayerControl.Value == id && NewGameWorld.InitialNode(world, world.PlayerControl) == "sunpu", "駿府で本人を操作");
        var pose = game.Pose();
        game.Press("load");
        await Frame();
        game.ReviewFraction(0);
        Check(ContractJson.CanonicalHash(world) == ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()) &&
            pose == game.Pose(), "復旧後の世界と3D配置が一致");
        if (!fromMap)
        {
            var end = world.Branches!.Journeys.Last().Legs[^1].EndAt;
            game.Advance(end + 2);
            var session = game.Session;
            var market = session.ArmyRoadView()!;
            Check(!market.Marching && market.Assembly?.Value == "site:kakegawa", "同じ部隊が掛川に集合");
            Check(market.LocalRicePrice == null && market.MaxPurchaseKg == 0, "市場のない掛川では購入不可");
            var arrival = session.DiagnosticSnapshot();
            var denied = session.ManageArmyRoad(new(new("request:kakegawa-no-market"), ArmyRoadAction.BuyRice, RiceKg: 1),
                session.PlayerControl, session.Revision);
            Check(denied.Code == ResultCode.NotAvailable &&
                ContractJson.CanonicalHash(arrival) == ContractJson.CanonicalHash(session.DiagnosticSnapshot()),
                "市場のない掛川では購入を拒否し在庫・所持金を保つ");
            var alternative = new WorldSession(ContractJson.Copy(arrival));
            var forward = alternative.ManageArmyRoad(new(new("request:kakegawa-forward"), ArmyRoadAction.March,
                new("site:hamamatsu")), alternative.PlayerControl, alternative.Revision);
            Check(forward.Code == ResultCode.Accepted, forward.VisibleReason);
            var returning = session.ManageArmyRoad(new(new("request:kakegawa-return"), ArmyRoadAction.March,
                new("site:sunpu")), session.PlayerControl, session.Revision);
            Check(returning.Code == ResultCode.Accepted, returning.VisibleReason);
            game.Advance(session.Seconds + 4);
            game.Advance(session.DiagnosticSnapshot().Branches!.Journeys.Last().Legs[^1].EndAt + 2);
            market = session.ArmyRoadView()!;
            Check(market.Assembly?.Value == "site:sunpu", "駿府への帰還");
            Check(market.MaxPurchaseKg > 0, "現地の積載余力と財布で兵糧を買える");
            var buy = session.ManageArmyRoad(new(new("request:kakegawa-food"), ArmyRoadAction.BuyRice, RiceKg: 1),
                session.PlayerControl, session.Revision);
            Check(buy.Code == ResultCode.Accepted, buy.VisibleReason);
            var after = session.ArmyRoadView()!;
            Check(after.PrivateMoney == market.PrivateMoney - market.LocalRicePrice &&
                after.CarryKg == market.CarryKg + 1 && after.LocalFreeRice == market.LocalFreeRice - 1,
                "私財・市場在庫・携行米が同じ売買で変化");
            game.ReviewFraction(0);
            game.Press("save");
            await WaitSave();
            var purchased = ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot());
            var returnedPose = game.Pose();
            game.Press("load");
            await Frame();
            game.ReviewFraction(0);
            Check(game.Session.ArmyRoadView() == after && returnedPose == game.Pose() &&
                ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()) == purchased,
                "帰還後の購入を実保存から復旧");
        }
    }
}
