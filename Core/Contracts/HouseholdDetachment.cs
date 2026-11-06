namespace HakutaiKagami.Core.Contracts;

public static class HouseholdDetachment
{
    internal static bool Handles(CourtAction action)=>action is CourtAction.OfferDetachment or CourtAction.AcceptDetachment
        or CourtAction.DeclineDetachment or CourtAction.EndDetachment;

    static IEnumerable<CourtEvent> Orders(ContractWorld w,long at,long rev)=>
        HouseholdCourt.Events(w).Where(e=>Handles(e.Input.Action) && e.At<=at && e.Revision<rev);

    static NewMilitaryRequest? Pickup(ContractWorld w,EntityId unit,long at,long rev)
    {
        foreach(var ledger in NewGameMilitary.LedgerStates(w)){
            var context=w.ForkMilitaryLedger();context.NewMilitary=ledger;
            if(!NewGameMilitary.UnitIdsFor(context).Contains(unit) || !NewGameMilitary.CustodyAt(context,at,rev-1))continue;
            return ledger.Requests.LastOrDefault(q=>q.Input.Action==NewMilitaryAction.Pickup && q.At<=at && q.Revision<rev);
        }
        return null;
    }

    static bool DirectFollower(ContractWorld w,EntityId person,long at,long rev)
    {
        var agreement=w.Companions?.Orders.LastOrDefault(q=>q.Input.Person==person && q.At<=at && q.Revision<rev &&
            q.Input.Action is CompanionAction.Agree or CompanionAction.EndAgreement);
        return agreement?.Input.Action==CompanionAction.Agree &&
            HouseholdService.BondsAt(w,at,rev).Any(b=>b.Subject==person && b.Target==w.PlayerControl && b.From<=at && at<b.Until) &&
            FamilyBirth.People(w).Any(p=>p.Id==person.Value && FamilyKin.LifeAt(w.NewGame!,p,at)==LifeState.Adult) &&
            !Assassination.DeadAt(w,person,at,rev);
    }

    public static CourtEvent? Appointment(ContractWorld w,EntityId unit,long at,long rev)
    {
        var orders=Orders(w,at,rev).ToArray();
        var acceptance=orders.LastOrDefault(e=>e.Input.Carrier==unit && e.Input.Action is CourtAction.AcceptDetachment or CourtAction.EndDetachment);
        if(acceptance?.Input.Action!=CourtAction.AcceptDetachment)return null;
        var offer=orders.Single(e=>e.Input.Id==acceptance.Input.Reference);
        return DirectFollower(w,acceptance.Actor,at,rev) && Pickup(w,unit,at,rev)?.Input.Id==offer.Input.Reference?acceptance:null;
    }

    internal static bool OnDuty(ContractWorld w,EntityId person,long at,long rev)
    {
        var last=Orders(w,at,rev).LastOrDefault(e=>e.Input.Person==person &&
            e.Input.Action is CourtAction.AcceptDetachment or CourtAction.EndDetachment);
        return last?.Input.Action==CourtAction.AcceptDetachment && last.Input.Carrier is {} unit &&
            Appointment(w,unit,at,rev)?.Input.Id==last.Input.Id;
    }

    public static int Leadership(ContractWorld w,EntityId unit,long at,long rev)=>
        Appointment(w,unit,at,rev) is {} order?CharacterDevelopment.EffectiveAt(w,order.Actor,DevelopmentAbility.Leadership,at):0;

    public static CourtInput? Offer(ContractWorld w,EntityId person,EntityId unit,EntityId id)
    {
        if(w.NewMilitary is not {} ledger || Pickup(w,unit,w.ElapsedSeconds,w.CommitSequence+1) is not {} pickup)return null;
        var input=new CourtInput(id,CourtAction.OfferDetachment,person,ledger.Terms.Owner,ledger.Terms.Site,Reference:pickup.Input.Id,Carrier:unit);
        try{Check(w,input,w.PlayerControl,ledger.Terms.Site,w.ElapsedSeconds,w.CommitSequence+1);return input;}
        catch(InvalidDataException){return null;}
    }

