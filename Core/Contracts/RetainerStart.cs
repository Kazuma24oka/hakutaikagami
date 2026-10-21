namespace HakutaiKagami.Core.Contracts;

// This is a declared fictional starting agreement for a created officer, not a historical appointment.
public static class RetainerStart
{
    public const string Ruleset="created-retainer-peace-v1",Contract="created-oda-member-direct-nobunaga-inspect-only-initial-agreement-v1";
    public static readonly EntityId Lord=new("person:nobunaga"),House=new("org:oda"),Basis=new("basis:created-retainer-start");
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset;
    public static string Fingerprint(ContractWorld w)=>ContractJson.CanonicalHash(new[]{Contract,PersonalStart.Fingerprint(w),MissionReview.Contract});
    public static ContractWorld Create(string name="作成武将",ulong seed=20261020)
    {
        var w=PersonalStart.Create(name,seed);w.WorldId=new("world:"+Ruleset);
        w.Definitions=w.Definitions with{Id=new("scenario:"+Ruleset),Ruleset=Ruleset,AuthorityBases=[..w.Definitions.AuthorityBases,new(Basis,"作成人物の開始合意（架空）。織田家所属/信長直属のみ。史実任命・土地・軍・徴税権ではない")]};
        w.Memberships=[..w.Memberships,new(new("membership:created-retainer"),PersonalStart.Person,House,"membership",Basis,0,long.MaxValue,0)];
        w.ServiceBonds=[..w.ServiceBonds,new(new("service:created-retainer"),PersonalStart.Person,Lord,"service",Basis,0,long.MaxValue,0)];
        foreach(var observer in new[]{PersonalStart.Person,Lord}){
            var id=new EntityId("evidence:created-retainer/"+observer.Value.Replace(':','/'));
            w.Evidence=[..w.Evidence,new(id,observer,PersonalStart.Person,"created-start-agreement:oda/nobunaga",0,0,null,null)];w.Knowledge=[..w.Knowledge,new(observer,id,0)];
        }
        w.Definitions=w.Definitions with{Version=RegionalBattle.Version(w)};ContractValidation.Validate(w);
        if(!StartConfiguration.Check(w,StartConfiguration.Describe(w,w.PlayerControl,StartTemplate.Retainer,PersonOrigin.Created,PersonalStart.Policy),PersonalStart.Policy).Eligible)throw new InvalidDataException("家臣の開始設定が不整合です");
        return w;
    }
    public static void Validate(ContractWorld w)
    {
        if(!Enabled(w))return;var actor=PersonalStart.Person;
        var membership=w.Memberships.Where(m=>m.Subject==actor).ToArray();var service=w.ServiceBonds.Where(m=>m.Subject==actor||m.Target==actor).ToArray();
        if(w.PlayerControl!=actor||membership.Length!=1||service.Length!=1||membership[0].Target!=House||service[0].Subject!=actor||service[0].Target!=Lord||
            membership.Concat(service).Any(b=>b.Basis!=Basis||b.From!=0||b.Until!=long.MaxValue)||
            w.Offices.Any(o=>o.Subject==actor)||w.LandRights.Any(l=>l.Subject==actor)||w.Units.Any(u=>u.Commander==actor)||w.ResourceLots.Any(l=>l.Owner==actor))
            throw new InvalidDataException("Created retainer cannot silently gain another service or coercive rights");
        foreach(var observer in new[]{actor,Lord}){
            var id=new EntityId("evidence:created-retainer/"+observer.Value.Replace(':','/'));
            var evidence=w.Evidence.SingleOrDefault(e=>e.Id==id);
            if(evidence==null||evidence.Observer!=observer||evidence.Subject!=actor||evidence.Claim!="created-start-agreement:oda/nobunaga"||evidence.ObservedAt!=0||evidence.RecordedAt!=0||evidence.Message!=null||evidence.Supersedes!=null||!w.Knowledge.Any(k=>k.Character==observer&&k.Evidence==id&&k.LearnedAt==0))throw new InvalidDataException("Initial service agreement requires each party's own knowledge");
        }
        var grants=w.Grants.Where(g=>g.Agent==actor).ToArray();
        if(grants.Length!=4||grants.Any(g=>g.Actions.Length!=1||g.Redelegate||g.Limit!=0||g.Assets.Length!=0||g.From!=0||g.Until!=long.MaxValue)||
            grants.Count(g=>g.Principal==actor&&g.Scope==ContractFixture.Castle&&g.Actions[0]==ActionKind.Move)!=1||
            RegionalWork.Roles.Any(r=>grants.Count(g=>g.Principal==r.Person&&g.Scope==RegionalCampaign.Site(r.Node)&&g.Actions[0]==ActionKind.Inspect)!=1))throw new InvalidDataException("Retainer inspection profile has an unregistered grant");
    }
}
