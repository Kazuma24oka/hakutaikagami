using Godot;
using System.IO;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
    bool guideReviewed;
    async Task ReviewPlayGuide()
    {
        guideReviewed=true;
        string before=ContractJson.CanonicalHash(game!.Session.DiagnosticSnapshot());
        string pose=game.Pose();
        for(int pass=0;pass<2;pass++){
            game.Press("play-guide");await Frame();await Frame();
            Check(game.ReviewGuideVisible,"actual guide opens without issuing an intent");
            if(pass==0)ViewportCapture.Save(GetViewport(),Path.Combine(TestOutput,"play-guide.png"));
            game.ReviewCloseGuide();await Frame();await Frame();
            Check(!game.ReviewGuideVisible,"actual close button closes guide");
            Check(before==ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot())&&pose==game.Pose(),"guide open/read/close preserves exact world and presentation");
        }
        File.WriteAllText(Path.Combine(TestOutput,"play-guide-world.sha256"),before);
    }
}
