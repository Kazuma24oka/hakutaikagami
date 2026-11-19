using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Proto;

// 試作版の動画「謀叛から合戦まで」（2026-11-19 ユーザー「謀叛から戦争まで動画でとって」）。
// --proto-film=<置き場> で起動すると、台本どおりに遊んで見せる。Godot の --write-movie で、ゲームが自分で描いた画面をそのまま動画にする。
// 保存は HAKUTAI_PROTO_SAVE で別の置き場を使う（遊ぶ人の保存に触らない）。
public partial class ProtoGame
{
    internal static string? FilmRoot => OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--proto-film="))?.Split('=', 2)[1];
    Label filmCaption = null!;
    async Task Hold(double seconds) { for (int n = 0; n < (int)Math.Ceiling(seconds * 30); n++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    async Task Cap(string text, double seconds) { filmCaption.Text = text; await Hold(seconds * 0.75); } // 動画を短く（2026-11-19 ユーザー「動画時間長すぎる」）
    async Task Until(Func<bool> done, double limitSeconds) { for (int n = 0; n < limitSeconds * 30 && !done(); n++) await Hold(1d / 30); }
    async Task Talk(double perLine = 2.2) { while (Scene.Open && !Scene.Choosing) { await Hold(perLine); if (!Scene.Choosing) Scene.Next(); } }
    void Follow(float dist, float pitch = 55) => SetCamera(dist, null, pitch);
    // 一気に飛ばすのは知らせで止まるので、目的の時刻に本当に着くまで飛ばし直す。
    async Task SkipTo(long t) { for (int n = 0; n < 200 && World.Second < t; n++) { Skip(t); await Until(() => skipUntil == 0, 20); } }
    async Task RunTo(long t, int speed) { Speed = speed; WaitUntil = t; await Until(() => World.Second >= t, 900); }

    async void RunFilm()
    {
        try
        {
            var layer = new CanvasLayer { Layer = 100 }; AddChild(layer);
            var band = new PanelContainer { AnchorLeft = 0, AnchorRight = 1, AnchorTop = 0, AnchorBottom = 0, OffsetTop = 96, OffsetBottom = 160, OffsetLeft = 200, OffsetRight = -200, MouseFilter = Control.MouseFilterEnum.Ignore };
            band.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.78f), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, ContentMarginLeft = 16, ContentMarginRight = 16 });
            filmCaption = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            filmCaption.AddThemeFontSizeOverride("font_size", 24); band.AddChild(filmCaption); layer.AddChild(band);
            var w = World; long Day = ProtoWorld.Day, H = ProtoWorld.Hour;

            // 2日目の終わりまでは撮らずに進める（謀叛の前の日々は別の動画で）。
            w.Advance(2 * Day + 4 * H + 50 * 60 - w.Second);
            Follow(420); await Cap("百代鏡　試作版：謀叛から合戦まで", 3);
            await Cap("3日目の明け方、鷹野城。近習の佐伯源三郎（自分）は城に詰めている", 3.5);
            await RunTo(2 * Day + 5 * H + 2 * 60, 60);
            await Cap("当主の鷹野義正が病で亡くなり、嫡男の正之が家を継いだ", 3.5);
            var notice = w.OffersHere.First(t => t.Kind == "death-notice");
            Act("accept:" + notice.Id); await Talk();
            await Cap("訃報を、岩切城の城主・岩切勝元へ届けに行く", 3);
            Speed = 300; Follow(700); w.GoToTask(notice.Id); ShowGoal();
            await Until(() => !w.Player.Moving, 40);
            Act("step:" + notice.Id); await Talk();
            await Cap("ここで「説き伏せる」と、勝元の忠誠が上がって謀叛を防げることがある。今回はあえて説かずに城へ戻る", 5);
            w.GoToTask(notice.Id); ShowGoal(); await Until(() => !w.Player.Moving, 40);
            Act("step:" + notice.Id); await Talk();

            await Cap("その頃、沢渡家の使番が、勝元を誘う書状を岩切城へ運んでいた……", 3.5);
            Speed = 500;
            for (int n = 0; n < 400 && !w.CouncilOpen && w.Second < 3 * Day; n++) { await SkipTo(w.Second + 10 * 60); }
            await Cap("岩切勝元から手切れの書状が届いた。謀叛である。城の広間で軍議が開かれる", 4);
            OpenCouncil(); await Talk(2.8);
            await Cap("自分の陣は南の丘。今夜のうちに出て、丘を先に取る。合図は卯の刻（6時）の狼煙", 4.5);
            if (councilChoices is { } c)
            {
                c.Pos.Select(0); c.Dep.Select(0); c.Sig.Select(0); await Hold(1.5);
                c.Ok.EmitSignal(BaseButton.SignalName.Pressed);
            }
            await Talk();

            await Cap("兵30を預かり、夜のうちに南の丘へ向かう。足軽は兵10人につき1人の姿で、後ろについてくる", 4.5);
            var muster = w.OpenTasks.First(t => t.Kind == "muster");
            Speed = 300; Follow(900); w.GoToTask(muster.Id); ShowGoal();
            await Until(() => !w.Player.Moving, 40);
            await Cap("南の丘に陣を取った。小坂重忠の隊も丘に、正之の隊は道の口へ", 3.5);
            await SkipTo(3 * Day + 3 * H + 50 * 60);
            Follow(1500, 60);
            await Cap("4日目の夜明け前。岩切勢が城から出てくる。敵は見える範囲に入ったときだけ地図に出る", 4);
            await RunTo(3 * Day + 4 * H + 20 * 60, 60);
            if (w.Facts.Any(f => f.Id.StartsWith("taken-by-takano")))
                await Cap("丘を先に押さえられた岩切勢は、手前で止まった", 3.5);
            await SkipTo(3 * Day + 5 * H + 58 * 60);
            Follow(1300, 60);
            await Cap("卯の刻。正之の陣から合図の狼煙が上がる。上に両軍の総士気が出る", 3.5);
            await RunTo(3 * Day + 6 * H + 3 * 60, 30);
            await Cap("要所（丘・道の口・森の縁）を持つ側は総士気が上がる。隊が崩れると下がり、0で総崩れ", 4.5);
            // 本人も丘から目の前の敵へ攻めかかる。ぶつかると押し合いのゲージと采配が出る。
            var foeId = w.Actions().Where(a => a.Id.StartsWith("attack:")).Select(a => a.Id).FirstOrDefault();
            if (foeId != null)
            {
                Act(foeId); Follow(700, 55); WaitUntil = 3 * Day + 14 * H;
                await Cap("丘を下りて、目の前の岩切勝元の隊へ攻めかかる", 3);
                Speed = 30; await Until(() => w.ClashOf(w.Player.Id) != null || w.War.Result != "", 60);
                if (w.ClashOf(w.Player.Id) != null)
                {
                    await Cap("ぶつかった。間のゲージは押し合い。右下に采配が出る（遊ぶときは10倍に落ちる。動画では早回し）", 4.5);
                    await Cap("「かかれ！」は気力を使って一気に押し返す", 2.5);
                    if (w.CanKakare) Buttons["clash-kakare"].EmitSignal(BaseButton.SignalName.Pressed);
                    int seenF = w.Facts.Count; bool usedTactic = false, guarded = false;
                    for (int n = 0; n < 30 * 90 && w.Second < 3 * Day + 6 * H + 50 * 60 && !w.War.Routed.Contains(w.Player.Id) && w.War.Result == ""; n++)
                    {
                        await Hold(1d / 30); if (Speed != 60) Speed = 60;
                        if (w.Facts.Count > seenF) { seenF = w.Facts.Count; filmCaption.Text = w.Facts[^1].Text; }
                        if (!usedTactic && w.GaugeOf(w.Player.Id) >= 100) { usedTactic = true; filmCaption.Text = "戦法のゲージがたまった。戦法「鼓舞」で士気を立て直す"; Buttons["clash-tactic"].EmitSignal(BaseButton.SignalName.Pressed); await Hold(2.5); }
                        else if (!guarded && w.ClashOf(w.Player.Id) is { Push: < -50 }) { guarded = true; filmCaption.Text = "押し込まれている。構えを「防御」に変えて耐える"; Buttons["clash-stance-防御"].EmitSignal(BaseButton.SignalName.Pressed); await Hold(2.5); }
                        else if (w.CanKakare && n % 60 == 0) { filmCaption.Text = "かかれ！"; Buttons["clash-kakare"].EmitSignal(BaseButton.SignalName.Pressed); }
                    }
                }
            }
            if (w.War.Routed.Contains(w.Player.Id)) await Cap("自分の隊は崩れた。本陣まで退けば一度だけ立て直せる", 3.5);
            Follow(1600, 62); Speed = 300; WaitUntil = 3 * Day + 15 * H;
            await Cap("あとは全体の成り行き。崩れた隊は本陣で立て直して戻ってくる", 3.5);
            int seen = w.Facts.Count;
            for (int n = 0; n < 30 * 240 && w.War.Result == ""; n++)
            {
                await Hold(1d / 30);
                if (w.Facts.Count > seen) { var f = w.Facts[^1]; seen = w.Facts.Count; filmCaption.Text = f.Text; }
            }
            await Hold(2);
            await Cap(w.War.Result switch { "鎮めた" => "岩切勝元の謀叛は鎮まった", "退けられた" => "鷹野の討手は岩切勢に退けられた", "引き分け" => "日が傾き、両軍とも兵を引いた", _ => "合戦はまだ続いている" }, 4);
            await Cap("百代鏡　試作版", 2);
        }
        catch (Exception e) { GD.PrintErr("PROTO FILM 失敗: " + e); }
        GetTree().Quit();
    }
}
