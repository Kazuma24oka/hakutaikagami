namespace HakutaiKagami.Core.Contracts;

public static class IndependenceGate
{
    public static bool Handles(CourtAction action)=>action is CourtAction.RequestIndependentGate or CourtAction.AllowIndependentGate
        or CourtAction.RefuseIndependentGate or CourtAction.ReceiveIndependentGate or CourtAction.ReceiveIndependentKeep;
    static bool At(ContractWorld w,EntityId actor,IndependenceDeclaration d,long at,long rev)=>
        !Assassination.DeadAt(w,actor,at,rev) && TransportWork.At(w,actor,d.Site,at,rev,true);
    internal static void Check(ContractWorld w,CourtInput i,EntityId actor,EntityId site,long at,long rev)
    {
        var d=w.Declarations?.SingleOrDefault(d=>d.CheckGarrison==true && d.Pact==i.Reference && d.Revision<rev);
        if(d==null || i.Site!=d.Site || site!=d.Site || i.Owner!=d.FormerHouse || !JointIndependence.CommandedCastle(w,d.Founder,d.Site,d.At,d.Revision) ||
            !CastleHandover.HasGarrison(w,d.FormerHouse,d.Site,d.At,d.Revision))
            throw new InvalidDataException("旧主家の守備が残った独立原本を指定してください");
        var oldLord=HouseSuccession.Head(w,d.FormerHouse,at,rev-1);
        bool reply=i.Action is CourtAction.AllowIndependentGate or CourtAction.RefuseIndependentGate;
        if(actor!=(reply?oldLord:d.Founder) || i.Person!=(reply?d.Founder:oldLord) || actor==i.Person ||
            !At(w,actor,d,at,rev) || !At(w,i.Person,d,at,rev))throw new InvalidDataException("両当事者が城で立ち会う必要があります");
        var history=HouseholdCourt.Events(w).Where(e=>Handles(e.Input.Action) && e.Input.Reference==d.Pact && e.Revision<rev).ToArray();
        var last=history.LastOrDefault();
        bool allowed=i.Action switch{
            CourtAction.RequestIndependentGate=>last==null,
            CourtAction.AllowIndependentGate or CourtAction.RefuseIndependentGate=>last?.Input.Action==CourtAction.RequestIndependentGate && last.Input.Person==actor,
            CourtAction.ReceiveIndependentGate=>last?.Input.Action==CourtAction.AllowIndependentGate && last.Actor==oldLord,
            CourtAction.ReceiveIndependentKeep=>last?.Input.Action==CourtAction.ReceiveIndependentGate && last.Input.Person==oldLord &&
                !CastleHandover.HasGarrison(w,d.FormerHouse,d.Site,at,rev) && !CastleAdministration.Blocked(w,d.Site,at,rev),
            _=>false
        };
        if(!allowed)throw new InvalidDataException("城門の合意・受領、本丸の守備の順に確認してください");
    }
    internal static IEnumerable<(CourtInput Input,EntityId Actor)> NpcAnswers(ContractWorld w)
    {
        foreach(var request in HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.RequestIndependentGate)){
            var lord=request.Input.Person;
            if(lord==w.PlayerControl || HouseholdCourt.Events(w).Any(e=>e.Input.Reference==request.Input.Reference && e.Revision>request.Revision && Handles(e.Input.Action)))continue;
            bool agrees=w.Characters.Single(c=>c.Id==lord).WillObey &&
                SocialWorld.ConsentBonus(w,lord,request.Actor,w.ElapsedSeconds,w.CommitSequence)>=0;
            var choice=agrees?CourtAction.AllowIndependentGate:CourtAction.RefuseIndependentGate;
            var answer=Available(w,lord).FirstOrDefault(i=>i.Action==choice && i.Reference==request.Input.Reference);
            if(answer!=null)yield return(answer,lord);
        }
    }
    public static IEnumerable<CourtInput> Available(ContractWorld w,EntityId actor)
    {
        if(SocialWorld.Site(w,actor) is not {} site)yield break;
        foreach(var d in w.Declarations??[]){
            if(d.CheckGarrison!=true || d.Site!=site)continue;
            var lord=HouseSuccession.Head(w,d.FormerHouse);
            if(actor!=d.Founder && actor!=lord)continue;
            foreach(var action in new[]{CourtAction.RequestIndependentGate,CourtAction.AllowIndependentGate,CourtAction.RefuseIndependentGate,
                CourtAction.ReceiveIndependentGate,CourtAction.ReceiveIndependentKeep}){
                var input=new CourtInput(HouseholdCourt.Id(action.ToString(),d.Pact.Value),action,actor==lord?d.Founder:lord,d.FormerHouse,site,Reference:d.Pact);
                try{Check(w,input,actor,site,w.ElapsedSeconds,w.CommitSequence+1);}catch(InvalidDataException){continue;}
                yield return input;
            }
        }
    }
}
