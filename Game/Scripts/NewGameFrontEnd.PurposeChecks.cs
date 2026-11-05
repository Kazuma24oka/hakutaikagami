using Godot;
using System;
using System.IO;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    // 通常入口の配置確認をそのまま実行する。ユーザーのデータやOSには触れない。
    internal void VerifyPurposePreview()
    {
        if(TestKey!=null||NpcPolicyForEntry!=NpcJudgment.Policy)throw new InvalidOperationException("通常入口の方針ではありません");
        var next=new Button();var text=new Label();buttons["next"]=next;previewText=text;
        try{
            UpdatePreview();
            if(next.Disabled||preview==null||!preview.IsEligible())throw new InvalidOperationException("通常入口の配置確認に失敗: "+text.Text);
            confirmed=NewGameSetup.Confirm(NewGameWorld.Scenario(HistoricalStartCatalog.Current),Request(),preview);
            campaigns=new NewCampaignStore(Path.Combine(Path.GetTempPath(),"npc-purpose-entry-"+Guid.NewGuid().ToString("N")));
            var first=confirmed.People.First(p=>NewGameWorld.CanPlay(confirmed,p));
            var profile=CreateSelectedCampaign(first.Id);var world=NewCampaignStore.Build(profile);
            if(world.NewGame?.NpcRoutinePolicy!=NpcJudgment.Policy||!CharacterDevelopment.Enabled(world))throw new InvalidOperationException("開始データの方針が一致しません");
            var session=new WorldSession(world);session.SetPaused(false);session.AdvanceTo(20);
            if(session.DiagnosticSnapshot().NpcWork!.Decisions.Length==0)throw new InvalidOperationException("通常開始でNPCが判断していません");
            GD.Print("NPC_PURPOSE_ENTRY_PREVIEW_PASS");
            GD.Print("通常開始の実データ作成・NPC判断を確認: "+session.Seconds+"秒");
        }finally{buttons.Clear();next.Free();text.Free();}
    }
}
