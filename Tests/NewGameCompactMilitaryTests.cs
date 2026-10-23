using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class NewGameCompactMilitaryTests
{
    static void Open(WorldSession s){foreach(var m in s.GetActorView(s.PlayerControl).Messages.Where(m=>m.KnownBody==null).ToArray())Assert.Equal(ResultCode.Accepted,s.Open(s.PlayerControl,m.Id,s.Revision).Code);}
    static void Send(WorldSession s,NewMilitaryAction a){var r=s.DecideNewMilitary(new(new("request:compact-military/"+s.Revision),a){AuthorityPolicy=a==NewMilitaryAction.Request?NewGameMilitary.AuthorityContract:null},s.PlayerControl,s.Revision);Assert.True(r.Code==ResultCode.Accepted,r.VisibleReason);}
    [Theory][InlineData("person:nobunaga")][InlineData("person:shingen")][InlineData("person:ujiyasu")][InlineData("created-head")][InlineData("retainer")]
    public void CompactPermissionSetKeepsPhysicalMilitaryPickupReturnAndRestore(string actor)
    {
        bool head=actor=="created-head",retainer=actor=="retainer";
        var p=NewGameWorldTests.Officer(site:"site:kofu",role:head?StartTemplate.Daimyo:StartTemplate.Retainer,lord:retainer?"person:shingen":null);
        var setup=NewGameWorldTests.Setup(head||retainer?[p]:[],head?[new(p.Id,"org:compact-test",[p.Site],true)]:[]);
        string selected=head||retainer?p.Id:actor;var s=new WorldSession(NewGameWorld.Create(setup.World,setup.Registered,selected,personalAuthorityLayout:NewGameWorld.CompactPersonalAuthority));
        Send(s,NewMilitaryAction.Request);NewGameWorldTests.At(s,2);Open(s);Send(s,NewMilitaryAction.Accept);NewGameWorldTests.At(s,4);Open(s);Send(s,NewMilitaryAction.Pickup);
        var w=s.DiagnosticSnapshot();var grant=Assert.Single(w.NewMilitary!.Authority!.Grants);Assert.Equal(2*w.Characters.Length,w.Grants.Length);Assert.Equal(s.PlayerControl,grant.Mandate.Agent);Assert.DoesNotContain(RegionalAction.Engage,grant.Mandate.Actions);Assert.NotEqual(s.PlayerControl,grant.Owner);
        var copy=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(w)));foreach(var v in new[]{s,copy}){Send(v,NewMilitaryAction.Report);NewGameWorldTests.At(v,v.Seconds+1);Send(v,NewMilitaryAction.Return);NewGameWorldTests.At(v,v.Seconds+2);Open(v);}
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(copy.DiagnosticSnapshot()));Assert.NotNull(s.DiagnosticSnapshot().NewMilitary!.Authority!.Grants.Single().UntilRevision);
    }
}