    internal static void Check(ContractWorld w,CourtInput i,EntityId actor,EntityId place,long at,long rev)
    {
        if(i.Carrier is not {} unit || i.Site!=place || Pickup(w,unit,at,rev) is not {} pickup)
            throw new InvalidDataException("引受中の分隊を指定してください");
        var orders=Orders(w,at,rev).ToArray();
        if(i.Action==CourtAction.EndDetachment){
            var active=Appointment(w,unit,at,rev);
            if(active==null || i.Owner!=active.Input.Owner || i.Site!=active.Input.Site || i.Reference!=active.Input.Id || i.Person!=active.Actor ||
                actor!=w.PlayerControl && actor!=active.Actor)throw new InvalidDataException("現任の指揮委任を指定してください");
            return;
        }
        if(!DirectFollower(w,i.Person,at,rev) || PersonalCompanions.SiteAt(w,i.Person,at,rev)!=place ||
            PersonalCompanions.SiteAt(w,w.PlayerControl,at,rev)!=place || PersonalCompanions.Occupied(w,i.Person,at,rev-1))
            throw new InvalidDataException("現地で随行中の成人の直属家臣に相談してください");
        var ledger=NewGameMilitary.LedgerStates(w).Single(l=>l.Requests.Any(q=>q.Input.Id==pickup.Input.Id));
        if(ledger.Terms.Owner!=i.Owner || ledger.Terms.Site!=place)
            throw new InvalidDataException("兵を引き受けた本拠で指揮を引き継いでください");
        if(i.Action==CourtAction.OfferDetachment){
            if(orders.Any(e=>e.Input.Action==CourtAction.OfferDetachment && e.Input.Reference==pickup.Input.Id &&
                (e.Input.Carrier==unit || e.Input.Person==i.Person) && !orders.Any(r=>r.Input.Reference==e.Input.Id)))
                throw new InvalidDataException("同じ分隊または同じ人への相談は返事を待ってください");
            if(actor!=w.PlayerControl || i.Reference!=pickup.Input.Id)throw new InvalidDataException("本人の兵の引受原本が必要です");
        }else{
            var offer=orders.SingleOrDefault(e=>e.Input.Id==i.Reference && e.Input.Action==CourtAction.OfferDetachment);
            if(actor!=i.Person || offer==null || offer.Input.Person!=actor || offer.Input.Carrier!=unit ||
                offer.Input.Reference!=pickup.Input.Id || orders.Any(e=>e.Input.Reference==offer.Input.Id))
                throw new InvalidDataException("本人が未返答の委任に一度だけ答えられます");
        }
        if(i.Action!=CourtAction.DeclineDetachment && (Appointment(w,unit,at,rev)!=null ||
            (ledger.Binding?.Units??NewGameMilitary.UnitIds).Any(u=>Appointment(w,u,at,rev)?.Actor==i.Person)))
            throw new InvalidDataException("一人一分隊の指揮です。先に既存の委任を返してください");
    }

    internal static IEnumerable<(CourtInput Input,EntityId Actor)> Replies(ContractWorld w)
    {
        var orders=Orders(w,w.ElapsedSeconds,w.CommitSequence+1).ToArray();
        foreach(var offer in orders.Where(e=>e.Input.Action==CourtAction.OfferDetachment && !orders.Any(r=>r.Input.Reference==e.Input.Id))){
            var person=offer.Input.Person;
            bool willing=SocialWorld.Axes(w,person,offer.Actor).Hostility<=30;
            var action=willing?CourtAction.AcceptDetachment:CourtAction.DeclineDetachment;
            yield return(offer.Input with{Id=HouseholdCourt.Id("detachment-answer",offer.Input.Id.Value),Action=action,Reference=offer.Input.Id},person);
        }
    }
}
