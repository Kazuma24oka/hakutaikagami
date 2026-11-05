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
    internal (NewCampaignStore Store,NewCampaignProfile Profile) CreateJudgmentReview(string root)
    {
        if(TestKey!=null)throw new System.InvalidOperationException("通常の入口で検査してください");
        Conditions();
        var histories=GetNode<OptionButton>(page.GetPath()+"/HistoryMode");
        histories.Select((int)HistoryMode.Fictional);histories.EmitSignal(OptionButton.SignalName.ItemSelected,(long)HistoryMode.Fictional);
        var attention=GetNode<OptionButton>(page.GetPath()+"/NpcAttention");
        attention.Select((int)NpcAttention.Beginner);attention.EmitSignal(OptionButton.SignalName.ItemSelected,(long)NpcAttention.Beginner);
        chosen=NewGameWorld.Normalize([new("created:judgment-ui",null,"確認用の浪人",1520,new(60,50,70,40),StartTemplate.Ronin,"site:castle",null,null,HakutaiKagami.Core.Appearance.AppearanceCatalog.Default(1))]);
        var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);var setup=new WorldSetupRequest(chosen,[]);
        confirmed=NewGameSetup.Confirm(raw,setup,NewGameSetup.Preview(raw,setup));campaigns=new NewCampaignStore(root);
        return (campaigns,CreateSelectedCampaign("person:nobunaga"));
    }
}
