using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class MilitaryLedgerIdentityTests
{
    [Fact] public void OwnerAndSiteBothSeparateEveryPhysicalIdentity()
    {
        var rows=new[]{NewGameMilitary.LedgerIdentity(new("org:takeda"),new("site:kofu")),NewGameMilitary.LedgerIdentity(new("org:hojo"),new("site:kofu")),NewGameMilitary.LedgerIdentity(new("org:takeda"),new("site:odawara"))};
        var ids=rows.SelectMany(x=>new[]{x.Ledger,x.Army,x.Depot,x.Carry}.Concat(x.Units)).ToArray();
        Assert.Equal(ids.Length,ids.Distinct().Count());Assert.All(ids,x=>Assert.True(EntityId.IsValid(x.Value)));
    }
    [Fact] public void IdentityRoundTripIsStableAndDoesNotShareMutableUnits()
    {
        var a=NewGameMilitary.LedgerIdentity(new("org:takeda"),new("site:kofu"));
        var b=NewGameMilitary.LedgerIdentity(new("org:takeda"),new("site:kofu"));
        Assert.Equal(ContractJson.CanonicalHash(a),ContractJson.CanonicalHash(b));
        var copy=ContractJson.Read<MilitaryLedgerIdentity>(ContractJson.Write(a));Assert.Equal(ContractJson.CanonicalHash(a),ContractJson.CanonicalHash(copy));
        a.Units[0]=new("unit:changed");Assert.NotEqual(a.Units[0],b.Units[0]);
    }
    [Fact] public void ExistingWorldKeepsOriginalUnitsStockAndJournal()
    {
        var s=NewGameWorldTests.Session([NewGameWorldTests.Officer(site:"site:kofu",role:StartTemplate.Retainer,lord:"person:shingen")]);
        Assert.Null(NewGameMilitary.ExistingLedgerIdentity(s.DiagnosticSnapshot()));
        Assert.Equal(ResultCode.Accepted,s.DecideNewMilitary(new(new("request:ledger-binding"),NewMilitaryAction.Request),s.PlayerControl,s.Revision).Code);
        var w=s.DiagnosticSnapshot();var before=ContractJson.CanonicalHash(w);var id=NewGameMilitary.ExistingLedgerIdentity(w)!;
        Assert.Equal(w.NewMilitary!.Terms.Army,id.Army);Assert.Equal(NewGameMilitary.Depot,id.Depot);Assert.Equal(NewGameMilitary.Carry,id.Carry);Assert.Equal(NewGameMilitary.UnitIds,id.Units);
        Assert.Equal(w.NewMilitary.Terms.Owner,id.Owner);Assert.Equal(w.NewMilitary.Terms.Site,id.Site);
        Assert.Equal(before,ContractJson.CanonicalHash(w));ContractValidation.Validate(w);
    }
    [Theory][InlineData("")][InlineData("bad id")]
    public void InvalidOwnerCannotAcquireLedger(string owner)=>Assert.Throws<ArgumentException>(()=>NewGameMilitary.LedgerIdentity(new(owner),new("site:kofu")));
}
