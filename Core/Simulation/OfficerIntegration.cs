using HakutaiKagami.Core.Appearance;
using HakutaiKagami.Core.Common;

namespace HakutaiKagami.Core.Simulation;

public enum FieldPhase { Standing, Marching, Contact, Engaged, Routing, Withdrawing, Exited, Incapacitated }
public enum FieldAction { Engage, Withdraw }
public sealed record FieldDirective(string Id,string IssuerId,string UnitId,FieldAction Action,long IssuedAt,long DueAt,bool Delivered=false,bool Accepted=false,string Resolution="InTransit");
public sealed record FieldGrant(string ActorId,string UnitId,string Jurisdiction,string Basis,long ValidFrom,long ValidUntil)
{
    public bool Allows(string actor,string unit,long tick)=>ActorId==actor&&UnitId==unit&&Jurisdiction=="field-fixture-v1"&&ValidFrom<=tick&&tick<ValidUntil;
}
public sealed record FieldUnit(string Id,string CommanderId,string Side,int X,int PreviousX,int Soldiers,int InitialSoldiers,
    int Morale,FieldPhase Phase,bool WillEngage=false,long? ActionAt=null,int Travel=0,bool FixtureWilling=true);
public sealed record FieldLoss(string Id,long Tick,string UnitId,int Soldiers,int Morale);
public sealed class FieldBattle
{
    public int Schema {get;set;}=1;
    public string Rules {get;set;}="field-fixture-v1";
    public long StartedAt {get;set;}
    public long Tick {get;set;}
    public FieldUnit[] Units {get;set;}=[];
    public FieldGrant[] Grants {get;set;}=[];
    public FieldDirective[] Directives {get;set;}=[];
    public FieldLoss[] Losses {get;set;}=[];
}
public sealed class OfficerIntegration
{
    public int Schema {get;set;}=1;
    public Dictionary<string,OfficerAppearance> Looks {get;set;}=new(StringComparer.Ordinal);
    public double DisplayFraction {get;set;}
    public int DisplaySpeed {get;set;}=1;
    public FieldBattle? Battle {get;set;}
}

