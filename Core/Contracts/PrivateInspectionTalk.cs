namespace HakutaiKagami.Core.Contracts;
// A disclosed reply to an actual local consultation, never an NPC's hidden decision inputs.
public sealed record PrivateInspectionReply(long Revision,bool CanConfirm,long Fee,string Speech);
public static partial class PersonalCompanions
{
    internal static PrivateInspectionReply ConsultInspection(ContractWorld w,EntityId person,EntityId actor,long revision)
    {
        PrivateInspectionReply No(string text)=>new(w.CommitSequence,false,DailyPay,text);
        if(actor!=w.PlayerControl||revision!=w.CommitSequence)return No("状況が変わりました。もう一度相談してください。");
        if(!SingleInspectionEnabled(w)||person==actor||!w.Characters.Any(c=>c.Id==person))return No("この相手には、この用件を相談できません。");
        var a=w.Locations.Single(l=>l.Actor==actor);var b=w.Locations.Single(l=>l.Actor==person);
        if(NewGameMilitary.Deployed(w)||a.Kind!=LocationKind.AtSite||b.Kind!=LocationKind.AtSite||a.Site==null||a.Site!=b.Site)return No("本人に現地で会って相談してください。");
        if(SiteAt(w,actor,w.ElapsedSeconds,w.CommitSequence+1)==null)return No("先にご自身の用件を終えてから相談してください。");
        if(!CommissionContact(w,person,w.ElapsedSeconds,w.CommitSequence+1))return No("まず、先の情報交換を精算してから話をしましょう。");
        var prior=Orders(w).Where(q=>Before(q,w.ElapsedSeconds,w.CommitSequence+1)).ToArray();
        if(Active(prior,person))return No("継続中の協力があります。その契約の点検依頼として話してください。");
        if(Owed(w,person,w.ElapsedSeconds,w.CommitSequence+1)>0)return No("先に、未払いの手当を精算してもらいたい。");
        if(w.Characters.Single(c=>c.Id==person).Life!=LifeState.Adult||w.Offices.Any(o=>o.Subject==person&&o.From<=w.ElapsedSeconds&&w.ElapsedSeconds<o.Until))return No("今の役目では、この私的な用件は引き受けられません。");
        if(SiteAt(w,person,w.ElapsedSeconds,w.CommitSequence+1)==null||Occupied(w,person,w.ElapsedSeconds,w.CommitSequence)||NpcArmies.CommanderBusyAt(w,person,w.ElapsedSeconds,w.CommitSequence))return No("今は手が離せません。先の用件が終わってから相談してください。");
        if(!w.Characters.Single(c=>c.Id==person).WillObey)return No("今回は引き受けられません。");
        if(TransportWork.WalletBefore(w,actor,w.ElapsedSeconds,w.CommitSequence+1)<DailyPay)return No("点検一件には20文が必要です。用意できてから相談してください。");
        if(!CanCommission(w,person,w.ElapsedSeconds,w.CommitSequence+1))return No("今は引き受けられません。後で改めて相談してください。");
        return new(w.CommitSequence,true,DailyPay,"ここの点検を一件だけなら、20文で引き受けましょう。今の所属を離れる話ではありません。");
    }
}
public sealed partial class WorldSession
{
    public PrivateInspectionReply ConsultPrivateInspection(EntityId person,EntityId actor,long revision){lock(gate)return PersonalCompanions.ConsultInspection(state,person,actor,revision);}
}
