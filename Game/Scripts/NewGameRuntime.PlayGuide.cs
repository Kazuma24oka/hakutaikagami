using Godot;
using System;
public partial class NewGameRuntime
{
    AcceptDialog? playGuide;
    internal bool ReviewGuideCloseEnabled=>playGuide!=null&&!playGuide.GetOkButton().Disabled;
    internal bool ReviewGuideVisible=>playGuide?.Visible==true;
    internal void ReviewCloseGuide(){if(playGuide!.GetOkButton().Disabled)throw new InvalidOperationException("Actual guide close button is disabled");playGuide.GetOkButton().EmitSignal(Button.SignalName.Pressed);}
    void BuildPlayGuide(HFlowContainer toolbar)
    {
        Add(toolbar,"play-guide","操作の手引き",ShowPlayGuide);
    }
    void ShowPlayGuide()
    {
        // A view-only reference. Reading it issues no command, opens no letter and
        // never changes the authoritative clock or a hidden person's information.
        if(playGuide==null){
            playGuide=new(){Title="百代鏡 — 操作の手引き",Exclusive=false};AddChild(playGuide);playGuide.GetOkButton().Text="閉じる";
            var text=new RichTextLabel{BbcodeEnabled=true,FitContent=false,CustomMinimumSize=new(680,460),ScrollActive=true};playGuide.AddChild(text);
            text.Text="[b]一人の武将として判断する[/b]\n自分の移動と、部隊へ出す指示は別です。書状を送っただけでは、相手が受け取ったことも結果も確定しません。\n\n"+
                "[b]移動・仕事[/b]\n右の「移動・仕事」で行先を選び「この行先へ出発」。全国地図から拠点を選ぶこともできます。遠出には速度変更や「次の拠点まで」を使えます。現地に着いたら、点検や仕事の相談を選べます。\n\n"+
                "[b]軍務[/b]\n「軍務」で今できる判断を選び「軍務の判断を実行」。申出や準備の後は時間を進め、届いた条件を開封して確認します。許可を得ても現地で引き受けるまでは兵を動かせません。兵糧の携行、出陣、配置、帰還、返報、指揮返還を区別して進めます。\n\n"+
                "[b]書状・仕官[/b]\n「書状・仕官」で自分に届いた書状を選んで開きます。到着と開封は別です。重要な到着で時間が止まったら、内容を確認してから自動進行を再開できます。\n\n"+
                "[b]本人と部隊[/b]\n部隊と別行動する場合も、本人の移動だけで部隊は動きません。確認した部隊の記録地点への移動、照会、部隊への指示は、それぞれ別の操作です。古い情報を現在地と決めつけないようにします。\n\n"+
                "[b]保存と再開[/b]\n上の「保存」で同じ人物と世界を保存します。ロードはスタート画面の一覧からも行えます。保存中は完了表示を待ってください。";
        }
        playGuide.PopupCentered(new Vector2I(740,540));
    }
}
