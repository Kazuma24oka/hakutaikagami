using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
[Trait("Tier","Heavy")]
public sealed class NationalFieldSupplyTests
{
    static void Good(CommandResult r)=>Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);
    static void Advance(WorldSession s,long at){s.SetPaused(false);while(s.Seconds<at){long old=s.Seconds;s.AdvanceTo(at,100000);Assert.True(s.Seconds>old);s.SetPaused(false);}}
    static void Open(WorldSession s){foreach(var m in s.GetActorView(s.PlayerControl).Messages.Where(m=>m.KnownBody==null).ToArray())Good(s.Open(s.PlayerControl,m.Id,s.Revision));}
    static void Send(WorldSession s,NewMilitaryAction action)
    {
        var w=s.DiagnosticSnapshot();Good(s.DecideNewMilitary(new(new("request:national-military/"+s.Revision),action){AuthorityPolicy=action==NewMilitaryAction.Request?NewGameMilitary.AuthorityContract:null,LedgerPolicy=action==NewMilitaryAction.Request?NewGameMilitary.LedgerIdentityContract:null,ObservationPolicy=action==NewMilitaryAction.Request?NewGameMilitary.ObservationContract:null},s.PlayerControl,s.Revision));
    }
    [Fact]public void NativePresenceAndSupplyUseOneActualArmyAndOneRationLedgerAcrossRecovery()
    {
        var start=NationalStart.Scenario();var people=start.People.ToDictionary(p=>p.Id);var heads=start.Factions.ToDictionary(f=>f.Head);
        var retainer=start.People.First(p=>p.Lord!=null&&heads.ContainsKey(p.Lord)&&p.Site==people[p.Lord].Site&&p.Faction==heads[p.Lord].Id&&start.Castles.Any(c=>c.Id==p.Site&&c.Faction==p.Faction));
        // 全国の実在人物・城・家を使い、日課の有無とは独立に、軍の受渡しそのものを検査する。
        var w=NewGameWorld.Create(start,[],retainer.Id,20261113,NewGameWorld.CompactPersonalAuthority,NationalStart.RevenueEdition,
            inspectionPolicy:LocalInspectionFindings.Policy,workPolicy:WorkFollowup.Policy,economyPolicy:RegionalDomains.Policy,developmentPolicy:CharacterDevelopment.Policy,
            networkPolicy:WorldNetwork.CastlePolicy,districtControlPolicy:NationalDistrictControl.Policy,nationalPersonDataPolicy:NationalPersonEstimates.Policy,nationalSystems:NationalSystems.Current);
        var s=new WorldSession(w);Assert.False(FieldPresence.PlayerDeployed(w));Assert.Null(w.Presence);Assert.Null(w.Supply);
        Send(s,NewMilitaryAction.Request);Advance(s,s.Seconds+2);Open(s);Send(s,NewMilitaryAction.Accept);Advance(s,s.Seconds+2);Open(s);Send(s,NewMilitaryAction.Pickup);
        var actual=s.DiagnosticSnapshot();var depot=NewGameMilitary.DepotFor(actual);
        Good(s.ManageMilitaryRice(new(new("request:national-rice"),depot,60),s.PlayerControl,s.Revision));
        long load=10*MilitaryEconomy.PersonSecondsPerKg;var id=new EntityId("request:national-pack");
        Good(s.HandleSupply(id,s.PlayerControl,true,load,s.Revision));
        Assert.Equal(ResultCode.AlreadyAccepted,s.HandleSupply(id,s.PlayerControl,true,load,s.Revision).Code);
        Good(s.SetRegionalActive(true,s.Revision));actual=s.DiagnosticSnapshot();
        var origin=NewGameMilitary.Home(actual,0);var a=new FieldPoint(origin.X+10000,origin.Y);var b=new FieldPoint(origin.X+10000,origin.Y-16000);
        var form=new FieldRequest(new("request:national-formation"),s.PlayerControl,a,b);
        if(Environment.GetEnvironmentVariable("HAKUTAI_NATIONAL_FIELD_QA_DIR") is {} beforeDir){Directory.CreateDirectory(beforeDir);File.WriteAllBytes(Path.Combine(beforeDir,"before-form.json"),ContractJson.Write(actual));}
        Good(s.SetFormation(form,s.Revision));Assert.Equal(form.Id,Assert.Single(s.DiagnosticSnapshot().NewMilitary!.Orders).Id);
        var regional=new RegionalRequest(form.Id,s.PlayerControl,s.PlayerControl,RegionalAction.Move,a,b);Assert.Equal(ResultCode.AlreadyAccepted,s.IssueRegional(regional,s.Revision).Code);
        Assert.Equal(ResultCode.NotAvailable,s.IssueRegional(regional with{Id=new("request:other-army"),Recipient=new(retainer.Lord!)},s.Revision).Code);
        Advance(s,s.Seconds+3);actual=s.DiagnosticSnapshot();
        Assert.NotEmpty(actual.NewMilitary!.Orders);Assert.Null(actual.Field);Assert.Null(actual.Region);Assert.True(RegionalBattle.Active(actual));
        Assert.Equal(NewGameMilitary.KnownMilitary(actual).Select(v=>v.Unit),RegionalBattle.View(actual,s.PlayerControl).Select(v=>v.Unit));Assert.Empty(RegionalBattle.View(actual,new(retainer.Lord!)));
        for(int i=0;i<3;i++){var sample=FieldWorld.Sample(actual,i,s.Seconds);Assert.Equal(NewGameMilitary.UnitIdsFor(actual)[i],sample.Unit);Assert.Equal(NewGameMilitary.Position(actual,i,s.Seconds),sample.Position);}
        Assert.True(FieldPresence.PlayerDeployed(actual));Assert.Equal(load-300,FieldSupply.Carried(actual).Quantity);
        Assert.Equal(NewGameMilitary.CarryAt(actual,s.Seconds,s.Revision+1),FieldSupply.Carried(actual).Quantity);
        Assert.Null(actual.Presence);Assert.Null(actual.Supply);Assert.Equal(3,actual.Units.Length);
        Assert.DoesNotContain(actual.ResourceLots,l=>l.Id==FieldSupply.Depot||l.Id==FieldSupply.Carry);
        s.SetPaused(true);actual=s.DiagnosticSnapshot();
        if(Environment.GetEnvironmentVariable("HAKUTAI_NATIONAL_FIELD_QA_DIR") is {} qa){Directory.CreateDirectory(qa);File.WriteAllBytes(Path.Combine(qa,"world.json"),ContractJson.Write(actual));}
        var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"national-field-"+Guid.NewGuid().ToString("N")),actual.Definitions);
        Assert.True(store.Save(s,"deployed",actual.WorldId).Published);var host=new WorldSessionHost(new WorldSession(TravelWorld.Create()));
        Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("deployed"),host.Current,host.Current.Revision));
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
        foreach(var x in new[]{s,host.Current})
        {
            Advance(x,actual.ElapsedSeconds+2);Send(x,NewMilitaryAction.Recall);var returning=x.DiagnosticSnapshot();long arrival=returning.NewMilitary!.Orders.Last().Routes.SelectMany(r=>r).Select(l=>l.EndAt).DefaultIfEmpty(x.Seconds).Max();Advance(x,arrival);Good(x.SetRegionalActive(false,x.Revision));
            var snapshot=x.DiagnosticSnapshot();Assert.False(FieldPresence.PlayerDeployed(snapshot));
            Good(x.HandleSupply(new("request:national-unpack"),x.PlayerControl,false,FieldSupply.Carried(snapshot).Quantity,x.Revision));
            Assert.Equal(0,FieldSupply.Carried(x.DiagnosticSnapshot()).Quantity);
        }
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
    }
}
