using Godot;
using System;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    // 通常入口の配置確認をそのまま実行する。ユーザーのデータやOSには触れない。
    internal void VerifyPurposePreview()
    {
        if(TestKey!=null||NpcPolicyForEntry!=NewGameNpcRoutine.PurposePolicy)throw new InvalidOperationException("通常入口の方針ではありません");
        var next=new Button();var text=new Label();buttons["next"]=next;previewText=text;
        try{
            UpdatePreview();
            if(next.Disabled||preview==null||!preview.IsEligible())throw new InvalidOperationException("通常入口の配置確認に失敗: "+text.Text);
            GD.Print("NPC_PURPOSE_ENTRY_PREVIEW_PASS");
        }finally{buttons.Clear();next.Free();text.Free();}
    }
}
