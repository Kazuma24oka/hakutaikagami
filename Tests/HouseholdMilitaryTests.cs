using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class HouseholdMilitaryTests
{
    static WorldSession Start()=>new(MilitaryDebriefTests.New());
    static void At(WorldSession s,long at)=>NewGameWorldTests.At(s,at);
    static void Open(WorldSession s){foreach(var m in s.GetActorView(s.PlayerControl).Messages.Where(m=>m.KnownBody==null).ToArray())Assert.Equal(ResultCode.Accepted,s.Open(s.PlayerControl,m.Id,s.Revision).Code);}
    static NewMilitaryInput Input(WorldSession s,NewMilitaryAction action,long amount=0,bool scoped=true)=>new(new("request:house-military/"+s.Revision+"/"+action),action,amount){ObservationPolicy=action==NewMilitaryAction.Request?NewGameMilitary.ObservationContract:null,AuthorityPolicy=action==NewMilitaryAction.Request?NewGameMilitary.AuthorityContract:null,FromLedger=action==NewMilitaryAction.Request?NewGameMilitary.SourceForCurrentHouse(s.DiagnosticSnapshot()):null,LedgerPolicy=action==NewMilitaryAction.Request&&scoped?NewGameMilitary.LedgerIdentityContract:null,CycleContract=action==NewMilitaryAction.Request?NewGameMilitary.CycleForCurrentHouse(s.DiagnosticSnapshot()):null};
    static void Send(WorldSession s,NewMilitaryAction a,long amount=0,bool scoped=true){var result=s.DecideNewMilitary(Input(s,a,amount,scoped),s.PlayerControl,s.Revision);Assert.True(result.Code==ResultCode.Accepted,result.VisibleReason);}
    static void Permit(WorldSession s,bool scoped=true){Send(s,NewMilitaryAction.Request,scoped:scoped);At(s,s.Seconds+2);Open(s);Send(s,NewMilitaryAction.Accept);At(s,s.Seconds+2);Open(s);Send(s,NewMilitaryAction.Pickup);}
    static void Finish(WorldSession s){long carry=NewGameMilitary.View(s.DiagnosticSnapshot()).Carry;if(carry>0)Send(s,NewMilitaryAction.Unload,carry);Send(s,NewMilitaryAction.Report);Send(s,NewMilitaryAction.Return);At(s,s.Seconds+3);Open(s);}
    static void ChangeHouse(WorldSession s,string node,string lord){if(NewGameService.View(s.DiagnosticSnapshot()).Principal!=null){var current=NewGameService.View(s.DiagnosticSnapshot()).Cases.Last(c=>c.Principal==NewGameService.View(s.DiagnosticSnapshot()).Principal);Assert.Equal(ResultCode.Accepted,s.DecideService(new(new("request:house-leave/"+s.Revision),CareerAction.RequestRelease,current.Case),s.PlayerControl,s.Revision).Code);At(s,s.Seconds+2);Open(s);}Assert.Null(NewGameService.View(s.DiagnosticSnapshot()).Principal);Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:house-travel/"+s.Revision,node),s.Revision).Code);At(s,s.Seconds+2);At(s,s.GetBranchView(s.PlayerControl).ArrivalAt!.Value+2);var id=new EntityId("request:house-apply/"+s.Revision);Assert.Equal(ResultCode.Accepted,s.DecideService(new(id,CareerAction.Apply,Principal:new(lord)),s.PlayerControl,s.Revision).Code);At(s,s.Seconds+2);Open(s);Assert.Equal(ResultCode.Accepted,s.DecideService(new(new("request:house-accept/"+s.Revision),CareerAction.Accept,id),s.PlayerControl,s.Revision).Code);At(s,s.Seconds+2);Open(s);}
    static WorldSession AtSecondHouse(bool legacy=false){var s=Start();Permit(s,!legacy);Send(s,NewMilitaryAction.Load,100000);Finish(s);ChangeHouse(s,"odawara","person:ujiyasu");return s;}
    [Theory][InlineData(false)][InlineData(true)] public void SameOfficerTakesAnotherHouseArmyWithoutReplacingOldArmy(bool legacy)
    {
        var s=AtSecondHouse(legacy);var original=s.DiagnosticSnapshot();var oldUnits=ContractJson.Write(original.Units);var oldLotIds=original.ResourceLots.Select(l=>l.Id).ToHashSet();var oldLots=ContractJson.Write(original.ResourceLots.OrderBy(l=>l.Id.Value).ToArray());var oldState=ContractJson.Write(original.NewMilitary!);
        Assert.True(NewGameMilitary.ViewWithAuthority(original).CanRequest);Permit(s);var w=s.DiagnosticSnapshot();Assert.Equal(2,NewGameMilitary.LedgerStates(w).Length);Assert.Equal(6,w.Units.Length);
        Assert.Equal(oldState,ContractJson.Write(w.OtherMilitaryLedgers!.Single()));Assert.Contains(WorkRelations.Known(w),c=>c.Other==new EntityId("person:shingen")&&!c.Local);Assert.Equal(oldUnits,ContractJson.Write(w.Units.Where(u=>u.Organization==new EntityId("org:takeda")).ToArray()));
        Assert.Equal(oldLots,ContractJson.Write(w.ResourceLots.Where(l=>oldLotIds.Contains(l.Id)).OrderBy(l=>l.Id.Value).ToArray()));
        Assert.All(w.Units.Where(u=>u.Organization==new EntityId("org:hojo")),u=>Assert.Equal(s.PlayerControl,u.Commander));Assert.All(w.Units.Where(u=>u.Organization==new EntityId("org:takeda")),u=>Assert.Equal(new EntityId("person:shingen"),u.Commander));
        Send(s,NewMilitaryAction.Load,100000);Send(s,NewMilitaryAction.Deploy);At(s,s.Seconds+2);Send(s,NewMilitaryAction.Enter);Finish(s);ContractValidation.Validate(s.DiagnosticSnapshot());
    }
    [Fact] public void SaveRestoreAndSameFutureKeepBothHouseholdsExact()
    {
        var s=AtSecondHouse();Permit(s);Send(s,NewMilitaryAction.Load,100000);var copy=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(s.DiagnosticSnapshot())));foreach(var x in new[]{s,copy}){Send(x,NewMilitaryAction.Deploy);At(x,x.Seconds+2);Send(x,NewMilitaryAction.Enter);Finish(x);}Assert.Equal(ContractJson.CanonicalHash(s.DiagnosticSnapshot()),ContractJson.CanonicalHash(copy.DiagnosticSnapshot()));
    }
    [Fact] public void ReturnToFormerHouseReusesItsActualArmyAndInventory()
    {
        var s=AtSecondHouse();var first=ContractJson.Copy(s.DiagnosticSnapshot().NewMilitary!);Permit(s);Finish(s);ChangeHouse(s,"kofu","person:shingen");Permit(s);var w=s.DiagnosticSnapshot();Assert.Equal(2,NewGameMilitary.LedgerStates(w).Length);Assert.Equal(first.Terms.Army,w.NewMilitary!.Terms.Army);Assert.Equal(ContractJson.CanonicalHash(first.Binding!),ContractJson.CanonicalHash(w.NewMilitary.Binding!));Assert.Equal(2,NewGameMilitary.Cases(w).Length);Assert.Equal(6,w.Units.Length);Assert.Equal(1,NewGameMilitary.LedgerStates(w).Count(x=>x.Terms.Owner==new EntityId("org:takeda")));
    }
    [Fact] public void OldRequestReplayCannotSwitchActiveHouseOrDuplicateExecution()
    {
        var s=AtSecondHouse();var old=s.DiagnosticSnapshot().NewMilitary!.Requests.First().Input;Permit(s);string before=ContractJson.CanonicalHash(s.DiagnosticSnapshot());Assert.Equal(ResultCode.AlreadyAccepted,s.DecideNewMilitary(old,s.PlayerControl,0).Code);Assert.Equal(ResultCode.IdPayloadConflict,s.DecideNewMilitary(old with{Action=NewMilitaryAction.Accept},s.PlayerControl,s.Revision).Code);Assert.Equal(before,ContractJson.CanonicalHash(s.DiagnosticSnapshot()));
    }
    [Theory][InlineData("binding")][InlineData("archive")][InlineData("selection")][InlineData("stock")][InlineData("unopened")][InlineData("erased-history")]
    public void ForgedCrossHouseLedgerIsRejected(string part)
    {
        var s=AtSecondHouse();Permit(s);var w=s.DiagnosticSnapshot();if(part=="binding")w.NewMilitary=w.NewMilitary! with{Binding=w.OtherMilitaryLedgers!.Single().Binding};if(part=="archive")w.OtherMilitaryLedgers=[];if(part=="selection"){var old=w.NewMilitary!;w.NewMilitary=w.OtherMilitaryLedgers!.Single();w.OtherMilitaryLedgers=[old];}if(part=="unopened")w.OtherMilitaryLedgers=w.OtherMilitaryLedgers!.Select(x=>x with{Openings=[]}).ToArray();if(part=="erased-history"){var old=w.OtherMilitaryLedgers!.Single();var ids=old.Requests.Select(q=>q.Input.Id.Value).ToHashSet();var messages=w.Messaging.Messages.Where(m=>ids.Contains(m.CorrelationId)).Select(m=>m.Id).ToHashSet();w.OtherMilitaryLedgers=null;w.Units=w.Units.Where(u=>u.Organization!=old.Terms.Owner).ToArray();w.ResourceLots=w.ResourceLots.Where(l=>l.Owner!=old.Terms.Owner).ToArray();w.Messaging.Messages=w.Messaging.Messages.Where(m=>!messages.Contains(m.Id)).ToArray();w.Messaging.Attempts=w.Messaging.Attempts.Where(a=>!messages.Contains(a.MessageId)).ToArray();w.Messaging.Receipts=w.Messaging.Receipts.Where(a=>!messages.Contains(a.MessageId)).ToArray();w.Messaging.Knowledge=w.Messaging.Knowledge.Where(a=>!messages.Contains(a.MessageId)).ToArray();w.SealedMessages=w.SealedMessages.Where(a=>!messages.Contains(a.Value)).ToArray();}if(part=="stock")w.ResourceLots=w.ResourceLots.Select(l=>l.Owner==new EntityId("org:takeda")?l with{Quantity=l.Quantity+1}:l).ToArray();Assert.Throws<InvalidDataException>(()=>ContractValidation.Validate(w));
    }
    [Fact] public void ActualWaitingForMilitaryHandoverRemainsValidAfterChangingHouse()
    {
        var s=Start();Permit(s);Send(s,NewMilitaryAction.Load,100000);var card=NewGameService.View(s.DiagnosticSnapshot()).Cases.Single();
        Assert.Equal(ResultCode.Accepted,s.DecideService(new(new("request:wait-before-return"),CareerAction.RequestRelease,card.Case),s.PlayerControl,s.Revision).Code);At(s,s.Seconds+2);Open(s);Assert.Contains(s.DiagnosticSnapshot().Service!.Events,e=>e.Kind==CareerEventKind.ReleaseWaiting);
        Finish(s);ChangeHouse(s,"odawara","person:ujiyasu");Permit(s);ContractValidation.Validate(s.DiagnosticSnapshot());new WorldSession(ContractJson.Copy(s.DiagnosticSnapshot()));
    }
    [Fact] public void LateInquiryReplyBelongsToItsOriginalHouseEvenAfterNewPickup()
    {
        var s=AtSecondHouse();Send(s,NewMilitaryAction.Inquire);var old=s.DiagnosticSnapshot().NewMilitary!;string inquiry=old.Requests.Last().Message!;Permit(s);
        for(int n=0;n<12&&!s.DiagnosticSnapshot().OtherMilitaryLedgers!.Single().Events.Any(e=>e.Effect==MilitaryEffect.StatusSent&&e.Source==inquiry);n++){
            var w=s.DiagnosticSnapshot();var attempt=w.Messaging.Attempts.Where(a=>a.Status==HakutaiKagami.Core.Messaging.AttemptStatus.InTransit).OrderBy(a=>a.DueAt).First();At(s,Math.Max(s.Seconds+1,attempt.DueAt));
        }
        var after=s.DiagnosticSnapshot();var archive=after.OtherMilitaryLedgers!.Single();Assert.Contains(archive.Events,e=>e.Effect==MilitaryEffect.StatusSent&&e.Source==inquiry);Assert.DoesNotContain(after.NewMilitary!.Events,e=>e.Source==inquiry);Assert.Equal(new EntityId("org:hojo"),after.NewMilitary.Terms.Owner);Assert.True(NewGameMilitary.View(after).Custody);new WorldSession(ContractJson.Copy(after));
    }

    static void Form(WorldSession s,int offset)
    {
        var a=NewGameMilitary.Anchor(s.DiagnosticSnapshot());Assert.Equal(ResultCode.Accepted,s.DecideNewMilitary(new(new("request:history-line/"+s.Revision),NewMilitaryAction.Form,A:new(a.X+offset,a.Y-12000),B:new(a.X+offset,a.Y+12000)),s.PlayerControl,s.Revision).Code);
        At(s,s.DiagnosticSnapshot().NewMilitary!.Orders.Last().Routes.SelectMany(x=>x).Max(x=>x.EndAt)+1);
    }
    [Fact] public void HistoricalPhysicalPositionUsesTheArmyActiveAtThatInstant()
    {
        var s=Start();Permit(s);Send(s,NewMilitaryAction.Load,100000);Send(s,NewMilitaryAction.Deploy);Form(s,20000);long oldAt=s.Seconds;var old=RegionalCampaign.ActorPointAt(s.DiagnosticSnapshot(),s.PlayerControl,oldAt);
        Send(s,NewMilitaryAction.Recall);At(s,s.DiagnosticSnapshot().NewMilitary!.Orders.Last().Routes.SelectMany(x=>x).Max(x=>x.EndAt)+1);Send(s,NewMilitaryAction.Enter);Finish(s);ChangeHouse(s,"odawara","person:ujiyasu");Permit(s);Send(s,NewMilitaryAction.Load,100000);Send(s,NewMilitaryAction.Deploy);Form(s,30000);
        var w=s.DiagnosticSnapshot();Assert.Equal(old,RegionalCampaign.ActorPointAt(w,s.PlayerControl,oldAt));var copy=new WorldSession(ContractJson.Copy(w));Assert.Equal(old,RegionalCampaign.ActorPointAt(copy.DiagnosticSnapshot(),s.PlayerControl,oldAt));Assert.Equal(FieldWorld.ToWorld(NewGameMilitary.PersonPosition(w,s.Seconds)),RegionalCampaign.ActorPointAt(w,s.PlayerControl,s.Seconds));
    }

}
