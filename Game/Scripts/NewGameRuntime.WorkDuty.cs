using Godot;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void BuildWorkDuty(VBoxContainer stack)=>Add(stack,"work-duty","主君に役目を相談",ShowCurrentWorkDuty);
    void ShowCurrentWorkDuty()
    {
        if(WorkDuty.Candidate(Snapshot()) is {} p)ShowWorkDutyOffer(p);
        else if(WorkDuty.Active(Snapshot()) is {} a){PresentWork("点検の担当","引き続き、この拠点の陸路点検を頼む。報告を確認したら一件20文を渡そう。","対象：この主君・この拠点の新しい点検命令\n手当：完了報告の確認後20文。私財の立替なし。",a.Principal,a.Principal,"担当確認","次の行動");Choice("work-duty-back","地図へ戻る",CloseBottomTalk);}
        else throw new System.IO.InvalidDataException("主君の点検評価を確認し、現地で面会してください");
    }
    void ShowWorkDutyOffer(EntityId principal)
    {
        shownWork=null;PresentWork("陸路点検の担当の打診","点検の働きは認めている。この拠点の陸路点検を担当してくれぬか。今後の点検には一件20文を付けよう。","役目：この主君・この拠点の陸路点検\n手当：引受後の点検命令に一件20文、完了報告の確認後に現地で受取。辞去で担当終了。",principal,principal,"条件の提示","引き受けますか");
        Choice("work-duty-accept","この条件で担当を引き受けよう",()=>{Submit(Session.AcceptWorkDuty(new("duty:ui/"+Session.Revision),principal,Session.PlayerControl,Session.Revision));CloseBottomTalk();});
        Choice("work-duty-later","今は引き受けず地図へ戻る",CloseBottomTalk);
    }
}
