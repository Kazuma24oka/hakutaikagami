using System;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;

// 全国1560年の開始の画面の検査（2026-11-12、v110）：スタート→新規ゲーム→全国→織田信長→始める→近くの城へ出発。
public partial class NewGameFrontEnd
{
    // 検査の保存はユーザーの保存先ではなく、検査の出力先に作る。
    internal void ReviewUseRoot(string root)=>campaigns=new(System.IO.Path.Combine(root,"campaigns"));
    internal NewGameRuntime? ReviewGame=>game;
    internal async Task<string> ReviewNationalStart(Func<Task> frame)
    {
        ShowMenu();await frame();Press("new");await frame();scenarioChoice!.Select(1);Press("next");await frame();
        Check(national&&confirmed!=null&&confirmed.People.Length>3000,"全国の武将の一覧");
        var head=confirmed!.Factions.Select(f=>f.Head).Single(h=>confirmed.People.Single(p=>p.Id==h).Name.Contains("信長"));
        PickRow(head);Press("begin");for(int i=0;i<10;i++)await frame();
        Check(game!=null&&WorldNetwork.Castle(game.Session.DiagnosticSnapshot())&&game.Session.PlayerControl.Value==head,"全国の世界で信長として始まる");
        var w=game!.Session.DiagnosticSnapshot();var start=NewGameWorld.InitialNode(w,game.Session.PlayerControl);
        var goal=BranchRoutes.GraphEdges(w).Where(e=>e.From==start||e.To==start).Select(e=>e.From==start?e.To:e.From).Where(n=>n.StartsWith("c-",StringComparison.Ordinal)).OrderBy(n=>n,StringComparer.Ordinal).First();
        game.SetDestination(goal);game.Press("depart");for(int i=0;i<4000&&(game.IsDeparting||game.DepartureWorkerPending);i++)await frame();
        Check(game.LastDepartureError==null&&game.LastDepartureResult is ResultCode.Accepted or ResultCode.AlreadyAccepted,"出発が受け付けられる");
        return goal;
    }
}
