using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
// 遊び方の動画（2026-11-18）：--how-to-play-film=<置き場> で起動すると、スタート画面から全国の開始を選び、遊ぶ画面の主な操作を順に見せる。
// Godot の --write-movie で、ゲームが自分で描いた画面をそのまま動画にする。保存は置き場の中だけに作る（本物の保存と混ぜない）。
public partial class NewGameFrontEnd
{
    static string? FilmRoot=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--how-to-play-film="))?.Split('=',2)[1];
    Label filmCaption=null!;
    async Task FilmHold(double seconds){for(int n=0;n<(int)Math.Ceiling(seconds*30);n++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    async Task Say(string text,double seconds){filmCaption.Text=text;await FilmHold(seconds);}
    async void RunHowToPlayFilm()
    {
        try
        {
            var layer=new CanvasLayer{Layer=100};AddChild(layer);
            var band=new PanelContainer{AnchorLeft=0,AnchorRight=1,AnchorTop=1,AnchorBottom=1,OffsetTop=-74,OffsetBottom=-8,OffsetLeft=40,OffsetRight=-40,MouseFilter=MouseFilterEnum.Ignore};
            band.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=new Color(0,0,0,.78f),CornerRadiusTopLeft=8,CornerRadiusTopRight=8,CornerRadiusBottomLeft=8,CornerRadiusBottomRight=8,ContentMarginLeft=18,ContentMarginRight=18,ContentMarginTop=8,ContentMarginBottom=8});
            filmCaption=new Label{HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,AutowrapMode=TextServer.AutowrapMode.WordSmart};filmCaption.AddThemeFontSizeOverride("font_size",24);filmCaption.AddThemeColorOverride("font_color",new Color("f2dfb4"));
            band.AddChild(filmCaption);layer.AddChild(band);

            await Say("百代鏡の遊び方　—　スタート画面",3);
            await Say("「新規ゲーム」から始めます",2.5);Press("new");await FilmHold(1);
            var scenario=scenarioChoice!;scenario.Select(1);scenario.EmitSignal(OptionButton.SignalName.ItemSelected,1L);
            await Say("開始の条件：「全国」を選び、開始年（1467・1560・1600年）を選びます",4);
            await Say("歴史：史実尊重・準史実・仮想。史実尊重ほど史実の事件が起きやすくなります",4);
            await Say("声のかかりやすさ・手強さ・判断支援は、難易度のコースでまとめて選べます",4);
            var course=page.GetNode<OptionButton>("RuleCourse");course.Select(1);course.EmitSignal(OptionButton.SignalName.ItemSelected,1L);
            await Say("ルール：コースと5つのつまみ（史実の事件・脅威・捕縛・戦傷・出生）",4.5);
            Press("next");await FilmHold(2);
            await Say("全国の地図と武将の一覧から、操作する武将を選びます",3.5);
            Press("all-people");await FilmHold(1);
            await Say("武将名で探せます",2);search.Text="織田信長";search.EmitSignal(LineEdit.SignalName.TextChanged,"織田信長");await FilmHold(1);
            int index=Array.FindIndex(visiblePeople,p=>p.Name=="織田信長");if(index<0)throw new InvalidOperationException("信長が一覧にいません");
            personRows.Select(index);personRows.EmitSignal(ItemList.SignalName.ItemSelected,(long)index);personRows.EnsureCurrentIsVisible();
            await Say("ここでは織田信長（当主）を選びます。浪人や家臣を選んで始めることもできます",4.5);
            await Say("「この武将で始める」",2);Press("begin");
            for(int n=0;n<600&&(game==null||!game.Visible);n++)await FilmHold(1d/30);
            var g=game??throw new InvalidOperationException("ゲームが始まりません");await FilmHold(1);
            await Say("遊ぶ画面：上の欄で時間を進める・止める・保存する。真ん中が今いる場所",4.5);
            await Say("世界はゲーム内の時計で動き、書状は使者が運んで届きます。知らないことは見えません",4.5);
            if(g.ReviewHasButton("nation")){g.Press("nation");await Say("「全国地図」：全国の勢力と城。もう一度押すと元の視点へ",4);g.Press("near");await FilmHold(1);}
            await Say("右の「移動」で行き先を選び「行先へ出発」。速さを上げると、歩いて進む様子が見えます",4);
            if(g.ReviewDepartNearest()){for(int n=0;n<300&&g.IsDeparting;n++)await FilmHold(1d/30);g.ReviewSelectSpeed(360);g.Press("auto");await Say("360倍で進めています。近くの城や町は名前つきで見えます",6);g.Press("pause");for(int n=0;n<300&&g.IsClockControlling;n++)await FilmHold(1d/30);}
            await Say("「帳面」：書状・人物・判断・約束・家督・軍勢・表示の7つの面",3.5);
            string[] tabs=["書状：届いた書状と出した書状。種類で絞り込めます","人物：5能力と6分野32技能、療養と教育","判断：知っている危機ごとに、対処案を比べます","約束：家の約束と、自分の約束","家督：当主・後継の候補・元服の年。後継の指名と隠居","軍勢：家の軍勢。当主は国と率いる人と優先度を選んで出陣を命じます","表示：文字の大きさ"];
            for(int n=0;n<tabs.Length;n++){g.ShowLedger(n);await Say(tabs[n],3.5);}
            g.ReviewCloseWindows();await FilmHold(.5);
            if(g.ReviewHasButton("time-settings")){g.Press("time-settings");await Say("「時間の設定」：書状・敵の接近・訃報・重い病状などで、止まるか遅くなるかを選びます",4.5);g.ReviewCloseWindows();}
            if(g.ReviewHasButton("auto")){g.Press("auto");await Say("「自動進行」で時間が流れます。速さは横の欄で選べます",4);if(g.ReviewHasButton("pause"))g.Press("pause");await FilmHold(1);}
            if(g.ReviewHasButton("work")){g.Press("work");await Say("会話：「現地で仕事を相談」。Enter か Space で返事の選択へ、Esc で閉じます",4.5);g.ReviewTalkKey(Key.Enter);await FilmHold(2.5);g.ReviewTalkKey(Key.Escape);await FilmHold(1);}
            await Say("下の「会う・書く・任せる・動く」から、人に会う・書状を書く・仕事を任せる・移動するなどの操作を選びます",5);
            if(g.ReviewHasButton("chronicle")){g.Press("chronicle");await Say("「年代記」：自分が知った出来事の記録",3.5);g.ReviewCloseWindows();}
            if(g.ReviewHasButton("save")){g.Press("save");await Say("「保存」：同じ人物・同じ世界の続きから再開できます",3.5);}
            await Say("以上が基本の遊び方です。百代鏡",3);
            GD.Print("HOW_TO_PLAY_FILM_DONE");QuitGracefully();
        }
        catch(Exception e){GD.PrintErr("HOW_TO_PLAY_FILM_FAILED "+e);QuitGracefully(1);}
    }
}
