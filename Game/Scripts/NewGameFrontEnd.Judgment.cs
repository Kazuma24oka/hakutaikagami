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
    }
}
