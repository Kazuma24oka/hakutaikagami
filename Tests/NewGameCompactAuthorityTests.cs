using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class NewGameCompactAuthorityTests
{
    static bool Permits(ContractWorld world,EntityId actor,Intent intent,long at)=>(bool)typeof(WorldSession).GetMethod("Permits",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.Invoke(null,new object[]{world,actor,intent,at})!;
    static ContractWorld Make(bool compact=true,int count=1){var records=Enumerable.Range(0,count).Select(i=>NewGameWorldTests.Officer("created:compact-"+i,site:NationalRoutes.Sites[i%NationalRoutes.Sites.Length].Id.Value)).ToArray();var setup=NewGameWorldTests.Setup(records);return NewGameWorld.Create(setup.World,setup.Registered,"person:nobunaga",personalAuthorityLayout:compact?NewGameWorld.CompactPersonalAuthority:null);}
    [Fact]public void EveryAuthoredSiteAndActionHasTheSamePermissionAsLegacyEncoding()
    {
        var old=Make(false);var compact=Make();var basis=NewGameWorld.Inspect(old,"request:compact-permissions");
        foreach(var person in old.Characters)foreach(var site in old.Definitions.Sites)foreach(var action in Enum.GetValues<ActionKind>()){
            var intent=basis with{Actor=person.Id,Executor=person.Id,Scope=site.Id,Action=action};
            Assert.Equal(Permits(old,person.Id,intent,0),Permits(compact,person.Id,intent,0));
        }
        Assert.Equal(109*old.Characters.Length,old.Grants.Length);Assert.Equal(2*compact.Characters.Length,compact.Grants.Length);
    }
    [Fact]public void CompactGrantIsNotAnUnknownSiteWildcardOrSomeoneElsesGrant()
    {
        var w=Make();var i=NewGameWorld.Inspect(w,"request:compact-limits");Assert.False(Permits(w,w.PlayerControl,i with{Scope=new("site:unregistered")},0));Assert.False(Permits(w,new("person:unknown"),i,0));Assert.False(Permits(w,w.PlayerControl,i with{Amount=1},0));Assert.False(Permits(w,w.PlayerControl,i,-1));
    }
    [Theory][InlineData(false)][InlineData(true)]public void InspectionAndPhysicalMovementRestoreAndContinue(bool compact)
    {
        var s=new WorldSession(Make(compact));Assert.Equal(ResultCode.Accepted,s.Submit(NewGameWorld.Inspect(s.DiagnosticSnapshot(),"request:compact-inspect"),s.Revision).Code);NewGameWorldTests.At(s,5);
        Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:compact-move","fork"),s.Revision).Code);NewGameWorldTests.At(s,10);
        var other=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(s.DiagnosticSnapshot())));foreach(var v in new[]{s,other})NewGameWorldTests.At(v,20);Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(other.DiagnosticSnapshot()));
    }
    [Fact]public void LegacyLayoutStaysOmittedAndRetainsItsInitialDefinitionHash()
    {
        var w=Make(false);var bytes=ContractJson.Write(w);Assert.DoesNotContain("PersonalAuthorityLayout",System.Text.Encoding.UTF8.GetString(bytes));Assert.Null(ContractJson.Read<ContractWorld>(bytes).NewGame!.PersonalAuthorityLayout);Assert.Equal(bytes,ContractJson.Write(new WorldSession(w).DiagnosticSnapshot()));
    }
    [Theory][InlineData("unknown")][InlineData("remove")][InlineData("extra")][InlineData("action")][InlineData("principal")]
    public void CompactWorldCannotLaunderForgedPermission(string kind)
    {
        var w=Make();ContractValidation.Validate(w);switch(kind){case "unknown":w.NewGame=w.NewGame! with{PersonalAuthorityLayout="unknown"};break;case "remove":w.Grants=w.Grants.Skip(1).ToArray();break;case "extra":w.Grants=[..w.Grants,w.Grants[1] with{Id=new("grant:forged")}];break;case "action":w.Grants[1].Actions[0]=ActionKind.Transfer;break;case "principal":w.Grants[1]=w.Grants[1] with{Principal=new("person:shingen")};break;}Assert.Throws<InvalidDataException>(()=>new WorldSession(w));
    }
    [Fact]public void MinorHasNoPersonalInspectionRightsInEitherEncoding()
    {
        var records=new[]{NewGameWorldTests.Officer() with{BirthYear=1550}};var setup=NewGameWorldTests.Setup(records);foreach(var layout in new string?[]{null,NewGameWorld.CompactPersonalAuthority}){var w=NewGameWorld.Create(setup.World,setup.Registered,"person:nobunaga",personalAuthorityLayout:layout);var i=NewGameWorld.Inspect(w,"request:minor-inspect");Assert.False(Permits(w,new("created:test"),i,0));}
    }
    [Theory][InlineData(false)][InlineData(true)]public void EntryProfileAndRealGenerationSaveRebuildTheSameAuthorityLayout(bool compact)
    {
        var records=new[]{NewGameWorldTests.Officer()};var setup=NewGameWorldTests.Setup(records);var root=Path.Combine(Path.GetTempPath(),"hakutai-compact-profile",Guid.NewGuid().ToString("N"));var store=new NewCampaignStore(root);
        var profile=store.New(setup.World,setup.Registered,"person:nobunaga",20261024,compact?NewGameWorld.CompactPersonalAuthority:null);var initial=NewCampaignStore.Build(profile);var session=new WorldSession(initial);
        Assert.Equal(ResultCode.Accepted,session.Submit(TravelWorld.Move(session.DiagnosticSnapshot(),"request:compact-profile-go","fork"),session.Revision).Code);NewGameWorldTests.At(session,5);session.SetPaused(true);store.Saves(profile).Save(session,"manual",initial.WorldId);
        var again=new NewCampaignStore(root);var read=again.Read(profile.Instance);Assert.Equal(profile.InitialHash,ContractJson.CanonicalHash(NewCampaignStore.Build(read)));Assert.Equal(compact?NewGameWorld.CompactPersonalAuthority:null,read.PersonalAuthorityLayout);
        var list=again.List(out int unreadable);Assert.Equal(0,unreadable);Assert.Single(list);var host=new WorldSessionHost(new WorldSession(NewCampaignStore.Build(read)));Assert.Equal(ResultCode.Accepted,host.CommitLoad(again.Saves(read).Prepare("manual"),host.Current,host.Current.Revision));Assert.Equal(ContractJson.Write(session.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
        foreach(var s in new[]{session,host.Current})NewGameWorldTests.At(s,10);Assert.Equal(ContractJson.Write(session.DiagnosticSnapshot()),ContractJson.Write(host.Current.DiagnosticSnapshot()));
        if(!compact)Assert.DoesNotContain("PersonalAuthorityLayout",File.ReadAllText(Path.Combine(again.DirectoryFor(read.Instance),"start.json")));
    }
}
