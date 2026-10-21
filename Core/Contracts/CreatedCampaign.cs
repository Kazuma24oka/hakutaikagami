namespace HakutaiKagami.Core.Contracts;

public sealed record CommandMandate(EntityId Id,EntityId Principal,EntityId Agent,EntityId Army,EntityId[] Units,
    RegionalAction[] Actions,EntityId Basis,long From,long Until);
// Explicit placement exercise, not historical Oda troop numbers or a private army.
public static class CreatedCampaign
{
    public const string Ruleset="created-retainer-campaign-v1",Contract="same-world-oda-training100-placement-only-mandate-dispatch-pose-v1";
    public static readonly EntityId Army=new("army:created-field"),Basis=new("basis:created-field-command"),Mandate=new("mandate:created-field");
    public static readonly StartBudgetPolicy Policy=new(100,1,1,1,[]);
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset||EngagementMission.Enabled(w);
    public static string Fingerprint(ContractWorld w)=>ContractJson.CanonicalHash(EngagementMission.Enabled(w)?new[]{Contract,RetainerStart.Fingerprint(w),ContractJson.CanonicalHash(w.CommandMandates),EngagementMission.Contract+ContractJson.CanonicalHash(w.Engagement!.Terms)}:new[]{Contract,RetainerStart.Fingerprint(w),ContractJson.CanonicalHash(w.CommandMandates)});
    public static ContractWorld Create(string name="作成武将",ulong seed=20261020,long commandUntil=long.MaxValue)
    {
        if(commandUntil<=0)throw new InvalidDataException("指揮許可の期限が不正です");
        var w=RetainerStart.Create(name,seed);w.WorldId=new("world:"+Ruleset);
        w.Definitions=w.Definitions with{Id=new("scenario:"+Ruleset),Ruleset=Ruleset,AuthorityBases=[..w.Definitions.AuthorityBases,new(Basis,"織田家所属の試作兵100の配置任務。史実兵数・私有兵・交戦許可ではない")],Armies=[..w.Definitions.Armies,new(Army,RetainerStart.House,RetainerStart.Lord)]};
        var ids=FieldWorld.UnitIds.ToHashSet();w.Units=w.Units.Select(u=>ids.Contains(u.Id)?u with{Commander=PersonalStart.Person,Organization=RetainerStart.House,Army=Army}:u).ToArray();
        w.Region=w.Region! with{Units=w.Region.Units.Select(u=>ids.Contains(u.Id)?u with{Commander=PersonalStart.Person}:u).ToArray()};
        w.CommandMandates=[new(Mandate,RetainerStart.Lord,PersonalStart.Person,Army,FieldWorld.UnitIds,[RegionalAction.Move,RegionalAction.Hold,RegionalAction.Retreat],Basis,0,commandUntil)];
        foreach(var observer in new[]{PersonalStart.Person,RetainerStart.Lord}){var id=new EntityId("evidence:created-command/"+observer.Value.Replace(':','/'));w.Evidence=[..w.Evidence,new(id,observer,PersonalStart.Person,"created-start-command:placement100",0,0,null,null)];w.Knowledge=[..w.Knowledge,new(observer,id,0)];}
        w.Definitions=w.Definitions with{Version=RegionalBattle.Version(w)};ContractValidation.Validate(w);
        if(!StartConfiguration.Check(w,StartConfiguration.Describe(w,w.PlayerControl,StartTemplate.Retainer,PersonOrigin.Created,Policy),Policy).Eligible)throw new InvalidDataException("本隊配置任務の開始設定が不整合です");return w;
    }
    public static bool CanCommand(ContractWorld w,RegionalRequest r,long at)
    {
        if(!Enabled(w)||r.Actor!=PersonalStart.Person||r.Recipient!=r.Actor)return false;
        return w.CommandMandates!.Any(m=>m.Agent==r.Actor&&m.Principal==RetainerStart.Lord&&m.Army==Army&&m.Actions.Contains(r.Action)&&(r.Action!=RegionalAction.Engage||EngagementMission.CanEngage(w,r,at))&&
            (m.From<=at&&at<m.Until||r.Action==RegionalAction.Retreat&&w.Region!.Active)&&
            m.Units.All(id=>w.Units.Any(u=>u.Id==id&&u.Commander==r.Actor&&u.Army==m.Army)));
    }
    public static bool CanDeploy(ContractWorld w)=>Enabled(w)&&w.Locations.Any(l=>l.Actor==w.PlayerControl&&l.Kind==LocationKind.AtSite&&l.Site==ContractFixture.Castle)&&
        w.CommandMandates!.Any(m=>m.From<=w.ElapsedSeconds&&w.ElapsedSeconds<m.Until);
    public static void Validate(ContractWorld w)
    {
        if(!Enabled(w))return;RetainerStart.ValidateIdentity(w,true);
        var m=w.CommandMandates;if(m==null||m.Length!=1)throw new InvalidDataException("An explicit command mandate is required");var grant=m[0];
        if(grant.Id!=Mandate||grant.Principal!=RetainerStart.Lord||grant.Agent!=PersonalStart.Person||grant.Army!=Army||grant.Basis!=Basis||grant.From!=0||grant.Until<=0||
            !grant.Units.SequenceEqual(FieldWorld.UnitIds)||!grant.Actions.SequenceEqual((EngagementMission.Enabled(w)?new[]{RegionalAction.Move,RegionalAction.Hold,RegionalAction.Retreat,RegionalAction.Engage}:new[]{RegionalAction.Move,RegionalAction.Hold,RegionalAction.Retreat}))||w.Region!.PerForce!=3||w.Field!.Orders.Length!=0)
            throw new InvalidDataException("Invalid limited placement mandate");
        if(!w.Definitions.Armies.Contains(new(Army,RetainerStart.House,RetainerStart.Lord)))throw new InvalidDataException("Force is not the lord's organization army");
        if(w.Units.Where(u=>u.Commander==PersonalStart.Person).Select(u=>u.Id).ToHashSet().SetEquals(grant.Units)==false||
            grant.Units.Any(id=>!w.Units.Any(u=>u.Id==id&&u.Commander==PersonalStart.Person&&u.Army==Army&&u.Organization==RetainerStart.House)||!w.Region.Units.Any(u=>u.Id==id&&u.Commander==PersonalStart.Person&&u.Side==0))||
            w.Region.Units.Where(u=>grant.Units.Contains(u.Id)).Sum(u=>u.InitialSoldiers)!=100||w.Atlas!.Anchor!="castle")throw new InvalidDataException("Assigned units and command mandate differ");
        foreach(var observer in new[]{PersonalStart.Person,RetainerStart.Lord}){var id=new EntityId("evidence:created-command/"+observer.Value.Replace(':','/'));if(!w.Evidence.Any(e=>e.Id==id&&e.Observer==observer&&e.Subject==PersonalStart.Person&&e.Claim=="created-start-command:placement100"&&e.ObservedAt==0&&e.RecordedAt==0&&e.Message==null&&e.Supersedes==null)||!w.Knowledge.Any(k=>k.Character==observer&&k.Evidence==id&&k.LearnedAt==0))throw new InvalidDataException("Each party must know initial command terms");}
    }
}
