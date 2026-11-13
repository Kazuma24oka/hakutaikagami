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
        var w=s.DiagnosticSnapshot();Good(s.DecideNewMilitary(new(new("request:national-military/"+s.Revision),action){AuthorityPolicy=action==NewMilitaryAction.Request?NewGameMilitary.AuthorityContract:null,LedgerPolicy=action==NewMilitaryAction.Request?NewGameMilitary.LedgerIdentityContract:null},s.PlayerControl,s.Revision));
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
        Good(s.SetRegionalActive(true,s.Revision));Advance(s,s.Seconds+3);actual=s.DiagnosticSnapshot();
        Assert.True(FieldPresence.PlayerDeployed(actual));Assert.Equal(load-300,FieldSupply.Carried(actual).Quantity);
        Assert.Equal(NewGameMilitary.CarryAt(actual,s.Seconds,s.Revision+1),FieldSupply.Carried(actual).Quantity);
        Assert.Null(actual.Presence);Assert.Null(actual.Supply);Assert.Equal(3,actual.Units.Length);
        Assert.DoesNotContain(actual.ResourceLots,l=>l.Id==FieldSupply.Depot||l.Id==FieldSupply.Carry);
        s.SetPaused(true);actual=s.DiagnosticSnapshot();
        var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"national-field-"+Guid.NewGuid().ToString("N")),actual.Definitions);
        Assert.True(store.Save(s,"deployed",actual.WorldId).Published);var host=new WorldSessionHost(new WorldSession(TravelWorld.Create()));
        Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("deployed"),host.Current,host.Current.Revision));
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
        foreach(var x in new[]{s,host.Current})
        {
            Advance(x,actual.ElapsedSeconds+2);Good(x.SetRegionalActive(false,x.Revision));
            var snapshot=x.DiagnosticSnapshot();Assert.False(FieldPresence.PlayerDeployed(snapshot));
            Good(x.HandleSupply(new("request:national-unpack"),x.PlayerControl,false,FieldSupply.Carried(snapshot).Quantity,x.Revision));
            Assert.Equal(0,FieldSupply.Carried(x.DiagnosticSnapshot()).Quantity);
        }
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
    }
}
