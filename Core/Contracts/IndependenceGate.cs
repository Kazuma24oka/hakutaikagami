namespace HakutaiKagami.Core.Contracts;

public static class IndependenceGate
{
    public static bool Handles(CourtAction action)=>action is CourtAction.RequestIndependentGate or CourtAction.AllowIndependentGate
        or CourtAction.RefuseIndependentGate or CourtAction.ReceiveIndependentGate or CourtAction.ReceiveIndependentKeep;
    // 指揮官が隊を離れて城に戻った場合、本人の到着と各隊の位置を別々に確認する。
    static bool ReturnedInPerson(ContractWorld w,EntityId actor,EntityId site,long at,long rev)
    {using var _ccRev=ControlContext.At(rev);
        if(actor!=w.PlayerControl)return false;
        var state=NewGameMilitary.LedgerStates(w).SelectMany(s=>s.Requests.Select(q=>(Ledger:s,Request:q)))
            .Where(x=>(x.Request.At,x.Request.Revision).CompareTo((at,rev))<0 &&
                x.Request.Input.Action is NewMilitaryAction.Withdraw or NewMilitaryAction.Attach or NewMilitaryAction.Enter)
            .OrderByDescending(x=>x.Request.Revision).FirstOrDefault();
        if(state.Request?.Input.Action!=NewMilitaryAction.Withdraw || state.Ledger.Terms.Site!=site)return false;
        var journey=state.Ledger.Withdrawal?.Journeys.LastOrDefault(j=>j.Revision>=state.Request.Revision &&
            (j.At,j.Revision).CompareTo((at,rev))<0);
        if(journey==null || journey.Route.Any(l=>l.EndAt>at))return false;
        var anchor=FieldWorld.FromWorld(BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==site).Position);
        return RegionalGround.At(journey.Route,journey.Origin,at)==anchor;
    }
    public static EntityId? MeetingSite(ContractWorld w,EntityId actor)
    {
        if(SocialWorld.Site(w,actor) is {} site)return site;
        return (w.Declarations??[]).Concat(LocalClaims(w,w.CommitSequence+1)).LastOrDefault(d=>d.CheckGarrison==true && d.Founder==actor &&
            ReturnedInPerson(w,actor,d.Site,w.ElapsedSeconds,w.CommitSequence+1))?.Site;
    }
    static bool At(ContractWorld w,EntityId actor,IndependenceDeclaration d,long at,long rev)=>
        !Assassination.DeadAt(w,actor,at,rev) && (TransportWork.At(w,actor,d.Site,at,rev,true) || ReturnedInPerson(w,actor,d.Site,at,rev));
    // 交渉の参照は、独立した本人なら密約原本、追随者なら本人の公開返答に結び付ける。
    internal static IEnumerable<IndependenceDeclaration> LocalClaims(ContractWorld w,long rev)
    {using var _ccRev=ControlContext.At(rev);
        foreach(var declaration in w.Declarations??[]){
            if(declaration.CheckGarrison!=true || declaration.Revision>=rev)continue;
            if(CastleHandover.HasGarrison(w,declaration.FormerHouse,declaration.Site,declaration.At,declaration.Revision))
                yield return declaration;
            foreach(var answer in HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.JoinDeclaredHouse &&
                e.Input.Owner==declaration.House && e.Place!=declaration.Site && e.Revision<rev)){
                if(CommandedAtAnswer(w,answer))
                    yield return declaration with{Pact=answer.Input.Id,Founder=answer.Actor,Site=answer.Place,At=answer.At,Revision=answer.Revision};
            }
        }
    }
    static bool CommandedAtAnswer(ContractWorld w,CourtEvent answer)=>
        JointIndependence.CommandedCastle(w,answer.Actor,answer.Place,answer.At,answer.Revision);
    internal static void Check(ContractWorld w,CourtInput i,EntityId actor,EntityId site,long at,long rev)
    {using var _ccRev=ControlContext.At(rev);
        var d=LocalClaims(w,rev).SingleOrDefault(d=>d.Pact==i.Reference);
        if(d==null || i.Site!=d.Site || site!=d.Site || i.Owner!=d.FormerHouse || !JointIndependence.CommandedCastle(w,d.Founder,d.Site,d.At,d.Revision))
            throw new InvalidDataException("旧主家の守備が残った独立原本を指定してください");
        bool alliedCastellan=HouseholdCourt.Events(w).Any(e=>e.Input.Id==d.Pact && e.Input.Action==CourtAction.JoinDeclaredHouse);
        if(alliedCastellan && (CastleAdministration.Owner(w,d.Site,at,rev)!=d.FormerHouse || !JointIndependence.CommandedCastle(w,d.Founder,d.Site,at,rev) ||
            DefectionPacts.HouseAt(w,d.Founder,at,rev-1)!=d.House))
            throw new InvalidDataException("引渡しの時点で城を預かる追随城主の立会いが必要です");
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
        foreach(var joined in HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.JoinDeclaredHouse && e.Actor!=w.PlayerControl).ToArray()){
            var last=HouseholdCourt.Events(w).LastOrDefault(e=>Handles(e.Input.Action) && e.Input.Reference==joined.Input.Id);
            if(w.ElapsedSeconds<=(last?.At??joined.At))continue;
            var next=Available(w,joined.Actor).FirstOrDefault(i=>i.Reference==joined.Input.Id &&
                i.Action is CourtAction.RequestIndependentGate or CourtAction.ReceiveIndependentGate or CourtAction.ReceiveIndependentKeep);
            if(next!=null)yield return(next,joined.Actor);
        }
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
    public static CourtAction? LastWitnessed(ContractWorld w,EntityId actor)
    {
        var site=MeetingSite(w,actor);
        if(site==null)return null;
        return HouseholdCourt.Events(w).LastOrDefault(e=>Handles(e.Input.Action) && e.Place==site &&
            (e.Actor==actor || e.Input.Person==actor))?.Input.Action;
    }
    public static IEnumerable<CourtInput> Available(ContractWorld w,EntityId actor)
    {
        if(MeetingSite(w,actor) is not {} site)yield break;
        foreach(var d in LocalClaims(w,w.CommitSequence+1)){
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