// This fixed two-unit fixture is not the production battle/authority/NPC model.
public static class OfficerIntegrationFlow
{
    public const int ContactDistance=1600,MarchStep=1000,RetreatStep=1500,ExitDistance=10000;
    public static OfficerIntegration Create(IEnumerable<string> ids)=>new(){Looks=ids.Select((id,i)=>(id,i)).ToDictionary(x=>x.id,x=>AppearanceCatalog.Default(x.i),StringComparer.Ordinal)};
    public static void Validate(OfficerIntegration state,IEnumerable<string> ids,long tick)
    {
        static void Check(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
        Check(state.Schema==1 && state.Looks.Keys.Order().SequenceEqual(ids.Order()),"Officer appearance references/schema mismatch.");
        Check(double.IsFinite(state.DisplayFraction)&&state.DisplayFraction>=0&&state.DisplayFraction<1&&new[]{1,2,4}.Contains(state.DisplaySpeed),"Invalid display clock.");
        foreach(var a in state.Looks.Values)a.Validate(AppearanceCatalog.Slots);
        var b=state.Battle;if(b is null)return;
        Check(b.Schema==1&&b.Rules=="field-fixture-v1"&&b.StartedAt>=0&&b.StartedAt<=tick&&b.Tick==tick,"Battle rules/clock mismatch.");
        Check(b.Units.Length==2&&b.Units.Select(u=>u.Id).Distinct().Count()==2&&b.Units.Select(u=>u.CommanderId).Distinct().Count()==2,"Battle unit identity mismatch.");
        Check(b.Units.Select(u=>u.Id).Order().SequenceEqual(new[]{"unit-blue","unit-red"})&&b.Units.All(u=>u.Side=="fixture-"+u.Id[5..]),"Unknown fixture unit IDs.");
        Check(b.Units.Select(u=>u.Side).Order().SequenceEqual(new[]{"fixture-blue","fixture-red"}),"Unknown fixture side.");
        Check(b.Grants.Select(g=>(g.ActorId,g.UnitId)).Distinct().Count()==b.Grants.Length,"Duplicate field grants.");
        foreach(var g in b.Grants)Check(b.Units.Any(u=>u.Id==g.UnitId&&u.CommanderId==g.ActorId)&&g.Jurisdiction==b.Rules&&!string.IsNullOrWhiteSpace(g.Basis)&&g.ValidFrom>=0&&g.ValidUntil>g.ValidFrom,"Invalid field command jurisdiction/basis/period.");
        foreach(var u in b.Units)
        {
            Check(state.Looks.ContainsKey(u.CommanderId)&&!string.IsNullOrWhiteSpace(u.Id)&&Enum.IsDefined(u.Phase),"Broken commander/state.");
            Check(u.InitialSoldiers is >0 and <=10000&&u.Soldiers>=0&&u.Soldiers<=u.InitialSoldiers&&u.Morale is >=0 and <=100&&
                Math.Abs((long)u.X)<=ExitDistance&&Math.Abs((long)u.PreviousX)<=ExitDistance&&u.Travel>=0&&(u.ActionAt is null||u.ActionAt<=tick),"Invalid battle quantities.");
            Check(u.Soldiers+ b.Losses.Where(l=>l.UnitId==u.Id).Sum(l=>l.Soldiers)==u.InitialSoldiers,"Battle soldier ledger mismatch.");
            Check(u.Morale==Math.Max(0,(u.Side=="fixture-red"?100:55)-b.Losses.Where(l=>l.UnitId==u.Id).Sum(l=>l.Morale)),"Battle morale ledger mismatch.");
            Check(u.ActionAt==b.Losses.Where(l=>l.UnitId==u.Id).Select(l=>(long?)l.Tick).Max(),"Battle action/effect mismatch.");
            Check((u.Soldiers==0)==(u.Phase==FieldPhase.Incapacitated),"Zero force/phase mismatch.");
            Check(u.Phase!=FieldPhase.Exited||Math.Abs(u.X)==ExitDistance,"Exited unit has not reached boundary.");
        }
        Check(b.Directives.Select(d=>d.Id).Distinct().Count()==b.Directives.Length&&b.Directives.Length<=256,"Duplicate/excessive directives.");
        foreach(var d in b.Directives)Check(!string.IsNullOrWhiteSpace(d.Id)&&b.Units.Any(u=>u.Id==d.UnitId&&u.CommanderId==d.IssuerId)&&
            Enum.IsDefined(d.Action)&&d.IssuedAt>=b.StartedAt&&d.IssuedAt<=tick&&d.DueAt>d.IssuedAt&&d.Delivered==(d.DueAt<=tick)&&
            new[]{"InTransit","Accepted","Unauthorized","Refused","AlreadyStopped"}.Contains(d.Resolution)&&d.Accepted==(d.Resolution=="Accepted")&&
            d.Delivered==(d.Resolution!="InTransit"),"Invalid field directive.");
        Check(b.Losses.Select(l=>l.Id).Distinct().Count()==b.Losses.Length,"Duplicate battle effect.");
        foreach(var l in b.Losses)Check(b.Units.Any(u=>u.Id==l.UnitId)&&l.Id==$"field/{l.Tick}/{l.UnitId}"&&l.Tick>b.StartedAt&&l.Tick<=tick&&l.Soldiers>0&&l.Morale>0,"Invalid battle effect.");
    }
    public static void Dispatch(FieldBattle b,string id,string issuer,string unit,FieldAction action,long tick,int delay)
    {
        if(delay is <1 or >100||!Enum.IsDefined(action)||string.IsNullOrWhiteSpace(id))throw new ArgumentException("Invalid field order.");
        var old=b.Directives.SingleOrDefault(d=>d.Id==id);
        if(old is not null){if(old.IssuerId!=issuer||old.UnitId!=unit||old.Action!=action||old.DueAt-old.IssuedAt!=delay)throw new InvalidOperationException("Directive ID reused with different payload.");return;}
        if(!b.Grants.Any(g=>g.Allows(issuer,unit,tick)))throw new UnauthorizedAccessException("No active unit command grant in the fixture jurisdiction.");
        if(b.Directives.Any(d=>d.UnitId==unit&&d.DueAt==tick+delay&&d.Action!=action))throw new InvalidOperationException("Conflicting fixture directives at the same effect time are rejected.");
        b.Directives=[..b.Directives,new(id,issuer,unit,action,tick,checked(tick+delay))];
    }
    public static void Advance(OfficerIntegration state,long tick)
    {
        state.DisplayFraction=0;
        var b=state.Battle;if(b is null)return;
        if(tick!=b.Tick+1)throw new InvalidOperationException("Battle must advance exactly once with the common clock.");
        b.Tick=tick;b.Units=b.Units.Select(u=>u with{PreviousX=u.X}).ToArray();
        foreach(var d in b.Directives.Where(d=>!d.Delivered&&d.DueAt==tick).OrderBy(d=>d.Id,StringComparer.Ordinal).ToArray())
        {
            var recipient=b.Units.Single(u=>u.Id==d.UnitId);
            bool authorized=b.Grants.Any(g=>g.Allows(d.IssuerId,d.UnitId,tick));
            string resolution=!authorized?"Unauthorized":recipient.Phase is FieldPhase.Routing or FieldPhase.Exited or FieldPhase.Incapacitated?"AlreadyStopped":!recipient.FixtureWilling?"Refused":"Accepted";
            bool accepted=resolution=="Accepted";
            b.Directives=b.Directives.Select(x=>x.Id==d.Id?x with{Delivered=true,Accepted=accepted,Resolution=resolution}:x).ToArray();
            if(!accepted)continue;
            b.Units=b.Units.Select(u=>u.Id!=d.UnitId||u.Phase is FieldPhase.Routing or FieldPhase.Exited or FieldPhase.Incapacitated?u:
                d.Action==FieldAction.Withdraw?u with{Phase=FieldPhase.Withdrawing,WillEngage=false}:u with{Phase=FieldPhase.Marching,WillEngage=true}).ToArray();
        }
        var before=b.Units;
        // Simultaneous losses read the same pre-damage state. Contact is one distinct tick.
        if(before.All(u=>u.WillEngage&&u.Phase is FieldPhase.Contact or FieldPhase.Engaged)&&Math.Abs(before[0].X-before[1].X)<=ContactDistance)
        {
            b.Units=before.Select(u=>
            {
                int damage=Math.Min(u.Soldiers,u.Side=="fixture-blue"?12:8),morale=u.Side=="fixture-blue"?15:8;
                if(damage==0)return u;
                b.Losses=[..b.Losses,new($"field/{tick}/{u.Id}",tick,u.Id,damage,morale)];
                int remaining=u.Soldiers-damage,m=Math.Max(0,u.Morale-morale);
                return u with{Soldiers=remaining,Morale=m,ActionAt=tick,Phase=remaining==0?FieldPhase.Incapacitated:m<=20?FieldPhase.Routing:FieldPhase.Engaged};
            }).ToArray();return;
        }
        var snapshot=b.Units;
        b.Units=snapshot.Select(u=>
        {
            if(u.Phase is FieldPhase.Exited or FieldPhase.Incapacitated or FieldPhase.Standing)return u;
            int sign=u.Side=="fixture-red"?-1:1;
            if(u.Phase is FieldPhase.Routing or FieldPhase.Withdrawing)
            {
                int x=Math.Clamp(u.X+sign*RetreatStep,-ExitDistance,ExitDistance);
                return u with{X=x,Travel=checked(u.Travel+Math.Abs(x-u.X)),Phase=Math.Abs(x)==ExitDistance?FieldPhase.Exited:u.Phase};
            }
            var enemy=snapshot.Single(x=>x.Id!=u.Id);
            if(enemy.Phase is FieldPhase.Exited or FieldPhase.Incapacitated or FieldPhase.Routing or FieldPhase.Withdrawing)return u with{Phase=FieldPhase.Standing};
            int gap=Math.Abs(enemy.X-u.X),step=Math.Min(MarchStep,Math.Max(0,(gap-ContactDistance)/2));
            int next=u.X+Math.Sign(enemy.X-u.X)*step;
            return u with{X=next,Travel=checked(u.Travel+step),Phase=gap<=ContactDistance?FieldPhase.Contact:FieldPhase.Marching};
        }).ToArray();
        if(b.Units.All(u=>u.WillEngage&&u.Phase==FieldPhase.Marching)&&Math.Abs(b.Units[0].X-b.Units[1].X)<=ContactDistance)
            b.Units=b.Units.Select(u=>u with{Phase=FieldPhase.Contact}).ToArray();
    }
}

public sealed partial class TaxSimulation
{
    public OfficerIntegration? OfficerState=>state.Officers is null?null:SnapshotCodec.Copy(state.Officers);
    public void EnableOfficerIntegration()=>Mutate(s=>s.Officers??=OfficerIntegrationFlow.Create(s.World.Characters.Select(c=>c.Id)));
    public void CaptureDisplayClock(double fraction,int speed)=>Mutate(s=>{if(s.Officers is null)throw new InvalidOperationException();s.Officers.DisplayFraction=fraction;s.Officers.DisplaySpeed=speed;});
    public void AssignAppearance(string characterId,OfficerAppearance look)=>Mutate(s=>
    {
        if(s.Officers is null||!s.Officers.Looks.ContainsKey(characterId))throw new ArgumentException("Unknown officer ID.");
        look.Validate(AppearanceCatalog.Slots);s.Officers.Looks[characterId]=SnapshotCodec.Copy(look);
    });
    public void StartFieldFixture()=>Mutate(s=>
    {
        if(s.Officers is null)throw new InvalidOperationException("Officer integration is disabled.");
        if(s.Officers.Battle is not null)throw new InvalidOperationException("Battle fixture already exists; use session reset for a new run.");
        var ids=s.World.Characters.Select(c=>c.Id).ToArray();
        s.Officers.Battle=new(){StartedAt=s.Tick,Tick=s.Tick,Units=[
            new("unit-red",ids[0],"fixture-red",-5000,-5000,100,100,100,FieldPhase.Standing),
            new("unit-blue",ids[1],"fixture-blue",5000,5000,60,60,55,FieldPhase.Standing)],Grants=[
            new(ids[0],"unit-red","field-fixture-v1","検証用部隊指揮委任",s.Tick,long.MaxValue),
            new(ids[1],"unit-blue","field-fixture-v1","検証用部隊指揮委任",s.Tick,long.MaxValue)]};
    });
    public void DispatchField(string id,string issuerId,string unitId,FieldAction action,int delay=1)=>Mutate(s=>
        OfficerIntegrationFlow.Dispatch(s.Officers?.Battle??throw new InvalidOperationException("No field fixture."),id,issuerId,unitId,action,s.Tick,delay));
    public void EngageFieldFixture()=>Mutate(s=>
    {
        var b=s.Officers?.Battle??throw new InvalidOperationException("No field fixture.");
        OfficerIntegrationFlow.Dispatch(b,"fixture-engage-red","character-001","unit-red",FieldAction.Engage,s.Tick,1);
        OfficerIntegrationFlow.Dispatch(b,"fixture-engage-blue","character-002","unit-blue",FieldAction.Engage,s.Tick,1);
    });
    public void RevokeFieldGrant(string actorId)=>Mutate(s=>{var b=s.Officers?.Battle??throw new InvalidOperationException();b.Grants=b.Grants.Where(g=>g.ActorId!=actorId).ToArray();});
}
